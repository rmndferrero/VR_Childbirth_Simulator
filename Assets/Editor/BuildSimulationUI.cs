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

        // Load Default TMP Font
        var tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

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
            EditorUtility.SetDirty(dialogueUI);
            Debug.Log("[BuildSimulationUI] Connected phraseDecisionMatrix to FloatingPokeDialogueUI.");
        }

        // 8. Save Scene
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[BuildSimulationUI] Successfully built SummaryScreen and PhraseDecisionMatrix in MainScene!");
    }

    private static GameObject CreateChipPrefab(TMP_FontAsset font)
    {
        var tempObj = new GameObject("DraggablePhraseChip");
        var rt = tempObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(215f, 32f);

        var img = tempObj.AddComponent<Image>();
        img.color = new Color(0.20f, 0.60f, 0.90f, 0.95f);

        var outline = tempObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0.9f, 1f, 0.5f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var cg = tempObj.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;

        var btn = tempObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;

        // Label Text child
        var tmp = CreateTMPChild(tempObj.transform, "LabelText", "Phrase Chip", 11.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white, font);
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.offsetMin = new Vector2(8f, 2f);
        tmp.rectTransform.offsetMax = new Vector2(-8f, -2f);
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        var chipComp = tempObj.AddComponent<DraggablePhraseChip>();
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
        var root = CreateUIRect("PhraseDecisionMatrix", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(root, new Color(0.06f, 0.10f, 0.16f, 0.97f));
        AddOutline(root, new Color(0.18f, 0.55f, 0.78f, 0.75f), new Vector2(2f, -2f));

        var matrixComp = root.AddComponent<PhraseDecisionMatrix>();
        matrixComp.scenarioData = AssetDatabase.LoadAssetAtPath<PhraseFormulationScenarioSO>("Assets/ScenarioData/Scenarios/PhraseFormulationScenario.asset");
        matrixComp.chipPrefab = chipPrefab;

        // Header
        var header = CreateUIRect("HeaderBar", root.transform, new Vector2(0f, 0.88f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var titleTxt = CreateTMPChild(header.transform, "Title", "PATIENT INFORMED CONSENT & COMMUNICATION", 16f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.25f, 0.78f, 0.95f), font);
        titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.45f);
        titleTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleTxt.rectTransform.offsetMin = Vector2.zero;
        titleTxt.rectTransform.offsetMax = Vector2.zero;

        var subTxt = CreateTMPChild(header.transform, "Subtitle", "Arrange 1 phrase into each slot to formulate your verbal statement to the mother:", 11f, FontStyles.Italic, TextAlignmentOptions.Center, new Color(0.70f, 0.80f, 0.90f), font);
        subTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
        subTxt.rectTransform.anchorMax = new Vector2(1f, 0.45f);
        subTxt.rectTransform.offsetMin = Vector2.zero;
        subTxt.rectTransform.offsetMax = Vector2.zero;

        // Slots Container (Y ~ 0.67 to 0.87)
        var slotsCont = CreateUIRect("SlotsContainer", root.transform, new Vector2(0.02f, 0.67f), new Vector2(0.98f, 0.87f), Vector2.zero, Vector2.zero);
        var hlg = slotsCont.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        matrixComp.introSlot = CreateDropSlot(slotsCont.transform, "Slot_1_Intro", PhraseSlotCategory.Introduction, 0.25f, "1. Introduction (25%)", new Color(0.12f, 0.28f, 0.45f, 0.8f), font);
        matrixComp.identSlot = CreateDropSlot(slotsCont.transform, "Slot_2_Ident", PhraseSlotCategory.PatientIdentification, 0.35f, "2. Patient ID (35%)", new Color(0.40f, 0.28f, 0.12f, 0.8f), font);
        matrixComp.explainSlot = CreateDropSlot(slotsCont.transform, "Slot_3_Explain", PhraseSlotCategory.ProcedureExplanation, 0.40f, "3. Procedure Explanation (40%)", new Color(0.12f, 0.36f, 0.25f, 0.8f), font);

        // Live Statement Preview Card (Y ~ 0.50 to 0.65)
        var previewCard = CreateUIRect("LivePreviewCard", root.transform, new Vector2(0.02f, 0.50f), new Vector2(0.98f, 0.65f), Vector2.zero, Vector2.zero);
        AddImage(previewCard, new Color(0.10f, 0.16f, 0.24f, 0.9f));
        AddOutline(previewCard, new Color(0.25f, 0.40f, 0.55f, 0.5f), new Vector2(1f, -1f));

        var prevLabel = CreateTMPChild(previewCard.transform, "Label", "Formulated Statement Preview:", 10.5f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.55f, 0.70f, 0.85f), font);
        prevLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.65f);
        prevLabel.rectTransform.anchorMax = new Vector2(0.98f, 0.95f);
        prevLabel.rectTransform.offsetMin = Vector2.zero;
        prevLabel.rectTransform.offsetMax = Vector2.zero;

        var prevTxt = CreateTMPChild(previewCard.transform, "StatementText", matrixComp.defaultEmptyPrompt, 11.5f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Color.white, font);
        prevTxt.rectTransform.anchorMin = new Vector2(0.02f, 0.05f);
        prevTxt.rectTransform.anchorMax = new Vector2(0.98f, 0.65f);
        prevTxt.rectTransform.offsetMin = Vector2.zero;
        prevTxt.rectTransform.offsetMax = Vector2.zero;
        matrixComp.liveStatementText = prevTxt;

        // Token Pool Header
        var poolHeader = CreateUIRect("PoolHeader", root.transform, new Vector2(0.02f, 0.44f), new Vector2(0.98f, 0.49f), Vector2.zero, Vector2.zero);
        var pHeaderTxt = CreateTMPChild(poolHeader.transform, "Text", "Available Phrase Tokens (Tap or Drag into matching slot above):", 11f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.85f, 0.90f, 0.95f), font);
        pHeaderTxt.rectTransform.anchorMin = Vector2.zero;
        pHeaderTxt.rectTransform.anchorMax = Vector2.one;
        pHeaderTxt.rectTransform.offsetMin = Vector2.zero;
        pHeaderTxt.rectTransform.offsetMax = Vector2.zero;

        // Token Pool Container (Y ~ 0.13 to 0.44)
        var poolCont = CreateUIRect("TokenPoolContainer", root.transform, new Vector2(0.02f, 0.13f), new Vector2(0.98f, 0.44f), Vector2.zero, Vector2.zero);
        AddImage(poolCont, new Color(0.04f, 0.08f, 0.12f, 0.7f));
        var glg = poolCont.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(215f, 32f);
        glg.spacing = new Vector2(10f, 6f);
        glg.padding = new RectOffset(6, 6, 6, 6);
        matrixComp.chipPoolContainer = poolCont.transform;

        // Evaluation & Feedback Bar (Y ~ 0.01 to 0.12)
        var evalBar = CreateUIRect("EvaluationBar", root.transform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.12f), Vector2.zero, Vector2.zero);
        AddImage(evalBar, new Color(0.08f, 0.13f, 0.20f, 0.95f));

        // Confirm Button
        var confirmBtnObj = CreateUIRect("ConfirmButton", evalBar.transform, new Vector2(0.01f, 0.10f), new Vector2(0.32f, 0.90f), Vector2.zero, Vector2.zero);
        AddImage(confirmBtnObj, new Color(0.15f, 0.55f, 0.85f, 0.95f));
        var confirmBtn = confirmBtnObj.AddComponent<Button>();
        var cBtnTxt = CreateTMPChild(confirmBtnObj.transform, "Text", "Speak to Mother", 13.5f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        cBtnTxt.rectTransform.anchorMin = Vector2.zero;
        cBtnTxt.rectTransform.anchorMax = Vector2.one;
        cBtnTxt.rectTransform.offsetMin = Vector2.zero;
        cBtnTxt.rectTransform.offsetMax = Vector2.zero;
        matrixComp.confirmButton = confirmBtn;

        // Score Gauge Fill & Text
        var gaugeBg = CreateUIRect("GaugeBackground", evalBar.transform, new Vector2(0.35f, 0.25f), new Vector2(0.55f, 0.75f), Vector2.zero, Vector2.zero);
        AddImage(gaugeBg, new Color(0.18f, 0.24f, 0.32f, 1f));

        var gaugeFill = CreateUIRect("GaugeFill", gaugeBg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fillImg = AddImage(gaugeFill, new Color(0.20f, 0.83f, 0.60f, 1f));
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;
        matrixComp.evaluationGaugeFill = fillImg;

        var scoreTxt = CreateTMPChild(gaugeBg.transform, "ScoreText", "0%", 12f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        scoreTxt.rectTransform.anchorMin = Vector2.zero;
        scoreTxt.rectTransform.anchorMax = Vector2.one;
        scoreTxt.rectTransform.offsetMin = Vector2.zero;
        scoreTxt.rectTransform.offsetMax = Vector2.zero;
        matrixComp.scorePercentText = scoreTxt;

        // Feedback Banner Text
        var feedTxt = CreateTMPChild(evalBar.transform, "FeedbackText", "Fill all 3 slots to speak.", 11f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.85f, 0.92f, 0.98f), font);
        feedTxt.rectTransform.anchorMin = new Vector2(0.57f, 0.05f);
        feedTxt.rectTransform.anchorMax = new Vector2(0.99f, 0.95f);
        feedTxt.rectTransform.offsetMin = Vector2.zero;
        feedTxt.rectTransform.offsetMax = Vector2.zero;
        matrixComp.feedbackBannerText = feedTxt;

        return root;
    }

    private static PhraseDropSlot CreateDropSlot(Transform parent, string name, PhraseSlotCategory cat, float weight, string title, Color bgColor, TMP_FontAsset font)
    {
        var slotObj = CreateUIRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bg = AddImage(slotObj, bgColor);
        var outline = AddOutline(slotObj, new Color(0.35f, 0.45f, 0.55f, 0.6f), new Vector2(1.5f, -1.5f));

        var dropSlot = slotObj.AddComponent<PhraseDropSlot>();
        dropSlot.targetCategory = cat;
        dropSlot.stageWeight = weight;
        dropSlot.slotTitle = title;
        dropSlot.slotBackground = bg;
        dropSlot.slotOutline = outline;

        // Header Label
        var headerObj = CreateUIRect("Header", slotObj.transform, new Vector2(0f, 0.70f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var hTxt = CreateTMPChild(headerObj.transform, "Text", title, 10.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.85f, 0.92f, 1f), font);
        hTxt.rectTransform.anchorMin = Vector2.zero;
        hTxt.rectTransform.anchorMax = Vector2.one;
        hTxt.rectTransform.offsetMin = Vector2.zero;
        hTxt.rectTransform.offsetMax = Vector2.zero;
        dropSlot.slotHeaderLabel = hTxt;

        // Chip Container
        var chipCont = CreateUIRect("ChipContainer", slotObj.transform, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.68f), Vector2.zero, Vector2.zero);
        dropSlot.chipContainer = chipCont.transform;

        // Placeholder Text
        var ph = CreateTMPChild(chipCont.transform, "PlaceholderText", $"Drop {cat} Here", 10.5f, FontStyles.Italic, TextAlignmentOptions.Center, new Color(0.6f, 0.7f, 0.8f, 0.7f), font);
        ph.rectTransform.anchorMin = Vector2.zero;
        ph.rectTransform.anchorMax = Vector2.one;
        ph.rectTransform.offsetMin = Vector2.zero;
        ph.rectTransform.offsetMax = Vector2.zero;
        dropSlot.placeholderText = ph;

        return dropSlot;
    }

    private static GameObject BuildSummaryScreen(Transform parent, TMP_FontAsset font)
    {
        var root = CreateUIRect("SummaryScreen", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(root, new Color(0.05f, 0.08f, 0.14f, 0.98f));
        AddOutline(root, new Color(0.22f, 0.65f, 0.85f, 0.85f), new Vector2(2f, -2f));

        var controller = root.AddComponent<SummaryScreenController>();
        controller.rootScreenObject = root;

        // Header Bar
        var header = CreateUIRect("HeaderBar", root.transform, new Vector2(0f, 0.88f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var hTitle = CreateTMPChild(header.transform, "Title", "CLINICAL EVALUATION & PROTOCOL DEBRIEF", 17f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.28f, 0.80f, 0.98f), font);
        hTitle.rectTransform.anchorMin = Vector2.zero;
        hTitle.rectTransform.anchorMax = Vector2.one;
        hTitle.rectTransform.offsetMin = Vector2.zero;
        hTitle.rectTransform.offsetMax = Vector2.zero;

        // Page Container (Y ~ 0.14 to 0.88)
        var pageContainer = CreateUIRect("PageContainer", root.transform, new Vector2(0.02f, 0.14f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);

        // ── PAGE 1: DIALOGUE & COMMUNICATION DEBRIEF ──
        var p1 = CreateUIRect("Page_1_Dialogue", pageContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        controller.page1Dialogue = p1;

        var p1Rating = CreateTMPChild(p1.transform, "RatingBanner", "PATIENT COMMUNICATION: OPTIMAL (100%)", 14.5f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.20f, 0.85f, 0.55f), font);
        p1Rating.rectTransform.anchorMin = new Vector2(0.02f, 0.88f);
        p1Rating.rectTransform.anchorMax = new Vector2(0.98f, 0.99f);
        p1Rating.rectTransform.offsetMin = Vector2.zero;
        p1Rating.rectTransform.offsetMax = Vector2.zero;
        controller.p1OverallRatingText = p1Rating;

        var p1Arch = CreateTMPChild(p1.transform, "ArchetypeTitle", "Clinical Profile: Exemplary Patient-Centered Communicator", 12f, FontStyles.Italic, TextAlignmentOptions.TopLeft, new Color(0.75f, 0.85f, 0.95f), font);
        p1Arch.rectTransform.anchorMin = new Vector2(0.02f, 0.79f);
        p1Arch.rectTransform.anchorMax = new Vector2(0.98f, 0.88f);
        p1Arch.rectTransform.offsetMin = Vector2.zero;
        p1Arch.rectTransform.offsetMax = Vector2.zero;
        controller.p1ArchetypeTitleText = p1Arch;

        // 3 Bars
        controller.p1BarProfessionalism = CreateMetricRow(p1.transform, "Row_Professionalism", 0.68f, "Professionalism & Role Decorum", out controller.p1TextProfessionalism, new Color(0.22f, 0.70f, 0.95f), font);
        controller.p1BarEmpathy = CreateMetricRow(p1.transform, "Row_Empathy", 0.56f, "Compassion & Bedside Manner", out controller.p1TextEmpathy, new Color(0.95f, 0.60f, 0.20f), font);
        controller.p1BarClarity = CreateMetricRow(p1.transform, "Row_Clarity", 0.44f, "Sensory & Procedural Preparation", out controller.p1TextClarity, new Color(0.25f, 0.80f, 0.50f), font);

        // Debrief Notes Box (Y ~ 0.02 to 0.40)
        var debriefBox = CreateUIRect("DebriefNotesBox", p1.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.40f), Vector2.zero, Vector2.zero);
        AddImage(debriefBox, new Color(0.08f, 0.12f, 0.18f, 0.9f));
        AddOutline(debriefBox, new Color(0.22f, 0.35f, 0.50f, 0.5f), new Vector2(1f, -1f));

        var debriefLabel = CreateTMPChild(debriefBox.transform, "Label", "Preceptor Clinical Debrief Notes:", 11.5f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.40f, 0.80f, 0.98f), font);
        debriefLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.76f);
        debriefLabel.rectTransform.anchorMax = new Vector2(0.98f, 0.96f);
        debriefLabel.rectTransform.offsetMin = Vector2.zero;
        debriefLabel.rectTransform.offsetMax = Vector2.zero;

        var debriefNotes = CreateTMPChild(debriefBox.transform, "Notes", "• Exemplary patient communication...", 11f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        debriefNotes.rectTransform.anchorMin = new Vector2(0.02f, 0.36f);
        debriefNotes.rectTransform.anchorMax = new Vector2(0.98f, 0.76f);
        debriefNotes.rectTransform.offsetMin = Vector2.zero;
        debriefNotes.rectTransform.offsetMax = Vector2.zero;
        controller.p1DebriefNotesText = debriefNotes;

        var critiques = CreateTMPChild(debriefBox.transform, "Critiques", "", 10.5f, FontStyles.Italic, TextAlignmentOptions.TopLeft, new Color(0.90f, 0.75f, 0.40f), font);
        critiques.rectTransform.anchorMin = new Vector2(0.02f, 0.04f);
        critiques.rectTransform.anchorMax = new Vector2(0.98f, 0.36f);
        critiques.rectTransform.offsetMin = Vector2.zero;
        critiques.rectTransform.offsetMax = Vector2.zero;
        controller.p1CritiquesListText = critiques;

        // ── PAGE 2: PERINEAL CARE & 9-BALL SCRUB ──
        var p2 = CreateUIRect("Page_2_Perineal", pageContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        controller.page2Perineal = p2;

        var p2Title = CreateTMPChild(p2.transform, "Title", "PERINEAL CARE & 9-BALL ANTISEPTIC SCRUB PROTOCOL", 15f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.28f, 0.80f, 0.98f), font);
        p2Title.rectTransform.anchorMin = new Vector2(0.02f, 0.88f);
        p2Title.rectTransform.anchorMax = new Vector2(0.98f, 0.98f);
        p2Title.rectTransform.offsetMin = Vector2.zero;
        p2Title.rectTransform.offsetMax = Vector2.zero;

        var p2Status = CreateTMPChild(p2.transform, "Status", "Step 2: Wash (100%)\nStep 3: 7.5% Iodine 9-Ball Technique\nStep 4: Rinse (100%)\nStep 5: 10% Iodine Surgical Paint", 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        p2Status.rectTransform.anchorMin = new Vector2(0.02f, 0.40f);
        p2Status.rectTransform.anchorMax = new Vector2(0.98f, 0.86f);
        p2Status.rectTransform.offsetMin = Vector2.zero;
        p2Status.rectTransform.offsetMax = Vector2.zero;
        controller.p2StatusSummaryText = p2Status;

        var p2Mistakes = CreateTMPChild(p2.transform, "Mistakes", "Mistakes: None", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.95f, 0.45f, 0.45f), font);
        p2Mistakes.rectTransform.anchorMin = new Vector2(0.02f, 0.04f);
        p2Mistakes.rectTransform.anchorMax = new Vector2(0.98f, 0.38f);
        p2Mistakes.rectTransform.offsetMin = Vector2.zero;
        p2Mistakes.rectTransform.offsetMax = Vector2.zero;
        controller.p2MistakesListText = p2Mistakes;

        // ── PAGE 3: STERILE DRAPING PROTOCOL ──
        var p3 = CreateUIRect("Page_3_Draping", pageContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        controller.page3Draping = p3;

        var p3Title = CreateTMPChild(p3.transform, "Title", "STERILE DRAPING PROTOCOL (4 ANATOMICAL ZONES)", 15f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.28f, 0.80f, 0.98f), font);
        p3Title.rectTransform.anchorMin = new Vector2(0.02f, 0.88f);
        p3Title.rectTransform.anchorMax = new Vector2(0.98f, 0.98f);
        p3Title.rectTransform.offsetMin = Vector2.zero;
        p3Title.rectTransform.offsetMax = Vector2.zero;

        var p3Status = CreateTMPChild(p3.transform, "Status", "• Two-Handed Unfolding Technique: Compliant\n• Zone 1: Under Buttocks\n• Zone 2: Abdomen\n• Zone 3: Left Thigh\n• Zone 4: Right Thigh", 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white, font);
        p3Status.rectTransform.anchorMin = new Vector2(0.02f, 0.40f);
        p3Status.rectTransform.anchorMax = new Vector2(0.98f, 0.86f);
        p3Status.rectTransform.offsetMin = Vector2.zero;
        p3Status.rectTransform.offsetMax = Vector2.zero;
        controller.p3StatusSummaryText = p3Status;

        var p3Mistakes = CreateTMPChild(p3.transform, "Mistakes", "Draping Violations: None", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.95f, 0.45f, 0.45f), font);
        p3Mistakes.rectTransform.anchorMin = new Vector2(0.02f, 0.04f);
        p3Mistakes.rectTransform.anchorMax = new Vector2(0.98f, 0.38f);
        p3Mistakes.rectTransform.offsetMin = Vector2.zero;
        p3Mistakes.rectTransform.offsetMax = Vector2.zero;
        controller.p3MistakesListText = p3Mistakes;

        // ── PAGE 4: TABLE SETUP & MASTER SCORE ──
        var p4 = CreateUIRect("Page_4_TableAndFinal", pageContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        controller.page4TableAndFinal = p4;

        var p4Title = CreateTMPChild(p4.transform, "Title", "MAYO TABLE PREPARATION & FINAL CLINICAL VERDICT", 15f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.28f, 0.80f, 0.98f), font);
        p4Title.rectTransform.anchorMin = new Vector2(0.02f, 0.88f);
        p4Title.rectTransform.anchorMax = new Vector2(0.98f, 0.98f);
        p4Title.rectTransform.offsetMin = Vector2.zero;
        p4Title.rectTransform.offsetMax = Vector2.zero;

        var p4Verdict = CreateTMPChild(p4.transform, "Verdict", "OVERALL RESULT: PASS (EXCELLENT)", 16f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.20f, 0.85f, 0.55f), font);
        p4Verdict.rectTransform.anchorMin = new Vector2(0.02f, 0.76f);
        p4Verdict.rectTransform.anchorMax = new Vector2(0.98f, 0.88f);
        p4Verdict.rectTransform.offsetMin = Vector2.zero;
        p4Verdict.rectTransform.offsetMax = Vector2.zero;
        controller.p4VerdictGradeText = p4Verdict;

        var p4Score = CreateTMPChild(p4.transform, "Score", "Final Score: 100 / 100", 22f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.35f, 0.78f, 0.98f), font);
        p4Score.rectTransform.anchorMin = new Vector2(0.02f, 0.60f);
        p4Score.rectTransform.anchorMax = new Vector2(0.98f, 0.74f);
        p4Score.rectTransform.offsetMin = Vector2.zero;
        p4Score.rectTransform.offsetMax = Vector2.zero;
        controller.p4FinalScoreText = p4Score;

        var p4Mistakes = CreateTMPChild(p4.transform, "Mistakes", "Itemized Deductions Ledger:\n✓ Flawless Procedure! Zero sterile violations.", 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.95f, 0.95f, 0.95f), font);
        p4Mistakes.rectTransform.anchorMin = new Vector2(0.02f, 0.04f);
        p4Mistakes.rectTransform.anchorMax = new Vector2(0.98f, 0.58f);
        p4Mistakes.rectTransform.offsetMin = Vector2.zero;
        p4Mistakes.rectTransform.offsetMax = Vector2.zero;
        controller.p4OverallMistakesListText = p4Mistakes;

        // ── FOOTER NAVIGATION BAR ──
        var footer = CreateUIRect("FooterNavBar", root.transform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.12f), Vector2.zero, Vector2.zero);
        AddImage(footer, new Color(0.08f, 0.12f, 0.18f, 0.95f));

        // Button Back
        var backObj = CreateUIRect("Button_Back", footer.transform, new Vector2(0.02f, 0.15f), new Vector2(0.20f, 0.85f), Vector2.zero, Vector2.zero);
        AddImage(backObj, new Color(0.18f, 0.28f, 0.38f, 0.95f));
        controller.buttonBack = backObj.AddComponent<Button>();
        var bTxt = CreateTMPChild(backObj.transform, "Text", "◀ Previous", 12.5f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        bTxt.rectTransform.anchorMin = Vector2.zero;
        bTxt.rectTransform.anchorMax = Vector2.one;
        bTxt.rectTransform.offsetMin = Vector2.zero;
        bTxt.rectTransform.offsetMax = Vector2.zero;

        // Page Indicator
        var indObj = CreateUIRect("Text_PageIndicator", footer.transform, new Vector2(0.22f, 0.15f), new Vector2(0.72f, 0.85f), Vector2.zero, Vector2.zero);
        var pInd = CreateTMPChild(indObj.transform, "Text", "Page 1 of 4: Patient Communication", 13f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.85f, 0.92f, 0.98f), font);
        pInd.rectTransform.anchorMin = Vector2.zero;
        pInd.rectTransform.anchorMax = Vector2.one;
        pInd.rectTransform.offsetMin = Vector2.zero;
        pInd.rectTransform.offsetMax = Vector2.zero;
        controller.textPageIndicator = pInd;

        // Button Next
        var nextObj = CreateUIRect("Button_Next", footer.transform, new Vector2(0.75f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero);
        AddImage(nextObj, new Color(0.15f, 0.55f, 0.85f, 0.95f));
        controller.buttonNext = nextObj.AddComponent<Button>();
        var nTxt = CreateTMPChild(nextObj.transform, "Text", "Next ▶", 12.5f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        nTxt.rectTransform.anchorMin = Vector2.zero;
        nTxt.rectTransform.anchorMax = Vector2.one;
        nTxt.rectTransform.offsetMin = Vector2.zero;
        nTxt.rectTransform.offsetMax = Vector2.zero;

        // Button Restart
        var restartObj = CreateUIRect("Button_Restart", footer.transform, new Vector2(0.75f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero);
        AddImage(restartObj, new Color(0.20f, 0.75f, 0.50f, 0.95f));
        controller.buttonRestart = restartObj.AddComponent<Button>();
        var rTxt = CreateTMPChild(restartObj.transform, "Text", "🔄 Restart", 12.5f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        rTxt.rectTransform.anchorMin = Vector2.zero;
        rTxt.rectTransform.anchorMax = Vector2.one;
        rTxt.rectTransform.offsetMin = Vector2.zero;
        rTxt.rectTransform.offsetMax = Vector2.zero;
        restartObj.SetActive(false);

        return root;
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
