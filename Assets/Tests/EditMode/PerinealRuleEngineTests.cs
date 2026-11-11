using System.Collections.Generic;
using NUnit.Framework;

public class PerinealRuleEngineTests
{
    private PerinealRuleEngine engine;

    [SetUp]
    public void SetUp()
    {
        engine = new PerinealRuleEngine(PerinealRuleConfig.CreateDefault());
    }

    private static PerinealFactContext BaseFacts(int zone, float rawT, HashSet<int> completed, int stage)
    {
        return new PerinealFactContext
        {
            isHeldByPickupForceps = false,
            isCottonUsed = false,
            lastCompletedZoneIndex = -1,
            secondsSinceLastCompletion = 99f,
            touchedZoneIndex = zone,
            degenerateTrackLength = false,
            rawT = rawT,
            clampedT = rawT < 0f ? 0f : (rawT > 1f ? 1f : rawT),
            activeAttemptZoneIndex = -1,
            hasActiveEntry = false,
            entryRawT = 0f,
            contactGapSeconds = 0f,
            isReverseDirection = false,
            currentProgress = 0f,
            nextProgressTowardTarget = 0f,
            passedMidpoint = false,
            completedZones = completed,
            currentStageIndex = stage,
            stageMistakes = 0
        };
    }

    [Test]
    public void MonsPubis_TapStartOrEndWithoutDrag_DoesNotComplete()
    {
        var completed = new HashSet<int> { 0 };

        // Frame 1: touch Start corridor (rawT = 0.15)
        var f1 = BaseFacts(1, 0.15f, completed, 1);
        var r1 = engine.Evaluate(in f1);
        Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, r1.decision);
        Assert.IsFalse(r1.resolvedReverseDirection);

        // Frame 2..6: stay resting on Start (never reaches opposite endpoint, never flips reverse!)
        for (int i = 0; i < 5; i++)
        {
            var stay = BaseFacts(1, 0.15f, completed, 1);
            stay.activeAttemptZoneIndex = 1;
            stay.hasActiveEntry = true;
            stay.entryRawT = r1.resolvedEntryRawT;
            stay.currentProgress = r1.updatedProgress;
            stay.nextProgressTowardTarget = r1.updatedProgress;
            stay.passedMidpoint = r1.updatedPassedMidpoint;
            stay.contactGapSeconds = 0.016f;

            var rStay = engine.Evaluate(in stay);
            Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, rStay.decision);
            Assert.IsFalse(rStay.resolvedReverseDirection, "Mons Pubis must NOT flip reverse while resting at Start.");
            Assert.Less(rStay.updatedProgress, 0.25f);
        }

        // Now test resting near End (rawT = 0.88) from fresh state
        var end1 = BaseFacts(1, 0.88f, completed, 1);
        var re1 = engine.Evaluate(in end1);
        Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, re1.decision);
        Assert.IsTrue(re1.resolvedReverseDirection);

        var endStay = BaseFacts(1, 0.88f, completed, 1);
        endStay.activeAttemptZoneIndex = 1;
        endStay.hasActiveEntry = true;
        endStay.entryRawT = re1.resolvedEntryRawT;
        endStay.isReverseDirection = true;
        endStay.currentProgress = re1.updatedProgress;
        endStay.nextProgressTowardTarget = re1.updatedProgress;
        endStay.passedMidpoint = re1.updatedPassedMidpoint;
        endStay.contactGapSeconds = 0.016f;

        var reStay = engine.Evaluate(in endStay);
        Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, reStay.decision);
        Assert.Less(reStay.updatedProgress, 0.25f);
    }

    [Test]
    public void MonsPubis_GapTimeoutBetweenEndpoints_ResetsInsteadOfCompleting()
    {
        var completed = new HashSet<int> { 0 };
        var startTap = BaseFacts(1, 0.12f, completed, 1);
        var rStart = engine.Evaluate(in startTap);

        // User lifts cotton for 0.35s (> maxContactGapSeconds 0.22s) and taps opposite endpoint 0.90
        var lateEndTap = BaseFacts(1, 0.90f, completed, 1);
        lateEndTap.activeAttemptZoneIndex = 1;
        lateEndTap.hasActiveEntry = true;
        lateEndTap.entryRawT = rStart.resolvedEntryRawT;
        lateEndTap.currentProgress = rStart.updatedProgress;
        lateEndTap.nextProgressTowardTarget = 0.90f;
        lateEndTap.passedMidpoint = true;
        lateEndTap.contactGapSeconds = 0.35f;

        var rLate = engine.Evaluate(in lateEndTap);
        Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, rLate.decision);
        Assert.IsTrue(rLate.resetDragAttempt);
        Assert.IsTrue(rLate.resolvedReverseDirection, "Timeout must start a brand-new reverse attempt, not complete the old tap.");
        Assert.Less(rLate.updatedProgress, 0.25f);
    }

    [Test]
    public void MonsPubis_ContinuousDragBothDirections_Completes()
    {
        var completed = new HashSet<int> { 0 };

        // Forward: 0.12 -> 0.50 -> 0.90
        var fStart = BaseFacts(1, 0.12f, completed, 1);
        var r1 = engine.Evaluate(in fStart);
        Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, r1.decision);

        var fMid = BaseFacts(1, 0.50f, completed, 1);
        fMid.activeAttemptZoneIndex = 1;
        fMid.hasActiveEntry = true;
        fMid.entryRawT = r1.resolvedEntryRawT;
        fMid.currentProgress = r1.updatedProgress;
        fMid.nextProgressTowardTarget = 0.50f;
        fMid.passedMidpoint = r1.updatedPassedMidpoint;
        fMid.contactGapSeconds = 0.02f;
        var r2 = engine.Evaluate(in fMid);
        Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, r2.decision);
        Assert.IsTrue(r2.updatedPassedMidpoint);

        var fEnd = BaseFacts(1, 0.90f, completed, 1);
        fEnd.activeAttemptZoneIndex = 1;
        fEnd.hasActiveEntry = true;
        fEnd.entryRawT = r2.resolvedEntryRawT;
        fEnd.currentProgress = r2.updatedProgress;
        fEnd.nextProgressTowardTarget = 0.90f;
        fEnd.passedMidpoint = r2.updatedPassedMidpoint;
        fEnd.contactGapSeconds = 0.02f;
        var r3 = engine.Evaluate(in fEnd);
        Assert.AreEqual(PerinealRuleDecision.CompleteStroke, r3.decision);

        // Reverse: 0.88 -> 0.50 -> 0.10
        var bStart = BaseFacts(1, 0.88f, completed, 1);
        var b1 = engine.Evaluate(in bStart);
        Assert.IsTrue(b1.resolvedReverseDirection);

        var bMid = BaseFacts(1, 0.50f, completed, 1);
        bMid.activeAttemptZoneIndex = 1;
        bMid.hasActiveEntry = true;
        bMid.isReverseDirection = true;
        bMid.entryRawT = b1.resolvedEntryRawT;
        bMid.currentProgress = b1.updatedProgress;
        bMid.nextProgressTowardTarget = 0.50f;
        bMid.passedMidpoint = b1.updatedPassedMidpoint;
        bMid.contactGapSeconds = 0.02f;
        var b2 = engine.Evaluate(in bMid);
        Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, b2.decision);
        Assert.IsTrue(b2.updatedPassedMidpoint);

        var bEnd = BaseFacts(1, 0.10f, completed, 1);
        bEnd.activeAttemptZoneIndex = 1;
        bEnd.hasActiveEntry = true;
        bEnd.isReverseDirection = true;
        bEnd.entryRawT = b2.resolvedEntryRawT;
        bEnd.currentProgress = b2.updatedProgress;
        bEnd.nextProgressTowardTarget = 0.90f;
        bEnd.passedMidpoint = b2.updatedPassedMidpoint;
        bEnd.contactGapSeconds = 0.02f;
        var b3 = engine.Evaluate(in bEnd);
        Assert.AreEqual(PerinealRuleDecision.CompleteStroke, b3.decision);
    }

    [Test]
    public void FlexibleStageSequence_AllowsPairedOrderAndBlocksSkipping()
    {
        var completed = new HashSet<int>();
        Assert.AreEqual(0, engine.ResolveStageIndex(completed));

        // Skip Stage 0 (zone 0) directly to Mons Pubis (zone 1) -> must reject OutOfOrderStroke
        var skip0 = BaseFacts(1, 0.15f, completed, 0);
        var rSkip0 = engine.Evaluate(in skip0);
        Assert.AreEqual(PerinealRuleDecision.RejectViolation, rSkip0.decision);
        Assert.AreEqual(PerinealRuleViolation.OutOfOrderStroke, rSkip0.violation);

        // Complete Stage 0 & 1
        completed.Add(0);
        completed.Add(1);
        Assert.AreEqual(2, engine.ResolveStageIndex(completed));

        // Stage 2 allows zone 3 (Right Labia Minora) BEFORE zone 2 (Left Labia Majora)
        Assert.IsTrue(engine.IsZoneValidInStage(2, 2, completed));
        Assert.IsTrue(engine.IsZoneValidInStage(3, 2, completed));
        Assert.IsFalse(engine.IsZoneValidInStage(4, 2, completed)); // Thigh locked

        completed.Add(3);
        Assert.AreEqual(2, engine.ResolveStageIndex(completed), "Stage 2 stays open until Left Labia Majora (2) also completes.");
        Assert.IsFalse(engine.IsZoneValidInStage(3, 2, completed));
        Assert.IsTrue(engine.IsZoneValidInStage(2, 2, completed));

        // Attempting Thigh (4) while Labia (2) remains -> reject
        var prematureThigh = BaseFacts(4, 0.10f, completed, 2);
        var rPrematureThigh = engine.Evaluate(in prematureThigh);
        Assert.AreEqual(PerinealRuleDecision.RejectViolation, rPrematureThigh.decision);
        Assert.AreEqual(PerinealRuleViolation.OutOfOrderStroke, rPrematureThigh.violation);

        // Finish Labia pair -> Stage 3 (Thighs: 4 & 5)
        completed.Add(2);
        Assert.AreEqual(3, engine.ResolveStageIndex(completed));
        completed.Add(5); // Left thigh first
        Assert.AreEqual(3, engine.ResolveStageIndex(completed));
        Assert.IsFalse(engine.IsZoneValidInStage(6, 3, completed)); // Buttock locked
        completed.Add(4);

        // Stage 4 (Buttocks: 6 & 7)
        Assert.AreEqual(4, engine.ResolveStageIndex(completed));
        completed.Add(7); // Right buttock first
        Assert.AreEqual(4, engine.ResolveStageIndex(completed));
        Assert.IsFalse(engine.IsZoneValidInStage(8, 4, completed)); // Anus locked
        completed.Add(6);

        // Stage 5 (Anus: 8) unlocked last
        Assert.AreEqual(5, engine.ResolveStageIndex(completed));
        var anusTap = BaseFacts(8, 0.0f, completed, 5);
        var rAnus = engine.Evaluate(in anusTap);
        Assert.AreEqual(PerinealRuleDecision.CompleteStroke, rAnus.decision);

        completed.Add(8);
        Assert.AreEqual(engine.StageCount, engine.ResolveStageIndex(completed));
    }

    [Test]
    public void StrictDirectionUsedCottonAndHintThresholdRules_FireCleanly()
    {
        var completed = new HashSet<int>();

        // R5C: Starting at bottom (rawT = 0.80) on Right Labia Majora triggers WrongStartCorridor
        var wrongBottom = BaseFacts(0, 0.80f, completed, 0);
        wrongBottom.stageMistakes = 2; // 3rd mistake triggers hint flag
        var rBottom = engine.Evaluate(in wrongBottom);
        Assert.AreEqual(PerinealRuleDecision.RejectViolation, rBottom.decision);
        Assert.AreEqual(PerinealRuleViolation.WrongStartCorridor, rBottom.violation);
        Assert.IsTrue(rBottom.shouldHighlightHint);

        // R5C: Reversing upward mid-stroke
        var reverseUp = BaseFacts(0, 0.20f, completed, 0);
        reverseUp.activeAttemptZoneIndex = 0;
        reverseUp.hasActiveEntry = true;
        reverseUp.entryRawT = 0.10f;
        reverseUp.currentProgress = 0.65f;
        reverseUp.nextProgressTowardTarget = 0.65f;
        reverseUp.passedMidpoint = true;
        var rReverse = engine.Evaluate(in reverseUp);
        Assert.AreEqual(PerinealRuleDecision.RejectViolation, rReverse.decision);
        Assert.AreEqual(PerinealRuleViolation.ReverseWipeDirection, rReverse.violation);

        // R2 vs R2B: Used cotton immediately during withdrawal on just-completed zone ignores, else rejects
        var withdrawal = BaseFacts(0, 0.90f, completed, 1);
        withdrawal.isCottonUsed = true;
        withdrawal.lastCompletedZoneIndex = 0;
        withdrawal.secondsSinceLastCompletion = 0.25f;
        var rWithdraw = engine.Evaluate(in withdrawal);
        Assert.AreEqual(PerinealRuleDecision.IgnoreAlreadyCompleted, rWithdraw.decision);

        var reusedOnNext = BaseFacts(1, 0.15f, new HashSet<int> { 0 }, 1);
        reusedOnNext.isCottonUsed = true;
        reusedOnNext.lastCompletedZoneIndex = 0;
        reusedOnNext.secondsSinceLastCompletion = 1.5f;
        var rReuse = engine.Evaluate(in reusedOnNext);
        Assert.AreEqual(PerinealRuleDecision.RejectViolation, rReuse.decision);
        Assert.AreEqual(PerinealRuleViolation.ReusedCottonBall, rReuse.violation);

        // R1: Holding cotton in Pickup forceps flags advisory without rejecting valid stroke
        var pickupAdvisory = BaseFacts(0, 0.10f, completed, 0);
        pickupAdvisory.isHeldByPickupForceps = true;
        var rPickup = engine.Evaluate(in pickupAdvisory);
        Assert.AreEqual(PerinealRuleDecision.AdvanceProgress, rPickup.decision);
        Assert.IsTrue(rPickup.hasForcepsTransferAdvisory);
    }

    [Test]
    public void DecisionMatrixEvaluator_IntegratesRuleEngineViolationsOnStep3()
    {
        var cfg = DecisionMatrixConfig.CreatePlaceholder();
        var events = new List<EvaluationEvent>
        {
            new EvaluationEvent { step = 3, violationType = "Out_Of_Order_Stroke", action = "Out of order", criterion = MatrixCriterion.Sequence, severity = MistakeSeverity.Moderate, time = 1f },
            new EvaluationEvent { step = 3, violationType = "Reverse_Wipe_Direction", action = "Wrong direction", criterion = MatrixCriterion.Technique, severity = MistakeSeverity.Moderate, time = 2f }
        };

        var report = DecisionMatrixEvaluator.Evaluate(cfg, events);
        Assert.AreEqual(75f, report.GetCell(3, MatrixCriterion.Sequence).score, 0.001f);
        Assert.AreEqual(75f, report.GetCell(3, MatrixCriterion.Technique).score, 0.001f);
        Assert.AreEqual(100f, report.GetCell(3, MatrixCriterion.Sterility).score, 0.001f);
        Assert.Less(report.matrixScore, 100f);
    }
}
