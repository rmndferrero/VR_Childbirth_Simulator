using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the redesigned Assessment Summary UI at Step 8:
/// - Page 0: Overall Summary (mockup-aligned card layout + 4 mandatory stages + mistake ledger + clinical feedback)
/// - Page 1: Stage 1 Details — Dialogue & Clinical Decision-Making (Selected vs Recommended + Rationale)
/// - Page 2: Stage 2 Details — Perineal Cleaning (separated Perineal Washing & 9-Cotton-Ball cards)
/// - Page 3: Stage 3 Details — Draping (separated Draping the Mother & Draping the Table cards)
/// - Page 4: Stage 4 Details — Table Preparation (placements, wrong-slot attempts & dropped instruments)
/// - Page 5: Decision Matrix Evaluation (parallel WSM score + Save / Don't Save export bar)
/// - Page 6+: Mistake Screenshot Gallery
/// </summary>
public class SummaryScreenController : MonoBehaviour
{
    public static SummaryScreenController Instance { get; private set; }

    [Header("Root Panel")]
    public GameObject rootScreenObject;

    [Header("Overview & Stage Pages")]
    public GameObject page0Overview;
    public GameObject page1Dialogue;
    public GameObject page2Perineal;
    public GameObject page3Draping;
    public GameObject page4TableAndFinal;

    [Header("Universal Footer Navigation")]
    public Button buttonBack;
    public Button buttonOverview;
    public TMP_Text textPageIndicator;
    public Button buttonNext;
    public Button buttonRestart;
    public Button buttonMainMenu;

    [Header("Disclaimer (every evaluation page + report.html)")]
    [TextArea(2, 5)]
    public string disclaimer = EvaluationDisclaimer.DefaultText;
    public TMP_Text disclaimerText;

    [Header("Page 0: Overall Summary Components")]
    public TMP_Text ovHeroScoreText;
    public Image ovCompetencyPillBg;
    public TMP_Text ovCompetencyPillText;
    public Image ovProgressFill;

    [Tooltip("Exact 4 stage buttons in order: 0=Dialogue, 1=Perineal Cleaning, 2=Draping, 3=Table Preparation")]
    public Button[] ovStageButtons = new Button[4];
    public Image[] ovStagePillBgs = new Image[4];
    public TMP_Text[] ovStagePillTexts = new TMP_Text[4];

    [Tooltip("Exact separated Stage 2 sub-rows (Perineal Cleaning)")]
    public TMP_Text ovPerinealWashSubText;
    public TMP_Text ovPerinealCottonSubText;

    [Tooltip("Exact separated Stage 3 sub-rows (Draping)")]
    public TMP_Text ovDrapeMotherSubText;
    public TMP_Text ovDrapeTableSubText;

    [Header("Page 0: Mistake Breakdown Ledger")]
    public TMP_Text ovCottonAffectedValueText;
    public TMP_Text ovCottonDeductionValueText;
    public GameObject[] ovToolRowObjects = new GameObject[4];
    public TMP_Text[] ovToolRowLabelTexts = new TMP_Text[4];
    public TMP_Text[] ovToolRowValueTexts = new TMP_Text[4];
    public TMP_Text ovToolHandlingNoteText;
    public TMP_Text ovToolDeductionValueText;
    public TMP_Text ovTotalDeductionsValueText;

    [Header("Page 0: Clinical Feedback & Drill-Down")]
    public TMP_Text ovFeedbackHeadingText;
    public TMP_Text ovFeedbackBodyText;
    public Button buttonOverviewDrillDown;

    [Header("Page 1: Dialogue & Decision-Making UI")]
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

    [Header("Page 2: Perineal Cleaning UI (Separated Cards)")]
    public TMP_Text p2StatusSummaryText;
    public TMP_Text p2MistakesListText;
    public TMP_Text p2WashCardText;
    public TMP_Text p2CottonCardText;

    [Header("Page 3: Draping UI (Separated Cards)")]
    public TMP_Text p3StatusSummaryText;
    public TMP_Text p3MistakesListText;
    public TMP_Text p3MotherCardText;
    public TMP_Text p3TableCardText;

    [Header("Page 4: Table Preparation UI")]
    public TMP_Text p4FinalScoreText;
    public TMP_Text p4OverallMistakesListText;
    public TMP_Text p4PlacementsCardText;
    public TMP_Text p4DropsCardText;

    [Header("Page 5: Decision Matrix (parallel score)")]
    public GameObject page5Matrix;
    public TMP_Text p5ScoresText;
    public RectTransform p5GridContainer;
    public Button p5SaveButton;
    public Button p5DontSaveButton;
    public TMP_Text p5SaveStatusText;

    [Header("Page 6+: Mistake Gallery")]
    public GameObject page6Gallery;
    public TMP_Text p6HeaderText;
    public GameObject[] p6Cards;
    public RawImage[] p6Thumbnails;
    public TMP_Text[] p6CardTexts;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pageFlipSound;

    private int currentPageIndex = 0;
    private const int OverviewPageIndex = 0;
    private const int DialoguePageIndex = 1;
    private const int PerinealPageIndex = 2;
    private const int DrapingPageIndex = 3;
    private const int TablePrepPageIndex = 4;
    private const int MatrixPageIndex = 5;
    private const int FirstGalleryPageIndex = 6;

    private int MinPageIndex => page0Overview != null ? OverviewPageIndex : DialoguePageIndex;
    private int TotalPages => page5Matrix == null ? TablePrepPageIndex + 1 : FirstGalleryPageIndex + GalleryPageCount;

    private static readonly string[] PageTitles = new string[6]
    {
        "Overall Simulation Assessment",
        "Stage 1: Dialogue & Clinical Decision-Making",
        "Stage 2: Perineal Cleaning Review",
        "Stage 3: Sterile Draping Review",
        "Stage 4: Table Preparation Review",
        "Decision Matrix Evaluation"
    };

    private DecisionMatrixResult matrixResult;
    private readonly List<Texture2D> loadedThumbnails = new List<Texture2D>();
    private readonly Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();

    // Theme Palette (Dark Navy Clinical Tablet matching simulation + reference card clarity)
    private static readonly Color ColGreenBright = new Color(0.18f, 0.84f, 0.48f, 1f);
    private static readonly Color ColGreenPillBg = new Color(0.06f, 0.24f, 0.16f, 0.96f);
    private static readonly Color ColAmberBright = new Color(0.96f, 0.68f, 0.18f, 1f);
    private static readonly Color ColAmberPillBg = new Color(0.28f, 0.18f, 0.05f, 0.96f);
    private static readonly Color ColRedBright   = new Color(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Color ColRedPillBg   = new Color(0.28f, 0.09f, 0.10f, 0.96f);
    private static readonly Color BarNeutral     = new Color(0.22f, 0.66f, 0.94f, 1f);
    private static readonly Color CellNA         = new Color(0.15f, 0.20f, 0.28f, 1f);
    private static readonly Color CellHeader     = new Color(0.08f, 0.12f, 0.18f, 0.95f);
    private static readonly Color CellScored     = new Color(0.20f, 0.29f, 0.40f, 1f);

    private static Sprite cachedRoundedSprite;
    private static Sprite cachedBarSprite;

    public static Sprite GetRoundedSprite()
    {
        if (cachedRoundedSprite != null) return cachedRoundedSprite;

        const int sz = 32;
        const float r = 7f;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[sz * sz];
        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float dx = x < r ? (r - x - 0.5f) : (x >= sz - r ? x - (sz - r) + 0.5f : 0f);
                float dy = y < r ? (r - y - 0.5f) : (y >= sz - r ? y - (sz - r) + 0.5f : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = dist <= 0f ? 1f : Mathf.Clamp01(r + 0.5f - dist);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * sz + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        cachedRoundedSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
        return cachedRoundedSprite;
    }

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

        WireButtons();

        EvaluationReportExporter.OnSaveStateChanged -= HandleSaveStateChanged;
        EvaluationReportExporter.OnSaveStateChanged += HandleSaveStateChanged;

        if (SceneManager.GetActiveScene().name == "SummaryScene" ||
            (VRDemoGameManager.Instance == null && EvaluationReportExporter.State != EvaluationReportExporter.SaveState.None))
        {
            OpenSummaryScreen();
        }
    }

    private void WireButtons()
    {
        BindButton(buttonBack, PreviousPage);
        BindButton(buttonNext, NextPage);
        BindButton(buttonOverview, () => ShowPage(OverviewPageIndex));
        BindButton(buttonOverviewDrillDown, () => ShowPage(DialoguePageIndex));
        BindButton(buttonRestart, RestartSimulation);
        BindButton(buttonMainMenu, ReturnToMainMenu);
        BindButton(p5SaveButton, SaveEvaluation);
        BindButton(p5DontSaveButton, DontSaveEvaluation);

        if (ovStageButtons != null)
        {
            int[] targetPages = { DialoguePageIndex, PerinealPageIndex, DrapingPageIndex, TablePrepPageIndex };
            for (int i = 0; i < ovStageButtons.Length && i < targetPages.Length; i++)
            {
                int pageTarget = targetPages[i];
                BindButton(ovStageButtons[i], () => ShowPage(pageTarget));
            }
        }
    }

    private static void BindButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    public void OpenSummaryScreen()
    {
        if (rootScreenObject != null) rootScreenObject.SetActive(true);
        WireButtons();
        EvaluationDisclaimer.Text = disclaimer;
        if (disclaimerText != null) disclaimerText.text = EvaluationDisclaimer.Text;

        var gm = VRDemoGameManager.Instance;
        var rubric = gm != null ? gm.scoringRubric : VRDemoGameManager.LastRubric;
        var debrief = gm != null ? gm.communicationDebrief : VRDemoGameManager.LastDebrief;
        matrixResult = EvaluationLogger.Instance.Evaluate(rubric, debrief);

        PopulatePage0_Overview(rubric, debrief);
        PopulatePage1_Dialogue(debrief);
        PopulatePage2_Perineal();
        PopulatePage3_Draping();
        PopulatePage4_TableAndFinal();
        PopulatePage5_Matrix(rubric);

        ShowPage(MinPageIndex);
    }

    public void ShowPage(int index)
    {
        currentPageIndex = Mathf.Clamp(index, MinPageIndex, TotalPages - 1);

        if (page0Overview != null) page0Overview.SetActive(currentPageIndex == OverviewPageIndex);
        if (page1Dialogue != null) page1Dialogue.SetActive(currentPageIndex == DialoguePageIndex);
        if (page2Perineal != null) page2Perineal.SetActive(currentPageIndex == PerinealPageIndex);
        if (page3Draping != null) page3Draping.SetActive(currentPageIndex == DrapingPageIndex);
        if (page4TableAndFinal != null) page4TableAndFinal.SetActive(currentPageIndex == TablePrepPageIndex);
        if (page5Matrix != null) page5Matrix.SetActive(currentPageIndex == MatrixPageIndex);

        bool onGallery = page5Matrix != null && currentPageIndex >= FirstGalleryPageIndex;
        if (page6Gallery != null) page6Gallery.SetActive(onGallery);
        if (onGallery) PopulateGalleryPage(currentPageIndex - FirstGalleryPageIndex);
        if (currentPageIndex == MatrixPageIndex) RefreshSaveBar();

        if (textPageIndicator != null)
        {
            string title = currentPageIndex < PageTitles.Length
                ? PageTitles[currentPageIndex]
                : (GalleryPageCount > 1
                    ? $"Mistake Gallery ({currentPageIndex - FirstGalleryPageIndex + 1}/{GalleryPageCount})"
                    : "Mistake Gallery");
            int displayIndex = currentPageIndex - MinPageIndex + 1;
            int displayTotal = TotalPages - MinPageIndex;
            textPageIndicator.text = $"{displayIndex} / {displayTotal}  •  {title}";
        }

        if (buttonBack != null) buttonBack.interactable = currentPageIndex > MinPageIndex;
        if (buttonOverview != null) buttonOverview.gameObject.SetActive(page0Overview != null && currentPageIndex != OverviewPageIndex);
        if (buttonNext != null) buttonNext.gameObject.SetActive(currentPageIndex < TotalPages - 1);
        if (buttonRestart != null) buttonRestart.gameObject.SetActive(currentPageIndex == TotalPages - 1);

        PlayPageSound();
    }

    public void NextPage()
    {
        if (currentPageIndex < TotalPages - 1) ShowPage(currentPageIndex + 1);
    }

    public void PreviousPage()
    {
        if (currentPageIndex > MinPageIndex) ShowPage(currentPageIndex - 1);
    }

    // ═══════════════════════════════════════════════════════════════
    // PAGE 0: OVERALL SUMMARY (Matches Uploaded Mockup & Mandatory 4-Stage Hierarchy)
    // ═══════════════════════════════════════════════════════════════

    private void PopulatePage0_Overview(SimulationScoringRubricSO rubric, CommunicationDebriefData debrief)
    {
        if (page0Overview == null) return;

        int finalScore = GetRecordedFinalScore();
        int totalDeductions = GetRecordedTotalDeductions(finalScore);
        int passScore = rubric != null ? rubric.passThresholdScore : 70;
        int honorsScore = rubric != null ? rubric.honorsThresholdScore : 85;

        Color heroColor = finalScore >= honorsScore ? ColGreenBright : (finalScore >= passScore ? ColAmberBright : ColRedBright);
        Color pillBg = finalScore >= honorsScore ? ColGreenPillBg : (finalScore >= passScore ? ColAmberPillBg : ColRedPillBg);
        string pillText = finalScore >= honorsScore ? "Good performance" : (finalScore >= passScore ? "Passed" : "Needs improvement");
        if (finalScore == 100) pillText = "Excellent";

        if (ovHeroScoreText != null)
        {
            ovHeroScoreText.text = $"{finalScore}<size=55%><color=#7A92AD>/100</color></size>";
            ovHeroScoreText.color = heroColor;
        }
        SetPill(ovCompetencyPillBg, ovCompetencyPillText, pillText, heroColor, pillBg);

        if (ovProgressFill != null)
        {
            EnsureFillableSprite(ovProgressFill);
            ovProgressFill.fillAmount = Mathf.Clamp01(finalScore / 100f);
            ovProgressFill.color = heroColor;
        }

        // Gather stage metrics
        var events = EvaluationLogger.Instance.events ?? new List<EvaluationEvent>();
        int wrongWashCount = CountViolations(events, ClinicalViolationType.Wrong_Washing_Area);
        int washDeduction = SumLegacyDeductionsForTypes(events, ClinicalViolationType.Wrong_Washing_Area);

        int cottonMistakes = CountViolations(events,
            ClinicalViolationType.Out_Of_Order_Stroke,
            ClinicalViolationType.Reverse_Wipe_Direction,
            ClinicalViolationType.Reused_Cotton_Ball,
            ClinicalViolationType.Contaminated_Drop_Outside_Basin,
            ClinicalViolationType.Forceps_Transfer_Error);
        int affectedCottons = GetAffectedCottonCount();
        int cottonTierDeduction = GetCottonDeduction(affectedCottons);
        int strokePenalty = SumLegacyDeductionsForTypes(events,
            ClinicalViolationType.Out_Of_Order_Stroke,
            ClinicalViolationType.Reverse_Wipe_Direction);

        int motherDrapeMistakes = CountViolationsForStepAndDetail(events, 6, "Mayo", excludeMatch: true);
        int tableDrapeMistakes = CountViolationsForStepAndDetail(events, 6, "Mayo", excludeMatch: false);

        int tablePrepSlotMistakes = CountViolations(events, ClinicalViolationType.Incorrect_Table_Slot, ClinicalViolationType.Unsterile_Mayo_Placement_Violation);
        int toolDrops = GetTotalToolDropCount();
        int toolDeduction = GetToolDropDeduction(toolDrops);

        bool dialogueOk = debrief == null || (debrief.compositeScore >= 80f && CountViolations(events, ClinicalViolationType.Tool_Handled_Before_Consent) == 0);
        bool washOk = wrongWashCount == 0;
        bool cottonOk = affectedCottons == 0 && cottonMistakes == 0;
        bool perinealOk = washOk && cottonOk;
        bool motherDrapeOk = motherDrapeMistakes == 0;
        bool tableDrapeOk = tableDrapeMistakes == 0;
        bool drapingOk = motherDrapeOk && tableDrapeOk;
        bool tablePrepOk = tablePrepSlotMistakes == 0 && toolDrops == 0;

        // 4 Mandatory Top-Level Stages in Exact Order
        SetStagePill(0, dialogueOk ? "Passed" : "Needs improvement", dialogueOk);
        SetStagePill(1, perinealOk ? "Passed" : "Needs improvement", perinealOk);
        SetStagePill(2, drapingOk ? "Passed" : "Needs improvement", drapingOk);
        SetStagePill(3, tablePrepOk ? "Passed" : "Needs improvement", tablePrepOk);

        // Exact 2 Separated Sub-Rows under Perineal Cleaning
        if (ovPerinealWashSubText != null)
        {
            ovPerinealWashSubText.text = washOk
                ? "<color=#34D399>Passed</color>"
                : $"<color=#FBBF24>{wrongWashCount} wrong area{(washDeduction > 0 ? $" (-{washDeduction})" : "")}</color>";
        }
        if (ovPerinealCottonSubText != null)
        {
            int cottonStagePen = cottonTierDeduction + strokePenalty;
            ovPerinealCottonSubText.text = cottonOk
                ? "<color=#34D399>Passed</color>"
                : $"<color=#FBBF24>{cottonMistakes + affectedCottons} issue(s){(cottonStagePen > 0 ? $" (-{cottonStagePen})" : "")}</color>";
        }

        // Exact 2 Separated Sub-Rows under Draping
        if (ovDrapeMotherSubText != null)
        {
            ovDrapeMotherSubText.text = motherDrapeOk
                ? "<color=#34D399>Passed</color>"
                : $"<color=#FBBF24>{motherDrapeMistakes} error(s)</color>";
        }
        if (ovDrapeTableSubText != null)
        {
            ovDrapeTableSubText.text = tableDrapeOk
                ? "<color=#34D399>Passed</color>"
                : $"<color=#FBBF24>{tableDrapeMistakes} error(s)</color>";
        }

        // Mistake Breakdown Ledger
        if (ovCottonAffectedValueText != null) ovCottonAffectedValueText.text = affectedCottons.ToString();
        if (ovCottonDeductionValueText != null)
        {
            ovCottonDeductionValueText.text = cottonTierDeduction > 0 ? $"-{cottonTierDeduction}" : "0";
            ovCottonDeductionValueText.color = cottonTierDeduction > 0 ? ColRedBright : new Color(0.58f, 0.68f, 0.78f, 1f);
        }

        var dropEntries = GetToolDropBreakdownEntries();
        PopulateToolBreakdownRows(dropEntries, toolDrops);

        if (ovToolDeductionValueText != null)
        {
            ovToolDeductionValueText.text = toolDeduction > 0 ? $"-{toolDeduction}" : "0";
            ovToolDeductionValueText.color = toolDeduction > 0 ? ColRedBright : new Color(0.58f, 0.68f, 0.78f, 1f);
        }
        if (ovTotalDeductionsValueText != null)
        {
            ovTotalDeductionsValueText.text = totalDeductions > 0 ? $"-{totalDeductions}" : "0";
            ovTotalDeductionsValueText.color = totalDeductions > 0 ? ColRedBright : ColGreenBright;
        }

        // Clinical Feedback Block
        AssignClinicalFeedback(debrief, washOk, cottonOk, drapingOk, tablePrepSlotMistakes == 0, toolDrops);
    }

    private void PopulateToolBreakdownRows(List<ToolDropSummaryEntry> dropEntries, int totalDrops)
    {
        if (ovToolRowObjects == null) return;

        for (int i = 0; i < ovToolRowObjects.Length; i++)
        {
            if (ovToolRowObjects[i] != null) ovToolRowObjects[i].SetActive(false);
        }

        if (totalDrops == 0 || dropEntries.Count == 0)
        {
            if (ovToolRowObjects.Length > 0 && ovToolRowObjects[0] != null)
            {
                ovToolRowObjects[0].SetActive(true);
                if (ovToolRowLabelTexts[0] != null) ovToolRowLabelTexts[0].text = "Dropped tools";
                if (ovToolRowValueTexts[0] != null) ovToolRowValueTexts[0].text = "0";
            }
            if (ovToolHandlingNoteText != null)
            {
                ovToolHandlingNoteText.gameObject.SetActive(true);
                ovToolHandlingNoteText.text = "All instruments were handled properly.";
            }
            return;
        }

        if (ovToolHandlingNoteText != null) ovToolHandlingNoteText.gameObject.SetActive(false);

        int slotCap = Mathf.Min(ovToolRowObjects.Length, dropEntries.Count);
        for (int i = 0; i < slotCap; i++)
        {
            if (ovToolRowObjects[i] == null) continue;
            ovToolRowObjects[i].SetActive(true);

            // If more than 4 distinct tools dropped, summarize remaining in the last row cleanly
            if (i == slotCap - 1 && dropEntries.Count > ovToolRowObjects.Length)
            {
                int restCount = 0;
                for (int r = i; r < dropEntries.Count; r++) restCount += dropEntries[r].dropCount;
                if (ovToolRowLabelTexts[i] != null) ovToolRowLabelTexts[i].text = "Other tools dropped";
                if (ovToolRowValueTexts[i] != null) ovToolRowValueTexts[i].text = restCount.ToString();
            }
            else
            {
                if (ovToolRowLabelTexts[i] != null) ovToolRowLabelTexts[i].text = $"{dropEntries[i].toolName} dropped";
                if (ovToolRowValueTexts[i] != null) ovToolRowValueTexts[i].text = dropEntries[i].dropCount.ToString();
            }
        }
    }

    private void AssignClinicalFeedback(CommunicationDebriefData debrief, bool washOk, bool cottonOk, bool drapingOk, bool tableSlotsOk, int toolDrops)
    {
        if (ovFeedbackHeadingText == null || ovFeedbackBodyText == null) return;

        if (toolDrops > 0 || !tableSlotsOk)
        {
            ovFeedbackHeadingText.text = "Instrument handling";
            ovFeedbackBodyText.text = "Improper handling or misplacement of instruments can compromise sterility and increase contamination risks. Maintain firm control of instruments over the sterile field.";
        }
        else if (!cottonOk || !washOk)
        {
            ovFeedbackHeadingText.text = "Aseptic perineal cleansing";
            ovFeedbackBodyText.text = "Ensure anterior-to-posterior cleansing over the perineal zone using one fresh cotton ball per stroke and discarding cleanly into the waste basin.";
        }
        else if (!drapingOk)
        {
            ovFeedbackHeadingText.text = "Sterile draping technique";
            ovFeedbackBodyText.text = "Unfold sterile linen using both hands and drape anatomical zones and the Mayo stand in proper aseptic order.";
        }
        else if (debrief != null && debrief.compositeScore < 80f)
        {
            ovFeedbackHeadingText.text = "Therapeutic communication";
            ovFeedbackBodyText.text = "Verify two patient identifiers, introduce your nursing role clearly, and explain expected physical sensations before beginning care.";
        }
        else
        {
            ovFeedbackHeadingText.text = "Optimal sterile adherence";
            ovFeedbackBodyText.text = "All communication checks, perineal cleansing strokes, sterile drapes, and instrument placements met aseptic protocol standards.";
        }
    }

    private void SetStagePill(int idx, string text, bool passed)
    {
        if (ovStagePillBgs == null || idx >= ovStagePillBgs.Length) return;
        Color fg = passed ? ColGreenBright : ColAmberBright;
        Color bg = passed ? ColGreenPillBg : ColAmberPillBg;
        SetPill(ovStagePillBgs[idx], ovStagePillTexts != null && idx < ovStagePillTexts.Length ? ovStagePillTexts[idx] : null, text, fg, bg);
    }

    private static void SetPill(Image bg, TMP_Text label, string text, Color fg, Color fill)
    {
        if (bg != null)
        {
            if (bg.sprite == null) bg.sprite = GetRoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.color = fill;
        }
        if (label != null)
        {
            label.text = text;
            label.color = fg;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PAGE 1: STAGE 1 DETAILS — DIALOGUE & DECISION-MAKING
    // ═══════════════════════════════════════════════════════════════

    private void PopulatePage1_Dialogue(CommunicationDebriefData debrief)
    {
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
            string penTxt = debrief.dialogueDeduction > 0 ? $"  <color=#F87171>(-{debrief.dialogueDeduction} pts)</color>" : "  <color=#34D399>(0 deduction)</color>";
            p1OverallRatingText.text = $"Communication score: <b>{Mathf.RoundToInt(debrief.compositeScore)}%</b>{penTxt}";
        }

        if (p1ArchetypeTitleText != null)
        {
            p1ArchetypeTitleText.text = $"{debrief.tierRating}  •  {debrief.archetypeTitle}";
        }

        if (p1DebriefNotesText != null)
        {
            p1DebriefNotesText.text = string.IsNullOrWhiteSpace(debrief.personalizedInstructorDebrief)
                ? "No communication notes were recorded."
                : debrief.personalizedInstructorDebrief;
        }

        SetBar(p1BarProfessionalism, p1TextProfessionalism, debrief.professionalismPercent);
        SetBar(p1BarEmpathy, p1TextEmpathy, debrief.empathyPercent);
        SetBar(p1BarClarity, p1TextClarity, debrief.clarityPercent);

        if (p1CritiquesListText != null)
        {
            p1CritiquesListText.text = BuildDialogueDecisionsMarkup(debrief);
        }
    }

    private static string BuildDialogueDecisionsMarkup(CommunicationDebriefData debrief)
    {
        if (debrief.decisionEntries != null && debrief.decisionEntries.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var entry in debrief.decisionEntries)
            {
                string statusTag = entry.isSuboptimalOrWrong
                    ? $"<color=#FBBF24>[Needs Review • {entry.selectedScore:0.0}/5]</color>"
                    : $"<color=#34D399>[Optimal • {entry.selectedScore:0.0}/5]</color>";
                sb.AppendLine($"<b>{entry.slotLabel}</b> {statusTag}");
                sb.AppendLine($"<color=#9FB3C8>Q: {entry.promptQuestion}</color>");
                if (entry.isSuboptimalOrWrong)
                {
                    sb.AppendLine($"<color=#F87171>• Your Choice:</color> \"{entry.selectedPhrase}\" — {entry.whyWrongNote}");
                    sb.AppendLine($"<color=#34D399>• Recommended Answer:</color> \"{entry.recommendedPhrase}\"");
                    sb.AppendLine($"<color=#94A3B8>  Why it matters: {entry.clinicalRationale}</color>");
                }
                else
                {
                    sb.AppendLine($"<color=#E2E8F0>• Your Choice:</color> \"{entry.selectedPhrase}\"");
                    sb.AppendLine($"<color=#94A3B8>  Rationale: {entry.clinicalRationale}</color>");
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        return debrief.specificTokenCritiques.Count > 0
            ? "<b>Phrase observations:</b>\n• " + string.Join("\n• ", debrief.specificTokenCritiques.ToArray())
            : "<b>Phrase observations:</b>\n• No phrase observations were recorded.";
    }

    private void SetBar(Image bar, TMP_Text label, float percentage)
    {
        if (bar != null)
        {
            EnsureFillableSprite(bar);
            bar.fillAmount = Mathf.Clamp01(percentage / 100f);
            bar.color = BarNeutral;
        }

        if (label != null) label.text = $"{Mathf.RoundToInt(percentage)}%";
    }

    private static void EnsureFillableSprite(Image img)
    {
        if (img.sprite == null)
        {
            if (cachedBarSprite == null)
                cachedBarSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            img.sprite = cachedBarSprite;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PAGE 2: STAGE 2 DETAILS — PERINEAL CLEANING (SEPARATED SUB-CARDS)
    // ═══════════════════════════════════════════════════════════════

    private void PopulatePage2_Perineal()
    {
        var events = EvaluationLogger.Instance.events ?? new List<EvaluationEvent>();
        int wrongWashCount = CountViolations(events, ClinicalViolationType.Wrong_Washing_Area);
        int affectedCottons = GetAffectedCottonCount();
        int cottonTierDeduction = GetCottonDeduction(affectedCottons);
        int strokes3 = CountCorrectEvents(3, "Stroke");
        int strokes5 = CountCorrectEvents(5, "Stroke");

        if (p2WashCardText != null)
        {
            var washMistakes = FilterEventsByType(events, ClinicalViolationType.Wrong_Washing_Area);
            var sb = new StringBuilder();
            sb.AppendLine("<b>PART 1 — PERINEAL WASHING (Steps 2 & 4)</b>");
            sb.AppendLine($"• {StepStatusLine(2)}");
            sb.AppendLine($"• {StepStatusLine(4)}");
            if (washMistakes.Count == 0)
            {
                sb.AppendLine("<color=#34D399>• Area Compliance: Warm water poured cleanly over perineal area (0 off-target errors).</color>");
            }
            else
            {
                sb.AppendLine($"<color=#F87171>• Off-Target Pouring ({wrongWashCount}x):</color> Poured onto surrounding legs/abdomen during Step 2 wash.");
            }
            p2WashCardText.text = sb.ToString().TrimEnd();
        }

        if (p2CottonCardText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>PART 2 — 9-COTTON-BALL CLEANSING TECHNIQUE (Steps 3 & 5)</b>");
            sb.AppendLine($"• Step 3 (7.5% Scrub): {strokes3} strokes  |  Step 5 (10% Paint): {strokes5} strokes");
            sb.AppendLine($"• Cotton balls affected: <b>{affectedCottons}</b>   |   Cotton ball deduction: <b>{(cottonTierDeduction > 0 ? $"<color=#F87171>-{cottonTierDeduction} pts</color>" : "<color=#34D399>0 pts</color>")}</b>");

            var techMistakes = FilterMistakesByKeywords("wipe", "stroke", "cotton", "used", "order", "reverse");
            if (techMistakes.Count == 0)
            {
                sb.AppendLine("<color=#34D399>• Technique Compliance: Clean anterior-to-posterior order preserved with single-use cotton balls.</color>");
            }
            else
            {
                sb.AppendLine($"<color=#FBBF24>• Recorded Issues ({techMistakes.Count}):</color> " + string.Join("; ", techMistakes.Take(3)));
            }
            sb.Append("<color=#9FB3C8>Rationale: Clean-to-dirty strokes and single-use cotton prevent perineal-to-vaginal fecal flora transfer.</color>");
            p2CottonCardText.text = sb.ToString();
        }

        // Legacy fallback fields if an un-upgraded scene uses them
        if (p2StatusSummaryText != null)
        {
            p2StatusSummaryText.text =
                "<b>Steps recorded:</b>\n" +
                StepStatusLine(2) + "\n" +
                StepStatusLine(3, strokes3, "strokes") + "\n" +
                StepStatusLine(4) + "\n" +
                StepStatusLine(5, strokes5, "strokes");
        }
        if (p2MistakesListText != null)
        {
            p2MistakesListText.text = RecordedMistakesText(FilterMistakesByKeywords("wipe", "stroke", "cotton", "wash", "perineal", "iodine"));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PAGE 3: STAGE 3 DETAILS — DRAPING (SEPARATED SUB-CARDS)
    // ═══════════════════════════════════════════════════════════════

    private void PopulatePage3_Draping()
    {
        var events = EvaluationLogger.Instance.events ?? new List<EvaluationEvent>();
        int placedDrapeCount = CountCorrectEvents(6, "Drape");
        int motherMistakes = CountViolationsForStepAndDetail(events, 6, "Mayo", excludeMatch: true);
        int tableMistakes = CountViolationsForStepAndDetail(events, 6, "Mayo", excludeMatch: false);

        if (p3MotherCardText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>PART 1 — DRAPING THE MOTHER (4 Anatomical Zones)</b>");
            sb.AppendLine($"• Patient drapes placed: <b>{Mathf.Min(4, placedDrapeCount)} / 4</b> (Buttocks -> Abdomen -> Left Thigh -> Right Thigh)");
            sb.AppendLine(motherMistakes == 0
                ? "<color=#34D399>• Protocol Compliance: Sequential two-handed sterile draping maintained.</color>"
                : $"<color=#F87171>• Recorded Violations ({motherMistakes}):</color> One-handed hold or out-of-order drape placement.");
            p3MotherCardText.text = sb.ToString().TrimEnd();
        }

        if (p3TableCardText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>PART 2 — DRAPING THE TABLE (Mayo Stand Sterile Cover)</b>");
            bool mayoDraped = TablePrepManager.Instance != null ? TablePrepManager.Instance.isMayoDraped : placedDrapeCount >= 4;
            sb.AppendLine($"• Mayo stand sterile drape status: <b>{(mayoDraped ? "<color=#34D399>Draped & Sterile</color>" : "<color=#FBBF24>Not Completed</color>")}</b>");
            sb.AppendLine(tableMistakes == 0
                ? "<color=#34D399>• Technique Compliance: Mayo stand draped properly before placing any surgical tools.</color>"
                : $"<color=#F87171>• Recorded Violations ({tableMistakes}):</color> Improper 1-handed drape or instrument placed on bare Mayo stand.");
            sb.Append("<color=#9FB3C8>Rationale: Maintaining a sterile barrier over both mother and Mayo tray isolates the surgical field.</color>");
            p3TableCardText.text = sb.ToString();
        }

        if (p3StatusSummaryText != null)
        {
            p3StatusSummaryText.text =
                "<b>Steps recorded:</b>\n" +
                StepStatusLine(6, placedDrapeCount, "drapes placed");
        }
        if (p3MistakesListText != null)
        {
            p3MistakesListText.text = RecordedMistakesText(FilterMistakesByStep(6));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PAGE 4: STAGE 4 DETAILS — TABLE PREPARATION
    // ═══════════════════════════════════════════════════════════════

    private void PopulatePage4_TableAndFinal()
    {
        int score = GetRecordedFinalScore();
        var events = EvaluationLogger.Instance.events ?? new List<EvaluationEvent>();
        int wrongSlots = CountViolations(events, ClinicalViolationType.Incorrect_Table_Slot);
        int unsterileMayo = CountViolations(events, ClinicalViolationType.Unsterile_Mayo_Placement_Violation);
        int totalDrops = GetTotalToolDropCount();
        int toolDeduction = GetToolDropDeduction(totalDrops);
        var dropEntries = GetToolDropBreakdownEntries();

        if (p4PlacementsCardText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>MAYO TABLE INSTRUMENT PLACEMENTS</b>");
            bool prepDone = TablePrepManager.Instance != null && TablePrepManager.Instance.isTablePrepCompleted;
            sb.AppendLine($"• Setup Confirmation: <b>{(prepDone ? "<color=#34D399>Confirmed Complete</color>" : StepStatusLine(7))}</b>");
            sb.AppendLine($"• Wrong-slot placement attempts: <b>{wrongSlots}</b>   |   Unsterile bare-Mayo breaches: <b>{unsterileMayo}</b>");
            if (wrongSlots == 0 && unsterileMayo == 0)
            {
                sb.AppendLine("<color=#34D399>• Slot Accuracy: All instruments matched their designated tray positions.</color>");
            }
            else
            {
                var step7Mistakes = FilterMistakesByStep(7);
                if (step7Mistakes.Count > 0) sb.AppendLine($"<color=#FBBF24>• Recent Issues:</color> " + string.Join("; ", step7Mistakes.Take(3)));
            }
            p4PlacementsCardText.text = sb.ToString().TrimEnd();
        }

        if (p4DropsCardText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>INSTRUMENT HANDLING & DROPS</b>");
            if (totalDrops == 0 || dropEntries.Count == 0)
            {
                sb.AppendLine("• Dropped tools: <b>0</b>   |   Tool deduction: <color=#34D399><b>0 pts</b></color>");
                sb.AppendLine("<color=#34D399>• All instruments were handled properly throughout the simulation.</color>");
            }
            else
            {
                foreach (var entry in dropEntries)
                {
                    sb.AppendLine($"• {entry.toolName} dropped: <b>{entry.dropCount}</b>");
                }
                sb.AppendLine($"• Tool deduction (tiered): <color=#F87171><b>-{toolDeduction} pts</b></color>");
            }
            sb.Append("<color=#9FB3C8>Rationale: Dropping sterile instruments onto non-sterile surfaces contaminates the tray and requires replacement.</color>");
            p4DropsCardText.text = sb.ToString();
        }

        if (p4FinalScoreText != null) p4FinalScoreText.text = $"Recorded score: <b>{score} / 100</b>";
        if (p4OverallMistakesListText != null)
        {
            var allMistakes = GetRecordedMistakeStrings();
            p4OverallMistakesListText.text = allMistakes.Count == 0
                ? "<b>Recorded mistakes (0):</b>\n• No mistakes were recorded by the simulation."
                : $"<b>Recorded mistakes ({allMistakes.Count}):</b>\n• " + string.Join("\n• ", allMistakes.ToArray());
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PAGE 5: DECISION MATRIX EVALUATION (PRESERVED + WIRED)
    // ═══════════════════════════════════════════════════════════════

    private void PopulatePage5_Matrix(SimulationScoringRubricSO rubric)
    {
        if (page5Matrix == null) return;
        var config = EvaluationLogger.Instance.Matrix.ToConfig(rubric);
        int legacy = GetRecordedFinalScore();

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
        int rows = config.StepCount + 2;
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

    // ═══════════════════════════════════════════════════════════════
    // PAGE 6+: MISTAKE GALLERY
    // ═══════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════
    // DATA HELPERS & LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    private int GetRecordedFinalScore()
    {
        if (VRDemoGameManager.Instance != null) return Mathf.Clamp(VRDemoGameManager.Instance.currentScore, 0, 100);
        if (VRDemoGameManager.LastCompletedRecord != null) return Mathf.Clamp(VRDemoGameManager.LastCompletedRecord.finalScore, 0, 100);
        return 100;
    }

    private int GetRecordedTotalDeductions(int finalScore)
    {
        if (VRDemoGameManager.Instance != null) return VRDemoGameManager.Instance.TotalDeductions;
        if (VRDemoGameManager.LastCompletedRecord != null && VRDemoGameManager.LastCompletedRecord.totalDeductions > 0)
            return VRDemoGameManager.LastCompletedRecord.totalDeductions;
        return Mathf.Max(0, 100 - finalScore);
    }

    private int GetAffectedCottonCount()
    {
        if (VRDemoGameManager.Instance != null) return VRDemoGameManager.Instance.AffectedCottonBallCount;
        if (VRDemoGameManager.LastCompletedRecord != null) return VRDemoGameManager.LastCompletedRecord.affectedCottonBallCount;
        return 0;
    }

    private static int GetCottonDeduction(int count) => VRDemoGameManager.GetCottonBallTierPenalty(count);

    private int GetTotalToolDropCount()
    {
        if (VRDemoGameManager.Instance != null) return VRDemoGameManager.Instance.TotalToolDropCount;
        if (VRDemoGameManager.LastCompletedRecord != null) return VRDemoGameManager.LastCompletedRecord.totalToolDropCount;
        return 0;
    }

    private static int GetToolDropDeduction(int count) => VRDemoGameManager.GetToolDropTierPenalty(count);

    private List<ToolDropSummaryEntry> GetToolDropBreakdownEntries()
    {
        var result = new List<ToolDropSummaryEntry>();
        if (VRDemoGameManager.Instance != null)
        {
            foreach (var kvp in VRDemoGameManager.Instance.ToolDropCounts)
            {
                if (kvp.Value > 0) result.Add(new ToolDropSummaryEntry { toolName = kvp.Key, dropCount = kvp.Value });
            }
        }
        else if (VRDemoGameManager.LastCompletedRecord?.toolDropBreakdown != null)
        {
            foreach (var e in VRDemoGameManager.LastCompletedRecord.toolDropBreakdown)
            {
                if (e != null && e.dropCount > 0) result.Add(e);
            }
        }
        result.Sort((a, b) => b.dropCount.CompareTo(a.dropCount));
        return result;
    }

    private static int CountViolations(List<EvaluationEvent> events, params ClinicalViolationType[] types)
    {
        int count = 0;
        var set = new HashSet<string>(types.Select(t => t.ToString()));
        foreach (var e in events)
        {
            if (e != null && !e.isCorrect && !string.IsNullOrEmpty(e.violationType) && set.Contains(e.violationType)) count++;
        }
        return count;
    }

    private static int SumLegacyDeductionsForTypes(List<EvaluationEvent> events, params ClinicalViolationType[] types)
    {
        int sum = 0;
        var set = new HashSet<string>(types.Select(t => t.ToString()));
        foreach (var e in events)
        {
            if (e != null && !e.isCorrect && e.legacyPenaltyApplied && set.Contains(e.violationType)) sum += e.legacyPenalty;
        }
        return sum;
    }

    private static List<EvaluationEvent> FilterEventsByType(List<EvaluationEvent> events, ClinicalViolationType type)
    {
        string target = type.ToString();
        return events.Where(e => e != null && !e.isCorrect && e.violationType == target).ToList();
    }

    private static int CountViolationsForStepAndDetail(List<EvaluationEvent> events, int step, string keyword, bool excludeMatch)
    {
        int count = 0;
        foreach (var e in events)
        {
            if (e == null || e.isCorrect || e.step != step) continue;
            bool hasKw = (!string.IsNullOrEmpty(e.detail) && e.detail.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                         (!string.IsNullOrEmpty(e.action) && e.action.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            if (excludeMatch ? !hasKw : hasKw) count++;
        }
        return count;
    }

    private static string RecordedMistakesText(List<string> mistakes)
    {
        return mistakes.Count == 0
            ? "<b>Recorded mistakes (0):</b>\n• No mistakes were recorded in this section."
            : $"<b>Recorded mistakes ({mistakes.Count}):</b>\n• " + string.Join("\n• ", mistakes.ToArray());
    }

    private static string StepStatusLine(int step, int count = -1, string countLabel = null)
    {
        bool completed = false;
        foreach (var t in EvaluationLogger.Instance.stepTimings)
            if (t.step == step && t.endTime >= 0f) { completed = true; break; }

        string name = EvaluationLogger.Instance.Matrix.ToConfig().StepName(step);
        string extra = count >= 0 ? $"  <color=#9FB3C8>({count} {countLabel})</color>" : "";
        return $"Step {step}: {name} — {(completed ? "Completed" : "Not completed")}{extra}";
    }

    private static int CountCorrectEvents(int step, string actionPrefix)
    {
        int n = 0;
        foreach (var e in EvaluationLogger.Instance.events)
            if (e.isCorrect && e.step == step && e.action != null && e.action.StartsWith(actionPrefix)) n++;
        return n;
    }

    private List<string> GetRecordedMistakeStrings()
    {
        if (VRDemoGameManager.Instance != null) return VRDemoGameManager.Instance.mistakeLog;
        if (VRDemoGameManager.LastCompletedRecord?.mistakesMade != null) return VRDemoGameManager.LastCompletedRecord.mistakesMade;
        return new List<string>();
    }

    private List<string> FilterMistakesByStep(int step)
    {
        var matches = new List<string>();
        string prefix = $"Step {step}:";
        foreach (var m in GetRecordedMistakeStrings())
            if (m != null && m.StartsWith(prefix)) matches.Add(m);
        return matches;
    }

    private List<string> FilterMistakesByKeywords(params string[] keywords)
    {
        var matches = new List<string>();
        foreach (var m in GetRecordedMistakeStrings())
        {
            if (string.IsNullOrEmpty(m)) continue;
            string lower = m.ToLowerInvariant();
            foreach (var kw in keywords)
            {
                if (lower.Contains(kw)) { matches.Add(m); break; }
            }
        }
        return matches;
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
        PreloadAllThumbnails();
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
        catch (Exception ex)
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

    public void RestartSimulation()
    {
        EvaluationReportExporter.DiscardSession();
        PlayPageSound();
        Debug.Log("[SummaryScreen] Restarting simulation into MainScene...");
        VRSceneTransitionFader.FadeToScene("MainScene");
    }

    public void ReturnToMainMenu()
    {
        EvaluationReportExporter.DiscardSession();
        PlayPageSound();
        Debug.Log("[SummaryScreen] Returning to Main Menu...");
        VRSceneTransitionFader.FadeToScene("MenuScene");
    }

    private void PlayPageSound()
    {
        if (audioSource != null && pageFlipSound != null) audioSource.PlayOneShot(pageFlipSound);
    }
}
