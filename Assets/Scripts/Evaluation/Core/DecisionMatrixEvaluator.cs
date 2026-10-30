using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One repeated-or-not mistake, grouped per step and violation type. Feeds the Mistake Gallery.
/// </summary>
[Serializable]
public class MistakeSummary
{
    public int step;
    public string violationType;
    public string action;
    public MatrixCriterion criterion;
    public MistakeSeverity severity;
    public int count;
    public float firstTime;
    public string firstDetail;
    public string captureId;
    public bool scored;            // false for unclassified mistakes or the overridden Communication cell
}

/// <summary>
/// One cell of the matrix (step x criterion). Flat so JsonUtility can serialise the result.
/// </summary>
[Serializable]
public class MatrixCell
{
    public int step;
    public MatrixCriterion criterion;
    public bool applicable;
    public float score = 100f;
    public float totalDeduction;
    public int distinctViolations;
    public int totalOccurrences;
    public bool hasSevere;
    public bool usesCommunicationScore;
}

[Serializable]
public class CriterionScore
{
    public MatrixCriterion criterion;
    public float weight;            // as configured
    public float effectiveWeight;   // renormalised over criteria that have applicable cells
    public bool applicable;
    public float score;             // mean of applicable cells
}

[Serializable]
public class DecisionMatrixResult
{
    public List<MatrixCell> cells = new List<MatrixCell>();
    public List<CriterionScore> criteria = new List<CriterionScore>();
    public List<MistakeSummary> mistakes = new List<MistakeSummary>();   // Severe first, then by step / time
    public float matrixScore;
    // Threshold bands are kept in memory only (not exported or shown on the summary screen).
    [NonSerialized] public string rating;
    [NonSerialized] public bool passed;
    [NonSerialized] public bool honors;
    public int severeCount;
    public int unclassifiedCount;

    public MatrixCell GetCell(int step, MatrixCriterion criterion) =>
        cells.FirstOrDefault(c => c.step == step && c.criterion == criterion);

    public CriterionScore GetCriterion(MatrixCriterion criterion) =>
        criteria.FirstOrDefault(c => c.criterion == criterion);
}

/// <summary>
/// Weighted Sum Model Decision Matrix. Rows = steps 1..N, columns = criteria.
///   cell      = max(0, 100 - sum of deductions), each violation type deducted once per step
///   criterion = mean of applicable cells (N/A cells excluded)
///   matrix    = sum(w * criterion) / sum(w) over criteria with at least one applicable cell
/// Severe mistakes never fail the run; they are flagged and listed first.
/// </summary>
public static class DecisionMatrixEvaluator
{
    /// <param name="communicationScore">Phrase-board composite (0-100) for the communication step, or null if none.</param>
    public static DecisionMatrixResult Evaluate(DecisionMatrixConfig config, IEnumerable<EvaluationEvent> events, float? communicationScore = null)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        config.Validate();

        var result = new DecisionMatrixResult();
        int stepCount = config.StepCount;
        var mistakeEvents = (events ?? Enumerable.Empty<EvaluationEvent>())
            .Where(e => e != null && !e.isCorrect)
            .ToList();

        // Group repeats: one summary per (step, type).
        var groups = mistakeEvents
            .GroupBy(e => (e.step, e.GroupKey))
            .Select(g =>
            {
                var ordered = g.OrderBy(e => e.time).ToList();
                var first = ordered[0];
                return new MistakeSummary
                {
                    step = first.step,
                    violationType = first.violationType,
                    action = first.action,
                    criterion = first.criterion,
                    severity = ordered.Max(e => e.severity),
                    count = ordered.Count,
                    firstTime = first.time,
                    firstDetail = first.detail,
                    captureId = ordered.Select(e => e.captureId).FirstOrDefault(id => !string.IsNullOrEmpty(id))
                };
            })
            .ToList();

        // Build cells.
        for (int step = 1; step <= stepCount; step++)
        {
            for (int c = 0; c < DecisionMatrixConfig.CriterionCount; c++)
            {
                var criterion = (MatrixCriterion)c;
                var cellMistakes = groups.Where(m => m.step == step && m.criterion == criterion && m.severity != MistakeSeverity.NoMistake).ToList();
                var cell = new MatrixCell
                {
                    step = step,
                    criterion = criterion,
                    applicable = config.applicable[step - 1, c] || cellMistakes.Count > 0,
                    distinctViolations = cellMistakes.Count,
                    totalOccurrences = cellMistakes.Sum(m => m.count),
                    hasSevere = cellMistakes.Any(m => m.severity == MistakeSeverity.Severe)
                };

                bool useCommunication = criterion == MatrixCriterion.Communication
                                        && step == config.communicationStep
                                        && communicationScore.HasValue;
                if (useCommunication)
                {
                    cell.applicable = true;
                    cell.usesCommunicationScore = true;
                    cell.score = Clamp(communicationScore.Value, 0f, 100f);
                    cell.totalDeduction = 100f - cell.score;
                }
                else
                {
                    cell.totalDeduction = cellMistakes.Sum(m => config.DeductionFor(m.severity));
                    cell.score = Math.Max(0f, 100f - cell.totalDeduction);
                }

                foreach (var m in cellMistakes) m.scored = !useCommunication;
                result.cells.Add(cell);
            }
        }

        // Criterion scores and weighted sum.
        float weightSum = 0f, weighted = 0f;
        for (int c = 0; c < DecisionMatrixConfig.CriterionCount; c++)
        {
            var criterion = (MatrixCriterion)c;
            var applicableCells = result.cells.Where(x => x.criterion == criterion && x.applicable).ToList();
            var cs = new CriterionScore
            {
                criterion = criterion,
                weight = config.criterionWeights[c],
                applicable = applicableCells.Count > 0,
                score = applicableCells.Count > 0 ? applicableCells.Average(x => x.score) : 0f
            };
            if (cs.applicable && cs.weight > 0f)
            {
                weightSum += cs.weight;
                weighted += cs.weight * cs.score;
            }
            result.criteria.Add(cs);
        }
        foreach (var cs in result.criteria)
            cs.effectiveWeight = cs.applicable && weightSum > 0f ? cs.weight / weightSum : 0f;

        result.matrixScore = weightSum > 0f ? weighted / weightSum : 100f;
        result.passed = result.matrixScore >= config.passThreshold;
        result.honors = result.matrixScore >= config.honorsThreshold;
        result.rating = result.honors ? "Honors" : result.passed ? "Pass" : "Below Passing";

        result.mistakes = groups
            .OrderByDescending(m => m.severity)
            .ThenBy(m => m.step)
            .ThenBy(m => m.firstTime)
            .ToList();
        result.severeCount = groups.Count(m => m.severity == MistakeSeverity.Severe);
        result.unclassifiedCount = groups.Count(m => m.criterion == MatrixCriterion.Unclassified);
        return result;
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
}
