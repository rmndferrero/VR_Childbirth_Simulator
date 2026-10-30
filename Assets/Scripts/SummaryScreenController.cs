using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the modular 4-page paginated SummaryScreen GameObject at Step 8.
/// Allows students and clinical preceptors to navigate between:
/// - Page 1: Dialogue & Clinical Communication Competency Profile
/// - Page 2: Perineal Cleaning & 9-Ball Technique Review
/// - Page 3: Sterile Draping Protocol Review
/// - Page 4: Mayo Table Setup & Recorded Score
/// - Page 5: Decision Matrix Evaluation (parallel WSM score)
/// Every page shows the shared EvaluationDisclaimer above the footer navigation.
/// - Page 6+: Mistake Gallery (Severe first, 2 per page; Restart Button on the last page)
/// </summary>
public class SummaryScreenController : MonoBehaviour
{
    public static SummaryScreenController Instance { get; private set; }

    [Header("Root Panel")]
    public GameObject rootScreenObject;

    [Header("The 4 Page Panels")]
    public GameObject page1Dialogue;
    public GameObject page2Perineal;
    public GameObject page3Draping;
    public GameObject page4TableAndFinal;

    [Header("Universal Footer Navigation")]
    public Button buttonBack;
    public TMP_Text textPageIndicator;
    public Button buttonNext;
    public Button buttonRestart;

    [Header("Disclaimer (every evaluation page + report.html)")]
    [TextArea(2, 5)]
    public string disclaimer = EvaluationDisclaimer.DefaultText;
    public TMP_Text disclaimerText;

    [Header("Page 1: Communication Debrief UI Components")]
    public TMP_Text p1OverallRatingText;
    public TMP_Text p1ArchetypeTitleText;
    public TMP_Text p1DebriefNotesText;
    public Image p1BarProfessionalism;
    public TMP_Text p1TextProfessionalism;
    public Image p1BarEmpathy;
    public TMP_Text p1TextEmpathy;
    public Image p1BarClarity;
    public TMP_Text p1TextClarity;
    public TMP_Text p1CritiquesListText;

    [Header("Page 2: Perineal Care UI Components")]
    public TMP_Text p2StatusSummaryText;
    public TMP_Text p2MistakesListText;

    [Header("Page 3: Sterile Draping UI Components")]
    public TMP_Text p3StatusSummaryText;
    public TMP_Text p3MistakesListText;

    [Header("Page 4: Table Setup & Final Master Score")]
    public TMP_Text p4FinalScoreText;
    public TMP_Text p4OverallMistakesListText;

    [Header("Page 5: Decision Matrix (parallel score)")]
    public GameObject page5Matrix;
    public TMP_Text p5ScoresText;
    [Tooltip("Empty RectTransform; the step x criterion grid is built here at runtime.")]
    public RectTransform p5GridContainer;
    public Button p5SaveButton;
    public Button p5DontSaveButton;
    public TMP_Text p5SaveStatusText;

    [Header("Page 6+: Mistake Gallery")]
    public GameObject page6Gallery;
    public TMP_Text p6HeaderText;
    [Tooltip("One entry per card slot on a gallery page.")]
    public GameObject[] p6Cards;
    public RawImage[] p6Thumbnails;
    public TMP_Text[] p6CardTexts;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pageFlipSound;

    private int currentPageIndex = 0;
    private const int MatrixPageIndex = 4;
    private const int FirstGalleryPageIndex = 5;
    private int TotalPages => page5Matrix == null ? 4 : FirstGalleryPageIndex + GalleryPageCount;

    private static readonly string[] PageTitles = new string[5]
    {
        "Patient Communication & Informed Consent",
        "Perineal Cleaning & Antiseptic Scrub",
        "Sterile Draping Protocol",
        "Mayo Table Setup & Recorded Score",
        "Decision Matrix Evaluation"
    };

    private DecisionMatrixResult matrixResult;
    private readonly List<Texture2D> loadedThumbnails = new List<Texture2D>();
    private int CardsPerGalleryPage => p6Cards != null && p6Cards.Length > 0 ? p6Cards.Length : 2;
    private int GalleryPageCount
    {
        get
        {
            if (page6Gallery == null) return 0;
            int n = matrixResult != null ? matrixResult.mistakes.Count : 0;
            return Mathf.Max(1, Mathf.CeilToInt(n / (float)CardsPerGalleryPage));
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (rootScreenObject == null) rootScreenObject = gameObject;
        EvaluationDisclaimer.Text = disclaimer;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (buttonBack != null)
        {
            buttonBack.onClick.RemoveAllListeners();
            buttonBack.onClick.AddListener(PreviousPage);
        }

        if (buttonNext != null)
        {
            buttonNext.onClick.RemoveAllListeners();
            buttonNext.onClick.AddListener(NextPage);
        }

        if (buttonRestart != null)
        {
            buttonRestart.onClick.RemoveAllListeners();
            buttonRestart.onClick.AddListener(RestartSimulation);
        }

        if (p5SaveButton != null)
        {
            p5SaveButton.onClick.RemoveAllListeners();
            p5SaveButton.onClick.AddListener(SaveEvaluation);
        }

        if (p5DontSaveButton != null)
        {
            p5DontSaveButton.onClick.RemoveAllListeners();
            p5DontSaveButton.onClick.AddListener(DontSaveEvaluation);
        }

        EvaluationReportExporter.OnSaveStateChanged += HandleSaveStateChanged;
    }

    public void OpenSummaryScreen()
    {
        if (rootScreenObject != null) rootScreenObject.SetActive(true);
        EvaluationDisclaimer.Text = disclaimer;
        if (disclaimerText != null) disclaimerText.text = EvaluationDisclaimer.Text;

        PopulatePage1_Dialogue();
        PopulatePage2_Perineal();
        PopulatePage3_Draping();
        PopulatePage4_TableAndFinal();
        PopulatePage5_Matrix();

        ShowPage(0);
    }

    public void ShowPage(int index)
    {
        currentPageIndex = Mathf.Clamp(index, 0, TotalPages - 1);

        if (page1Dialogue != null) page1Dialogue.SetActive(currentPageIndex == 0);
        if (page2Perineal != null) page2Perineal.SetActive(currentPageIndex == 1);
        if (page3Draping != null) page3Draping.SetActive(currentPageIndex == 2);
        if (page4TableAndFinal != null) page4TableAndFinal.SetActive(currentPageIndex == 3);
        if (page5Matrix != null) page5Matrix.SetActive(currentPageIndex == MatrixPageIndex);
        bool onGallery = page5Matrix != null && currentPageIndex >= FirstGalleryPageIndex;
        if (page6Gallery != null) page6Gallery.SetActive(onGallery);
        if (onGallery) PopulateGalleryPage(currentPageIndex - FirstGalleryPageIndex);
        if (currentPageIndex == MatrixPageIndex) RefreshSaveBar();

        if (textPageIndicator != null)
        {
            string title = currentPageIndex < PageTitles.Length
                ? PageTitles[currentPageIndex]
                : (GalleryPageCount > 1 ? $"Mistake Gallery ({currentPageIndex - FirstGalleryPageIndex + 1}/{GalleryPageCount})" : "Mistake Gallery");
            textPageIndicator.text = $"Page {currentPageIndex + 1} of {TotalPages}: {title}";
        }

        if (buttonBack != null)
        {
            buttonBack.interactable = (currentPageIndex > 0);
        }

        if (buttonNext != null)
        {
            buttonNext.gameObject.SetActive(currentPageIndex < TotalPages - 1);
        }

        if (buttonRestart != null)
        {
            buttonRestart.gameObject.SetActive(currentPageIndex == TotalPages - 1);
        }

        PlayPageSound();
    }

    public void NextPage()
    {
        if (currentPageIndex < TotalPages - 1)
        {
            ShowPage(currentPageIndex + 1);
        }
    }

    public void PreviousPage()
    {
        if (currentPageIndex > 0)
        {
            ShowPage(currentPageIndex - 1);
        }
    }

    private void PopulatePage1_Dialogue()
    {
        CommunicationDebriefData debrief = VRDemoGameManager.Instance != null
            ? VRDemoGameManager.Instance.communicationDebrief
            : null;

        if (debrief == null)
        {
            debrief = new CommunicationDebriefData
            {
                professionalismPercent = 80f,
                empathyPercent = 80f,
                clarityPercent = 80f,
                compositeScore = 80f,
                personalizedInstructorDebrief = "No communication notes were recorded."
            };
        }

        if (p1OverallRatingText != null)
        {
            p1OverallRatingText.text = $"Communication score: <b>{Mathf.RoundToInt(debrief.compositeScore)}%</b>";
        }

        if (p1ArchetypeTitleText != null)
        {
            p1ArchetypeTitleText.text = "Communication summary";
        }

        if (p1DebriefNotesText != null)
        {
            p1DebriefNotesText.text = string.IsNullOrWhiteSpace(debrief.personalizedInstructorDebrief)
                ? "No communication notes were recorded."
                : $"\"{debrief.personalizedInstructorDebrief}\"";
        }

        // Sub-criteria progress bars
        SetBar(p1BarProfessionalism, p1TextProfessionalism, debrief.professionalismPercent);
        SetBar(p1BarEmpathy, p1TextEmpathy, debrief.empathyPercent);
        SetBar(p1BarClarity, p1TextClarity, debrief.clarityPercent);

        if (p1CritiquesListText != null)
        {
            p1CritiquesListText.text = debrief.specificTokenCritiques.Count > 0
                ? "<b>Phrase observations:</b>\n• " + string.Join("\n• ", debrief.specificTokenCritiques.ToArray())
                : "<b>Phrase observations:</b>\n• No phrase observations were recorded.";
        }
    }

    // One neutral colour for every bar: no grade banding.
    private static readonly Color BarNeutral = new Color(0.22f, 0.62f, 0.90f);
    private static Sprite barSprite;

    private void SetBar(Image bar, TMP_Text label, float percentage)
    {
        if (bar != null)
        {
            // A Filled Image ignores fillAmount without a sprite, so a 0% bar drew full width.
            if (bar.sprite == null)
            {
                if (barSprite == null)
                    barSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
                bar.sprite = barSprite;
            }
            bar.fillAmount = percentage / 100f;
            bar.color = BarNeutral;
        }

        if (label != null)
        {
            label.text = $"{Mathf.RoundToInt(percentage)}%";
        }
    }

    private void PopulatePage2_Perineal()
    {
        if (p2StatusSummaryText != null)
        {
            p2StatusSummaryText.text =
                "<b>Steps recorded:</b>\n" +
                StepStatusLine(2) + "\n" +
                StepStatusLine(3, CountCorrectEvents(3, "Stroke"), "strokes") + "\n" +
                StepStatusLine(4) + "\n" +
                StepStatusLine(5, CountCorrectEvents(5, "Stroke"), "strokes");
        }

        if (p2MistakesListText != null)
        {
            var perinealMistakes = FilterMistakesByKeywords("wipe", "stroke", "cotton", "wash", "perineal", "iodine");
            p2MistakesListText.text = RecordedMistakesText(perinealMistakes);
        }
    }

    private void PopulatePage3_Draping()
    {
        if (p3StatusSummaryText != null)
        {
            p3StatusSummaryText.text =
                "<b>Steps recorded:</b>\n" +
                StepStatusLine(6, CountCorrectEvents(6, "Drape"), "of 4 drapes placed");
        }

        if (p3MistakesListText != null)
        {
            var drapingMistakes = FilterMistakesByStep(6);
            p3MistakesListText.text = RecordedMistakesText(drapingMistakes);
        }
    }

    private static string RecordedMistakesText(List<string> mistakes)
    {
        return mistakes.Count == 0
            ? "<b>Recorded mistakes (0):</b>\n• No mistakes were recorded in this section."
            : $"<b>Recorded mistakes ({mistakes.Count}):</b>\n• " + string.Join("\n• ", mistakes.ToArray());
    }

    /// <summary>"Completed" only when the step has a closed timing entry (the phase moved past it).</summary>
    private static string StepStatusLine(int step, int count = -1, string countLabel = null)
    {
        bool completed = false;
        foreach (var t in EvaluationLogger.Instance.stepTimings)
            if (t.step == step && t.endTime >= 0f) { completed = true; break; }

        string name = EvaluationLogger.Instance.Matrix.ToConfig().StepName(step);
        string extra = count >= 0 ? $"  <color=#9FB3C8>({count} {countLabel})</color>" : "";
        return $"• Step {step}: {name} — {(completed ? "Completed" : "Not completed")}{extra}";
    }

    private static int CountCorrectEvents(int step, string actionPrefix)
    {
        int n = 0;
        foreach (var e in EvaluationLogger.Instance.events)
            if (e.isCorrect && e.step == step && e.action != null && e.action.StartsWith(actionPrefix)) n++;
        return n;
    }

    private void PopulatePage4_TableAndFinal()
    {
        int score = VRDemoGameManager.Instance != null ? VRDemoGameManager.Instance.currentScore : 100;

        if (p4FinalScoreText != null)
        {
            p4FinalScoreText.text = $"Recorded score: <b>{score} / 100</b>";
        }

        if (p4OverallMistakesListText != null)
        {
            List<string> allMistakes = VRDemoGameManager.Instance != null
                ? VRDemoGameManager.Instance.mistakeLog
                : new List<string>();

            p4OverallMistakesListText.text = allMistakes.Count == 0
                ? "<b>Recorded mistakes (0):</b>\n• No mistakes were recorded by the simulation."
                : $"<b>Recorded mistakes ({allMistakes.Count}):</b>\n• " + string.Join("\n• ", allMistakes.ToArray());
        }
    }

    // ── Page 5: Decision Matrix ──

    private static readonly Color CellNA = new Color(0.15f, 0.20f, 0.28f, 1f);
    private static readonly Color CellHeader = new Color(0.08f, 0.12f, 0.18f, 0.95f);
    // One neutral colour for every scored cell: no pass/fail banding.
    private static readonly Color CellScored = new Color(0.20f, 0.29f, 0.40f, 1f);

    private void PopulatePage5_Matrix()
    {
        var gm = VRDemoGameManager.Instance;
        var rubric = gm != null ? gm.scoringRubric : null;
        matrixResult = EvaluationLogger.Instance.Evaluate(rubric, gm != null ? gm.communicationDebrief : null);
        var config = EvaluationLogger.Instance.Matrix.ToConfig(rubric);

        int legacy = gm != null ? Mathf.Clamp(gm.currentScore, 0, 100) : 100;

        if (p5ScoresText != null)
        {
            p5ScoresText.text =
                $"Simulation score (penalty-based): <b>{legacy} / 100</b>     " +
                $"Decision matrix score: <b>{matrixResult.matrixScore:0.0} / 100</b>\n" +
                $"<size=85%>Severe mistakes recorded: {matrixResult.severeCount}  •  [!] = Severe  •  N/A = not assessed</size>";
        }

        BuildMatrixGrid(config);
    }

    private void BuildMatrixGrid(DecisionMatrixConfig config)
    {
        if (p5GridContainer == null) return;
        for (int i = p5GridContainer.childCount - 1; i >= 0; i--) Destroy(p5GridContainer.GetChild(i).gameObject);

        int cols = DecisionMatrixConfig.CriterionCount + 1;
        int rows = config.StepCount + 2; // header + steps + criterion score row
        const float nameColWidth = 0.32f;
        float critColWidth = (1f - nameColWidth) / DecisionMatrixConfig.CriterionCount;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x0 = c == 0 ? 0f : nameColWidth + (c - 1) * critColWidth;
                float x1 = c == 0 ? nameColWidth : x0 + critColWidth;
                float y1 = 1f - r / (float)rows, y0 = 1f - (r + 1) / (float)rows;

                Color bg = CellHeader;
                string text;
                bool bold = r == 0 || r == rows - 1 || c == 0;
                var criterion = (MatrixCriterion)(c - 1);
                var cs = c > 0 ? matrixResult.GetCriterion(criterion) : null;

                if (r == 0)
                {
                    text = c == 0 ? "Step" : $"{criterion} ({cs.weight * 100f:0}%)";
                }
                else if (r == rows - 1)
                {
                    text = c == 0 ? "Criterion Score" : cs.applicable ? $"{cs.score:0.0}" : "N/A";
                    if (c > 0) bg = cs.applicable ? CellScored : CellNA;
                }
                else if (c == 0)
                {
                    text = $"{r}. {config.StepName(r)}";
                }
                else
                {
                    var cell = matrixResult.GetCell(r, criterion);
                    if (cell == null || !cell.applicable)
                    {
                        text = "<color=#8A96A8>N/A</color>";
                        bg = CellNA;
                    }
                    else
                    {
                        bg = CellScored;
                        text = $"{cell.score:0}";
                        if (cell.hasSevere) text += " <color=#FFE4E4>[!]</color>";
                        if (cell.usesCommunicationScore) text += " <size=80%>(dialogue)</size>";
                        else if (cell.totalOccurrences > 0) text += $" <size=80%>x{cell.totalOccurrences}</size>";
                    }
                }

                CreateGridCell(r, c, x0, x1, y0, y1, bg, text, bold, c == 0 && r > 0);
            }
        }
    }

    private void CreateGridCell(int r, int c, float x0, float x1, float y0, float y1, Color bg, string text, bool bold, bool leftAlign)
    {
        var cellGo = new GameObject($"Cell_{r}_{c}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cellGo.layer = p5GridContainer.gameObject.layer;
        var rt = (RectTransform)cellGo.transform;
        rt.SetParent(p5GridContainer, false);
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = new Vector2(1f, 1f);
        rt.offsetMax = new Vector2(-1f, -1f);
        var img = cellGo.GetComponent<Image>();
        img.color = bg;
        img.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.layer = cellGo.layer;
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6f, 0f);
        trt.offsetMax = new Vector2(-6f, 0f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        if (p5ScoresText != null) tmp.font = p5ScoresText.font;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 8f;
        tmp.fontSizeMax = 10.5f;
        tmp.fontSize = 10.5f;
        tmp.color = Color.white;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = leftAlign ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        tmp.text = text;
    }

    // ── Page 6+: Mistake Gallery ──

    private void PopulateGalleryPage(int galleryPage)
    {
        var mistakes = matrixResult != null ? matrixResult.mistakes : new List<MistakeSummary>();
        int perPage = CardsPerGalleryPage;
        int start = galleryPage * perPage;

        if (p6HeaderText != null)
        {
            p6HeaderText.text = mistakes.Count == 0
                ? "<color=#34D399><b>[OK] No mistakes recorded - nothing to review.</b></color>"
                : $"MISTAKE GALLERY  <size=75%>({mistakes.Count} total, Severe first  •  showing {start + 1}-{Mathf.Min(start + perPage, mistakes.Count)})</size>";
        }

        var matrixSO = EvaluationLogger.Instance.Matrix;
        var config = matrixSO.ToConfig();
        for (int i = 0; i < perPage; i++)
        {
            int idx = start + i;
            bool has = idx < mistakes.Count;
            if (p6Cards != null && i < p6Cards.Length && p6Cards[i] != null) p6Cards[i].SetActive(has);
            if (!has) continue;

            var m = mistakes[idx];
            if (p6Thumbnails != null && i < p6Thumbnails.Length && p6Thumbnails[i] != null)
            {
                var tex = LoadThumbnail(m.captureId);
                p6Thumbnails[i].texture = tex;
                p6Thumbnails[i].color = tex != null ? Color.white : CellNA;
            }

            if (p6CardTexts != null && i < p6CardTexts.Length && p6CardTexts[i] != null)
            {
                bool severe = m.severity == MistakeSeverity.Severe;
                string tag = severe ? "<color=#EF4444><b>[SEVERE]</b></color>" : "<color=#FBBF24><b>[MODERATE]</b></color>";
                string criterion = m.criterion == MatrixCriterion.Unclassified ? "Unclassified" : m.criterion.ToString();
                string fix = EvaluationReportExporter.CorrectTechnique(matrixSO, m.violationType);
                string detail = EvaluationReportExporter.DetailIfInformative(m);
                p6CardTexts[i].text =
                    $"{tag} {criterion}  <color=#9FB3C8>•  Step {m.step}: {config.StepName(m.step)}  •  {m.count}x</color>\n" +
                    $"<color=#F87171>What went wrong:</color> {m.action}{(detail.Length > 0 ? $" ({detail})" : "")}\n" +
                    (string.IsNullOrEmpty(fix) ? "" : $"<color=#34D399>Correct technique:</color> {fix}");
            }
        }
    }

    // ── Save / Don't Save ──

    public void SaveEvaluation()
    {
        PreloadAllThumbnails();
        EvaluationReportExporter.SaveSession();
        PlayPageSound();
        RefreshSaveBar();
    }

    public void DontSaveEvaluation()
    {
        PreloadAllThumbnails();   // keep the gallery working after the temp files are deleted
        EvaluationReportExporter.DiscardSession();
        PlayPageSound();
        RefreshSaveBar();
    }

    private void HandleSaveStateChanged(EvaluationReportExporter.SaveState state) => RefreshSaveBar();

    private void RefreshSaveBar()
    {
        var state = EvaluationReportExporter.State;
        bool canChoose = state == EvaluationReportExporter.SaveState.Pending;
        if (p5SaveButton != null) p5SaveButton.gameObject.SetActive(canChoose);
        if (p5DontSaveButton != null) p5DontSaveButton.gameObject.SetActive(canChoose);
        if (p5SaveStatusText == null) return;

        switch (state)
        {
            case EvaluationReportExporter.SaveState.Pending:
                p5SaveStatusText.text = "Save this evaluation (report, CSV, screenshots)?";
                break;
            case EvaluationReportExporter.SaveState.Saved:
                p5SaveStatusText.text = $"<color=#34D399><b>Evaluation saved.</b></color> <size=85%>{EvaluationReportExporter.SavedFolderPath}</size>";
                break;
            case EvaluationReportExporter.SaveState.Discarded:
                p5SaveStatusText.text = "<color=#FBBF24>Evaluation not saved.</color> <size=85%>Temporary screenshots deleted.</size>";
                break;
            default:
                p5SaveStatusText.text = "";
                break;
        }
    }

    private readonly Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();

    private void PreloadAllThumbnails()
    {
        if (matrixResult == null) return;
        foreach (var m in matrixResult.mistakes) LoadThumbnail(m.captureId);
    }

    private Texture2D LoadThumbnail(string captureId)
    {
        if (string.IsNullOrEmpty(captureId)) return null;
        if (thumbnailCache.TryGetValue(captureId, out var cached) && cached != null) return cached;
        var capture = GameplayCaptureService.Instance;
        if (capture == null) return null;
        if (!capture.savedCaptures.TryGetValue(captureId, out string path))
            path = Path.Combine(capture.SessionFolder ?? "", captureId + ".png");
        if (!File.Exists(path)) return null;

        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path))) { Destroy(tex); return null; }
            loadedThumbnails.Add(tex);
            thumbnailCache[captureId] = tex;
            return tex;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SummaryScreen] Could not load capture '{captureId}': {ex.Message}");
            return null;
        }
    }

    private void OnDestroy()
    {
        EvaluationReportExporter.OnSaveStateChanged -= HandleSaveStateChanged;
        foreach (var t in loadedThumbnails) if (t != null) Destroy(t);
        loadedThumbnails.Clear();
        thumbnailCache.Clear();
    }

    /// <summary>Mistake log lines recorded during the given step ("Step N: ...").</summary>
    private List<string> FilterMistakesByStep(int step)
    {
        var matches = new List<string>();
        if (VRDemoGameManager.Instance == null) return matches;
        string prefix = $"Step {step}:";
        foreach (var m in VRDemoGameManager.Instance.mistakeLog)
            if (m.StartsWith(prefix)) matches.Add(m);
        return matches;
    }

    private List<string> FilterMistakesByKeywords(params string[] keywords)
    {
        var matches = new List<string>();
        if (VRDemoGameManager.Instance == null) return matches;

        foreach (var m in VRDemoGameManager.Instance.mistakeLog)
        {
            string lower = m.ToLower();
            foreach (var kw in keywords)
            {
                if (lower.Contains(kw))
                {
                    matches.Add(m);
                    break;
                }
            }
        }
        return matches;
    }

    public void RestartSimulation()
    {
        EvaluationReportExporter.DiscardSession(); // no-op if already saved
        PlayPageSound();
        Debug.Log("[SummaryScreen] Restarting simulation...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void PlayPageSound()
    {
        if (audioSource != null && pageFlipSound != null)
        {
            audioSource.PlayOneShot(pageFlipSound);
        }
    }
}
