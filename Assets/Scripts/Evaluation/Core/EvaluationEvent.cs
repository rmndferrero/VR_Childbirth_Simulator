using System;

/// <summary>
/// Decision Matrix criteria (matrix columns). Unclassified events are logged but never scored.
/// </summary>
public enum MatrixCriterion
{
    Unclassified = -1,
    Sterility = 0,
    Sequence = 1,
    Technique = 2,
    Communication = 3
}

/// <summary>
/// Severity tiers from the thesis proposal: No Mistake (0), Moderate (-25 cell points), Severe (-50 + flag).
/// </summary>
public enum MistakeSeverity
{
    NoMistake = 0,
    Moderate = 1,
    Severe = 2
}

/// <summary>
/// One structured entry in the evaluation log. Correct actions and violations share this shape
/// so the timeline, the Decision Matrix and the exported record all read from the same list.
/// Pure C# (no UnityEngine dependency) so the evaluator can be EditMode-tested in isolation.
/// </summary>
[Serializable]
public class EvaluationEvent
{
    public float time;                 // seconds since the session started
    public int step;                   // SimulationPhase as int (1-8)
    public string action;              // short human-readable action / mistake text
    public bool isCorrect;
    public string violationType;       // ClinicalViolationType name, empty for correct or unclassified actions
    public MatrixCriterion criterion = MatrixCriterion.Unclassified;
    public MistakeSeverity severity = MistakeSeverity.NoMistake;
    public int count;                  // occurrence number of this violation type within this step (1 = first)
    public string detail;
    public string captureId;           // gameplay screenshot id, filled by the capture service

    // Legacy (100-minus-penalties) bookkeeping, kept for the instructor report only.
    public int legacyPenalty;
    public bool legacyPenaltyApplied;

    /// <summary>Key used to group repeats: the violation type, or the action text when unclassified.</summary>
    public string GroupKey => string.IsNullOrEmpty(violationType) ? action : violationType;
}

/// <summary>
/// Start / end time of one simulation step, in seconds since the session started.
/// </summary>
[Serializable]
public class StepTiming
{
    public int step;
    public float startTime;
    public float endTime = -1f;

    public float Duration => endTime < 0f ? 0f : endTime - startTime;
}
