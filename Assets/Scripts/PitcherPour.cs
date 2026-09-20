using UnityEngine;
using System.Collections.Generic;

public class PitcherPour : MonoBehaviour
{
    [Header("Visual Settings")]
    public ParticleSystem waterParticleSystem;
    public float pourAngleThreshold = 45f; // Starts pouring when tilted 45 degrees
    public float emissionRate = 50f;       // How much water pours out

    [Header("Mechanical Settings")]
    [Tooltip("Where the invisible cleaning raycast shoots from (e.g., the spout).")]
    public Transform spoutOrigin;
    [Tooltip("How far down the water reaches to clean.")]
    public float pourDistance = 1.5f;
    [Tooltip("Radius of the water stream (matches visual particle stream).")]
    public float waterStreamRadius = 0.04f;

    private ParticleSystem.EmissionModule emissionModule;
    private bool isPouring = false;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly HashSet<GameObject> processedObjects = new HashSet<GameObject>();
    private List<WashableIodinePuddle> cachedPuddles = new List<WashableIodinePuddle>();

    void Awake()
    {
        CachePuddles();
    }

    void Start()
    {
        if (waterParticleSystem != null)
        {
            emissionModule = waterParticleSystem.emission;
            emissionModule.rateOverTime = 0f;
        }
        CachePuddles();
    }

    public void CachePuddles()
    {
        var found = FindObjectsByType<WashableIodinePuddle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        cachedPuddles = new List<WashableIodinePuddle>(found);
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
        if (spoutOrigin == null) return;

        Ray ray = new Ray(spoutOrigin.position, Vector3.down);
        int hitCount = Physics.SphereCastNonAlloc(ray, waterStreamRadius, hitBuffer, pourDistance, ~0, QueryTriggerInteraction.Collide);
        
        processedObjects.Clear();
        bool hitMotherPerineum = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (col == null) continue;
            GameObject go = col.gameObject;
            if (processedObjects.Contains(go)) continue;
            processedObjects.Add(go);

            // Check if water hit mother anatomy / perineal zone
            if (col.transform.root.name.Contains("Mother") || go.name.Contains("StrokeZone") || go.name.Contains("bodyfinal") || go.name.Contains("pelvis"))
            {
                hitMotherPerineum = true;
            }

            // Wash Iodine Puddles on Stroke Landmarks (Phase 3: Antiseptic Rinse)
            WashableIodinePuddle iodinePuddle = col.GetComponent<WashableIodinePuddle>()
                                             ?? col.GetComponentInParent<WashableIodinePuddle>()
                                             ?? col.GetComponentInChildren<WashableIodinePuddle>();
            if (iodinePuddle != null)
            {
                iodinePuddle.WashWithWater(Time.deltaTime);
            }
        }

        if (PerinealCareManager.Instance == null || PerinealCareManager.Instance.cleaningProgressUI == null) return;

        // 1. Step 2: Preliminary Water Wash Progress
        if (PerinealCareManager.Instance.currentState == PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH && hitMotherPerineum)
        {
            PerinealCareManager.Instance.cleaningProgressUI.ReportWashStep1(Time.deltaTime);
        }
        // 2. Step 4: Antiseptic Water Rinse Progress (based on light-green puddles washed away)
        else if (PerinealCareManager.Instance.currentState == PerinealCareState.STATE_4_ANTISEPTIC_RINSE)
        {
            if (cachedPuddles == null || cachedPuddles.Count == 0) CachePuddles();

            if (cachedPuddles != null && cachedPuddles.Count > 0)
            {
                float totalCleanliness = 0f;
                for (int p = 0; p < cachedPuddles.Count; p++)
                {
                    var pud = cachedPuddles[p];
                    if (pud != null)
                    {
                        totalCleanliness += (1.0f - pud.GetRemainingOpacity());
                    }
                    else
                    {
                        totalCleanliness += 1.0f;
                    }
                }
                float progress = totalCleanliness / cachedPuddles.Count;
                PerinealCareManager.Instance.cleaningProgressUI.UpdateRinseProgress(progress);
            }
        }
    }
}