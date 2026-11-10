using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the 9-ball perineal scrub/paint technique in sequence (Strokes 1 through 9).
/// Enforces 1 cotton per stroke, automatic advancement, pure 3D slider line-segment touch/progress tracking,
/// bidirectional support for Stroke 2 (Mons Pubis), strict top-to-bottom directional checking for Strokes 1 & 3-8,
/// instant tap for Stroke 9 (Anus), and immediate guide-to-puddle replacement upon stroke completion.
/// </summary>
public class StrokeTrackingManager : MonoBehaviour
{
    public static StrokeTrackingManager Instance { get; private set; }

    [Header("Sequence Configuration")]
    [Tooltip("All 9 zone definitions listed in clinical order.")]
    public List<StrokeZoneDefinition> allZonesInOrder = new List<StrokeZoneDefinition>();

    [Header("Scene Stroke Sliders")]
    public StrokeSliderTrack[] sliderTracks = new StrokeSliderTrack[9];

    [Header("State")]
    public int currentStrokeIndex = 0;
    public AntisepticType currentPhaseAntiseptic = AntisepticType.Iodine_7_5_Scrub;
    private readonly HashSet<int> completedIndices = new HashSet<int>();

    [Header("Stroke Tuning")]
    [Tooltip("Required progress percentage along the stroke slider to complete (0.85 = 85%).")]
    public float requiredProgressThreshold = 0.85f;

    [Tooltip("Detection radius (meters) around the slider path line on the skin (~4.5cm).")]
    public float sliderTouchRadius = 0.045f;

    [Tooltip("Tap detection radius (meters) for Stroke 9 Anus (~4.0cm).")]
    public float tapTouchRadius = 0.040f;

    [Tooltip("Time (seconds) without touch before a halfway stroke resets back to start (0%).")]
    public float strokeAbandonmentTimeout = 1.0f;

    [Header("Mistake Lockout & Red Feedback Duration")]
    [Tooltip("Duration in seconds that all stroke interactions and progression are completely blocked while the red error animation plays.")]
    public float mistakeLockoutDuration = 1.25f;

    public bool IsMistakeLockoutActive => (Time.time - lastMistakeTriggerTime) < mistakeLockoutDuration;

    private float currentStrokeProgress = 0f;
    private bool isCurrentStrokeReverse = false;
    private int activeStrokeMistakeCount = 0;
    private Vector3 lastTouchPos = Vector3.zero;
    private float lastMistakeTriggerTime = -99f;
    private float lastUsedCottonWarningTime = -99f;
    private float lastPickupForcepsStrokeTime = -99f;
    private float lastActiveTouchTime = -99f;

    public event System.Action<int, StrokeZoneDefinition> OnStrokeAdvanced;
    public event System.Action<string, bool> OnStrokeValidated;
    public event System.Action<int> OnMistakeZoneTriggered;
    public event System.Action<int, float, Vector3> OnStrokeProgressUpdated; // (zoneIndex, progress 0-1, handleWorldPos)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        CacheSliderTracks();
    }

    private void Start()
    {
        if (sliderTracks == null || sliderTracks.Length == 0 || sliderTracks[0] == null)
        {
            CacheSliderTracks();
        }
    }

    private void OnEnable()
    {
        if (sliderTracks == null || sliderTracks.Length == 0 || sliderTracks[0] == null)
        {
            CacheSliderTracks();
        }
    }

    public void CacheSliderTracks()
    {
        var foundSliders = FindObjectsByType<StrokeSliderTrack>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<StrokeSliderTrack>(foundSliders);
        list.Sort((a, b) => a.strokeIndex.CompareTo(b.strokeIndex));
        sliderTracks = list.ToArray();

        for (int i = 0; i < sliderTracks.Length; i++)
        {
            if (sliderTracks[i] != null && sliderTracks[i].strokeIndex == 1)
            {
                sliderTracks[i].isBidirectional = true;
            }
        }

        Debug.Log($"[StrokeTrackingManager] Cached {sliderTracks.Length} scene-based stroke sliders.");
    }

    public void ResetForPhase(AntisepticType phaseAntiseptic)
    {
        currentPhaseAntiseptic = phaseAntiseptic;
        currentStrokeIndex = 0;
        completedIndices.Clear();
        currentStrokeProgress = 0f;
        isCurrentStrokeReverse = false;
        activeStrokeMistakeCount = 0;
        lastTouchPos = Vector3.zero;
        lastMistakeTriggerTime = -99f;
        lastUsedCottonWarningTime = -99f;
        lastActiveTouchTime = -99f;

        CacheSliderTracks();

        if (sliderTracks != null)
        {
            foreach (var track in sliderTracks)
            {
                if (track != null)
                {
                    track.ResetProgress();
                    if (track.puddleLayer != null)
                    {
                        track.puddleLayer.ResetPuddles();
                    }
                }
            }
        }

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

    public int GetActiveStrokeMistakeCount()
    {
        return activeStrokeMistakeCount;
    }

    public float GetCurrentStrokeProgress()
    {
        return currentStrokeProgress;
    }

    public bool IsStrokeCompleted(int index)
    {
        return completedIndices.Contains(index) || index < currentStrokeIndex;
    }

    /// <summary>
    /// Gets the top (start/anterior) and bottom (end/posterior) endpoints of any zone directly from the scene slider.
    /// </summary>
    public void GetZoneEndpoints(int zoneIndex, out Vector3 startPoint, out Vector3 endPoint)
    {
        if (sliderTracks == null || sliderTracks.Length == 0) CacheSliderTracks();

        if (sliderTracks != null && zoneIndex >= 0 && zoneIndex < sliderTracks.Length && sliderTracks[zoneIndex] != null)
        {
            startPoint = sliderTracks[zoneIndex].GetStartWorldPosition();
            endPoint = sliderTracks[zoneIndex].GetEndWorldPosition();
            return;
        }

        startPoint = Vector3.zero;
        endPoint = Vector3.down * 0.05f;
    }

    private void Update()
    {
        // Abandonment check: if a stroke was started (> 1% progress) but player stopped touching for > strokeAbandonmentTimeout (1.0s), smoothly reset to 0%
        if (currentStrokeProgress > 0.01f && currentStrokeIndex < 9)
        {
            if (Time.time - lastActiveTouchTime > strokeAbandonmentTimeout)
            {
                currentStrokeProgress = 0f;
                isCurrentStrokeReverse = false;
                lastTouchPos = Vector3.zero;
                GetZoneEndpoints(currentStrokeIndex, out Vector3 startPt, out _);
                OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, 0f, startPt);

                if (sliderTracks != null && currentStrokeIndex >= 0 && currentStrokeIndex < sliderTracks.Length)
                {
                    var activeTrack = sliderTracks[currentStrokeIndex];
                    if (activeTrack != null)
                    {
                        activeTrack.ResetProgress();
                    }
                }
            }
        }

        // Zero-allocation check: only check active cottons that are currently held
        if (CottonState.activeCottons == null || CottonState.activeCottons.Count == 0) return;

        foreach (var cotton in CottonState.activeCottons)
        {
            if (cotton == null || !cotton.gameObject.activeInHierarchy) continue;

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
    /// Identifies which zone index (0 to 8) a touch point belongs to using pure 3D slider line-segment math.
    /// Finds the closest zone slider to the cotton ball. If within sliderTouchRadius, returns that zone index.
    /// </summary>
    public int GetZoneIndexForPoint(Vector3 worldPoint)
    {
        if (sliderTracks == null || sliderTracks.Length == 0) CacheSliderTracks();

        int bestIndex = -1;
        float bestDistSq = float.MaxValue;
        float maxRadius = sliderTouchRadius; // 4.5cm

        for (int i = 0; i < sliderTracks.Length; i++)
        {
            var track = sliderTracks[i];
            if (track == null) continue;

            float radius = track.isTapOnly ? tapTouchRadius : maxRadius;
            float distSq = GetDistanceSqToTrack(worldPoint, track, out float t);

            if (distSq <= radius * radius && (track.isTapOnly || (t >= -0.20f && t <= 1.20f)))
            {
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }
        }

        return bestIndex;
    }

    private float GetDistanceSqToTrack(Vector3 worldPoint, StrokeSliderTrack track, out float t)
    {
        if (track == null)
        {
            t = 0f;
            return float.MaxValue;
        }

        Vector3 startPos = track.GetStartWorldPosition();

        if (track.isTapOnly)
        {
            t = 0f;
            return (worldPoint - startPos).sqrMagnitude;
        }

        Vector3 endPos = track.GetEndWorldPosition();
        return DistanceToSegmentSq(worldPoint, startPos, endPos, out t);
    }

    /// <summary>
    /// Computes squared shortest distance from point P to line segment [A, B] and normalized projection parameter t.
    /// </summary>
    public static float DistanceToSegmentSq(Vector3 p, Vector3 a, Vector3 b, out float t)
    {
        Vector3 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.000001f)
        {
            t = 0f;
            return (p - a).sqrMagnitude;
        }

        t = Vector3.Dot(p - a, ab) / lenSq;
        float clampedT = Mathf.Clamp01(t);
        Vector3 closestPt = a + clampedT * ab;
        return (p - closestPt).sqrMagnitude;
    }

    /// <summary>
    /// Core logic for handling a cotton ball touching a stroke zone with real-time 1D slider tracking.
    /// - For Stroke 2 (Mons Pubis / isBidirectional): Allows initiation from EITHER StartPoint or EndPoint with 0 error.
    /// - For Strokes 1, 3, 4, 5, 6, 7, 8: Strictly requires starting at StartPoint (top) and wiping to EndPoint (bottom).
    /// - For Stroke 9 (Anus): Instant tap contact validation.
    /// </summary>
    public void ProcessCottonTouch(CottonState cotton, int touchedZoneIndex, Vector3 touchPos)
    {
        if (cotton == null) return;
        if (touchedZoneIndex < 0 || touchedZoneIndex >= 9) return;

        // MISTAKE LOCKOUT: Block all stroke interactions, swipe progression, and completion while the red error animation is playing!
        if (IsMistakeLockoutActive)
        {
            return;
        }

        // Decision Matrix only: strokes must be done with the Handling forceps, never the Pickup (transfer) forceps.
        if (cotton.currentHolder == ForcepsRole.Pickup && Time.time - lastPickupForcepsStrokeTime > 1.5f)
        {
            lastPickupForcepsStrokeTime = Time.time;
            EvaluationLogger.Instance.LogMatrixOnlyViolation(ClinicalViolationType.Forceps_Transfer_Error,
                "Stroke attempted with Pickup forceps", $"Zone {touchedZoneIndex + 1}");
        }

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
                EvaluationLogger.Instance.LogMatrixOnlyViolation(ClinicalViolationType.Reused_Cotton_Ball,
                    "Stroke attempted with used cotton", $"Zone {touchedZoneIndex + 1}");
            }
            return;
        }

        // =========================================================================
        // ACTIVE ZONE TOUCH: Progress and complete active stroke
        // =========================================================================
        if (touchedZoneIndex == currentStrokeIndex)
        {
            if (completedIndices.Contains(currentStrokeIndex)) return;

            lastActiveTouchTime = Time.time;

            // ---------------------------------------------------------------------
            // Stroke 9: Anus (Instant Tap / Contact Validation on Anus Target)
            // ---------------------------------------------------------------------
            if (currentStrokeIndex == 8)
            {
                OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, 1.0f, touchPos);
                CompleteCurrentStroke(cotton);
                return;
            }

            // ---------------------------------------------------------------------
            // Strokes 1 to 8: 1D Real-Time Slider Tracking
            // ---------------------------------------------------------------------
            GetZoneEndpoints(currentStrokeIndex, out Vector3 startPoint, out Vector3 endPoint);
            Vector3 strokeVec = endPoint - startPoint;
            float lenSq = strokeVec.sqrMagnitude;

            if (lenSq < 0.0001f)
            {
                OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, 1.0f, touchPos);
                CompleteCurrentStroke(cotton);
                return;
            }

            StrokeSliderTrack activeTrack = (sliderTracks != null && currentStrokeIndex >= 0 && currentStrokeIndex < sliderTracks.Length)
                ? sliderTracks[currentStrokeIndex]
                : null;
            bool isBidirectional = (activeTrack != null && activeTrack.isBidirectional) || (currentStrokeIndex == 1);

            // Project touch position onto stroke vector (0 = StartPoint, 1 = EndPoint)
            float rawT = Vector3.Dot(touchPos - startPoint, strokeVec) / lenSq;
            float clampedT = Mathf.Clamp01(rawT);

            // 1. Fresh Stroke Initiation:
            if (currentStrokeProgress <= 0.01f)
            {
                if (isBidirectional)
                {
                    // Bidirectional (StrokeSlider_2_Mons_Pubis): Accept start from EITHER StartPoint or EndPoint with zero error!
                    if (rawT <= 0.38f)
                    {
                        isCurrentStrokeReverse = false;
                        if (activeTrack != null) activeTrack.SetReverseFill(false);
                        currentStrokeProgress = Mathf.Max(0.05f, clampedT);
                        Vector3 handlePos = Vector3.Lerp(startPoint, endPoint, currentStrokeProgress);
                        OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, currentStrokeProgress, handlePos);
                    }
                    else if (rawT >= 0.62f)
                    {
                        isCurrentStrokeReverse = true;
                        if (activeTrack != null) activeTrack.SetReverseFill(true);
                        float revT = 1.0f - clampedT;
                        currentStrokeProgress = Mathf.Max(0.05f, revT);
                        Vector3 handlePos = Vector3.Lerp(endPoint, startPoint, currentStrokeProgress);
                        OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, currentStrokeProgress, handlePos);
                    }
                }
                else
                {
                    // Strict Top-to-Bottom Directional Strokes (Strokes 1, 3, 4, 5, 6, 7, 8):
                    // Player touches near Start Point / Top section (t <= 0.35) -> Initialize downward wipe!
                    if (rawT <= 0.35f)
                    {
                        isCurrentStrokeReverse = false;
                        if (activeTrack != null) activeTrack.SetReverseFill(false);
                        currentStrokeProgress = Mathf.Max(0.05f, clampedT);
                        Vector3 handlePos = Vector3.Lerp(startPoint, endPoint, currentStrokeProgress);
                        OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, currentStrokeProgress, handlePos);
                    }
                    // If player touches near End Point / Bottom (t >= 0.55) before starting at top -> Wrong direction attempt!
                    else if (rawT >= 0.55f)
                    {
                        HandleMistakeTouch(touchPos, currentStrokeIndex, "Incorrect technique! Start at the top and wipe downward.");
                    }
                }
            }
            // 2. Stroke In Progress:
            else
            {
                float effectiveT = isCurrentStrokeReverse ? (1.0f - clampedT) : clampedT;
                Vector3 effectiveStart = isCurrentStrokeReverse ? endPoint : startPoint;
                Vector3 effectiveEnd = isCurrentStrokeReverse ? startPoint : endPoint;

                // Advancing towards target opposite endpoint
                if (effectiveT > currentStrokeProgress)
                {
                    currentStrokeProgress = Mathf.MoveTowards(currentStrokeProgress, effectiveT, Time.deltaTime * 1.2f);
                    Vector3 handlePos = Vector3.Lerp(effectiveStart, effectiveEnd, currentStrokeProgress);
                    OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, currentStrokeProgress, handlePos);

                    // Validate completion if opposite endpoint threshold reached
                    if (currentStrokeProgress >= requiredProgressThreshold)
                    {
                        OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, 1.0f, effectiveEnd);
                        CompleteCurrentStroke(cotton);
                        return;
                    }
                }
                else if (isBidirectional)
                {
                    // For Mons Pubis, if the player switches ends early in the stroke, seamlessly flip direction without triggering any mistake!
                    if (currentStrokeProgress < 0.40f && effectiveT < 0.25f)
                    {
                        isCurrentStrokeReverse = !isCurrentStrokeReverse;
                        if (activeTrack != null) activeTrack.SetReverseFill(isCurrentStrokeReverse);
                        float flippedT = 1.0f - effectiveT;
                        currentStrokeProgress = Mathf.Max(0.05f, flippedT);
                        Vector3 newStart = isCurrentStrokeReverse ? endPoint : startPoint;
                        Vector3 newEnd = isCurrentStrokeReverse ? startPoint : endPoint;
                        Vector3 handlePos = Vector3.Lerp(newStart, newEnd, currentStrokeProgress);
                        OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, currentStrokeProgress, handlePos);
                    }
                }
                // Reverse upward wiping check for strict unidirectional strokes
                else if (effectiveT < currentStrokeProgress - 0.28f)
                {
                    HandleMistakeTouch(touchPos, currentStrokeIndex, "Wipe downward from top to bottom only!");
                }
            }

            lastTouchPos = touchPos;
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

            HandleMistakeTouch(touchPos, touchedZoneIndex, "Out of order! Take the correct anatomical stroke in sequence.");
        }
    }

    public void HandleMistakeTouch(Vector3 touchPos, int mistakeZoneIndex, string reason = "Stroke out of order!")
    {
        // Never trigger mistake on completed zones
        if (completedIndices.Contains(mistakeZoneIndex) || mistakeZoneIndex < currentStrokeIndex)
        {
            return;
        }

        // Lock out all interactions for the duration of the red animation
        lastMistakeTriggerTime = Time.time;
        lastTouchPos = Vector3.zero;
        currentStrokeProgress = 0f;
        isCurrentStrokeReverse = false;
        activeStrokeMistakeCount++;

        // Immediately reset any visual fill/bead progress on active slider
        if (sliderTracks != null && currentStrokeIndex >= 0 && currentStrokeIndex < sliderTracks.Length)
        {
            var activeTrack = sliderTracks[currentStrokeIndex];
            if (activeTrack != null)
            {
                activeTrack.ResetProgress();
            }
        }
        GetZoneEndpoints(currentStrokeIndex, out Vector3 startPt, out _);
        OnStrokeProgressUpdated?.Invoke(currentStrokeIndex, 0f, startPt);

        if (mistakeZoneIndex >= 0)
        {
            OnMistakeZoneTriggered?.Invoke(mistakeZoneIndex);
        }

        if (VRHeadsetVisualFeedback.Instance != null)
        {
            VRHeadsetVisualFeedback.Instance.TriggerMistakeFlash(1.2f);
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
        EvaluationLogger.Instance.LogCorrect($"Stroke {finishedIndex + 1} completed", MatrixCriterion.Technique, zoneName);

        // Immediately hide the completed stroke's visual guide and replace it with the Betadine puddle on the skin!
        if (sliderTracks != null && finishedIndex >= 0 && finishedIndex < sliderTracks.Length)
        {
            var track = sliderTracks[finishedIndex];
            if (track != null)
            {
                track.ResetProgress();
                track.SetVisualElementsVisible(false);

                if (track.puddleLayer != null)
                {
                    if (currentPhaseAntiseptic == AntisepticType.Iodine_7_5_Scrub)
                    {
                        track.puddleLayer.Apply7_5Puddle();
                    }
                    else if (currentPhaseAntiseptic == AntisepticType.Iodine_10_Paint)
                    {
                        track.puddleLayer.Apply10Coating();
                    }
                }
            }
        }

        // 1 Cotton per stroke rule: Cotton is spent immediately upon stroke completion
        if (cotton != null)
        {
            cotton.isUsed = true;
        }

        // Advance to next stroke and reset mistake count for the new stroke
        currentStrokeIndex++;
        currentStrokeProgress = 0f;
        isCurrentStrokeReverse = false;
        activeStrokeMistakeCount = 0;
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
