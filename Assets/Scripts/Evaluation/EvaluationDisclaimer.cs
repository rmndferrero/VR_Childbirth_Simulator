/// <summary>
/// The one disclaimer shown on every Step 8 evaluation page and at the top of report.html.
/// SummaryScreenController copies its Inspector-editable string into Text on Awake.
/// </summary>
public static class EvaluationDisclaimer
{
    public const string DefaultText =
        "Disclaimer: This summary only shows what the simulation recorded. The system can miss or misrecord actions, " +
        "so it is not an assessment of clinical competence. Please review these results with your Clinical Instructor (CI).";

    private static string text;

    public static string Text
    {
        get => string.IsNullOrWhiteSpace(text) ? DefaultText : text;
        set => text = value;
    }
}
