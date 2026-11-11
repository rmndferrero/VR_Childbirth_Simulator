using System;
using System.Collections.Generic;

/// <summary>
/// Violation categories emitted by the forward-chaining PerinealRuleEngine.
/// Mapped cleanly by callers to ClinicalViolationType for DecisionMatrixEvaluator.
/// </summary>
public enum PerinealRuleViolation
{
    None = 0,
    ForcepsTransferError = 1,
    ReusedCottonBall = 2,
    OutOfOrderStroke = 3,
    ReverseWipeDirection = 4,
    WrongStartCorridor = 5,
    IncompleteSwipe = 6
}

/// <summary>
/// Deterministic consequence returned by PerinealRuleEngine.Evaluate.
/// </summary>
public enum PerinealRuleDecision
{
    AwaitStart = 0,
    AdvanceProgress = 1,
    CompleteStroke = 2,
    IgnoreAlreadyCompleted = 3,
    RejectViolation = 4,
    ResetAttempt = 5
}

/// <summary>
/// Declarative equivalence group in the 9-ball Clean-to-Dirty clinical precedence sequence.
/// Zones inside a paired stage may be performed in either order, provided all members complete
/// before advancing to the next anatomical stage.
/// </summary>
[Serializable]
public sealed class PerinealStageRule
{
    public int stageIndex;
    public string stageName;
    public int[] zoneIndices;
    public string prerequisiteFailureMessage;

    public PerinealStageRule() { }

    public PerinealStageRule(int stageIndex, string stageName, int[] zoneIndices, string prerequisiteFailureMessage)
    {
        this.stageIndex = stageIndex;
        this.stageName = stageName;
        this.zoneIndices = zoneIndices ?? Array.Empty<int>();
        this.prerequisiteFailureMessage = prerequisiteFailureMessage;
    }
}

/// <summary>
/// Per-zone kinematic and directional rules (e.g., bidirectional Mons Pubis vs top-to-bottom labia vs Anus tap).
/// </summary>
[Serializable]
public sealed class PerinealZoneRuleConfig
{
    public int zoneIndex;
    public string zoneName;
    public bool isTapOnly;
    public bool allowBidirectional;
    public float forwardStartMaxT = 0.35f;
    public float bidirectionalEndMinT = 0.65f;
    public float reverseMistakeMinT = 0.55f;
    public float requiredProgressThreshold = 0.85f;
    public float minimumDragSpan = 0.50f;
    public float midpointMinT = 0.30f;
    public float midpointMaxT = 0.70f;
    public float maxContactGapSeconds = 0.22f;
    public float reverseUpwardTolerance = 0.28f;
}

/// <summary>
/// Complete Knowledge Base configuration consumed by PerinealRuleEngine.
/// Created automatically with thesis-aligned defaults or populated from PerinealCareRuleSetSO.
/// </summary>
public sealed class PerinealRuleConfig
{
    public PerinealStageRule[] stages;
    public PerinealZoneRuleConfig[] zones;
    public int mistakesRequiredForHint = 3;

    public static PerinealRuleConfig CreateDefault()
    {
        return new PerinealRuleConfig
        {
            mistakesRequiredForHint = 3,
            stages = new[]
            {
                new PerinealStageRule(0, "Right Labia Majora", new[] { 0 },
                    "Out of order! Start with the Right Labia Majora first (cleanest-to-dirtiest sequence)."),
                new PerinealStageRule(1, "Mons Pubis", new[] { 1 },
                    "Out of order! Cleanse the Mons Pubis second before proceeding to labia or thighs."),
                new PerinealStageRule(2, "Left Labia Majora / Right Labia Minora", new[] { 2, 3 },
                    "Out of order! Complete both remaining labia strokes before inner thighs, buttocks, or anus."),
                new PerinealStageRule(3, "Inner Thighs", new[] { 4, 5 },
                    "Out of order! Cleanse both inner thighs before moving to buttocks or anus."),
                new PerinealStageRule(4, "Buttocks", new[] { 6, 7 },
                    "Out of order! Cleanse both buttocks before the final anus stroke."),
                new PerinealStageRule(5, "Anus (Final)", new[] { 8 },
                    "Out of order! Save the anus for last after all prior zones are completed.")
            },
            zones = new[]
            {
                MakeZone(0, "Right Labia Majora", false, false),
                MakeZone(1, "Mons Pubis", false, true),
                MakeZone(2, "Left Labia Majora", false, false),
                MakeZone(3, "Right Labia Minora", false, false),
                MakeZone(4, "Right Inner Thigh", false, false),
                MakeZone(5, "Left Inner Thigh", false, false),
                MakeZone(6, "Left Buttock", false, false),
                MakeZone(7, "Right Buttock", false, false),
                MakeZone(8, "Anus", true, false)
            }
        };
    }

    private static PerinealZoneRuleConfig MakeZone(int idx, string name, bool tapOnly, bool bidirectional)
    {
        return new PerinealZoneRuleConfig
        {
            zoneIndex = idx,
            zoneName = name,
            isTapOnly = tapOnly,
            allowBidirectional = bidirectional
        };
    }

    public PerinealZoneRuleConfig GetZoneConfig(int zoneIndex)
    {
        if (zones != null && zoneIndex >= 0 && zoneIndex < zones.Length && zones[zoneIndex] != null)
        {
            return zones[zoneIndex];
        }
        return MakeZone(zoneIndex, $"Zone {zoneIndex + 1}", zoneIndex == 8, zoneIndex == 1);
    }
}

/// <summary>
/// Working Memory snapshot fed into the Inference Engine on every cotton-to-zone evaluation step.
/// </summary>
public struct PerinealFactContext
{
    public bool isHeldByPickupForceps;
    public bool isCottonUsed;
    public int lastCompletedZoneIndex;
    public float secondsSinceLastCompletion;

    public int touchedZoneIndex;
    public bool degenerateTrackLength;
    public float rawT;
    public float clampedT;

    public int activeAttemptZoneIndex;
    public bool hasActiveEntry;
    public float entryRawT;
    public float contactGapSeconds;
    public bool isReverseDirection;
    public float currentProgress;
    public float nextProgressTowardTarget;
    public bool passedMidpoint;

    public ICollection<int> completedZones;
    public int currentStageIndex;
    public int stageMistakes;
}

/// <summary>
/// Explainable output emitted by PerinealRuleEngine for VR execution, UI hints, and Decision Matrix logging.
/// </summary>
public struct RuleEvaluationResult
{
    public string ruleId;
    public PerinealRuleDecision decision;
    public PerinealRuleViolation violation;
    public bool hasForcepsTransferAdvisory;
    public int targetZoneIndex;
    public bool resolvedReverseDirection;
    public float resolvedEntryRawT;
    public float updatedProgress;
    public bool updatedPassedMidpoint;
    public bool resetDragAttempt;
    public string userFeedback;
    public string clinicalRationale;
    public bool shouldHighlightHint;
}

/// <summary>
/// Deterministic Priority-Ordered Forward-Chaining Production System for the 9-Ball Perineal Care technique.
/// Pure C# (zero UnityEngine dependency) so rules can be EditMode-verified independently and integrated
/// cleanly with DecisionMatrixEvaluator via typed violations.
/// </summary>
public sealed class PerinealRuleEngine
{
    public const string RuleForcepsTransfer = "R1_HANDLING_FORCEPS";
    public const string RuleSingleUseCotton = "R2_SINGLE_USE_COTTON";
    public const string RulePostCompletionWithdrawal = "R2B_POST_STROKE_WITHDRAWAL";
    public const string RuleCompletedZoneGuard = "R3_COMPLETED_ZONE_GUARD";
    public const string RuleStageSequence = "R4_STAGE_SEQUENCE";
    public const string RuleTapCompletion = "R5A_TAP_TARGET";
    public const string RuleBidirectionalEntry = "R5B_BIDIRECTIONAL_ENTRY";
    public const string RuleDirectionalWipe = "R5C_DIRECTIONAL_WIPE";
    public const string RuleContinuousSwipeReset = "R6A_CONTINUOUS_SWIPE_RESET";
    public const string RuleSwipeProgress = "R6B_SWIPE_PROGRESS";
    public const string RuleCompleteStroke = "R6C_COMPLETE_STROKE";

    private readonly PerinealRuleConfig config;

    public PerinealRuleConfig Config => config;

    public PerinealRuleEngine(PerinealRuleConfig config = null)
    {
        this.config = config ?? PerinealRuleConfig.CreateDefault();
    }

    public int StageCount => config.stages != null ? config.stages.Length : 0;

    public bool IsZoneValidInStage(int zoneIndex, int stageIndex, ICollection<int> completedZones)
    {
        if (config.stages == null || stageIndex < 0 || stageIndex >= config.stages.Length)
            return false;

        if (completedZones != null && completedZones.Contains(zoneIndex))
            return false;

        int[] members = config.stages[stageIndex].zoneIndices;
        if (members == null) return false;

        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] == zoneIndex) return true;
        }
        return false;
    }

    public void GetValidZonesForStage(int stageIndex, ICollection<int> completedZones, List<int> results)
    {
        if (results == null) return;
        results.Clear();
        if (config.stages == null || stageIndex < 0 || stageIndex >= config.stages.Length)
            return;

        int[] members = config.stages[stageIndex].zoneIndices;
        if (members == null) return;

        for (int i = 0; i < members.Length; i++)
        {
            int idx = members[i];
            if (completedZones == null || !completedZones.Contains(idx))
            {
                results.Add(idx);
            }
        }
    }

    public int ResolveStageIndex(ICollection<int> completedZones)
    {
        if (config.stages == null || config.stages.Length == 0)
            return 0;

        for (int s = 0; s < config.stages.Length; s++)
        {
            int[] members = config.stages[s].zoneIndices;
            if (members == null || members.Length == 0) continue;

            for (int i = 0; i < members.Length; i++)
            {
                if (completedZones == null || !completedZones.Contains(members[i]))
                {
                    return s;
                }
            }
        }

        return config.stages.Length;
    }

    public int GetRepresentativeZoneIndex(int stageIndex, ICollection<int> completedZones, int fallbackTotalZones = 9)
    {
        if (config.stages == null || stageIndex < 0 || stageIndex >= config.stages.Length)
            return fallbackTotalZones;

        int[] members = config.stages[stageIndex].zoneIndices;
        if (members != null)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (completedZones == null || !completedZones.Contains(members[i]))
                {
                    return members[i];
                }
            }
        }

        return fallbackTotalZones;
    }

    public string FormatValidChoicesHint(int stageIndex, ICollection<int> completedZones)
    {
        var valid = new List<int>(2);
        GetValidZonesForStage(stageIndex, completedZones, valid);
        if (valid.Count == 0) return "Procedure Complete";
        if (valid.Count == 1) return config.GetZoneConfig(valid[0]).zoneName;
        return $"{config.GetZoneConfig(valid[0]).zoneName} or {config.GetZoneConfig(valid[1]).zoneName}";
    }

    public RuleEvaluationResult Evaluate(in PerinealFactContext facts)
    {
        var result = new RuleEvaluationResult
        {
            targetZoneIndex = facts.touchedZoneIndex,
            hasForcepsTransferAdvisory = facts.isHeldByPickupForceps,
            shouldHighlightHint = facts.stageMistakes >= config.mistakesRequiredForHint
        };

        // Priority 1/2: Single-use cotton rule (with brief withdrawal grace window on the just-finished zone)
        if (facts.isCottonUsed)
        {
            if (facts.touchedZoneIndex == facts.lastCompletedZoneIndex && facts.secondsSinceLastCompletion < 0.85f)
            {
                result.ruleId = RulePostCompletionWithdrawal;
                result.decision = PerinealRuleDecision.IgnoreAlreadyCompleted;
                result.clinicalRationale = "Ignoring continued proximity during immediate withdrawal from just-cleansed zone.";
                return result;
            }

            result.ruleId = RuleSingleUseCotton;
            result.decision = PerinealRuleDecision.RejectViolation;
            result.violation = PerinealRuleViolation.ReusedCottonBall;
            result.userFeedback = "Cotton already used! Take a fresh cotton ball from the tray.";
            result.clinicalRationale = "Reusing a contaminated cotton ball breaks sterile perineal care (Rule R2).";
            return result;
        }

        // Priority 3: Repeat-zone idempotency guard
        if (facts.completedZones != null && facts.completedZones.Contains(facts.touchedZoneIndex))
        {
            result.ruleId = RuleCompletedZoneGuard;
            result.decision = PerinealRuleDecision.IgnoreAlreadyCompleted;
            result.clinicalRationale = "Zone already completed; ignored to prevent double-counting or duplicate application (Rule R3).";
            return result;
        }

        // Priority 4: Clean-to-Dirty / Topological Stage Sequence Rule
        if (!IsZoneValidInStage(facts.touchedZoneIndex, facts.currentStageIndex, facts.completedZones))
        {
            result.ruleId = RuleStageSequence;
            result.decision = PerinealRuleDecision.RejectViolation;
            result.violation = PerinealRuleViolation.OutOfOrderStroke;
            result.shouldHighlightHint = (facts.stageMistakes + 1) >= config.mistakesRequiredForHint;
            result.userFeedback = BuildSequenceViolationMessage(facts.currentStageIndex, facts.completedZones);
            result.clinicalRationale = "Selected anatomical zone violates Clean-to-Dirty stage prerequisites (Rule R4).";
            return result;
        }

        PerinealZoneRuleConfig zoneCfg = config.GetZoneConfig(facts.touchedZoneIndex);

        // Priority 5A: Tap-only target (Stroke 9: Anus)
        if (zoneCfg.isTapOnly || facts.degenerateTrackLength)
        {
            result.ruleId = RuleTapCompletion;
            result.decision = PerinealRuleDecision.CompleteStroke;
            result.updatedProgress = 1.0f;
            result.userFeedback = $"{zoneCfg.zoneName} Completed!";
            result.clinicalRationale = "Final terminal contact target validated after all preceding stages completed (Rule R5A).";
            return result;
        }

        // Determine if this touch represents a fresh attempt on this zone
        bool sameActiveZone = facts.activeAttemptZoneIndex == facts.touchedZoneIndex;
        bool timedOutInProgress = sameActiveZone && facts.hasActiveEntry && facts.contactGapSeconds > zoneCfg.maxContactGapSeconds;
        bool freshAttempt = !sameActiveZone || !facts.hasActiveEntry || facts.currentProgress <= 0.01f || timedOutInProgress;

        if (freshAttempt)
        {
            return EvaluateFreshEntry(facts, zoneCfg, timedOutInProgress);
        }

        // Priority 5C / 6: Continuous wipe validation on an in-progress attempt
        return EvaluateInProgressSwipe(facts, zoneCfg);
    }

    private RuleEvaluationResult EvaluateFreshEntry(in PerinealFactContext facts, PerinealZoneRuleConfig zoneCfg, bool resetPrior)
    {
        var result = new RuleEvaluationResult
        {
            targetZoneIndex = facts.touchedZoneIndex,
            hasForcepsTransferAdvisory = facts.isHeldByPickupForceps,
            resetDragAttempt = resetPrior,
            shouldHighlightHint = facts.stageMistakes >= config.mistakesRequiredForHint
        };

        if (zoneCfg.allowBidirectional)
        {
            if (facts.rawT <= zoneCfg.forwardStartMaxT)
            {
                result.ruleId = RuleBidirectionalEntry;
                result.decision = PerinealRuleDecision.AdvanceProgress;
                result.resolvedReverseDirection = false;
                result.resolvedEntryRawT = facts.clampedT;
                result.updatedProgress = Math.Max(0.05f, facts.clampedT);
                result.updatedPassedMidpoint = IsInsideMidpointWindow(facts.rawT, zoneCfg);
                result.clinicalRationale = "Bidirectional swipe initiated from Start endpoint corridor (Rule R5B).";
                return result;
            }

            if (facts.rawT >= zoneCfg.bidirectionalEndMinT)
            {
                result.ruleId = RuleBidirectionalEntry;
                result.decision = PerinealRuleDecision.AdvanceProgress;
                result.resolvedReverseDirection = true;
                result.resolvedEntryRawT = facts.clampedT;
                result.updatedProgress = Math.Max(0.05f, 1.0f - facts.clampedT);
                result.updatedPassedMidpoint = IsInsideMidpointWindow(facts.rawT, zoneCfg);
                result.clinicalRationale = "Bidirectional swipe initiated from End endpoint corridor (Rule R5B).";
                return result;
            }

            result.ruleId = RuleBidirectionalEntry;
            result.decision = PerinealRuleDecision.AwaitStart;
            result.resolvedEntryRawT = facts.clampedT;
            result.updatedProgress = 0f;
            result.clinicalRationale = "Mid-track touch on bidirectional zone ignored until an endpoint is chosen (Rule R5B).";
            return result;
        }

        // Strict top-to-bottom directional track
        if (facts.rawT <= zoneCfg.forwardStartMaxT)
        {
            result.ruleId = RuleDirectionalWipe;
            result.decision = PerinealRuleDecision.AdvanceProgress;
            result.resolvedReverseDirection = false;
            result.resolvedEntryRawT = facts.clampedT;
            result.updatedProgress = Math.Max(0.05f, facts.clampedT);
            result.updatedPassedMidpoint = IsInsideMidpointWindow(facts.rawT, zoneCfg);
            result.clinicalRationale = "Directional anterior-to-posterior swipe initiated from top Start endpoint (Rule R5C).";
            return result;
        }

        if (facts.rawT >= zoneCfg.reverseMistakeMinT)
        {
            result.ruleId = RuleDirectionalWipe;
            result.decision = PerinealRuleDecision.RejectViolation;
            result.violation = PerinealRuleViolation.WrongStartCorridor;
            result.shouldHighlightHint = (facts.stageMistakes + 1) >= config.mistakesRequiredForHint;
            result.userFeedback = "Incorrect technique! Start at the top and wipe downward.";
            result.clinicalRationale = "Touching posterior endpoint before wiping downward violates anterior-to-posterior sterile rule (Rule R5C).";
            return result;
        }

        result.ruleId = RuleDirectionalWipe;
        result.decision = PerinealRuleDecision.AwaitStart;
        result.clinicalRationale = "Touch outside initial start corridor awaits valid top entry (Rule R5C).";
        return result;
    }

    private RuleEvaluationResult EvaluateInProgressSwipe(in PerinealFactContext facts, PerinealZoneRuleConfig zoneCfg)
    {
        float effectiveT = facts.isReverseDirection ? (1.0f - facts.clampedT) : facts.clampedT;
        bool passedMidpointNow = facts.passedMidpoint || IsInsideMidpointWindow(facts.rawT, zoneCfg);

        var result = new RuleEvaluationResult
        {
            targetZoneIndex = facts.touchedZoneIndex,
            hasForcepsTransferAdvisory = facts.isHeldByPickupForceps,
            resolvedReverseDirection = facts.isReverseDirection,
            resolvedEntryRawT = facts.entryRawT,
            updatedPassedMidpoint = passedMidpointNow,
            shouldHighlightHint = facts.stageMistakes >= config.mistakesRequiredForHint
        };

        // Directional reversal guard for strict unidirectional strokes
        if (!zoneCfg.allowBidirectional && effectiveT < facts.currentProgress - zoneCfg.reverseUpwardTolerance)
        {
            result.ruleId = RuleDirectionalWipe;
            result.decision = PerinealRuleDecision.RejectViolation;
            result.violation = PerinealRuleViolation.ReverseWipeDirection;
            result.shouldHighlightHint = (facts.stageMistakes + 1) >= config.mistakesRequiredForHint;
            result.userFeedback = "Wipe downward from top to bottom only!";
            result.clinicalRationale = "Reversed upward wiping drags perineal flora backward toward clean tissue (Rule R5C).";
            return result;
        }

        float candidateProgress = facts.currentProgress;
        if (effectiveT > facts.currentProgress)
        {
            candidateProgress = Math.Max(facts.currentProgress, facts.nextProgressTowardTarget);
        }

        float travelSpan = Math.Abs(facts.clampedT - facts.entryRawT);
        bool reachedThreshold = candidateProgress >= zoneCfg.requiredProgressThreshold;
        bool satisfiedDragSpan = travelSpan >= zoneCfg.minimumDragSpan;

        if (reachedThreshold && satisfiedDragSpan && passedMidpointNow)
        {
            result.ruleId = RuleCompleteStroke;
            result.decision = PerinealRuleDecision.CompleteStroke;
            result.updatedProgress = 1.0f;
            result.userFeedback = $"{zoneCfg.zoneName} Completed!";
            result.clinicalRationale = "Continuous swipe traversed midpoint and reached opposite endpoint threshold (Rule R6C).";
            return result;
        }

        result.ruleId = RuleSwipeProgress;
        result.decision = PerinealRuleDecision.AdvanceProgress;
        result.updatedProgress = reachedThreshold ? Math.Min(candidateProgress, zoneCfg.requiredProgressThreshold - 0.05f) : candidateProgress;
        result.clinicalRationale = "Tracking continuous cotton movement along anatomical stroke path (Rule R6B).";
        return result;
    }

    private static bool IsInsideMidpointWindow(float rawT, PerinealZoneRuleConfig cfg)
    {
        return rawT >= cfg.midpointMinT && rawT <= cfg.midpointMaxT;
    }

    private string BuildSequenceViolationMessage(int stageIndex, ICollection<int> completedZones)
    {
        if (config.stages == null || stageIndex < 0 || stageIndex >= config.stages.Length)
        {
            return "Out of order! Take the correct anatomical stroke in sequence.";
        }

        PerinealStageRule stage = config.stages[stageIndex];
        string choices = FormatValidChoicesHint(stageIndex, completedZones);
        if (!string.IsNullOrEmpty(stage.prerequisiteFailureMessage))
        {
            return stage.stageIndex >= 2 && stage.stageIndex <= 4
                ? $"Out of order! Complete {choices} first."
                : stage.prerequisiteFailureMessage;
        }

        return $"Out of order! Complete {choices} next in sequence.";
    }
}
