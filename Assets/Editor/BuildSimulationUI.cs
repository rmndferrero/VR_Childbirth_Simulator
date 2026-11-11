using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class BuildSimulationUI
{
    private const string PrefabDir = "Assets/Prefabs";
    private const string ChipPrefabPath = PrefabDir + "/DraggablePhraseChip.prefab";
    private const string InterBoldFontPath = "Assets/Fonts/Inter_Bold_SDF.asset";
    private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Build Simulation UI (SummaryScreen & PhraseMatrix)")]
    public static void BuildUI()
    {
        // 1. Ensure Scene is loaded
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "MainScene")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        }

        // 2. Find DialogueCanvas
        var dialogueCanvasObj = GameObject.Find("DialogueCanvas");
        if (dialogueCanvasObj == null)
        {
            Debug.LogError("[BuildSimulationUI] DialogueCanvas not found in scene!");
            return;
        }

        // Ensure directories exist
        if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);

        // Load Inter Bold TMP Font
        var tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterBoldFontPath);
        if (tmpFont == null)
        {
            tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        }

        // 3. Create Chip Prefab
        var chipPrefab = CreateChipPrefab(tmpFont);

        // 4. Build or Replace PhraseDecisionMatrix GameObject
        var matrixObj = dialogueCanvasObj.transform.Find("PhraseDecisionMatrix")?.gameObject;
        if (matrixObj != null) Object.DestroyImmediate(matrixObj);

        matrixObj = BuildPhraseDecisionMatrix(dialogueCanvasObj.transform, chipPrefab, tmpFont);
        matrixObj.SetActive(false); // Initially hidden until "Talk to Mother" clicked

        // 5. Build or Replace SummaryScreen GameObject
        var summaryObj = dialogueCanvasObj.transform.Find("SummaryScreen")?.gameObject;
        if (summaryObj != null) Object.DestroyImmediate(summaryObj);

        summaryObj = BuildSummaryScreen(dialogueCanvasObj.transform, tmpFont);
        summaryObj.SetActive(false); // Initially hidden until Step 8

        // 6. Connect references to VRDemoGameManager
        var gameMgr = Object.FindFirstObjectByType<VRDemoGameManager>();
        if (gameMgr != null)
        {
            var rubric = AssetDatabase.LoadAssetAtPath<SimulationScoringRubricSO>("Assets/ScenarioData/Scoring/OSCE_Strict_Scoring_Rubric.asset");
            gameMgr.scoringRubric = rubric;
            gameMgr.summaryScreenController = summaryObj.GetComponent<SummaryScreenController>();
            EditorUtility.SetDirty(gameMgr);
            Debug.Log("[BuildSimulationUI] Connected scoringRubric and summaryScreenController to VRDemoGameManager.");
        }

        // 7. Connect references to FloatingPokeDialogueUI
        var dialogueUI = dialogueCanvasObj.GetComponent<FloatingPokeDialogueUI>();
        if (dialogueUI != null)
        {
            dialogueUI.phraseDecisionMatrix = matrixObj.GetComponent<PhraseDecisionMatrix>();
            dialogueUI.usePhraseDecisionMatrix = true;
            EditorUtility.SetDirty(dialogueUI);
            Debug.Log("[BuildSimulationUI] Connected phraseDecisionMatrix to FloatingPokeDialogueUI.");
        }

        // 8. Save Scene
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[BuildSimulationUI] Successfully built 3-Column PhraseDecisionMatrix and SummaryScreen in MainScene!");
    }

    private static GameObject CreateChipPrefab(TMP_FontAsset font)
    {
        var tempObj = new GameObject("DraggablePhraseChip");
        var rt = tempObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(250f, 38f);

        var img = tempObj.AddComponent<Image>();
        img.color = new Color(0.10f, 0.16f, 0.24f, 0.95f); // #1A2638 Dark Slate

        var outline = tempObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.32f, 0.48f, 0.85f); // #33527A Subtle Slate Border
        outline.effectDistance = new Vector2(1f, -1f);

        var cg = tempObj.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;

        var btn = tempObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;

        // Label Text child - Large, clean "Choice 1"
        var tmp = CreateTMPChild(tempObj.transform, "LabelText", "Choice 1", 13.5f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.offsetMin = new Vector2(6f, 2f);
        tmp.rectTransform.offsetMax = new Vector2(-6f, -2f);
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        var chipComp = tempObj.AddComponent<DraggablePhraseChip>();
        chipComp.choiceNumber = 1;
        chipComp.labelText = tmp;
        chipComp.chipBackgroundImage = img;
        chipComp.chipOutline = outline;

        var prefab = PrefabUtility.SaveAsPrefabAsset(tempObj, ChipPrefabPath);
        Object.DestroyImmediate(tempObj);

        Debug.Log($"[BuildSimulationUI] Saved Chip Prefab at: {ChipPrefabPath}");
        return prefab;
    }

    private static GameObject BuildPhraseDecisionMatrix(Transform parent, GameObject chipPrefab, TMP_FontAsset font)
    {
        // Canvas Main Panel
        var root = CreateUIRect("PhraseDecisionMatrix", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(root, new Color(0.06f, 0.09f, 0.14f, 0.98f)); // Deep Slate #0F1724
        AddOutline(root, new Color(0.20f, 0.28f, 0.38f, 0.75f), new Vector2(1.5f, -1.5f));

        var matrixComp = root.AddComponent<PhraseDecisionMatrix>();
        matrixComp.scenarioData = AssetDatabase.LoadAssetAtPath<PhraseFormulationScenarioSO>("Assets/ScenarioData/Scenarios/PhraseFormulationScenario.asset");
        matrixComp.chipPrefab = chipPrefab;

        // ══════════════════════════════════════════════════════════════════
        // 1. HEADER BAR (Y ~ 0.91 to 1.0)
        // ══════════════════════════════════════════════════════════════════
        var header = CreateUIRect("HeaderBar", root.transform, new Vector2(0f, 0.90f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var titleTxt = CreateTMPChild(header.transform, "Title", "Create your dialogue", 18.5f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.45f);
        titleTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleTxt.rectTransform.offsetMin = Vector2.zero;
        titleTxt.rectTransform.offsetMax = Vector2.zero;

        var subTxt = CreateTMPChild(header.transform, "Subtitle", "Hover over a choice to preview dialogue. Drag or tap to socket into your dialogue.", 10.5f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.58f, 0.68f, 0.80f), font);
        subTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
        subTxt.rectTransform.anchorMax = new Vector2(1f, 0.45f);
        subTxt.rectTransform.offsetMin = Vector2.zero;
        subTxt.rectTransform.offsetMax = Vector2.zero;

        // ══════════════════════════════════════════════════════════════════
        // 2. THREE PARALLEL CATEGORY COLUMNS (Y ~ 0.38 to 0.90)
        // ══════════════════════════════════════════════════════════════════
        var colsContainer = CreateUIRect("ColumnsContainer", root.transform, new Vector2(0.02f, 0.38f), new Vector2(0.98f, 0.90f), Vector2.zero, Vector2.zero);
        var colsHLG = colsContainer.AddComponent<HorizontalLayoutGroup>();
        colsHLG.spacing = 12f;
        colsHLG.childControlWidth = true;
        colsHLG.childControlHeight = true;
        colsHLG.childForceExpandWidth = true;
        colsHLG.childForceExpandHeight = true;

        // Column 1: Greeting
        var col1 = CreateColumn(colsContainer.transform, "Column_1_Greeting", "1. Greeting", PhraseSlotCategory.Introduction, 0.25f, font, out PhraseDropSlot slot1, out Transform bank1);
        matrixComp.introSlot = slot1;
        matrixComp.column1ChoicesContainer = bank1;

        // Column 2: Patient Identification
        var col2 = CreateColumn(colsContainer.transform, "Column_2_PatientID", "2. Patient ID", PhraseSlotCategory.PatientIdentification, 0.35f, font, out PhraseDropSlot slot2, out Transform bank2);
        matrixComp.identSlot = slot2;
        matrixComp.column2ChoicesContainer = bank2;

        // Column 3: Procedure Explanation
        var col3 = CreateColumn(colsContainer.transform, "Column_3_Procedure", "3. Procedure", PhraseSlotCategory.ProcedureExplanation, 0.40f, font, out PhraseDropSlot slot3, out Transform bank3);
        matrixComp.explainSlot = slot3;
        matrixComp.column3ChoicesContainer = bank3;

        // ══════════════════════════════════════════════════════════════════
        // 3. LIVE FORMULATED STATEMENT CARD (Y ~ 0.12 to 0.36)
        // ══════════════════════════════════════════════════════════════════
        var previewCard = CreateUIRect("LivePreviewCard", root.transform, new Vector2(0.02f, 0.13f), new Vector2(0.98f, 0.36f), Vector2.zero, Vector2.zero);
        var previewBg = AddImage(previewCard, new Color(0.07f, 0.10f, 0.15f, 0.95f)); // #111A26
        var previewOutline = AddOutline(previewCard, new Color(0.18f, 0.28f, 0.40f, 0.70f), new Vector2(1.5f, -1.5f));
        matrixComp.liveStatementCardBg = previewBg;
        matrixComp.liveStatementCardOutline = previewOutline;

        var prevLabel = CreateTMPChild(previewCard.transform, "Label", "Formulated Statement Preview:", 10.5f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.22f, 0.74f, 0.97f), font);
        prevLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.76f);
        prevLabel.rectTransform.anchorMax = new Vector2(0.98f, 0.96f);
        prevLabel.rectTransform.offsetMin = Vector2.zero;
        prevLabel.rectTransform.offsetMax = Vector2.zero;

        var prevTxt = CreateTMPChild(previewCard.transform, "StatementText", matrixComp.defaultEmptyPrompt, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        prevTxt.rectTransform.anchorMin = new Vector2(0.02f, 0.05f);
        prevTxt.rectTransform.anchorMax = new Vector2(0.98f, 0.75f);
        prevTxt.rectTransform.offsetMin = Vector2.zero;
        prevTxt.rectTransform.offsetMax = Vector2.zero;
        prevTxt.textWrappingMode = TextWrappingModes.Normal;
        matrixComp.liveStatementText = prevTxt;

        // ══════════════════════════════════════════════════════════════════
        // 4. FOOTER & CONFIRMATION BAR (Y ~ 0.01 to 0.12)
        // ══════════════════════════════════════════════════════════════════
        var evalBar = CreateUIRect("FooterBar", root.transform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.12f), Vector2.zero, Vector2.zero);
        AddImage(evalBar, new Color(0.05f, 0.08f, 0.12f, 0.60f));

        // Centered Confirm Button (Clean fixed dimensions: 200x38, zero stretch bugs)
        var confirmBtnObj = CreateUIRect("ConfirmButton", evalBar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(200f, 38f));
        var cBtnImg = AddImage(confirmBtnObj, new Color(0.08f, 0.44f, 0.68f, 0.95f)); // #1470AD Medical Cyan
        var confirmBtn = confirmBtnObj.AddComponent<Button>();
        var cBtnColors = confirmBtn.colors;
        cBtnColors.highlightedColor = new Color(0.12f, 0.55f, 0.85f, 1f);
        cBtnColors.disabledColor = new Color(0.18f, 0.22f, 0.28f, 0.8f);
        confirmBtn.colors = cBtnColors;

        var cBtnTxt = CreateTMPChild(confirmBtnObj.transform, "Text", "Speak to Mother", 13.5f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        cBtnTxt.rectTransform.anchorMin = Vector2.zero;
        cBtnTxt.rectTransform.anchorMax = Vector2.one;
        cBtnTxt.rectTransform.offsetMin = Vector2.zero;
        cBtnTxt.rectTransform.offsetMax = Vector2.zero;
        matrixComp.confirmButton = confirmBtn;

        // Feedback Banner Text (Centered above button)
        var feedTxt = CreateTMPChild(evalBar.transform, "FeedbackText", "Fill all 3 slots to speak (0/3 slots slotted).", 10.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.65f, 0.75f, 0.85f), font);
        feedTxt.rectTransform.anchorMin = new Vector2(0.02f, 0.68f);
        feedTxt.rectTransform.anchorMax = new Vector2(0.98f, 0.98f);
        feedTxt.rectTransform.offsetMin = Vector2.zero;
        feedTxt.rectTransform.offsetMax = Vector2.zero;
        matrixComp.feedbackBannerText = feedTxt;

        return root;
    }

    private static GameObject CreateColumn(Transform parent, string colName, string categoryTitle, PhraseSlotCategory cat, float weight, TMP_FontAsset font, out PhraseDropSlot slot, out Transform bank)
    {
        var colObj = CreateUIRect(colName, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(colObj, new Color(0.08f, 0.12f, 0.18f, 0.65f)); // Column background
        AddOutline(colObj, new Color(0.18f, 0.26f, 0.36f, 0.60f), new Vector2(1f, -1f));

        var colVLG = colObj.AddComponent<VerticalLayoutGroup>();
        colVLG.padding = new RectOffset(6, 6, 6, 6);
        colVLG.spacing = 8f;
        colVLG.childControlWidth = true;
        colVLG.childControlHeight = false;
        colVLG.childForceExpandWidth = true;
        colVLG.childForceExpandHeight = false;

        // 1. Column Category Header
        var catHeader = CreateUIRect("CategoryHeader", colObj.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 22f));
        var catHeaderLE = catHeader.AddComponent<LayoutElement>();
        catHeaderLE.preferredHeight = 22f;
        catHeaderLE.minHeight = 22f;
        var catHeaderTxt = CreateTMPChild(catHeader.transform, "Text", categoryTitle, 11f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.38f, 0.74f, 0.96f), font);
        catHeaderTxt.rectTransform.anchorMin = Vector2.zero;
        catHeaderTxt.rectTransform.anchorMax = Vector2.one;
        catHeaderTxt.rectTransform.offsetMin = Vector2.zero;
        catHeaderTxt.rectTransform.offsetMax = Vector2.zero;

        // 2. Drop Slot (Top Socket)
        var slotObj = CreateUIRect($"Slot_{cat}", colObj.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 44f));
        var slotLE = slotObj.AddComponent<LayoutElement>();
        slotLE.preferredHeight = 44f;
        slotLE.minHeight = 44f;

        var slotBg = AddImage(slotObj, new Color(0.06f, 0.10f, 0.16f, 0.90f));
        var slotOutline = AddOutline(slotObj, new Color(0.22f, 0.34f, 0.48f, 0.70f), new Vector2(1.5f, -1.5f));

        slot = slotObj.AddComponent<PhraseDropSlot>();
        slot.targetCategory = cat;
        slot.stageWeight = weight;
        slot.slotTitle = categoryTitle;
        slot.slotBackground = slotBg;
        slot.slotOutline = slotOutline;

        // Chip receptacle inside slot
        var chipCont = CreateUIRect("ChipContainer", slotObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        slot.chipContainer = chipCont.transform;

        // Placeholder Text
        var ph = CreateTMPChild(slotObj.transform, "PlaceholderText", $"[ Drop {categoryTitle} Here ]", 11f, FontStyles.Italic, TextAlignmentOptions.Center, new Color(0.40f, 0.52f, 0.65f, 0.85f), font);
        ph.rectTransform.anchorMin = Vector2.zero;
        ph.rectTransform.anchorMax = Vector2.one;
        ph.rectTransform.offsetMin = Vector2.zero;
        ph.rectTransform.offsetMax = Vector2.zero;
        slot.placeholderText = ph;

        // 3. Choices Bank (Home for the 3 choice chips)
        var bankObj = CreateUIRect("ChoicesBank", colObj.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 135f));
        var bankLE = bankObj.AddComponent<LayoutElement>();
        bankLE.preferredHeight = 135f;
        bankLE.minHeight = 135f;

        var bankVLG = bankObj.AddComponent<VerticalLayoutGroup>();
        bankVLG.spacing = 6f;
        bankVLG.childControlWidth = true;
        bankVLG.childControlHeight = false;
        bankVLG.childForceExpandWidth = true;
        bankVLG.childForceExpandHeight = false;

        bank = bankObj.transform;

        return colObj;
    }

    public static GameObject BuildSummaryScreen(Transform parent, TMP_FontAsset font)
    {
        var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Unity UI Samples/Textures and Sprites/Rounded UI/UIPanel.png");
        var btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Unity UI Samples/Textures and Sprites/Rounded UI/UIButtonDefault.png");
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/VRTemplateAssets/Sprites/UI/CircleMask.png");

        var root = CreateUIRect("SummaryScreen", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var rootImg = AddRoundedImage(root, new Color(0.035f, 0.055f, 0.105f, 0.985f), panelSprite);
        AddOutline(root, new Color(0.16f, 0.42f, 0.58f, 0.72f), new Vector2(1.5f, -1.5f));

        var controller = root.AddComponent<SummaryScreenController>();
        controller.rootScreenObject = root;

        var pageContainer = CreateUIRect("PageContainer", root.transform, new Vector2(0.025f, 0.165f), new Vector2(0.975f, 0.965f), Vector2.zero, Vector2.zero);

        BuildSummaryPage0Overview(pageContainer.transform, controller, panelSprite, btnSprite, circleSprite, font);
        BuildSummaryPage1Dialogue(pageContainer.transform, controller, panelSprite, font);
        BuildSummaryPage2Perineal(pageContainer.transform, controller, panelSprite, font);
        BuildSummaryPage3Draping(pageContainer.transform, controller, panelSprite, font);
        BuildSummaryPage4TablePrep(pageContainer.transform, controller, panelSprite, font);
        BuildSummaryPage5Matrix(pageContainer.transform, controller, panelSprite, btnSprite, font);
        BuildSummaryPage6Gallery(pageContainer.transform, controller, panelSprite, font);

        // Shared Disclaimer Strip between content and footer
        var disclaimer = CreateTMPChild(root.transform, "Disclaimer", EvaluationDisclaimer.DefaultText, 9.5f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.65f, 0.75f, 0.85f, 0.9f), font);
        disclaimer.rectTransform.anchorMin = new Vector2(0.03f, 0.102f);
        disclaimer.rectTransform.anchorMax = new Vector2(0.97f, 0.158f);
        disclaimer.rectTransform.offsetMin = Vector2.zero;
        disclaimer.rectTransform.offsetMax = Vector2.zero;
        disclaimer.enableAutoSizing = true;
        disclaimer.fontSizeMin = 7.5f;
        disclaimer.fontSizeMax = 9.5f;
        controller.disclaimerText = disclaimer;

        BuildSummaryFooter(root.transform, controller, btnSprite, font);
        return root;
    }

    private static void BuildSummaryPage0Overview(Transform parent, SummaryScreenController c, Sprite panelSprite, Sprite btnSprite, Sprite circleSprite, TMP_FontAsset font)
    {
        var p0 = CreateUIRect("Page_0_OverallSummary", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        c.page0Overview = p0;

        // Eyebrow + Hero Header (Y: 0.78..0.99)
        var eyebrow = CreateTMPChild(p0.transform, "Eyebrow", "SIMULATION ASSESSMENT", 10f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.54f, 0.67f, 0.79f), font);
        SetAnchors(eyebrow.rectTransform, 0.02f, 0.935f, 0.98f, 0.99f);

        var title = CreateTMPChild(p0.transform, "Title", "Final Performance", 19f, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white, font);
        SetAnchors(title.rectTransform, 0.02f, 0.87f, 0.98f, 0.94f);

        var heroScore = CreateTMPChild(p0.transform, "HeroScore", "85<size=55%><color=#7A92AD>/100</color></size>", 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.18f, 0.84f, 0.48f), font);
        SetAnchors(heroScore.rectTransform, 0.02f, 0.795f, 0.45f, 0.875f);
        c.ovHeroScoreText = heroScore;

        var pillObj = CreateUIRect("CompetencyPill", p0.transform, new Vector2(0.67f, 0.808f), new Vector2(0.98f, 0.862f), Vector2.zero, Vector2.zero);
        c.ovCompetencyPillBg = AddRoundedImage(pillObj, new Color(0.06f, 0.24f, 0.16f, 0.96f), btnSprite ?? panelSprite);
        c.ovCompetencyPillText = CreateTMPChild(pillObj.transform, "Text", "Good performance", 10.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.18f, 0.84f, 0.48f), font);
        SetAnchors(c.ovCompetencyPillText.rectTransform, 0.05f, 0.05f, 0.95f, 0.95f);

        var barTrack = CreateUIRect("ProgressTrack", p0.transform, new Vector2(0.02f, 0.770f), new Vector2(0.98f, 0.788f), Vector2.zero, Vector2.zero);
        AddRoundedImage(barTrack, new Color(0.11f, 0.16f, 0.24f, 1f), btnSprite ?? panelSprite);
        var barFillObj = CreateUIRect("ProgressFill", barTrack.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        c.ovProgressFill = AddImage(barFillObj, new Color(0.18f, 0.84f, 0.48f));
        c.ovProgressFill.type = Image.Type.Filled;
        c.ovProgressFill.fillMethod = Image.FillMethod.Horizontal;
        c.ovProgressFill.fillAmount = 0.85f;

        CreateDivider(p0.transform, 0.748f);

        // Performance Breakdown (Mandatory Exact 4 Top-Level Stages + Separated Sub-rows, Y: 0.435..0.735)
        var pbHeader = CreateTMPChild(p0.transform, "PerfBreakdownHeader", "Performance breakdown", 12.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white, font);
        SetAnchors(pbHeader.rectTransform, 0.02f, 0.695f, 0.98f, 0.742f);

        c.ovStageButtons = new Button[4];
        c.ovStagePillBgs = new Image[4];
        c.ovStagePillTexts = new TMP_Text[4];

        // 1. Dialogue
        CreateOverviewStageRow(p0.transform, 0, "1. Dialogue", 0.640f, 0.690f, btnSprite, panelSprite, font, c);
        // 2. Perineal Cleaning + ONLY 2 separated sub-rows
        CreateOverviewStageRow(p0.transform, 1, "2. Perineal Cleaning", 0.586f, 0.634f, btnSprite, panelSprite, font, c);
        c.ovPerinealWashSubText = CreateOverviewSubRow(p0.transform, "Sub_PerinealWash", "• Perineal Washing", 0.548f, 0.584f, font);
        c.ovPerinealCottonSubText = CreateOverviewSubRow(p0.transform, "Sub_9Cotton", "• 9-Cotton-Ball Cleansing Technique", 0.512f, 0.548f, font);
        // 3. Draping + 2 separated sub-rows
        CreateOverviewStageRow(p0.transform, 2, "3. Draping", 0.460f, 0.508f, btnSprite, panelSprite, font, c);
        c.ovDrapeMotherSubText = CreateOverviewSubRow(p0.transform, "Sub_DrapeMother", "• Draping the Mother", 0.422f, 0.458f, font);
        c.ovDrapeTableSubText = CreateOverviewSubRow(p0.transform, "Sub_DrapeTable", "• Draping the Table", 0.386f, 0.422f, font);
        // 4. Table Preparation
        CreateOverviewStageRow(p0.transform, 3, "4. Table Preparation", 0.334f, 0.382f, btnSprite, panelSprite, font, c);

        CreateDivider(p0.transform, 0.320f);

        // Mistake Breakdown Ledger (Y: 0.125..0.312)
        var mbHeader = CreateTMPChild(p0.transform, "MistakeBreakdownHeader", "Mistake breakdown", 12.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white, font);
        SetAnchors(mbHeader.rectTransform, 0.02f, 0.278f, 0.98f, 0.316f);

        c.ovCottonAffectedValueText = CreateLedgerRow(p0.transform, "Row_CottonAffected", "Cotton balls affected", "0", 0.244f, 0.276f, Color.white, font, out _);
        c.ovCottonDeductionValueText = CreateLedgerRow(p0.transform, "Row_CottonDeduction", "Cotton ball deduction", "0", 0.212f, 0.244f, new Color(0.95f, 0.30f, 0.30f), font, out _);

        c.ovToolRowObjects = new GameObject[4];
        c.ovToolRowLabelTexts = new TMP_Text[4];
        c.ovToolRowValueTexts = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            float yTop = 0.210f - i * 0.026f;
            float yBot = yTop - 0.025f;
            c.ovToolRowValueTexts[i] = CreateLedgerRow(p0.transform, $"Row_ToolDrop_{i}", "Dropped tools", "0", yBot, yTop, Color.white, font, out var lbl, out var rowGo);
            c.ovToolRowLabelTexts[i] = lbl;
            c.ovToolRowObjects[i] = rowGo;
            if (i > 0) rowGo.SetActive(false);
        }

        c.ovToolHandlingNoteText = CreateTMPChild(p0.transform, "ToolHandlingNote", "All instruments were handled properly.", 9.5f, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, new Color(0.55f, 0.72f, 0.65f), font);
        SetAnchors(c.ovToolHandlingNoteText.rectTransform, 0.04f, 0.160f, 0.96f, 0.184f);

        c.ovToolDeductionValueText = CreateLedgerRow(p0.transform, "Row_ToolDeduction", "Tool deduction", "0", 0.132f, 0.160f, new Color(0.95f, 0.30f, 0.30f), font, out _);
        c.ovTotalDeductionsValueText = CreateLedgerRow(p0.transform, "Row_TotalDeductions", "Total deductions", "0", 0.104f, 0.132f, new Color(0.95f, 0.30f, 0.30f), font, out var totLbl);
        totLbl.color = new Color(0.85f, 0.92f, 0.98f);

        CreateDivider(p0.transform, 0.098f);

        // CLINICAL FEEDBACK + Circular ↓ Drill-Down Button (Y: 0.00..0.095)
        var cfLabel = CreateTMPChild(p0.transform, "CF_Label", "CLINICAL FEEDBACK", 9.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.55f, 0.67f, 0.78f), font);
        SetAnchors(cfLabel.rectTransform, 0.02f, 0.072f, 0.85f, 0.096f);

        c.ovFeedbackHeadingText = CreateTMPChild(p0.transform, "CF_Heading", "Instrument handling", 12f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white, font);
        SetAnchors(c.ovFeedbackHeadingText.rectTransform, 0.02f, 0.045f, 0.85f, 0.074f);

        c.ovFeedbackBodyText = CreateTMPChild(p0.transform, "CF_Body", "Maintain sterile field control of all instruments throughout the procedure.", 10f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.72f, 0.81f, 0.90f), font);
        SetAnchors(c.ovFeedbackBodyText.rectTransform, 0.02f, 0.002f, 0.85f, 0.046f);
        c.ovFeedbackBodyText.enableAutoSizing = true;
        c.ovFeedbackBodyText.fontSizeMin = 8f;
        c.ovFeedbackBodyText.fontSizeMax = 10.5f;

        var drillObj = CreateUIRect("Button_DrillDown", p0.transform, new Vector2(0.90f, 0.015f), new Vector2(0.965f, 0.080f), Vector2.zero, Vector2.zero);
        AddRoundedImage(drillObj, new Color(0.11f, 0.24f, 0.38f, 0.95f), circleSprite ?? btnSprite);
        AddOutline(drillObj, new Color(0.28f, 0.78f, 0.96f, 0.75f), new Vector2(1f, -1f));
        c.buttonOverviewDrillDown = drillObj.AddComponent<Button>();
        var drillTxt = CreateTMPChild(drillObj.transform, "Arrow", "↓", 15f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        SetAnchors(drillTxt.rectTransform, 0f, 0f, 1f, 1f);
    }

    private static void BuildSummaryPage1Dialogue(Transform parent, SummaryScreenController c, Sprite panelSprite, TMP_FontAsset font)
    {
        var p1 = CreateUIRect("Page_1_Dialogue", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        c.page1Dialogue = p1;

        var hdr = CreateTMPChild(p1.transform, "Header", "STAGE 1: DIALOGUE & CLINICAL DECISION-MAKING", 14f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.28f, 0.82f, 0.98f), font);
        SetAnchors(hdr.rectTransform, 0.02f, 0.91f, 0.98f, 0.98f);

        var p1Rating = CreateTMPChild(p1.transform, "RatingBanner", "Communication score: 100%", 13f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white, font);
        SetAnchors(p1Rating.rectTransform, 0.02f, 0.84f, 0.98f, 0.90f);
        c.p1OverallRatingText = p1Rating;

        var p1Arch = CreateTMPChild(p1.transform, "ArchetypeTitle", "Communication summary", 11f, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, new Color(0.75f, 0.85f, 0.95f), font);
        SetAnchors(p1Arch.rectTransform, 0.02f, 0.785f, 0.98f, 0.835f);
        c.p1ArchetypeTitleText = p1Arch;

        c.p1BarProfessionalism = CreateMetricRow(p1.transform, "Row_Professionalism", 0.72f, "Professionalism & Role Decorum", out c.p1TextProfessionalism, new Color(0.22f, 0.70f, 0.95f), font);
        c.p1BarEmpathy = CreateMetricRow(p1.transform, "Row_Empathy", 0.64f, "Compassion & Bedside Manner", out c.p1TextEmpathy, new Color(0.95f, 0.60f, 0.20f), font);
        c.p1BarClarity = CreateMetricRow(p1.transform, "Row_Clarity", 0.56f, "Sensory & Procedural Preparation", out c.p1TextClarity, new Color(0.25f, 0.80f, 0.50f), font);

        var debriefBox = CreateCardPanel("DialogueDecisionsCard", p1.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.49f), panelSprite);
        var dNotes = CreateTMPChild(debriefBox.transform, "SummaryNote", "", 10.5f, FontStyles.Italic, TextAlignmentOptions.TopLeft, new Color(0.78f, 0.87f, 0.96f), font);
        SetAnchors(dNotes.rectTransform, 0.02f, 0.80f, 0.98f, 0.96f);
        c.p1DebriefNotesText = dNotes;

        var critiques = CreateTMPChild(debriefBox.transform, "DecisionEntries", "", 10.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        SetAnchors(critiques.rectTransform, 0.02f, 0.03f, 0.98f, 0.78f);
        critiques.enableAutoSizing = true;
        critiques.fontSizeMin = 8f;
        critiques.fontSizeMax = 10.5f;
        c.p1CritiquesListText = critiques;
    }

    private static void BuildSummaryPage2Perineal(Transform parent, SummaryScreenController c, Sprite panelSprite, TMP_FontAsset font)
    {
        var p2 = CreateUIRect("Page_2_Perineal", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        c.page2Perineal = p2;

        var hdr = CreateTMPChild(p2.transform, "Header", "STAGE 2: PERINEAL CLEANING (WASHING & 9-COTTON TECHNIQUE)", 14f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.28f, 0.82f, 0.98f), font);
        SetAnchors(hdr.rectTransform, 0.02f, 0.91f, 0.98f, 0.98f);

        var washCard = CreateCardPanel("Card_PerinealWashing", p2.transform, new Vector2(0.02f, 0.51f), new Vector2(0.98f, 0.88f), panelSprite);
        c.p2WashCardText = CreateTMPChild(washCard.transform, "Body", "", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        SetAnchors(c.p2WashCardText.rectTransform, 0.025f, 0.06f, 0.975f, 0.94f);
        c.p2WashCardText.enableAutoSizing = true;
        c.p2WashCardText.fontSizeMin = 9f;
        c.p2WashCardText.fontSizeMax = 11.5f;

        var cottonCard = CreateCardPanel("Card_NineBallTechnique", p2.transform, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.48f), panelSprite);
        c.p2CottonCardText = CreateTMPChild(cottonCard.transform, "Body", "", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        SetAnchors(c.p2CottonCardText.rectTransform, 0.025f, 0.06f, 0.975f, 0.94f);
        c.p2CottonCardText.enableAutoSizing = true;
        c.p2CottonCardText.fontSizeMin = 9f;
        c.p2CottonCardText.fontSizeMax = 11.5f;
    }

    private static void BuildSummaryPage3Draping(Transform parent, SummaryScreenController c, Sprite panelSprite, TMP_FontAsset font)
    {
        var p3 = CreateUIRect("Page_3_Draping", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        c.page3Draping = p3;

        var hdr = CreateTMPChild(p3.transform, "Header", "STAGE 3: STERILE DRAPING (MOTHER & MAYO TABLE)", 14f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.28f, 0.82f, 0.98f), font);
        SetAnchors(hdr.rectTransform, 0.02f, 0.91f, 0.98f, 0.98f);

        var motherCard = CreateCardPanel("Card_DrapingMother", p3.transform, new Vector2(0.02f, 0.51f), new Vector2(0.98f, 0.88f), panelSprite);
        c.p3MotherCardText = CreateTMPChild(motherCard.transform, "Body", "", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        SetAnchors(c.p3MotherCardText.rectTransform, 0.025f, 0.06f, 0.975f, 0.94f);
        c.p3MotherCardText.enableAutoSizing = true;
        c.p3MotherCardText.fontSizeMin = 9f;
        c.p3MotherCardText.fontSizeMax = 11.5f;

        var tableCard = CreateCardPanel("Card_DrapingTable", p3.transform, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.48f), panelSprite);
        c.p3TableCardText = CreateTMPChild(tableCard.transform, "Body", "", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        SetAnchors(c.p3TableCardText.rectTransform, 0.025f, 0.06f, 0.975f, 0.94f);
        c.p3TableCardText.enableAutoSizing = true;
        c.p3TableCardText.fontSizeMin = 9f;
        c.p3TableCardText.fontSizeMax = 11.5f;
    }

    private static void BuildSummaryPage4TablePrep(Transform parent, SummaryScreenController c, Sprite panelSprite, TMP_FontAsset font)
    {
        var p4 = CreateUIRect("Page_4_TableAndFinal", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        c.page4TableAndFinal = p4;

        var hdr = CreateTMPChild(p4.transform, "Header", "STAGE 4: TABLE PREPARATION & INSTRUMENT HANDLING", 14f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.28f, 0.82f, 0.98f), font);
        SetAnchors(hdr.rectTransform, 0.02f, 0.91f, 0.98f, 0.98f);

        var scoreTxt = CreateTMPChild(p4.transform, "ScoreBanner", "Recorded score: 100 / 100", 16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.18f, 0.84f, 0.48f), font);
        SetAnchors(scoreTxt.rectTransform, 0.02f, 0.84f, 0.98f, 0.90f);
        c.p4FinalScoreText = scoreTxt;

        var placeCard = CreateCardPanel("Card_Placements", p4.transform, new Vector2(0.02f, 0.46f), new Vector2(0.98f, 0.82f), panelSprite);
        c.p4PlacementsCardText = CreateTMPChild(placeCard.transform, "Body", "", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        SetAnchors(c.p4PlacementsCardText.rectTransform, 0.025f, 0.06f, 0.975f, 0.94f);
        c.p4PlacementsCardText.enableAutoSizing = true;
        c.p4PlacementsCardText.fontSizeMin = 9f;
        c.p4PlacementsCardText.fontSizeMax = 11.5f;

        var dropsCard = CreateCardPanel("Card_InstrumentDrops", p4.transform, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.43f), panelSprite);
        c.p4DropsCardText = CreateTMPChild(dropsCard.transform, "Body", "", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        SetAnchors(c.p4DropsCardText.rectTransform, 0.025f, 0.06f, 0.975f, 0.94f);
        c.p4DropsCardText.enableAutoSizing = true;
        c.p4DropsCardText.fontSizeMin = 9f;
        c.p4DropsCardText.fontSizeMax = 11.5f;
    }

    private static void BuildSummaryPage5Matrix(Transform parent, SummaryScreenController c, Sprite panelSprite, Sprite btnSprite, TMP_FontAsset font)
    {
        var p5 = CreateUIRect("Page_5_DecisionMatrix", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        c.page5Matrix = p5;

        var hdr = CreateTMPChild(p5.transform, "Header", "DECISION MATRIX EVALUATION (WSM)", 14f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.28f, 0.82f, 0.98f), font);
        SetAnchors(hdr.rectTransform, 0.02f, 0.91f, 0.98f, 0.98f);

        c.p5ScoresText = CreateTMPChild(p5.transform, "MatrixScores", "Simulation score: 100 / 100   Decision matrix: 100.0 / 100", 11f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Color.white, font);
        SetAnchors(c.p5ScoresText.rectTransform, 0.02f, 0.82f, 0.98f, 0.90f);

        var gridObj = CreateCardPanel("MatrixGridContainer", p5.transform, new Vector2(0.02f, 0.16f), new Vector2(0.98f, 0.80f), panelSprite);
        c.p5GridContainer = gridObj.GetComponent<RectTransform>();

        c.p5SaveStatusText = CreateTMPChild(p5.transform, "SaveStatus", "Save this evaluation (report, CSV, screenshots)?", 10.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.85f, 0.92f, 0.98f), font);
        SetAnchors(c.p5SaveStatusText.rectTransform, 0.02f, 0.02f, 0.56f, 0.13f);

        c.p5SaveButton = CreateSimpleButton("Button_SaveEvaluation", p5.transform, "Save Evaluation", new Vector2(0.58f, 0.02f), new Vector2(0.77f, 0.13f), new Color(0.14f, 0.52f, 0.34f, 0.95f), btnSprite, font);
        c.p5DontSaveButton = CreateSimpleButton("Button_DontSave", p5.transform, "Don't Save", new Vector2(0.79f, 0.02f), new Vector2(0.98f, 0.13f), new Color(0.45f, 0.18f, 0.18f, 0.95f), btnSprite, font);
        p5.SetActive(false);
    }

    private static void BuildSummaryPage6Gallery(Transform parent, SummaryScreenController c, Sprite panelSprite, TMP_FontAsset font)
    {
        var p6 = CreateUIRect("Page_6_MistakeGallery", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        c.page6Gallery = p6;

        c.p6HeaderText = CreateTMPChild(p6.transform, "GalleryHeader", "MISTAKE GALLERY", 14f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.28f, 0.82f, 0.98f), font);
        SetAnchors(c.p6HeaderText.rectTransform, 0.02f, 0.91f, 0.98f, 0.98f);

        c.p6Cards = new GameObject[2];
        c.p6Thumbnails = new RawImage[2];
        c.p6CardTexts = new TMP_Text[2];

        for (int i = 0; i < 2; i++)
        {
            float yMax = i == 0 ? 0.88f : 0.44f;
            float yMin = i == 0 ? 0.47f : 0.03f;
            var card = CreateCardPanel($"GalleryCard_{i}", p6.transform, new Vector2(0.02f, yMin), new Vector2(0.98f, yMax), panelSprite);
            c.p6Cards[i] = card;

            var thumbGo = CreateUIRect("Thumbnail", card.transform, new Vector2(0.02f, 0.08f), new Vector2(0.32f, 0.92f), Vector2.zero, Vector2.zero);
            c.p6Thumbnails[i] = thumbGo.AddComponent<RawImage>();
            c.p6Thumbnails[i].color = new Color(0.15f, 0.20f, 0.28f, 1f);

            c.p6CardTexts[i] = CreateTMPChild(card.transform, "CardText", "", 11f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
            SetAnchors(c.p6CardTexts[i].rectTransform, 0.34f, 0.08f, 0.98f, 0.92f);
            c.p6CardTexts[i].enableAutoSizing = true;
            c.p6CardTexts[i].fontSizeMin = 8.5f;
            c.p6CardTexts[i].fontSizeMax = 11f;
        }
        p6.SetActive(false);
    }

    private static void BuildSummaryFooter(Transform root, SummaryScreenController c, Sprite btnSprite, TMP_FontAsset font)
    {
        var footer = CreateUIRect("FooterNavBar", root, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.098f), Vector2.zero, Vector2.zero);
        AddRoundedImage(footer, new Color(0.07f, 0.11f, 0.17f, 0.96f), btnSprite);

        c.buttonBack = CreateSimpleButton("Button_Back", footer.transform, "◀ Prev", new Vector2(0.012f, 0.14f), new Vector2(0.145f, 0.86f), new Color(0.16f, 0.25f, 0.35f, 0.95f), btnSprite, font);
        c.buttonOverview = CreateSimpleButton("Button_Overview", footer.transform, "Overview", new Vector2(0.155f, 0.14f), new Vector2(0.285f, 0.86f), new Color(0.14f, 0.32f, 0.44f, 0.95f), btnSprite, font);

        c.textPageIndicator = CreateTMPChild(footer.transform, "Text_PageIndicator", "1 / 6  •  Overall Simulation Assessment", 11f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.85f, 0.92f, 0.98f), font);
        SetAnchors(c.textPageIndicator.rectTransform, 0.295f, 0.10f, 0.675f, 0.90f);
        c.textPageIndicator.enableAutoSizing = true;
        c.textPageIndicator.fontSizeMin = 8.5f;
        c.textPageIndicator.fontSizeMax = 11.5f;

        c.buttonNext = CreateSimpleButton("Button_Next", footer.transform, "Next ▶", new Vector2(0.685f, 0.14f), new Vector2(0.825f, 0.86f), new Color(0.14f, 0.52f, 0.82f, 0.95f), btnSprite, font);
        c.buttonRestart = CreateSimpleButton("Button_Restart", footer.transform, "🔄 Restart", new Vector2(0.685f, 0.14f), new Vector2(0.825f, 0.86f), new Color(0.18f, 0.68f, 0.45f, 0.95f), btnSprite, font);
        c.buttonRestart.gameObject.SetActive(false);

        c.buttonMainMenu = CreateSimpleButton("Button_MainMenu", footer.transform, "⌂ Main Menu", new Vector2(0.835f, 0.14f), new Vector2(0.988f, 0.86f), new Color(0.14f, 0.22f, 0.34f, 0.95f), btnSprite, font);
    }

    private static void CreateOverviewStageRow(Transform parent, int idx, string title, float yMin, float yMax, Sprite btnSprite, Sprite panelSprite, TMP_FontAsset font, SummaryScreenController c)
    {
        var row = CreateUIRect($"StageRow_{idx}", parent, new Vector2(0.02f, yMin), new Vector2(0.98f, yMax), Vector2.zero, Vector2.zero);
        AddRoundedImage(row, new Color(0.065f, 0.10f, 0.165f, 0.82f), panelSprite);
        c.ovStageButtons[idx] = row.AddComponent<Button>();

        var lbl = CreateTMPChild(row.transform, "Label", title, 11.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.93f, 0.96f, 0.99f), font);
        SetAnchors(lbl.rectTransform, 0.025f, 0f, 0.68f, 1f);

        var pillObj = CreateUIRect("Pill", row.transform, new Vector2(0.71f, 0.16f), new Vector2(0.98f, 0.84f), Vector2.zero, Vector2.zero);
        c.ovStagePillBgs[idx] = AddRoundedImage(pillObj, new Color(0.06f, 0.24f, 0.16f, 0.96f), btnSprite ?? panelSprite);
        c.ovStagePillTexts[idx] = CreateTMPChild(pillObj.transform, "Text", "Passed", 9.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.18f, 0.84f, 0.48f), font);
        SetAnchors(c.ovStagePillTexts[idx].rectTransform, 0.04f, 0.04f, 0.96f, 0.96f);
    }

    private static TMP_Text CreateOverviewSubRow(Transform parent, string name, string label, float yMin, float yMax, TMP_FontAsset font)
    {
        var row = CreateUIRect(name, parent, new Vector2(0.055f, yMin), new Vector2(0.97f, yMax), Vector2.zero, Vector2.zero);
        var left = CreateTMPChild(row.transform, "Label", label, 10f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Color(0.74f, 0.83f, 0.92f), font);
        SetAnchors(left.rectTransform, 0f, 0f, 0.64f, 1f);

        var right = CreateTMPChild(row.transform, "Status", "<color=#34D399>Passed</color>", 10f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, Color.white, font);
        SetAnchors(right.rectTransform, 0.65f, 0f, 1f, 1f);
        return right;
    }

    private static TMP_Text CreateLedgerRow(Transform parent, string name, string labelText, string valText, float yMin, float yMax, Color valColor, TMP_FontAsset font, out TMP_Text leftOut)
    {
        return CreateLedgerRow(parent, name, labelText, valText, yMin, yMax, valColor, font, out leftOut, out _);
    }

    private static TMP_Text CreateLedgerRow(Transform parent, string name, string labelText, string valText, float yMin, float yMax, Color valColor, TMP_FontAsset font, out TMP_Text leftOut, out GameObject rowGo)
    {
        rowGo = CreateUIRect(name, parent, new Vector2(0.02f, yMin), new Vector2(0.98f, yMax), Vector2.zero, Vector2.zero);
        leftOut = CreateTMPChild(rowGo.transform, "Label", labelText, 10.5f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Color(0.84f, 0.90f, 0.96f), font);
        SetAnchors(leftOut.rectTransform, 0f, 0f, 0.78f, 1f);

        var right = CreateTMPChild(rowGo.transform, "Value", valText, 11f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, valColor, font);
        SetAnchors(right.rectTransform, 0.78f, 0f, 1f, 1f);
        return right;
    }

    private static GameObject CreateCardPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Sprite panelSprite)
    {
        var box = CreateUIRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        AddRoundedImage(box, new Color(0.065f, 0.105f, 0.17f, 0.90f), panelSprite);
        AddOutline(box, new Color(0.20f, 0.42f, 0.58f, 0.48f), new Vector2(1f, -1f));
        return box;
    }

    private static Button CreateSimpleButton(string name, Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Sprite btnSprite, TMP_FontAsset font)
    {
        var btnObj = CreateUIRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        AddRoundedImage(btnObj, bgColor, btnSprite);
        AddOutline(btnObj, new Color(0.32f, 0.72f, 0.90f, 0.45f), new Vector2(1f, -1f));
        var btn = btnObj.AddComponent<Button>();
        var txt = CreateTMPChild(btnObj.transform, "Text", text, 11f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        SetAnchors(txt.rectTransform, 0.04f, 0.04f, 0.96f, 0.96f);
        return btn;
    }

    private static Image AddRoundedImage(GameObject go, Color color, Sprite sprite)
    {
        var img = AddImage(go, color);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
        }
        return img;
    }

    private static void CreateDivider(Transform parent, float yAnchor)
    {
        var div = CreateUIRect("Divider", parent, new Vector2(0.02f, yAnchor), new Vector2(0.98f, yAnchor + 0.002f), Vector2.zero, Vector2.zero);
        AddImage(div, new Color(0.18f, 0.27f, 0.38f, 0.85f)).raycastTarget = false;
    }

    private static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image CreateMetricRow(Transform parent, string name, float yAnchorCenter, string label, out TMP_Text valText, Color barColor, TMP_FontAsset font)
    {
        var row = CreateUIRect(name, parent, new Vector2(0.02f, yAnchorCenter - 0.05f), new Vector2(0.98f, yAnchorCenter + 0.05f), Vector2.zero, Vector2.zero);

        var lbl = CreateTMPChild(row.transform, "Label", label, 11f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.85f, 0.90f, 0.95f), font);
        lbl.rectTransform.anchorMin = new Vector2(0f, 0f);
        lbl.rectTransform.anchorMax = new Vector2(0.35f, 1f);
        lbl.rectTransform.offsetMin = Vector2.zero;
        lbl.rectTransform.offsetMax = Vector2.zero;

        var bg = CreateUIRect("BarBG", row.transform, new Vector2(0.36f, 0.20f), new Vector2(0.72f, 0.80f), Vector2.zero, Vector2.zero);
        AddImage(bg, new Color(0.15f, 0.20f, 0.28f, 1f));

        var fill = CreateUIRect("BarFill", bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fillImg = AddImage(fill, barColor);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;

        var val = CreateTMPChild(row.transform, "Value", "5.0 / 5.0 (100%)", 11f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white, font);
        val.rectTransform.anchorMin = new Vector2(0.74f, 0f);
        val.rectTransform.anchorMax = new Vector2(1f, 1f);
        val.rectTransform.offsetMin = Vector2.zero;
        val.rectTransform.offsetMax = Vector2.zero;
        valText = val;

        return fillImg;
    }

    private static GameObject CreateUIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return go;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Outline AddOutline(GameObject go, Color color, Vector2 dist)
    {
        var ol = go.AddComponent<Outline>();
        ol.effectColor = color;
        ol.effectDistance = dist;
        return ol;
    }

    private static TextMeshProUGUI CreateTMPChild(Transform parent, string name, string text, float size, FontStyles style, TextAlignmentOptions align, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        return tmp;
    }
}
