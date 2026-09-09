using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the 9-ball perineal scrub/paint technique in sequence (Strokes 1 through 9).
/// Enforces 1 cotton per stroke, automatic advancement, and instant mistake detection with red visual alerts.
/// </summary>
public class StrokeTrackingManager : MonoBehaviour
{
    public static StrokeTrackingManager Instance { get; private set; }

    [Header("Sequence Configuration")]
    [Tooltip("All 9 zone definitions listed in clinical order.")]
    public List<StrokeZoneDefinition> allZonesInOrder = new List<StrokeZoneDefinition>();

    [Header("Zone Trigger Components")]
    public List<StrokeZoneTrigger> zoneTriggers = new List<StrokeZoneTrigger>();

    [Header("State")]
    public int currentStrokeIndex = 0;
    public AntisepticType currentPhaseAntiseptic = AntisepticType.Iodine_7_5_Scrub;
    private readonly HashSet<int> completedIndices = new HashSet<int>();

    [Header("Stroke Tuning")]
    [Tooltip("Distance (in meters) the cotton must travel across the active zone to validate a stroke (~2.6cm).")]
    public float requiredStrokeDistance = 0.026f;

    [Tooltip("Dwell time (in seconds) required for dab strokes like Zone 9 (Anus).")]
    public float requiredDabTime = 0.35f;

    private float currentStrokeTravel = 0f;
    private float currentDabTime = 0f;
    private int consecutiveMoveSamples = 0;
    private Vector3 lastTouchPos = Vector3.zero;
    private float lastMistakeTriggerTime = 0f;
    private float lastUsedCottonWarningTime = 0f;
    private CottonState[] cachedCottons;

    public event System.Action<int, StrokeZoneDefinition> OnStrokeAdvanced;
    public event System.Action<string, bool> OnStrokeValidated;
    public event System.Action<int> OnMistakeZoneTriggered;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        CacheZoneTriggers();
    }

    private void Start()
    {
        if (zoneTriggers == null || zoneTriggers.Count == 0)
        {
            CacheZoneTriggers();
        }
    }

    private void OnEnable()
    {
        if (zoneTriggers == null || zoneTriggers.Count == 0)
        {
            CacheZoneTriggers();
        }
    }

    public void CacheZoneTriggers()
    {
        var foundTriggers = FindObjectsByType<StrokeZoneTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        var triggerList = new List<StrokeZoneTrigger>(foundTriggers);
        triggerList.Sort((a, b) => {
            int orderA = (a != null && a.zoneDefinition != null) ? a.zoneDefinition.expectedOrderIndex : 99;
            int orderB = (b != null && b.zoneDefinition != null) ? b.zoneDefinition.expectedOrderIndex : 99;
            return orderA.CompareTo(orderB);
        });

        zoneTriggers = triggerList;

        if (allZonesInOrder == null || allZonesInOrder.Count == 0)
        {
            allZonesInOrder = new List<StrokeZoneDefinition>();
            foreach (var trig in zoneTriggers)
            {
                if (trig != null && trig.zoneDefinition != null)
                {
                    allZonesInOrder.Add(trig.zoneDefinition);
                }
            }
        }

        Debug.Log($"[StrokeTrackingManager] Cached {zoneTriggers.Count} stroke zone triggers.");
    }

    public void ResetForPhase(AntisepticType phaseAntiseptic)
    {
        currentPhaseAntiseptic = phaseAntiseptic;
        currentStrokeIndex = 0;
        completedIndices.Clear();
        currentStrokeTravel = 0f;
        currentDabTime = 0f;
        lastTouchPos = Vector3.zero;

        CacheZoneTriggers();

        Debug.Log($"[StrokeTrackingManager] Phase Reset: {phaseAntiseptic} | Starting Stroke 1 of 9.");

        var activeDef = GetCurrentActiveZone();
        OnStrokeAdvanced?.Invoke(currentStrokeIndex, activeDef);
    }

    public StrokeZoneDefinition GetCurrentActiveZone()
    {
        if (allZonesInOrder != null && currentStrokeIndex >= 0 && currentStrokeIndex < allZonesInOrder.Count)
        {
            return allZonesInOrder[currentStrokeIndex];
        }
        return null;
    }

    public int GetCurrentStrokeIndex()
    {
        return currentStrokeIndex;
    }

    public StrokeZoneTrigger GetActiveZoneTrigger()
    {
        if (zoneTriggers == null || zoneTriggers.Count == 0) CacheZoneTriggers();

        if (zoneTriggers != null && currentStrokeIndex >= 0 && currentStrokeIndex < zoneTriggers.Count)
        {
            return zoneTriggers[currentStrokeIndex];
        }
        return null;
    }

    private void Update()
    {
        // Zero-allocation check: only check active cottons that are currently held by forceps or hand
        if (CottonState.activeCottons == null || CottonState.activeCottons.Count == 0) return;

        foreach (var cotton in CottonState.activeCottons)
        {
            if (cotton == null || !cotton.gameObject.activeInHierarchy) continue;

            // Only evaluate positions for cotton balls that are actively held or manipulated
            if (cotton.currentHolder != null)
            {
                int zoneIdx = GetZoneIndexForPoint(cotton.transform.position);
                if (zoneIdx >= 0)
                {
                    ProcessCottonTouch(cotton, zoneIdx, cotton.transform.position);
                }
            }
        }
    }

    /// <summary>
    /// Identifies which zone index (0 to 8) a touch point belongs to, or -1 if outside any zone.
    /// Uses exact local-space Oriented Bounding Box (OBB) math.
    /// </summary>
    public int GetZoneIndexForPoint(Vector3 worldPoint)
    {
        if (zoneTriggers == null || zoneTriggers.Count == 0) CacheZoneTriggers();

        // 1. Check active zone first
        var activeTrig = GetActiveZoneTrigger();
        if (activeTrig != null)
        {
            BoxCollider activeBox = activeTrig.GetComponent<BoxCollider>();
            if (activeBox != null && IsPointInBox(activeBox, worldPoint, 0.015f))
            {
                return currentStrokeIndex;
            }
        }

        // 2. Check all other zones
        for (int i = 0; i < zoneTriggers.Count; i++)
        {
            var trig = zoneTriggers[i];
            if (trig == null) continue;

            BoxCollider box = trig.GetComponent<BoxCollider>();
            if (box != null && IsPointInBox(box, worldPoint, 0.015f))
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsPointInBox(BoxCollider box, Vector3 worldPoint, float tolerance = 0.015f)
    {
        if (box == null) return false;
        Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 halfSize = box.size * 0.5f + Vector3.one * tolerance;
        return Mathf.Abs(localPoint.x) <= halfSize.x &&
               Mathf.Abs(localPoint.y) <= halfSize.y &&
               Mathf.Abs(localPoint.z) <= halfSize.z;
    }

    public void OnCottonEnterZone(CottonState cotton, StrokeZoneDefinition zone, Vector3 pos)
    {
        int zoneIdx = GetZoneIndexFromDefinition(zone);
        ProcessCottonTouch(cotton, zoneIdx, pos);
    }

    public void OnCottonStayInZone(CottonState cotton, StrokeZoneDefinition zone, Vector3 pos)
    {
        int zoneIdx = GetZoneIndexFromDefinition(zone);
        ProcessCottonTouch(cotton, zoneIdx, pos);
    }

    public void OnCottonExitZone(CottonState cotton, StrokeZoneDefinition zone)
    {
        lastTouchPos = Vector3.zero;
        currentStrokeTravel = 0f;
        currentDabTime = 0f;
        consecutiveMoveSamples = 0;
    }

    private int GetZoneIndexFromDefinition(StrokeZoneDefinition zone)
    {
        if (zone != null) return zone.expectedOrderIndex;
        return -1;
    }

    /// <summary>
    /// Core logic for handling a cotton ball touching a stroke zone.
    /// </summary>
    public void ProcessCottonTouch(CottonState cotton, int touchedZoneIndex, Vector3 touchPos)
    {
        if (cotton == null) return;
        if (touchedZoneIndex < 0 || touchedZoneIndex >= 9) return;

        // Auto-soak fail-safe if cotton is not soaked
        if (!cotton.isSoaked)
        {
            cotton.isSoaked = true;
            cotton.antisepticType = currentPhaseAntiseptic;
        }

        // 1 COTTON PER STROKE RULE: If cotton was already used on a prior stroke, warn player
        if (cotton.isUsed)
        {
            if (Time.time - lastUsedCottonWarningTime > 1.5f)
            {
                lastUsedCottonWarningTime = Time.time;
                OnStrokeValidated?.Invoke("Cotton already used! Take a fresh cotton ball from the tray.", false);
                Debug.LogWarning("[StrokeTrackingManager] Player attempted to stroke with an already used cotton ball.");
            }
            return;
        }

        // =========================================================================
        // ACTIVE ZONE TOUCH: Progress and complete active stroke
        // =========================================================================
        if (touchedZoneIndex == currentStrokeIndex)
        {
            if (completedIndices.Contains(currentStrokeIndex)) return;

            if (lastTouchPos == Vector3.zero)
            {
                lastTouchPos = touchPos;
                currentDabTime = 0f;
                currentStrokeTravel = 0f;
                consecutiveMoveSamples = 0;
                return;
            }

            float delta = Vector3.Distance(touchPos, lastTouchPos);
            lastTouchPos = touchPos;

            // Zone 9: Anus (Direct Dab / Pat - requires steady contact dwell)
            if (currentStrokeIndex == 8)
            {
                currentDabTime += Time.deltaTime;
                if (currentDabTime >= requiredDabTime)
                {
                    CompleteCurrentStroke(cotton);
                }
            }
            // Zones 1-8: Swipes (Requires continuous movement travel across the zone)
            else
            {
                if (delta > 0.0006f && delta < 0.15f)
                {
                    currentStrokeTravel += delta;
                    consecutiveMoveSamples++;
                }

                // Strict Stroke Requirement: must achieve required travel distance and multiple movement samples
                if (currentStrokeTravel >= requiredStrokeDistance && consecutiveMoveSamples >= 3)
                {
                    CompleteCurrentStroke(cotton);
                }
            }
        }
        // =========================================================================
        // WRONG / OUT-OF-ORDER ZONE TOUCH: Flash Red Error
        // =========================================================================
        else
        {
            // If the touched zone is ALREADY COMPLETED, ignore it completely (no mistake alert or red pulse)
            if (completedIndices.Contains(touchedZoneIndex) || touchedZoneIndex < currentStrokeIndex)
            {
                return;
            }

            if (Time.time - lastMistakeTriggerTime > 0.85f)
            {
                lastMistakeTriggerTime = Time.time;
                HandleMistakeTouch(touchPos, touchedZoneIndex, "Out of order! Take the correct anatomical stroke in sequence.");
            }
        }
    }

    public void HandleMistakeTouch(Vector3 touchPos, int mistakeZoneIndex, string reason = "Stroke out of order!")
    {
        // Never trigger mistake on completed zones
        if (completedIndices.Contains(mistakeZoneIndex) || mistakeZoneIndex < currentStrokeIndex)
        {
            return;
        }

        lastTouchPos = Vector3.zero;
        currentStrokeTravel = 0f;
        currentDabTime = 0f;
        consecutiveMoveSamples = 0;

        if (mistakeZoneIndex >= 0)
        {
            OnMistakeZoneTriggered?.Invoke(mistakeZoneIndex);
        }

        if (PerinealCareManager.Instance != null)
        {
            PerinealCareManager.Instance.RecordClinicalViolation(reason, 5);
        }

        OnStrokeValidated?.Invoke(reason, false);
    }

    private void CompleteCurrentStroke(CottonState cotton)
    {
        if (completedIndices.Contains(currentStrokeIndex)) return;

        int finishedIndex = currentStrokeIndex;
        completedIndices.Add(finishedIndex);
        var completedDef = GetCurrentActiveZone();
        string zoneName = completedDef != null ? completedDef.zoneName : $"Stroke {finishedIndex + 1}";

        Debug.Log($"[StrokeTrackingManager] Stroke {finishedIndex + 1}/9 Completed: {zoneName}");
        OnStrokeValidated?.Invoke($"{zoneName} Completed!", true);

        // Apply Antiseptic Visual Layer (Light Blue Puddle in 7.5% Scrub / Persistent Amber in 10% Paint)
        if (zoneTriggers != null && finishedIndex >= 0 && finishedIndex < zoneTriggers.Count)
        {
            var trig = zoneTriggers[finishedIndex];
            if (trig != null)
            {
                var puddle = trig.GetComponent<WashableIodinePuddle>();
                if (puddle != null)
                {
                    if (currentPhaseAntiseptic == AntisepticType.Iodine_7_5_Scrub)
                    {
                        puddle.Apply7_5Puddle();
                    }
                    else if (currentPhaseAntiseptic == AntisepticType.Iodine_10_Paint)
                    {
                        puddle.Apply10Coating();
                    }
                }
            }
        }

        // 1 Cotton per stroke rule: Cotton is spent immediately upon stroke completion
        if (cotton != null)
        {
            cotton.isUsed = true;
        }

        // Advance to next stroke
        currentStrokeIndex++;
        currentStrokeTravel = 0f;
        currentDabTime = 0f;
        consecutiveMoveSamples = 0;
        lastTouchPos = Vector3.zero;

        var nextDef = GetCurrentActiveZone();
        OnStrokeAdvanced?.Invoke(currentStrokeIndex, nextDef);

        // Check if all 9 strokes finished
        if (currentStrokeIndex >= 9)
        {
            Debug.Log("[StrokeTrackingManager] All 9 Perineal Strokes Completed Successfully!");

            if (PerinealCareManager.Instance != null)
            {
                if (currentPhaseAntiseptic == AntisepticType.Iodine_7_5_Scrub)
                {
                    PerinealCareManager.Instance.On7_5ScrubCompleted();
                }
                else if (currentPhaseAntiseptic == AntisepticType.Iodine_10_Paint)
                {
                    PerinealCareManager.Instance.On10PaintCompleted();
                }
            }
        }
    }
}



