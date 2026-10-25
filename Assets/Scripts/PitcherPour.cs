using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the Perineal Water Pitcher pour mechanics:
/// - Tilting beyond threshold pours water from the calibrated SpoutOrigin.
/// - Custom Scene View Gizmos allow 1-click manual positioning of the spout tip in Unity Editor.
/// - Washes antiseptic / iodine puddles during Step 2 (Wash) and Step 4 (Rinse).
/// </summary>
public class PitcherPour : MonoBehaviour
{
    [Header("Manual Spout Calibration")]
    [Tooltip("The exact physical point where water pours from. You can move and rotate this freely in Scene View!")]
    public Transform spoutOrigin;

    [Header("Visual Settings")]
    public ParticleSystem waterParticleSystem;
    public float pourAngleThreshold = 45f; // Starts pouring when tilted 45 degrees
    public float emissionRate = 50f;       // How much water pours out

    [Header("Mechanical Settings")]
    [Tooltip("How far down the water reaches to clean.")]
    public float pourDistance = 1.5f;
    [Tooltip("Radius of the water stream (matches visual particle stream).")]
    public float waterStreamRadius = 0.04f;

    [Tooltip("Radius around the water stream/hit point where puddles are washed.")]
    public float puddleWashRadius = 0.16f;

    private ParticleSystem.EmissionModule emissionModule;
    private bool isPouring = false;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly HashSet<GameObject> processedObjects = new HashSet<GameObject>();
    private List<WashableIodinePuddle> cachedPuddles = new List<WashableIodinePuddle>();

    void Awake()
    {
        CachePuddles();
        SyncParticleSystemToSpout();
    }

    void Start()
    {
        if (waterParticleSystem != null)
        {
            emissionModule = waterParticleSystem.emission;
            emissionModule.rateOverTime = 0f;
        }
        CachePuddles();
        SyncParticleSystemToSpout();
    }

    public void CachePuddles()
    {
        var root = GameObject.Find("Mother_Betadine_Puddles");
        if (root != null)
        {
            cachedPuddles = new List<WashableIodinePuddle>(root.GetComponentsInChildren<WashableIodinePuddle>(true));
        }
        else
        {
            var found = FindObjectsByType<WashableIodinePuddle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            cachedPuddles = new List<WashableIodinePuddle>(found);
        }
    }

    void LateUpdate()
    {
        SyncParticleSystemToSpout();
    }

    private void SyncParticleSystemToSpout()
    {
        if (spoutOrigin != null && waterParticleSystem != null)
        {
            if (waterParticleSystem.transform.parent != spoutOrigin)
            {
                waterParticleSystem.transform.SetParent(spoutOrigin, false);
            }
            waterParticleSystem.transform.localPosition = Vector3.zero;
        }
    }

    void Update()
    {
        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        if (tiltAngle > pourAngleThreshold)
        {
            if (!isPouring) StartPouring();
            CastWaterRay();
        }
        else
        {
            if (isPouring) StopPouring();
        }
    }

    private void StartPouring()
    {
        isPouring = true;
        if (waterParticleSystem != null)
        {
            emissionModule.rateOverTime = emissionRate;
            if (!waterParticleSystem.isPlaying)
            {
                waterParticleSystem.Play();
            }
        }
    }

    private void StopPouring()
    {
        isPouring = false;
        if (waterParticleSystem != null)
        {
            emissionModule.rateOverTime = 0f;
        }
    }

    private void CastWaterRay()
    {
        Transform originT = spoutOrigin != null ? spoutOrigin : transform;
        Vector3 rayOrigin = originT.position;
        Vector3 rayDir = Vector3.down;
        Ray ray = new Ray(rayOrigin, rayDir);
        int hitCount = Physics.SphereCastNonAlloc(ray, waterStreamRadius, hitBuffer, pourDistance, ~0, QueryTriggerInteraction.Collide);
        
        processedObjects.Clear();
        bool hitMotherPerineum = false;
        Vector3 primaryHitPoint = rayOrigin + rayDir * pourDistance;
        float closestHitDist = float.MaxValue;
        WashableIodinePuddle lastDirectPuddle = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (col == null) continue;
            GameObject go = col.gameObject;

            // Ignore pitcher's own colliders
            if (go == gameObject || go.transform.IsChildOf(transform)) continue;

            if (processedObjects.Contains(go)) continue;
            processedObjects.Add(go);

            if (hitBuffer[i].distance < closestHitDist)
            {
                closestHitDist = hitBuffer[i].distance;
                primaryHitPoint = hitBuffer[i].point;
            }

            // Check if water hit mother anatomy / perineal zone
            if (col.transform.root.name.Contains("Mother") || col.transform.root.name.Contains("MODEL") || go.name.Contains("Mother") || go.name.Contains("StrokeZone") || go.name.Contains("bodyfinal") || go.name.Contains("pelvis") || go.name.Contains("Perineal"))
            {
                hitMotherPerineum = true;
            }

            // Direct collider wash if present
            WashableIodinePuddle directPuddle = col.GetComponent<WashableIodinePuddle>()
                                             ?? col.GetComponentInParent<WashableIodinePuddle>()
                                             ?? col.GetComponentInChildren<WashableIodinePuddle>();
            if (directPuddle != null)
            {
                directPuddle.WashWithWater(Time.deltaTime);
                lastDirectPuddle = directPuddle;
            }
        }

        // Proximity wash for puddles
        if (cachedPuddles == null || cachedPuddles.Count == 0) CachePuddles();

        if (cachedPuddles != null && cachedPuddles.Count > 0)
        {
            for (int p = 0; p < cachedPuddles.Count; p++)
            {
                var pud = cachedPuddles[p];
                if (pud == null || !pud.isPuddleActive || pud.isFullyWashed) continue;
                if (lastDirectPuddle != null && pud == lastDirectPuddle) continue; // Avoid double-washing in a single frame

                Vector3 pudPos = pud.puddle75Object != null ? pud.puddle75Object.transform.position : pud.transform.position;

                // Check distance from puddle to primary hit point
                float distToHitPoint = Vector3.Distance(primaryHitPoint, pudPos);

                // Also check perpendicular distance from puddle to the vertical water ray
                Vector3 toPuddle = pudPos - rayOrigin;
                float downProj = Vector3.Dot(toPuddle, rayDir);
                bool inVerticalRange = (downProj >= 0f && downProj <= pourDistance + 0.1f);
                float distToRay = inVerticalRange ? Vector3.Cross(rayDir, toPuddle).magnitude : float.MaxValue;

                if (distToHitPoint <= puddleWashRadius || distToRay <= puddleWashRadius || (hitMotherPerineum && distToHitPoint <= puddleWashRadius * 1.5f))
                {
                    pud.WashWithWater(Time.deltaTime);
                }
            }
        }

        if (PerinealCareManager.Instance == null || PerinealCareManager.Instance.cleaningProgressUI == null) return;

        // 1. Step 2: Preliminary Water Wash Progress
        if (PerinealCareManager.Instance.currentState == PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH && hitMotherPerineum)
        {
            PerinealCareManager.Instance.cleaningProgressUI.ReportWashStep1(Time.deltaTime);
        }
        // 2. Step 4: Antiseptic Water Rinse Progress
        else if (PerinealCareManager.Instance.currentState == PerinealCareState.STATE_4_ANTISEPTIC_RINSE)
        {
            if (cachedPuddles != null && cachedPuddles.Count > 0)
            {
                float totalCleanliness = 0f;
                int activeCount = 0;

                for (int p = 0; p < cachedPuddles.Count; p++)
                {
                    var pud = cachedPuddles[p];
                    if (pud != null && (pud.isPuddleActive || pud.isFullyWashed))
                    {
                        activeCount++;
                        totalCleanliness += (1.0f - pud.GetRemainingOpacity());
                    }
                }

                float progress = activeCount > 0 ? (totalCleanliness / activeCount) : 0f;
                PerinealCareManager.Instance.cleaningProgressUI.UpdateRinseProgress(progress);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform originT = spoutOrigin != null ? spoutOrigin : transform;
        if (originT != null)
        {
            // Bright Cyan Wire Sphere at the exact spout origin point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(originT.position, 0.012f);

            // Forward direction vector ray (where spout is aimed)
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawRay(originT.position, originT.forward * 0.12f);

            // Downward pour stream ray
            Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.5f);
            Gizmos.DrawRay(originT.position, Vector3.down * pourDistance);
        }
    }
}
