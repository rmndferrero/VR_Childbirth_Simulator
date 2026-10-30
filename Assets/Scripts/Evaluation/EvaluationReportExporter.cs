using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Fills the Decision Matrix fields of a SimulationRecord and writes the session export
/// (record JSON, events CSV, matrix CSV, self-contained HTML instructor report) into the
/// session folder next to the gameplay captures. Never changes the legacy score fields.
/// </summary>
public static class EvaluationReportExporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public enum SaveState { None, Pending, Saved, Discarded }

    /// <summary>Save state of the current session's evaluation export.</summary>
    public static SaveState State { get; private set; }
    public static string SavedFolderPath { get; private set; }
    public static event Action<SaveState> OnSaveStateChanged;

    private static SimulationRecord pendingRecord;
    private static SimulationScoringRubricSO pendingRubric;

    /// <summary>Permanent session folder (persistentDataPath/SimulationCaptures/sessionId).</summary>
    public static string GetSessionFolder()
    {
        var capture = GameplayCaptureService.Instance;
        if (capture != null && !string.IsNullOrEmpty(capture.SavedFolder)) return capture.SavedFolder;
        return Path.Combine(Application.persistentDataPath, "SimulationCaptures", EvaluationLogger.Instance.SessionId);
    }

    public static void ResetSaveState()
    {
        pendingRecord = null;
        pendingRubric = null;
        SavedFolderPath = null;
        SetState(SaveState.None);
    }

    /// <summary>Keeps the record until the user chooses Save Evaluation / Don't Save.</summary>
    public static void SetPending(SimulationRecord record, SimulationScoringRubricSO rubric)
    {
        pendingRecord = record;
        pendingRubric = rubric;
        SetState(SaveState.Pending);
    }

    /// <summary>Saves the pending record: moves captures to the permanent folder and writes JSON / CSV / HTML.</summary>
    public static bool SaveSession() => pendingRecord != null && SaveSession(pendingRecord, pendingRubric);

    public static bool SaveSession(SimulationRecord record, SimulationScoringRubricSO rubric)
    {
        if (State == SaveState.Saved) return true;
        var capture = GameplayCaptureService.Instance;
        if (capture != null) capture.PersistCaptures();
        record.sessionFolder = GetSessionFolder();
        bool ok = ExportSession(record, rubric);
        if (ok)
        {
            SavedFolderPath = record.sessionFolder;
            pendingRecord = null;
            SetState(SaveState.Saved);
        }
        return ok;
    }

    /// <summary>Drops the pending export and deletes temp captures (no-op once saved).</summary>
    public static void DiscardSession()
    {
        if (State == SaveState.Saved || State == SaveState.Discarded) return;
        var capture = GameplayCaptureService.Instance;
        if (capture != null) capture.DiscardCaptures();
        pendingRecord = null;
        SetState(SaveState.Discarded);
    }

    private static void SetState(SaveState state)
    {
        State = state;
        OnSaveStateChanged?.Invoke(state);
    }

    /// <summary>Adds the parallel evaluation data to the record. Existing fields are left untouched.</summary>
    public static void Populate(SimulationRecord record, SimulationScoringRubricSO rubric, CommunicationDebriefData communication)
    {
        var logger = EvaluationLogger.Instance;
        var result = logger.Evaluate(rubric, communication);

        record.sessionId = logger.SessionId;
        record.sessionFolder = GetSessionFolder();
        record.matrixScore = (float)Math.Round(result.matrixScore, 1);
        record.severeMistakeCount = result.severeCount;
        record.decisionMatrix = result;
        record.evaluationEvents = new List<EvaluationEvent>(logger.events);
        record.stepTimings = new List<StepTiming>(logger.stepTimings);
        record.capturePaths = logger.events
            .Where(e => !string.IsNullOrEmpty(e.captureId))
            .Select(e => e.captureId + ".png")
            .ToList();
    }

    /// <summary>Writes record.json, events.csv, matrix.csv and report.html into the session folder.</summary>
    public static bool ExportSession(SimulationRecord record, SimulationScoringRubricSO rubric)
    {
        string folder = string.IsNullOrEmpty(record.sessionFolder) ? GetSessionFolder() : record.sessionFolder;
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "SimulationRecord.json"), JsonUtility.ToJson(record, true));
            File.WriteAllText(Path.Combine(folder, "events.csv"), BuildEventsCsv(record));
            File.WriteAllText(Path.Combine(folder, "matrix.csv"), BuildMatrixCsv(record));
            File.WriteAllText(Path.Combine(folder, "report.html"), BuildHtmlReport(record, rubric, folder));
            Debug.Log($"[Evaluation Export] Session export written to: {folder}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Evaluation Export] Failed to write session export: {ex.Message}");
            return false;
        }
    }

    // ── CSV ──

    public static string BuildEventsCsv(SimulationRecord record)
    {
        var sb = new StringBuilder();
        sb.AppendLine("time_s,step,action,is_correct,violation_type,criterion,severity,count,detail,capture_id,legacy_penalty,legacy_penalty_applied");
        foreach (var e in record.evaluationEvents ?? new List<EvaluationEvent>())
        {
            sb.AppendLine(string.Join(",",
                e.time.ToString("0.00", Inv), e.step.ToString(Inv), Csv(e.action), e.isCorrect ? "1" : "0",
                Csv(e.violationType), e.criterion, e.severity, e.count.ToString(Inv), Csv(e.detail), Csv(e.captureId),
                e.legacyPenalty.ToString(Inv), e.legacyPenaltyApplied ? "1" : "0"));
        }
        return sb.ToString();
    }

    public static string BuildMatrixCsv(SimulationRecord record)
    {
        var sb = new StringBuilder();
        sb.AppendLine("step,criterion,applicable,score,total_deduction,distinct_violations,total_occurrences,has_severe,uses_communication_score");
        var r = record.decisionMatrix;
        if (r != null)
        {
            foreach (var c in r.cells)
            {
                sb.AppendLine(string.Join(",", c.step.ToString(Inv), c.criterion, c.applicable ? "1" : "0",
                    c.score.ToString("0.0", Inv), c.totalDeduction.ToString("0.0", Inv), c.distinctViolations.ToString(Inv),
                    c.totalOccurrences.ToString(Inv), c.hasSevere ? "1" : "0", c.usesCommunicationScore ? "1" : "0"));
            }
            sb.AppendLine();
            sb.AppendLine("criterion,weight,effective_weight,applicable,score");
            foreach (var c in r.criteria)
                sb.AppendLine(string.Join(",", c.criterion, c.weight.ToString("0.###", Inv), c.effectiveWeight.ToString("0.###", Inv),
                    c.applicable ? "1" : "0", c.score.ToString("0.0", Inv)));
            sb.AppendLine();
            sb.AppendLine($"matrix_score,{r.matrixScore.ToString("0.0", Inv)}");
            sb.AppendLine($"legacy_score,{record.finalScore.ToString(Inv)}");
        }
        return sb.ToString();
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        bool quote = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        s = s.Replace("\"", "\"\"");
        return quote ? $"\"{s}\"" : s;
    }

    // ── HTML ──

    public static string BuildHtmlReport(SimulationRecord record, SimulationScoringRubricSO rubric, string folder)
    {
        var r = record.decisionMatrix ?? new DecisionMatrixResult();
        var matrixSO = EvaluationLogger.Instance.Matrix;
        var config = matrixSO.ToConfig(rubric);

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>Simulation Evaluation Report</title><style>");
        sb.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#1f2937;background:#f9fafb}h1{margin:0 0 4px}h2{margin-top:32px;border-bottom:2px solid #e5e7eb;padding-bottom:4px}");
        sb.Append(".muted{color:#6b7280}.cards{display:flex;gap:16px;flex-wrap:wrap;margin-top:16px}.card{background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:12px 16px;min-width:180px}");
        sb.Append(".big{font-size:28px;font-weight:700}.disclaimer{background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px;padding:10px 14px;margin:12px 0;color:#1e3a8a}table{border-collapse:collapse;background:#fff;width:100%}th,td{border:1px solid #e5e7eb;padding:6px 8px;text-align:left;vertical-align:top}th{background:#f3f4f6}");
        sb.Append("td.cell{text-align:center;font-weight:600}.na{background:#e5e7eb;color:#6b7280}.sev{color:#b91c1c;font-weight:700}.mod{color:#b45309;font-weight:600}");
        sb.Append(".mistake{display:flex;gap:16px;background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:12px;margin:12px 0}.mistake.severe{border-left:6px solid #dc2626}.mistake.moderate{border-left:6px solid #f59e0b}");
        sb.Append(".mistake img{width:320px;max-width:100%;height:auto;border-radius:4px;background:#111}.noimg{width:320px;height:180px;display:flex;align-items:center;justify-content:center;background:#e5e7eb;color:#6b7280;border-radius:4px}");
        sb.Append("@media(max-width:700px){.mistake{flex-direction:column}}</style></head><body>");

        sb.Append($"<h1>Simulation Evaluation Report</h1><div class=\"muted\">{H(record.playerName)} &middot; {H(record.dateCompleted)} &middot; Session {H(record.sessionId)}</div>");

        sb.Append($"<div class=\"disclaimer\">{H(EvaluationDisclaimer.Text)}</div>");

        sb.Append("<div class=\"cards\">");
        sb.Append($"<div class=\"card\"><div class=\"muted\">Simulation score (penalty-based)</div><div class=\"big\">{record.finalScore} / 100</div></div>");
        sb.Append($"<div class=\"card\"><div class=\"muted\">Decision matrix score</div><div class=\"big\">{r.matrixScore.ToString("0.0", Inv)} / 100</div></div>");
        sb.Append($"<div class=\"card\"><div class=\"muted\">Severe mistakes recorded</div><div class=\"big\">{r.severeCount}</div></div>");
        sb.Append("</div>");

        // Matrix grid
        sb.Append("<h2>Decision Matrix</h2><table><tr><th>Step</th>");
        for (int c = 0; c < DecisionMatrixConfig.CriterionCount; c++)
        {
            var cs = r.GetCriterion((MatrixCriterion)c);
            string w = cs != null ? $" ({(cs.weight * 100f).ToString("0", Inv)}%)" : "";
            sb.Append($"<th>{(MatrixCriterion)c}{w}</th>");
        }
        sb.Append("</tr>");
        for (int step = 1; step <= config.StepCount; step++)
        {
            sb.Append($"<tr><td>{step}. {H(config.StepName(step))}</td>");
            for (int c = 0; c < DecisionMatrixConfig.CriterionCount; c++)
            {
                var cell = r.GetCell(step, (MatrixCriterion)c);
                if (cell == null || !cell.applicable) { sb.Append("<td class=\"cell na\">N/A</td>"); continue; }
                string flag = cell.hasSevere ? " &#9888;" : "";
                string note = cell.usesCommunicationScore ? "<br><small>phrase board</small>" : cell.totalOccurrences > 0 ? $"<br><small>{cell.totalOccurrences}x</small>" : "";
                sb.Append($"<td class=\"cell\">{cell.score.ToString("0", Inv)}{flag}{note}</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("<tr><th>Criterion score</th>");
        for (int c = 0; c < DecisionMatrixConfig.CriterionCount; c++)
        {
            var cs = r.GetCriterion((MatrixCriterion)c);
            sb.Append(cs != null && cs.applicable ? $"<th>{cs.score.ToString("0.0", Inv)}</th>" : "<th class=\"na\">N/A</th>");
        }
        sb.Append("</tr></table>");

        // Mistake gallery
        sb.Append("<h2>Mistakes (Severe first)</h2>");
        if (r.mistakes.Count == 0) sb.Append("<p>No mistakes recorded.</p>");
        foreach (var m in r.mistakes)
        {
            string cls = m.severity == MistakeSeverity.Severe ? "severe" : "moderate";
            sb.Append($"<div class=\"mistake {cls}\">");
            sb.Append(ImageTag(folder, m.captureId));
            sb.Append("<div>");
            sb.Append($"<div class=\"{(m.severity == MistakeSeverity.Severe ? "sev" : "mod")}\">{m.severity.ToString().ToUpperInvariant()} &middot; {m.criterion}</div>");
            sb.Append($"<h3 style=\"margin:4px 0\">{H(m.action)}</h3>");
            sb.Append($"<div class=\"muted\">Step {m.step}: {H(config.StepName(m.step))} &middot; first at {FormatTime(m.firstTime)} &middot; occurred {m.count}x</div>");
            string detail = DetailIfInformative(m);
            sb.Append($"<p><b>What went wrong:</b> {H(m.action)}{(detail.Length > 0 ? " (" + H(detail) + ")" : "")}</p>");
            string fix = CorrectTechnique(matrixSO, m.violationType);
            if (!string.IsNullOrEmpty(fix)) sb.Append($"<p><b>Correct technique:</b> {H(fix)}</p>");
            if (!m.scored) sb.Append(m.criterion == MatrixCriterion.Unclassified
                ? "<p class=\"muted\">Unclassified: listed for review, not scored in the matrix.</p>"
                : "<p class=\"muted\">Reflected in the phrase-board communication score.</p>");
            sb.Append("</div></div>");
        }

        // Step timings
        sb.Append("<h2>Step Timings</h2><table><tr><th>Step</th><th>Start</th><th>End</th><th>Duration</th></tr>");
        foreach (var t in record.stepTimings ?? new List<StepTiming>())
            sb.Append($"<tr><td>{t.step}. {H(config.StepName(t.step))}</td><td>{FormatTime(t.startTime)}</td><td>{(t.endTime < 0 ? "-" : FormatTime(t.endTime))}</td><td>{(t.endTime < 0 ? "-" : FormatTime(t.Duration))}</td></tr>");
        sb.Append("</table>");

        // Timeline
        sb.Append("<h2>Event Timeline</h2><table><tr><th>Time</th><th>Step</th><th>Action</th><th>Result</th><th>Criterion</th><th>Severity</th><th>#</th></tr>");
        foreach (var e in record.evaluationEvents ?? new List<EvaluationEvent>())
        {
            string res = e.isCorrect ? "Correct" : "Mistake";
            string sev = e.isCorrect ? "" : e.severity.ToString();
            sb.Append($"<tr><td>{FormatTime(e.time)}</td><td>{e.step}</td><td>{H(e.action)}</td><td>{res}</td><td>{e.criterion}</td><td class=\"{(e.severity == MistakeSeverity.Severe && !e.isCorrect ? "sev" : "")}\">{sev}</td><td>{(e.isCorrect ? "" : e.count.ToString(Inv))}</td></tr>");
        }
        sb.Append("</table>");

        // Legacy log
        sb.Append("<h2>Legacy Mistake Log</h2><ul>");
        foreach (var line in record.mistakesMade ?? new List<string>()) sb.Append($"<li>{H(line)}</li>");
        sb.Append("</ul></body></html>");
        return sb.ToString();
    }

    /// <summary>The first occurrence's detail, unless it only repeats the action text (e.g. an item id).</summary>
    public static string DetailIfInformative(MistakeSummary m)
    {
        if (string.IsNullOrEmpty(m.firstDetail)) return "";
        string d = m.firstDetail.Replace('_', ' ');
        string a = m.action ?? "";
        return a.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0 ? "" : m.firstDetail;
    }

    public static string CorrectTechnique(DecisionMatrixSO so, string violationType)
    {
        if (so == null || string.IsNullOrEmpty(violationType)) return null;
        if (!Enum.TryParse(violationType, out ClinicalViolationType type)) return null;
        return so.TryGetRule(type, out var rule) ? rule.correctTechnique : null;
    }

    private static string ImageTag(string folder, string captureId)
    {
        if (string.IsNullOrEmpty(captureId)) return "<div class=\"noimg\">No screenshot</div>";
        string path = Path.Combine(folder, captureId + ".png");
        try
        {
            if (File.Exists(path))
                return $"<img alt=\"{H(captureId)}\" src=\"data:image/png;base64,{Convert.ToBase64String(File.ReadAllBytes(path))}\">";
        }
        catch (Exception) { }
        // Capture may still be writing at export time; fall back to the relative file next to the report.
        return $"<img alt=\"{H(captureId)}\" src=\"{H(captureId)}.png\">";
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    private static string H(string s) => string.IsNullOrEmpty(s) ? "" : System.Net.WebUtility.HtmlEncode(s);
}
