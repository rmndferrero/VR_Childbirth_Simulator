using System;
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

    [Header("Step 2 Strict Perineal Wash Validation")]
    [Tooltip("Bounded trigger zone over the perineum used to strictly validate Step 2 pouring.")]
    public Collider perinealWashZoneCollider;
    [Tooltip("Continuous off-target pouring seconds required before logging Wrong_Washing_Area.")]
    public float wrongPourDwellThreshold = 0.55f;
    [Tooltip("Cooldown seconds between consecutive Wrong_Washing_Area penalties.")]
    public float wrongPourPenaltyCooldown = 2.5f;

    private ParticleSystem.EmissionModule emissionModule;
    private bool isPouring = false;
    private float offTargetPourTimer = 0f;
    private float lastWrongPourPenaltyTime = -99f;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private readonly HashSet<GameObject> processedObjects = new HashSet<GameObject>();
    private List<WashableIodinePuddle> cachedPuddles = new List<WashableIodinePuddle>();

    void Awake()
    {
        ResolvePerinealWashZone();
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
        ResolvePerinealWashZone();
        CachePuddles();
        SyncParticleSystemToSpout();
    }

    public void ResolvePerinealWashZone()
    {
        if (perinealWashZoneCollider == null)
        {
            var gizmo = FindFirstObjectByType<PerinealWashZoneGizmo>(FindObjectsInactive.Exclude);
            if (gizmo != null)
            {
                perinealWashZoneCollider = gizmo.GetComponent<Collider>();
            }
        }

        if (perinealWashZoneCollider == null)
        {
            var zoneObj = GameObject.Find("Perineal_Wash_Zone");
            if (zoneObj != null)
            {
                perinealWashZoneCollider = zoneObj.GetComponent<Collider>();
            }
        }
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
        offTargetPourTimer = 0f;
        if (waterParticleSystem != null)
        {
            emissionModule.rateOverTime = 0f;
        }
    }

    private void CastWaterRay()
    {
        PerinealCareState currentState = PerinealCareManager.Instance != null
            ? PerinealCareManager.Instance.currentState
            : PerinealCareState.STATE_1_TALK_TO_MOTHER;
        bool isStep2Wash = currentState == PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH;
        bool isStep4Rinse = currentState == PerinealCareState.STATE_4_ANTISEPTIC_RINSE;

        if (isStep2Wash && perinealWashZoneCollider == null)
        {
            ResolvePerinealWashZone();
        }

        Transform originT = spoutOrigin != null ? spoutOrigin : transform;
        Vector3 rayOrigin = originT.position;
        Vector3 rayDir = Vector3.down;
        Ray ray = new Ray(rayOrigin, rayDir);
        int hitCount = Physics.SphereCastNonAlloc(ray, waterStreamRadius, hitBuffer, pourDistance, ~0, QueryTriggerInteraction.Collide);

        processedObjects.Clear();
        bool hitPerinealWashZone = false;
        bool hitWrongMotherArea = false;
        string wrongAreaName = "";
        Vector3 primaryHitPoint = rayOrigin + rayDir * pourDistance;
        float closestHitDist = float.MaxValue;
        WashableIodinePuddle lastDirectPuddle = null;

        // Also check direct overlap in case the spout tip enters the Perineal_Wash_Zone volume
        if (perinealWashZoneCollider != null && perinealWashZoneCollider.enabled && perinealWashZoneCollider.gameObject.activeInHierarchy)
        {
            Vector3 closestPt = perinealWashZoneCollider.ClosestPoint(rayOrigin);
            if ((closestPt - rayOrigin).sqrMagnitude <= waterStreamRadius * waterStreamRadius)
            {
                hitPerinealWashZone = true;
            }
        }

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

            if (isStep4Rinse)
            {
                WashableIodinePuddle directPuddle = col.GetComponent<WashableIodinePuddle>()
                                                 ?? col.GetComponentInParent<WashableIodinePuddle>()
                                                 ?? col.GetComponentInChildren<WashableIodinePuddle>();
                if (directPuddle != null)
                {
                    directPuddle.WashWithWater(Time.deltaTime);
                    lastDirectPuddle = directPuddle;
                    hitPerinealWashZone = true;
                    continue;
                }
            }

            ClassifyStep2Collider(col, perinealWashZoneCollider, out bool isPerinealTarget, out bool isWrongMother);
            if (isPerinealTarget)
            {
                hitPerinealWashZone = true;
            }
            else if (isWrongMother)
            {
                hitWrongMotherArea = true;
                if (string.IsNullOrEmpty(wrongAreaName)) wrongAreaName = go.name;
            }
        }

        // Step 4 Antiseptic Rinse washes across all Mother_Betadine_Canvas puddles (unchanged behavior)
        if (isStep4Rinse)
        {
            bool hitMotherForRinse = hitPerinealWashZone || hitWrongMotherArea;

            if (cachedPuddles == null || cachedPuddles.Count == 0) CachePuddles();

            if (cachedPuddles != null && cachedPuddles.Count > 0)
            {
                for (int p = 0; p < cachedPuddles.Count; p++)
                {
                    var pud = cachedPuddles[p];
                    if (pud == null || !pud.isPuddleActive || pud.isFullyWashed) continue;
                    if (lastDirectPuddle != null && pud == lastDirectPuddle) continue; // Avoid double-washing in a single frame

                    Vector3 pudPos = pud.puddle75Object != null ? pud.puddle75Object.transform.position : pud.transform.position;

                    float distToHitPoint = Vector3.Distance(primaryHitPoint, pudPos);
                    Vector3 toPuddle = pudPos - rayOrigin;
                    float downProj = Vector3.Dot(toPuddle, rayDir);
                    bool inVerticalRange = (downProj >= 0f && downProj <= pourDistance + 0.1f);
                    float distToRay = inVerticalRange ? Vector3.Cross(rayDir, toPuddle).magnitude : float.MaxValue;

                    if (distToHitPoint <= puddleWashRadius || distToRay <= puddleWashRadius || (hitMotherForRinse && distToHitPoint <= puddleWashRadius * 1.5f))
                    {
                        pud.WashWithWater(Time.deltaTime);
                    }
                }
            }
        }

        if (PerinealCareManager.Instance == null || PerinealCareManager.Instance.cleaningProgressUI == null) return;

        // 1. Step 2: Strict Perineal Wash Zone Only (rejects legs, thighs, abdomen, buttocks outside Perineal_Wash_Zone)
        if (PerinealCareManager.Instance.currentState == PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH)
        {
            if (hitPerinealWashZone)
            {
                offTargetPourTimer = 0f;
                PerinealCareManager.Instance.cleaningProgressUI.ReportWashStep1(Time.deltaTime);
            }
            else if (hitWrongMotherArea)
            {
                offTargetPourTimer += Time.deltaTime;
                if (offTargetPourTimer >= wrongPourDwellThreshold && Time.time - lastWrongPourPenaltyTime >= wrongPourPenaltyCooldown)
                {
                    offTargetPourTimer = 0f;
                    lastWrongPourPenaltyTime = Time.time;

                    string areaLabel = FormatOffTargetAreaLabel(wrongAreaName);
                    if (VRDemoGameManager.Instance != null)
                    {
                        VRDemoGameManager.Instance.RecordViolation(
                            ClinicalViolationType.Wrong_Washing_Area,
                            $"Poured water onto {areaLabel} instead of perineum");
                        VRDemoGameManager.Instance.ShowWarning($"Pour warm water onto the perineal area only (avoid {areaLabel}).");
                    }
                    if (VRHeadsetVisualFeedback.Instance != null)
                    {
                        VRHeadsetVisualFeedback.Instance.PlayFeedbackCuesOnly(0.55f, 0.25f);
                    }
                }
            }
            else
            {
                offTargetPourTimer = 0f;
            }
        }
        // 2. Step 4: Antiseptic Water Rinse Progress (washes off light-green 7.5% Betadine puddles across canvas — unchanged)
        else if (PerinealCareManager.Instance.currentState == PerinealCareState.STATE_4_ANTISEPTIC_RINSE)
        {
            offTargetPourTimer = 0f;
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
        else
        {
            offTargetPourTimer = 0f;
        }
    }

    /// <summary>
    /// Classifies a Step 2 water stream collider hit strictly against the designated Perineal_Wash_Zone collider
    /// vs non-perineal anatomical regions (legs, inner thighs, abdomen, buttocks).
    /// </summary>
    public static void ClassifyStep2Collider(
        Collider col,
        Collider designatedWashZone,
        out bool isPerinealTarget,
        out bool isNonPerinealMother)
    {
        if (col == null)
        {
            isPerinealTarget = false;
            isNonPerinealMother = false;
            return;
        }

        GameObject go = col.gameObject;
        if ((designatedWashZone != null && col == designatedWashZone)
            || go.name == "Perineal_Wash_Zone"
            || go.name.StartsWith("Perineal_Wash", StringComparison.OrdinalIgnoreCase)
            || col.GetComponentInParent<PerinealWashZoneGizmo>() != null)
        {
            isPerinealTarget = true;
            isNonPerinealMother = false;
            return;
        }

        isPerinealTarget = false;
        isNonPerinealMother = IsNonPerinealMotherCollider(col, go);
    }

    private static bool IsNonPerinealMotherCollider(Collider col, GameObject go)
    {
        if (go.name == "Perineal_Wash_Zone"
            || go.name.StartsWith("Perineal_Wash", StringComparison.OrdinalIgnoreCase)
            || col.GetComponentInParent<PerinealWashZoneGizmo>() != null)
        {
            return false;
        }

        string rootName = col.transform.root.name;
        return rootName.Contains("Mother") || rootName.Contains("MODEL") ||
               go.name.Contains("Mother") || go.name.Contains("bodyfinal") ||
               go.name.Contains("pelvis") || go.name.Contains("Leg") ||
               go.name.Contains("Thigh") || go.name.Contains("Abdominal") ||
               go.name.Contains("Buttock");
    }

    private static string FormatOffTargetAreaLabel(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "surrounding skin";
        string lower = rawName.ToLowerInvariant();
        if (lower.Contains("left") && (lower.Contains("leg") || lower.Contains("thigh"))) return "left thigh/leg";
        if (lower.Contains("right") && (lower.Contains("leg") || lower.Contains("thigh"))) return "right thigh/leg";
        if (lower.Contains("leg") || lower.Contains("thigh")) return "thigh/leg";
        if (lower.Contains("abdom")) return "abdomen";
        if (lower.Contains("buttock")) return "buttocks";
        return "surrounding body area";
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
