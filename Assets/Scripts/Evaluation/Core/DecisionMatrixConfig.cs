using System;

/// <summary>
/// Plain-data configuration for the Decision Matrix evaluator.
/// Built at runtime from DecisionMatrixSO (Inspector-editable), or directly in tests.
/// </summary>
public sealed class DecisionMatrixConfig
{
    public const int CriterionCount = 4;

    public static readonly string[] PlaceholderStepNames =
    {
        "Talk to Mother / Consent",
        "Preliminary Water Wash",
        "7.5% Iodine Scrub",
        "Antiseptic Rinse",
        "10% Iodine Paint",
        "Sterile Draping",
        "Setup Mayo Table"
    };

    /// <summary>
    /// Placeholder applicability, [step - 1, criterion]: Sterility, Sequence, Technique, Communication.
    /// A cell that receives a violation is always treated as applicable, even if marked N/A here.
    /// </summary>
    public static readonly bool[,] PlaceholderApplicability =
    {
        { false, true,  false, true  }, // 1 Consent: tool-before-consent (Sequence) + phrase board (Communication)
        { true,  true,  true,  false }, // 2 Water wash
        { true,  true,  true,  false }, // 3 7.5% scrub
        { true,  true,  true,  false }, // 4 Rinse
        { true,  true,  true,  false }, // 5 10% paint
        { true,  true,  true,  false }, // 6 Draping
        { true,  false, true,  false }, // 7 Mayo table
    };

    public string[] stepNames = (string[])PlaceholderStepNames.Clone();

    /// <summary>Weights indexed by (int)MatrixCriterion. Normalised over the criteria that have applicable cells.</summary>
    public float[] criterionWeights = { 0.35f, 0.25f, 0.25f, 0.15f };

    public float moderateDeduction = 25f;
    public float severeDeduction = 50f;

    /// <summary>[step - 1, criterion]</summary>
    public bool[,] applicable = (bool[,])PlaceholderApplicability.Clone();

    /// <summary>Step whose Communication cell is replaced by the phrase-board composite score.</summary>
    public int communicationStep = 1;

    public float passThreshold = 70f;
    public float honorsThreshold = 85f;

    public int StepCount => applicable.GetLength(0);

    public static DecisionMatrixConfig CreatePlaceholder() => new DecisionMatrixConfig();

    public float DeductionFor(MistakeSeverity severity)
    {
        switch (severity)
        {
            case MistakeSeverity.Moderate: return moderateDeduction;
            case MistakeSeverity.Severe: return severeDeduction;
            default: return 0f;
        }
    }

    public string StepName(int step)
    {
        int i = step - 1;
        return stepNames != null && i >= 0 && i < stepNames.Length ? stepNames[i] : $"Step {step}";
    }

    public void Validate()
    {
        if (criterionWeights == null || criterionWeights.Length != CriterionCount)
            throw new ArgumentException($"criterionWeights must have {CriterionCount} entries.");
        if (applicable == null || applicable.GetLength(1) != CriterionCount)
            throw new ArgumentException($"applicable must be [steps, {CriterionCount}].");
    }
}
