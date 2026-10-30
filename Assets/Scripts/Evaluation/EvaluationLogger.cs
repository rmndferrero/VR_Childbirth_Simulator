using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Structured evaluation log for the Decision Matrix (parallel to the legacy score).
/// Plain C# singleton: VRDemoGameManager binds it on Awake and resets it in InitializeSimulation.
/// It only records; it never changes gameplay or VRDemoGameManager.currentScore.
/// </summary>
public class EvaluationLogger
{
    private static EvaluationLogger instance;
    public static EvaluationLogger Instance => instance ??= new EvaluationLogger();

    public readonly List<EvaluationEvent> events = new List<EvaluationEvent>();
    public readonly List<StepTiming> stepTimings = new List<StepTiming>();

    /// <summary>Raised after every logged event (capture service / live UI can listen).</summary>
    public event Action<EvaluationEvent> OnEventLogged;

    /// <summary>Raised by ResetSession with the new session id (used to name capture / export files).</summary>
    public event Action<string> OnSessionReset;

    public string SessionId { get; private set; } = DateTime.Now.ToString("yyyyMMdd_HHmmss");

    private VRDemoGameManager boundManager;
    private DecisionMatrixSO matrix;
    private float sessionStartTime;
    private readonly Dictionary<string, int> occurrenceCounts = new Dictionary<string, int>();

    public DecisionMatrixSO Matrix
    {
        get
        {
            if (matrix == null) matrix = ScriptableObject.CreateInstance<DecisionMatrixSO>(); // placeholder defaults
            return matrix;
        }
        set => matrix = value;
    }

    public float SessionTime => Time.time - sessionStartTime;

    private int CurrentStep => boundManager != null ? (int)boundManager.currentSimulationPhase : 0;

    public void Bind(VRDemoGameManager manager)
    {
        if (boundManager == manager) return;
        if (boundManager != null) boundManager.OnSimulationPhaseChanged -= HandlePhaseChanged;
        boundManager = manager;
        if (boundManager != null)
        {
            boundManager.OnSimulationPhaseChanged += HandlePhaseChanged;
            if (boundManager.decisionMatrix != null) matrix = boundManager.decisionMatrix;
        }
    }

    public void ResetSession()
    {
        events.Clear();
        stepTimings.Clear();
        occurrenceCounts.Clear();
        sessionStartTime = Time.time;
        SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        EvaluationReportExporter.ResetSaveState();
        OnSessionReset?.Invoke(SessionId);
    }

    // ── Step timing ──

    private void HandlePhaseChanged(SimulationPhase phase)
    {
        float now = SessionTime;
        int step = (int)phase;
        var open = stepTimings.Count > 0 ? stepTimings[stepTimings.Count - 1] : null;
        if (open != null && open.endTime < 0f)
        {
            if (open.step == step) return;
            open.endTime = now;
        }
        stepTimings.Add(new StepTiming { step = step, startTime = now });
    }

    // ── Logging API ──

    public EvaluationEvent LogCorrect(string action, MatrixCriterion criterion = MatrixCriterion.Unclassified, string detail = "")
    {
        return Add(new EvaluationEvent
        {
            time = SessionTime,
            step = CurrentStep,
            action = action,
            isCorrect = true,
            criterion = criterion,
            severity = MistakeSeverity.NoMistake,
            detail = detail
        });
    }

    public EvaluationEvent LogViolation(ClinicalViolationType type, string action, string detail, int legacyPenalty, bool legacyPenaltyApplied)
    {
        var e = new EvaluationEvent
        {
            time = SessionTime,
            step = CurrentStep,
            action = action,
            violationType = type.ToString(),
            detail = detail,
            legacyPenalty = legacyPenalty,
            legacyPenaltyApplied = legacyPenaltyApplied
        };
        if (Matrix.TryGetRule(type, out var rule))
        {
            e.criterion = rule.criterion;
            e.severity = rule.severity;
        }
        else
        {
            e.severity = MistakeSeverity.Moderate;
        }
        return Add(e);
    }

    /// <summary>Logs a violation to the Decision Matrix only; the legacy score is not touched.</summary>
    public EvaluationEvent LogMatrixOnlyViolation(ClinicalViolationType type, string action, string detail = "")
    {
        return LogViolation(type, action, detail, 0, false);
    }

    /// <summary>Free-text mistakes (RecordMistake) are classified by keyword; unknown text is logged unclassified.</summary>
    public EvaluationEvent LogFreeTextMistake(string text, int legacyPenalty, bool legacyPenaltyApplied)
    {
        string readable = string.IsNullOrEmpty(text) ? "Mistake" : text.Replace('_', ' ');
        if (TryClassifyMistakeText(text, out var type))
            return LogViolation(type, readable, text, legacyPenalty, legacyPenaltyApplied);

        return Add(new EvaluationEvent
        {
            time = SessionTime,
            step = CurrentStep,
            action = readable,
            criterion = MatrixCriterion.Unclassified,
            severity = MistakeSeverity.Moderate,
            detail = text,
            legacyPenalty = legacyPenalty,
            legacyPenaltyApplied = legacyPenaltyApplied
        });
    }

    public static bool TryClassifyMistakeText(string text, out ClinicalViolationType type)
    {
        type = default;
        if (string.IsNullOrEmpty(text)) return false;
        string t = text.ToLowerInvariant();

        if (t.Contains("out of order")) type = ClinicalViolationType.Out_Of_Order_Stroke;
        else if (t.Contains("wipe downward") || t.Contains("top to bottom")) type = ClinicalViolationType.Reverse_Wipe_Direction;
        else if (t.Contains("consent")) type = ClinicalViolationType.Tool_Handled_Before_Consent;
        else if (t.Contains("improper communication")) type = ClinicalViolationType.Suboptimal_Dialogue;
        else if (t.Contains("mayo table setup") || t.Contains("misplaced")) type = ClinicalViolationType.Incorrect_Table_Slot;
        else if (t.Contains("already used") || t.Contains("reused")) type = ClinicalViolationType.Reused_Cotton_Ball;
        else return false;
        return true;
    }

    private EvaluationEvent Add(EvaluationEvent e)
    {
        if (!e.isCorrect)
        {
            string key = $"{e.step}|{e.GroupKey}";
            occurrenceCounts.TryGetValue(key, out int n);
            occurrenceCounts[key] = ++n;
            e.count = n;
        }
        events.Add(e);

        // Severe response: red POV tint only (no blocking, no score change). Restarting an existing flash is harmless.
        if (!e.isCorrect && e.severity == MistakeSeverity.Severe && VRHeadsetVisualFeedback.Instance != null)
        {
            VRHeadsetVisualFeedback.Instance.FlashRedVignette(1.6f);
        }

        OnEventLogged?.Invoke(e);
        return e;
    }

    // ── Evaluation ──

    public DecisionMatrixResult Evaluate(SimulationScoringRubricSO rubric, CommunicationDebriefData communication)
    {
        // The serialized debrief field is never null on the manager; all-zero means the phrase board never ran.
        bool hasComm = communication != null &&
                       (communication.compositeScore > 0f || communication.professionalismPercent > 0f ||
                        communication.empathyPercent > 0f || communication.clarityPercent > 0f);
        float? comm = hasComm ? communication.compositeScore : (float?)null;
        return DecisionMatrixEvaluator.Evaluate(Matrix.ToConfig(rubric), events, comm);
    }
}
