using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the 9-ball perineal scrub/paint technique using a Clean-to-Dirty stage sequence:
/// Stage 0: Right Labia Majora (fixed 1st)
/// Stage 1: Mons Pubis (fixed 2nd, bidirectional with true endpoint-to-endpoint drag validation)
/// Stage 2: Left Labia Majora OR Right Labia Minora (paired, both required)
/// Stage 3: Right Inner Thigh OR Left Inner Thigh (paired, both required)
/// Stage 4: Left Buttock OR Right Buttock (paired, both required)
/// Stage 5: Anus (fixed final, tap-only)
/// </summary>
public class StrokeTrackingManager : MonoBehaviour
{
    public static StrokeTrackingManager Instance { get; private set; }

    [Header("Sequence Configuration")]
    [Tooltip("All 9 zone definitions listed in clinical order.")]
    public List<StrokeZoneDefinition> allZonesInOrder = new List<StrokeZoneDefinition>();

    [Header("Rule-Based Knowledge Base")]
    [Tooltip("Optional ScriptableObject rule configuration. Uses thesis Clean-to-Dirty defaults if unassigned.")]
    public PerinealCareRuleSetSO ruleSet;

    [Header("Scene Stroke Sliders")]
    public StrokeSliderTrack[] sliderTracks = new StrokeSliderTrack[9];

    [Header("State")]
    public int currentStrokeIndex = 0;
    public int currentStageIndex = 0;
    public AntisepticType currentPhaseAntiseptic = AntisepticType.Iodine_7_5_Scrub;
    private readonly HashSet<int> completedIndices = new HashSet<int>();
    private readonly List<int> reusableValidZones = new List<int>(2);

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

    private PerinealRuleEngine ruleEngine;
    private float currentStrokeProgress = 0f;
    private bool isCurrentStrokeReverse = false;
    private int activeAttemptZoneIndex = -1;
    private bool hasActiveEntry = false;
    private float entryRawT = 0f;
    private bool passedMidpoint = false;
    private int activeStrokeMistakeCount = 0;
    private Vector3 lastTouchPos = Vector3.zero;
    private float lastMistakeTriggerTime = -99f;
    private float lastUsedCottonWarningTime = -99f;
    private float lastPickupForcepsStrokeTime = -99f;
    private float lastActiveTouchTime = -99f;
    private int lastCompletedZoneIndex = -1;
    private float lastCompletedTime = -99f;
    private CottonState activeSwipeCotton = null;
    private CottonState lastCompletedCotton = null;
    private float lastWrongAntisepticWarningTime = -99f;

    public event System.Action<int, StrokeZoneDefinition> OnStrokeAdvanced;
    public event System.Action<string, bool> OnStrokeValidated;
    public event System.Action<int> OnMistakeZoneTriggered;
    public event System.Action<int, float, Vector3> OnStrokeProgressUpdated; // (zoneIndex, progress 0-1, handleWorldPos)
    public event System.Action<RuleEvaluationResult> OnRuleEvaluated;

    public PerinealRuleEngine RuleEngine
    {
        get
        {
            if (ruleEngine == null)
            {
                EnsureRuleEngine();
            }
            return ruleEngine;
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        EnsureRuleEngine();
        CacheSliderTracks();
    }

    private void Start()
    {
        EnsureRuleEngine();
        if (sliderTracks == null || sliderTracks.Length == 0 || sliderTracks[0] == null)
        {
            CacheSliderTracks();
        }
    }

    private void OnEnable()
    {
        EnsureRuleEngine();
        if (sliderTracks == null || sliderTracks.Length == 0 || sliderTracks[0] == null)
        {
            CacheSliderTracks();
        }
    }

    public void EnsureRuleEngine()
    {
        PerinealRuleConfig config = ruleSet != null ? ruleSet.ToConfig() : PerinealRuleConfig.CreateDefault();
        ruleEngine = new PerinealRuleEngine(config);
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
        EnsureRuleEngine();
        currentPhaseAntiseptic = phaseAntiseptic;
        completedIndices.Clear();
        currentStageIndex = RuleEngine.ResolveStageIndex(completedIndices);
        currentStrokeIndex = RuleEngine.GetRepresentativeZoneIndex(currentStageIndex, completedIndices, 9);

        ResetActiveDragState();
        activeStrokeMistakeCount = 0;
        lastMistakeTriggerTime = -99f;
        lastUsedCottonWarningTime = -99f;
        lastPickupForcepsStrokeTime = -99f;
        lastCompletedZoneIndex = -1;
        lastCompletedTime = -99f;
        lastCompletedCotton = null;
        lastWrongAntisepticWarningTime = -99f;

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

        Debug.Log($"[StrokeTrackingManager] Phase Reset: {phaseAntiseptic} | Starting Stage 1 (Right Labia Majora).");

        var activeDef = GetCurrentActiveZone();
        OnStrokeAdvanced?.Invoke(currentStrokeIndex, activeDef);
    }

    public StrokeZoneDefinition GetZoneDefinition(int zoneIndex)
    {
        if (allZonesInOrder != null && zoneIndex >= 0 && zoneIndex < allZonesInOrder.Count)
        {
            return allZonesInOrder[zoneIndex];
        }
        return null;
    }

    public StrokeZoneDefinition GetCurrentActiveZone()
    {
        if (activeAttemptZoneIndex >= 0 && !completedIndices.Contains(activeAttemptZoneIndex))
        {
            var attemptDef = GetZoneDefinition(activeAttemptZoneIndex);
            if (attemptDef != null) return attemptDef;
        }
        return GetZoneDefinition(currentStrokeIndex);
    }

    public int GetCurrentStrokeIndex()
    {
        return currentStrokeIndex;
    }

    public int GetCurrentStageIndex()
    {
        return currentStageIndex;
    }

    public int GetActiveAttemptZoneIndex()
    {
        return activeAttemptZoneIndex;
    }

    public int GetCompletedStrokeCount()
    {
        return completedIndices.Count;
    }

    public int GetActiveStrokeMistakeCount()
    {
        return activeStrokeMistakeCount;
    }

    public int GetMistakesRequiredForHint()
    {
        return RuleEngine.Config.mistakesRequiredForHint;
    }

    public float GetCurrentStrokeProgress()
    {
        return currentStrokeProgress;
    }

    public bool IsStrokeCompleted(int index)
    {
        return completedIndices.Contains(index);
    }

    public bool IsStrokeValidNow(int zoneIndex)
    {
        return RuleEngine.IsZoneValidInStage(zoneIndex, currentStageIndex, completedIndices);
    }

    public void GetValidStrokeIndicesForCurrentStage(List<int> results)
    {
        RuleEngine.GetValidZonesForStage(currentStageIndex, completedIndices, results);
    }

    public string GetValidChoicesSummary()
    {
        RuleEngine.GetValidZonesForStage(currentStageIndex, completedIndices, reusableValidZones);
        if (reusableValidZones.Count == 0) return "Procedure Complete";
        if (reusableValidZones.Count == 1) return GetDisplayZoneName(reusableValidZones[0]);
        return $"{GetDisplayZoneName(reusableValidZones[0])} or {GetDisplayZoneName(reusableValidZones[1])}";
    }

    private string GetDisplayZoneName(int zoneIndex)
    {
        var def = GetZoneDefinition(zoneIndex);
        if (def != null && !string.IsNullOrEmpty(def.zoneName))
        {
            return def.zoneName;
        }
        return RuleEngine.Config.GetZoneConfig(zoneIndex).zoneName;
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
        // Abandonment check: if a stroke was started (> 1% progress) but player stopped touching for > strokeAbandonmentTimeout (1.0s), reset to 0%
        if (currentStrokeProgress > 0.01f && completedIndices.Count < 9 && activeAttemptZoneIndex >= 0)
        {
            if (Time.time - lastActiveTouchTime > strokeAbandonmentTimeout)
            {
                int abandonedZone = activeAttemptZoneIndex;
                ResetActiveDragStateAndNotify(abandonedZone);
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
    /// Preserves stickiness on an active in-progress swipe and prefers incomplete zones over already-completed puddles.
    /// </summary>
    public int GetZoneIndexForPoint(Vector3 worldPoint)
    {
        if (sliderTracks == null || sliderTracks.Length == 0) CacheSliderTracks();
        if (sliderTracks == null || sliderTracks.Length == 0) return -1;

        // Stick to an in-progress active zone while the cotton stays within its widened corridor so overlapping/adjacent starters don't steal focus mid-swipe
        if (activeAttemptZoneIndex >= 0 && hasActiveEntry && !completedIndices.Contains(activeAttemptZoneIndex)
            && activeAttemptZoneIndex < sliderTracks.Length && sliderTracks[activeAttemptZoneIndex] != null)
        {
            var activeTrack = sliderTracks[activeAttemptZoneIndex];
            float activeRadius = (activeTrack.isTapOnly ? tapTouchRadius : sliderTouchRadius) + 0.03f;
            float activeDistSq = GetDistanceSqToTrack(worldPoint, activeTrack, out float activeT);
            if (activeDistSq <= activeRadius * activeRadius && (activeTrack.isTapOnly || (activeT >= -0.35f && activeT <= 1.35f)))
            {
                return activeAttemptZoneIndex;
            }
        }

        int bestIncompleteIndex = -1;
        float bestIncompleteDistSq = float.MaxValue;
        int bestCompletedIndex = -1;
        float bestCompletedDistSq = float.MaxValue;

        for (int i = 0; i < sliderTracks.Length; i++)
        {
            var track = sliderTracks[i];
            if (track == null) continue;

            float radius = track.isTapOnly ? tapTouchRadius : sliderTouchRadius;
            float distSq = GetDistanceSqToTrack(worldPoint, track, out float t);

            if (distSq <= radius * radius && (track.isTapOnly || (t >= -0.20f && t <= 1.20f)))
            {
                if (completedIndices.Contains(i))
                {
                    if (distSq < bestCompletedDistSq)
                    {
                        bestCompletedDistSq = distSq;
                        bestCompletedIndex = i;
                    }
                }
                else
                {
                    if (distSq < bestIncompleteDistSq)
                    {
                        bestIncompleteDistSq = distSq;
                        bestIncompleteIndex = i;
                    }
                }
            }
        }

        return bestIncompleteIndex >= 0 ? bestIncompleteIndex : bestCompletedIndex;
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
    /// Evaluates a cotton ball contact against the formal PerinealRuleEngine production rules.
    /// - Enforces single-use cotton, forceps advisory, Clean-to-Dirty paired stage precedence,
    ///   endpoint-to-endpoint continuous drag validation for bidirectional Mons Pubis,
    ///   strict anterior-to-posterior direction on unidirectional zones, and tap completion for Anus.
    /// </summary>
    public void ProcessCottonTouch(CottonState cotton, int touchedZoneIndex, Vector3 touchPos)
    {
        if (cotton == null) return;
        if (touchedZoneIndex < 0 || touchedZoneIndex >= 9) return;
        if (IsMistakeLockoutActive) return;

        // Preserve explicit jar antiseptic; only fallback-assign when untagged (None)
        if (!cotton.isSoaked)
        {
            cotton.isSoaked = true;
        }
        if (cotton.antisepticType == AntisepticType.None)
        {
            cotton.antisepticType = currentPhaseAntiseptic;
        }

        // Guard against wrong antiseptic cotton touching the perineum (e.g., 7.5% carried past rinse into 10% paint)
        if (!cotton.isUsed && cotton.antisepticType != currentPhaseAntiseptic)
        {
            HandleWrongAntisepticTouch(touchedZoneIndex, cotton.antisepticType);
            return;
        }

        // Cross-zone follow-through grace right after completing a stroke so lifting off past a neighbor doesn't false-flag ReusedCottonBall
        if (cotton.isUsed && cotton == lastCompletedCotton && (Time.time - lastCompletedTime) < 0.70f)
        {
            return;
        }

        // Mid-swipe neighbor-starter graze shield: once a valid stroke has started with this cotton,
        // brushing an adjacent zone mid-swipe must not cancel progress or flash a false red error.
        if (hasActiveEntry
            && activeAttemptZoneIndex >= 0
            && !completedIndices.Contains(activeAttemptZoneIndex)
            && currentStrokeProgress > 0f
            && !cotton.isUsed
            && cotton == activeSwipeCotton
            && (Time.time - lastActiveTouchTime) <= strokeAbandonmentTimeout
            && touchedZoneIndex != activeAttemptZoneIndex)
        {
            return;
        }

        GetZoneEndpoints(touchedZoneIndex, out Vector3 startPoint, out Vector3 endPoint);
        Vector3 strokeVec = endPoint - startPoint;
        float lenSq = strokeVec.sqrMagnitude;
        bool degenerateTrack = lenSq < 0.0001f;

        float rawT = degenerateTrack ? 0f : Vector3.Dot(touchPos - startPoint, strokeVec) / lenSq;
        float clampedT = Mathf.Clamp01(rawT);
        float effectiveT = isCurrentStrokeReverse ? (1.0f - clampedT) : clampedT;
        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
        float nextMovedProgress = (activeAttemptZoneIndex == touchedZoneIndex && hasActiveEntry && effectiveT > currentStrokeProgress)
            ? Mathf.MoveTowards(currentStrokeProgress, effectiveT, dt * 3.5f)
            : currentStrokeProgress;

        float contactGap = hasActiveEntry && activeAttemptZoneIndex == touchedZoneIndex
            ? Mathf.Max(0f, Time.time - lastActiveTouchTime)
            : 0f;

        var facts = new PerinealFactContext
        {
            isHeldByPickupForceps = cotton.currentHolder == ForcepsRole.Pickup,
            isCottonUsed = cotton.isUsed,
            lastCompletedZoneIndex = lastCompletedZoneIndex,
            secondsSinceLastCompletion = Time.time - lastCompletedTime,
            touchedZoneIndex = touchedZoneIndex,
            degenerateTrackLength = degenerateTrack,
            rawT = rawT,
            clampedT = clampedT,
            activeAttemptZoneIndex = activeAttemptZoneIndex,
            hasActiveEntry = hasActiveEntry,
            entryRawT = entryRawT,
            contactGapSeconds = contactGap,
            isReverseDirection = isCurrentStrokeReverse,
            currentProgress = currentStrokeProgress,
            nextProgressTowardTarget = nextMovedProgress,
            passedMidpoint = passedMidpoint,
            completedZones = completedIndices,
            currentStageIndex = currentStageIndex,
            stageMistakes = activeStrokeMistakeCount
        };

        RuleEvaluationResult result = RuleEngine.Evaluate(in facts);
        OnRuleEvaluated?.Invoke(result);

        // Decision Matrix advisory: strokes should be performed with Handling forceps, never Pickup (transfer) forceps
        if (result.hasForcepsTransferAdvisory && Time.time - lastPickupForcepsStrokeTime > 1.5f)
        {
            lastPickupForcepsStrokeTime = Time.time;
            EvaluationLogger.Instance.LogMatrixOnlyViolation(
                ClinicalViolationType.Forceps_Transfer_Error,
                "Stroke attempted with Pickup forceps",
                $"Zone {touchedZoneIndex + 1}");
        }

        if (result.resetDragAttempt && activeAttemptZoneIndex >= 0 && activeAttemptZoneIndex != touchedZoneIndex)
        {
            ResetActiveDragStateAndNotify(activeAttemptZoneIndex);
        }

        switch (result.decision)
        {
            case PerinealRuleDecision.IgnoreAlreadyCompleted:
                return;

            case PerinealRuleDecision.RejectViolation:
                HandleRuleViolation(cotton, touchPos, touchedZoneIndex, result);
                return;

            case PerinealRuleDecision.AwaitStart:
                if (result.resetDragAttempt)
                {
                    ResetActiveDragStateAndNotify(touchedZoneIndex);
                }
                lastActiveTouchTime = Time.time;
                lastTouchPos = touchPos;
                return;

            case PerinealRuleDecision.AdvanceProgress:
                ApplyProgressUpdate(touchedZoneIndex, startPoint, endPoint, result);
                activeSwipeCotton = cotton;
                lastActiveTouchTime = Time.time;
                lastTouchPos = touchPos;
                return;

            case PerinealRuleDecision.CompleteStroke:
                Vector3 completionPos = degenerateTrack ? touchPos : (result.resolvedReverseDirection ? startPoint : endPoint);
                OnStrokeProgressUpdated?.Invoke(touchedZoneIndex, 1.0f, completionPos);
                CompleteCurrentStroke(cotton, touchedZoneIndex);
                return;
        }
    }

    private void ApplyProgressUpdate(int zoneIndex, Vector3 startPoint, Vector3 endPoint, in RuleEvaluationResult result)
    {
        if (activeAttemptZoneIndex >= 0 && activeAttemptZoneIndex != zoneIndex)
        {
            ResetActiveDragStateAndNotify(activeAttemptZoneIndex);
        }

        activeAttemptZoneIndex = zoneIndex;
        hasActiveEntry = true;
        entryRawT = result.resolvedEntryRawT;
        passedMidpoint = result.updatedPassedMidpoint;
        isCurrentStrokeReverse = result.resolvedReverseDirection;
        currentStrokeProgress = result.updatedProgress;
        currentStrokeIndex = zoneIndex;

        StrokeSliderTrack track = GetTrack(zoneIndex);
        if (track != null)
        {
            track.SetReverseFill(isCurrentStrokeReverse);
        }

        Vector3 effectiveStart = isCurrentStrokeReverse ? endPoint : startPoint;
        Vector3 effectiveEnd = isCurrentStrokeReverse ? startPoint : endPoint;
        Vector3 handlePos = Vector3.Lerp(effectiveStart, effectiveEnd, currentStrokeProgress);
        OnStrokeProgressUpdated?.Invoke(zoneIndex, currentStrokeProgress, handlePos);
    }

    private void HandleRuleViolation(CottonState cotton, Vector3 touchPos, int touchedZoneIndex, in RuleEvaluationResult result)
    {
        if (result.violation == PerinealRuleViolation.ReusedCottonBall)
        {
            if (Time.time - lastUsedCottonWarningTime > 1.5f)
            {
                lastUsedCottonWarningTime = Time.time;
                OnStrokeValidated?.Invoke(result.userFeedback, false);
                Debug.LogWarning($"[StrokeTrackingManager] [{result.ruleId}] {result.clinicalRationale}");

                if (VRDemoGameManager.Instance != null)
                {
                    int cottonId = cotton != null ? cotton.GetInstanceID() : GetInstanceID();
                    VRDemoGameManager.Instance.RecordCottonBallMistake(
                        cottonId,
                        ClinicalViolationType.Reused_Cotton_Ball,
                        "Reused spent cotton ball",
                        result.userFeedback,
                        $"Zone {touchedZoneIndex + 1}");
                }
                else
                {
                    EvaluationLogger.Instance.LogMatrixOnlyViolation(
                        ClinicalViolationType.Reused_Cotton_Ball,
                        "Stroke attempted with used cotton",
                        $"Zone {touchedZoneIndex + 1}");
                }

                if (VRHeadsetVisualFeedback.Instance != null)
                {
                    VRHeadsetVisualFeedback.Instance.PlayFeedbackCuesOnly(0.55f, 0.25f);
                }
            }
            return;
        }

        ClinicalViolationType mappedViolation = MapViolationType(result.violation);
        HandleMistakeTouch(touchPos, touchedZoneIndex, result.userFeedback, mappedViolation, result.ruleId);
    }

    private void HandleWrongAntisepticTouch(int touchedZoneIndex, AntisepticType attemptedAntiseptic)
    {
        float cooldown = mistakeLockoutDuration > 0f ? mistakeLockoutDuration : 0f;
        if (cooldown > 0f && (Time.time - lastWrongAntisepticWarningTime) < cooldown)
        {
            return;
        }

        lastWrongAntisepticWarningTime = Time.time;
        lastMistakeTriggerTime = Time.time;

        int resetZone = activeAttemptZoneIndex >= 0 ? activeAttemptZoneIndex : touchedZoneIndex;
        ResetActiveDragStateAndNotify(resetZone);

        if (touchedZoneIndex >= 0)
        {
            OnMistakeZoneTriggered?.Invoke(touchedZoneIndex);
        }

        string expectedLabel = CottonJarZone.FormatAntisepticName(currentPhaseAntiseptic);
        string attemptedLabel = CottonJarZone.FormatAntisepticName(attemptedAntiseptic);
        string reason = $"Wrong antiseptic ({attemptedLabel})! Use {expectedLabel} for this step.";

        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.RecordWrongAntisepticMistake(attemptedAntiseptic, currentPhaseAntiseptic, reason);
        }
        else
        {
            EvaluationLogger.Instance.LogMatrixOnlyViolation(
                ClinicalViolationType.Wrong_Antiseptic_Solution,
                "Wrong antiseptic cotton applied to perineum",
                reason);
        }

        if (VRHeadsetVisualFeedback.Instance != null)
        {
            VRHeadsetVisualFeedback.Instance.TriggerMistakeFlash(1.15f);
            VRHeadsetVisualFeedback.Instance.ShowInFaceHUD(
                "WRONG ANTISEPTIC",
                reason,
                new Color(0.94f, 0.27f, 0.27f, 1f),
                2.3f);
        }

        OnStrokeValidated?.Invoke(reason, false);
    }

    private static ClinicalViolationType MapViolationType(PerinealRuleViolation violation)
    {
        switch (violation)
        {
            case PerinealRuleViolation.ForcepsTransferError:
                return ClinicalViolationType.Forceps_Transfer_Error;
            case PerinealRuleViolation.ReusedCottonBall:
                return ClinicalViolationType.Reused_Cotton_Ball;
            case PerinealRuleViolation.ReverseWipeDirection:
            case PerinealRuleViolation.WrongStartCorridor:
                return ClinicalViolationType.Reverse_Wipe_Direction;
            default:
                return ClinicalViolationType.Out_Of_Order_Stroke;
        }
    }

    public void HandleMistakeTouch(
        Vector3 touchPos,
        int mistakeZoneIndex,
        string reason = "Stroke out of order!",
        ClinicalViolationType violationType = ClinicalViolationType.Out_Of_Order_Stroke,
        string ruleId = "R4_STAGE_SEQUENCE")
    {
        if (completedIndices.Contains(mistakeZoneIndex))
        {
            return;
        }

        lastMistakeTriggerTime = Time.time;
        int resetZone = activeAttemptZoneIndex >= 0 ? activeAttemptZoneIndex : currentStrokeIndex;
        ResetActiveDragStateAndNotify(resetZone);
        activeStrokeMistakeCount++;

        if (mistakeZoneIndex >= 0)
        {
            OnMistakeZoneTriggered?.Invoke(mistakeZoneIndex);
        }

        if (VRHeadsetVisualFeedback.Instance != null)
        {
            if (PerinealCareManager.Instance != null && VRDemoGameManager.Instance != null)
                VRHeadsetVisualFeedback.Instance.PlayFeedbackCuesOnly(0.55f, 0.28f);
            else
                VRHeadsetVisualFeedback.Instance.TriggerMistakeFlash(1.2f);
        }

        Debug.LogWarning($"[StrokeTrackingManager] [{ruleId}] {reason}");

        if (PerinealCareManager.Instance != null)
        {
            PerinealCareManager.Instance.RecordClinicalViolation(violationType, reason, 5);
        }

        OnStrokeValidated?.Invoke(reason, false);
    }

    private void CompleteCurrentStroke(CottonState cotton, int finishedIndex)
    {
        if (finishedIndex < 0 || finishedIndex >= 9 || completedIndices.Contains(finishedIndex)) return;

        completedIndices.Add(finishedIndex);
        lastCompletedZoneIndex = finishedIndex;
        lastCompletedTime = Time.time;
        lastCompletedCotton = cotton;

        string zoneName = GetDisplayZoneName(finishedIndex);
        Debug.Log($"[StrokeTrackingManager] Stroke Completed ({completedIndices.Count}/9): Zone {finishedIndex + 1} - {zoneName}");
        OnStrokeValidated?.Invoke($"{zoneName} Completed!", true);
        EvaluationLogger.Instance.LogCorrect($"Stroke {finishedIndex + 1} completed", MatrixCriterion.Technique, zoneName);

        // Immediately hide the completed stroke's visual guide and replace it with the Betadine puddle on the skin
        var track = GetTrack(finishedIndex);
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

        // 1 Cotton per stroke rule: Cotton is spent immediately upon stroke completion
        if (cotton != null)
        {
            cotton.isUsed = true;
        }

        ResetActiveDragState();
        activeStrokeMistakeCount = 0;

        currentStageIndex = RuleEngine.ResolveStageIndex(completedIndices);
        currentStrokeIndex = RuleEngine.GetRepresentativeZoneIndex(currentStageIndex, completedIndices, 9);

        var nextDef = GetCurrentActiveZone();
        OnStrokeAdvanced?.Invoke(currentStrokeIndex, nextDef);

        if (completedIndices.Count >= 9 || currentStageIndex >= RuleEngine.StageCount)
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

    private StrokeSliderTrack GetTrack(int zoneIndex)
    {
        if (sliderTracks != null && zoneIndex >= 0 && zoneIndex < sliderTracks.Length)
        {
            return sliderTracks[zoneIndex];
        }
        return null;
    }

    private void ResetActiveDragState()
    {
        currentStrokeProgress = 0f;
        isCurrentStrokeReverse = false;
        activeAttemptZoneIndex = -1;
        hasActiveEntry = false;
        entryRawT = 0f;
        passedMidpoint = false;
        lastTouchPos = Vector3.zero;
        lastActiveTouchTime = -99f;
        activeSwipeCotton = null;
    }

    private void ResetActiveDragStateAndNotify(int zoneToReset)
    {
        ResetActiveDragState();
        if (zoneToReset >= 0 && zoneToReset < 9)
        {
            var track = GetTrack(zoneToReset);
            if (track != null)
            {
                track.ResetProgress();
            }
            GetZoneEndpoints(zoneToReset, out Vector3 startPt, out _);
            OnStrokeProgressUpdated?.Invoke(zoneToReset, 0f, startPt);
        }
    }
}
