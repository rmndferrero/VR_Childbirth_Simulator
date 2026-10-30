using System.Collections.Generic;
using NUnit.Framework;

public class DecisionMatrixEvaluatorTests
{
    private static EvaluationEvent Mistake(int step, string type, MatrixCriterion c, MistakeSeverity s, float t = 0f, string capture = null) =>
        new EvaluationEvent { step = step, violationType = type, action = type, criterion = c, severity = s, time = t, captureId = capture };

    private static DecisionMatrixConfig Config() => DecisionMatrixConfig.CreatePlaceholder();

    [Test]
    public void NoEvents_NoCommunication_Scores100AndHonors()
    {
        var r = DecisionMatrixEvaluator.Evaluate(Config(), new List<EvaluationEvent>());
        Assert.AreEqual(100f, r.matrixScore, 0.001f);
        Assert.AreEqual("Honors", r.rating);
        Assert.IsTrue(r.passed);
        // Communication has no applicable cell except step 1, which is applicable by placeholder
        Assert.IsTrue(r.GetCriterion(MatrixCriterion.Communication).applicable);
    }

    [Test]
    public void ModerateViolation_Deducts25_FromItsCell()
    {
        var events = new List<EvaluationEvent> { Mistake(3, "Out_Of_Order_Stroke", MatrixCriterion.Sequence, MistakeSeverity.Moderate) };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        Assert.AreEqual(75f, r.GetCell(3, MatrixCriterion.Sequence).score, 0.001f);
        Assert.AreEqual(100f, r.GetCell(2, MatrixCriterion.Sequence).score, 0.001f);
    }

    [Test]
    public void SevereViolation_Deducts50_AndFlags_ButDoesNotFailOnItsOwn()
    {
        var events = new List<EvaluationEvent> { Mistake(7, "Unsterile_Mayo_Placement_Violation", MatrixCriterion.Sterility, MistakeSeverity.Severe) };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        var cell = r.GetCell(7, MatrixCriterion.Sterility);
        Assert.AreEqual(50f, cell.score, 0.001f);
        Assert.IsTrue(cell.hasSevere);
        Assert.AreEqual(1, r.severeCount);
        Assert.IsTrue(r.passed);
    }

    [Test]
    public void RepeatedViolation_DeductedOncePerStep_CountStillIncreases()
    {
        var events = new List<EvaluationEvent>
        {
            Mistake(3, "Reverse_Wipe_Direction", MatrixCriterion.Technique, MistakeSeverity.Moderate, 1f),
            Mistake(3, "Reverse_Wipe_Direction", MatrixCriterion.Technique, MistakeSeverity.Moderate, 2f),
            Mistake(3, "Reverse_Wipe_Direction", MatrixCriterion.Technique, MistakeSeverity.Moderate, 3f),
        };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        var cell = r.GetCell(3, MatrixCriterion.Technique);
        Assert.AreEqual(75f, cell.score, 0.001f);
        Assert.AreEqual(1, cell.distinctViolations);
        Assert.AreEqual(3, cell.totalOccurrences);
        Assert.AreEqual(1, r.mistakes.Count);
        Assert.AreEqual(3, r.mistakes[0].count);
    }

    [Test]
    public void SameTypeInDifferentSteps_DeductedInEachStep()
    {
        var events = new List<EvaluationEvent>
        {
            Mistake(3, "Out_Of_Order_Stroke", MatrixCriterion.Sequence, MistakeSeverity.Moderate),
            Mistake(5, "Out_Of_Order_Stroke", MatrixCriterion.Sequence, MistakeSeverity.Moderate),
        };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        Assert.AreEqual(75f, r.GetCell(3, MatrixCriterion.Sequence).score, 0.001f);
        Assert.AreEqual(75f, r.GetCell(5, MatrixCriterion.Sequence).score, 0.001f);
    }

    [Test]
    public void CellScore_ClampsAtZero()
    {
        var events = new List<EvaluationEvent>
        {
            Mistake(4, "Floor_Drop_Instrument", MatrixCriterion.Sterility, MistakeSeverity.Moderate),
            Mistake(4, "Reused_Cotton_Ball", MatrixCriterion.Sterility, MistakeSeverity.Moderate),
            Mistake(4, "Contaminated_Drop_Outside_Basin", MatrixCriterion.Sterility, MistakeSeverity.Severe),
        };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        Assert.AreEqual(0f, r.GetCell(4, MatrixCriterion.Sterility).score, 0.001f);
    }

    [Test]
    public void CriterionScore_IsMeanOfApplicableCells_ExcludingNA()
    {
        // Sterility applicable in steps 2-7 (6 cells); step 1 is N/A.
        var events = new List<EvaluationEvent> { Mistake(2, "Floor_Drop_Instrument", MatrixCriterion.Sterility, MistakeSeverity.Moderate) };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        Assert.IsFalse(r.GetCell(1, MatrixCriterion.Sterility).applicable);
        Assert.AreEqual((75f + 5 * 100f) / 6f, r.GetCriterion(MatrixCriterion.Sterility).score, 0.001f);
    }

    [Test]
    public void ViolationInNACell_MakesCellApplicable()
    {
        var events = new List<EvaluationEvent> { Mistake(1, "Floor_Drop_Instrument", MatrixCriterion.Sterility, MistakeSeverity.Moderate) };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        var cell = r.GetCell(1, MatrixCriterion.Sterility);
        Assert.IsTrue(cell.applicable);
        Assert.AreEqual(75f, cell.score, 0.001f);
        Assert.AreEqual((75f + 6 * 100f) / 7f, r.GetCriterion(MatrixCriterion.Sterility).score, 0.001f);
    }

    [Test]
    public void MatrixScore_IsWeightedSum()
    {
        var events = new List<EvaluationEvent> { Mistake(2, "Floor_Drop_Instrument", MatrixCriterion.Sterility, MistakeSeverity.Moderate) };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events, communicationScore: 60f);
        float sterility = (75f + 500f) / 6f;
        float expected = 0.35f * sterility + 0.25f * 100f + 0.25f * 100f + 0.15f * 60f;
        Assert.AreEqual(expected, r.matrixScore, 0.001f);
    }

    [Test]
    public void CommunicationCell_UsesPhraseBoardScore_WithoutDoubleDeduction()
    {
        var events = new List<EvaluationEvent> { Mistake(1, "Inadequate_Dialogue", MatrixCriterion.Communication, MistakeSeverity.Severe) };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events, communicationScore: 42f);
        var cell = r.GetCell(1, MatrixCriterion.Communication);
        Assert.IsTrue(cell.usesCommunicationScore);
        Assert.AreEqual(42f, cell.score, 0.001f);
        Assert.IsTrue(cell.hasSevere);
        Assert.AreEqual(1, r.severeCount);
        Assert.IsFalse(r.mistakes[0].scored);
    }

    [Test]
    public void CommunicationCell_FallsBackToDeductions_WhenNoPhraseBoardScore()
    {
        var events = new List<EvaluationEvent> { Mistake(1, "Suboptimal_Dialogue", MatrixCriterion.Communication, MistakeSeverity.Moderate) };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        Assert.AreEqual(75f, r.GetCell(1, MatrixCriterion.Communication).score, 0.001f);
    }

    [Test]
    public void WeightsAreRenormalised_WhenCriterionHasNoApplicableCells()
    {
        var config = Config();
        for (int s = 0; s < config.StepCount; s++) config.applicable[s, (int)MatrixCriterion.Communication] = false;
        var events = new List<EvaluationEvent> { Mistake(2, "Floor_Drop_Instrument", MatrixCriterion.Sterility, MistakeSeverity.Moderate) };
        var r = DecisionMatrixEvaluator.Evaluate(config, events);
        float sterility = (75f + 500f) / 6f;
        float expected = (0.35f * sterility + 0.25f * 100f + 0.25f * 100f) / 0.85f;
        Assert.AreEqual(expected, r.matrixScore, 0.001f);
        Assert.AreEqual(0f, r.GetCriterion(MatrixCriterion.Communication).effectiveWeight, 0.001f);
    }

    [Test]
    public void CorrectAndUnclassifiedEvents_AreNotScored()
    {
        var events = new List<EvaluationEvent>
        {
            new EvaluationEvent { step = 3, action = "Stroke 1 completed", isCorrect = true, criterion = MatrixCriterion.Technique },
            new EvaluationEvent { step = 3, action = "Some free-text mistake", criterion = MatrixCriterion.Unclassified, severity = MistakeSeverity.Moderate },
        };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        Assert.AreEqual(100f, r.matrixScore, 0.001f);
        Assert.AreEqual(1, r.unclassifiedCount);
        Assert.AreEqual(1, r.mistakes.Count);
    }

    [Test]
    public void Mistakes_SortedSevereFirst_ThenStep_AndKeepFirstCapture()
    {
        var events = new List<EvaluationEvent>
        {
            Mistake(2, "Floor_Drop_Instrument", MatrixCriterion.Sterility, MistakeSeverity.Moderate, 1f),
            Mistake(6, "Out_Of_Order_Drape", MatrixCriterion.Sequence, MistakeSeverity.Moderate, 5f),
            Mistake(7, "Unsterile_Mayo_Placement_Violation", MatrixCriterion.Sterility, MistakeSeverity.Severe, 9f, "cap_07"),
            Mistake(7, "Unsterile_Mayo_Placement_Violation", MatrixCriterion.Sterility, MistakeSeverity.Severe, 10f, "cap_08"),
        };
        var r = DecisionMatrixEvaluator.Evaluate(Config(), events);
        Assert.AreEqual("Unsterile_Mayo_Placement_Violation", r.mistakes[0].violationType);
        Assert.AreEqual("cap_07", r.mistakes[0].captureId);
        Assert.AreEqual(2, r.mistakes[1].step);
        Assert.AreEqual(6, r.mistakes[2].step);
    }

    [Test]
    public void Rating_UsesThresholds()
    {
        var config = Config();
        // Sequence + Technique fully failed in every applicable step -> well below 70.
        var events = new List<EvaluationEvent>();
        for (int s = 1; s <= 7; s++)
        {
            events.Add(Mistake(s, "A" + s, MatrixCriterion.Sterility, MistakeSeverity.Severe));
            events.Add(Mistake(s, "B" + s, MatrixCriterion.Sterility, MistakeSeverity.Severe));
            events.Add(Mistake(s, "C" + s, MatrixCriterion.Sequence, MistakeSeverity.Severe));
            events.Add(Mistake(s, "D" + s, MatrixCriterion.Sequence, MistakeSeverity.Severe));
        }
        var r = DecisionMatrixEvaluator.Evaluate(config, events, 100f);
        Assert.IsFalse(r.passed);
        Assert.AreEqual("Below Passing", r.rating);
    }
}
