#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

[InitializeOnLoad]
public static class SetupSimulationSceneFlow
{
    private const string AutoBuildPrefKey  = "VR_Childbirth_HoloTutorial_V9_DedicatedButtonGlowAnchors";
    private const string MenuScenePath     = "Assets/Scenes/MenuScene.unity";
    private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";
    private const string MainScenePath     = "Assets/Scenes/MainScene.unity";
    private const string SummaryScenePath  = "Assets/Scenes/SummaryScene.unity";

    static SetupSimulationSceneFlow()
    {
        EditorApplication.delayCall += TryAutoRebuildTutorialSceneOnce;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [InitializeOnLoadMethod]
    private static void ForceRebuildInit()
    {
        EditorApplication.delayCall += TryAutoRebuildTutorialSceneOnce;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += TryAutoRebuildTutorialSceneOnce;
        }
    }

    private static void TryAutoRebuildTutorialSceneOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoRebuildTutorialSceneOnce;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryAutoRebuildTutorialSceneOnce;
            return;
        }

        if (!SessionState.GetBool(AutoBuildPrefKey, false))
        {
            SessionState.SetBool(AutoBuildPrefKey, true);
            RebuildTutorialSceneOnly();
        }
    }

    [MenuItem("Tools/1. Rebuild Modular Tutorial Scene NOW")]
    [MenuItem("Tools/Rebuild Holographic Tutorial Scene Only (Reset Positions)")]
    public static void RebuildTutorialSceneOnly()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterBoldFontPath)
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        BuildTutorialScene(font);
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneFlow] Rebuilt Holographic TutorialScene (Borderless + 100% Manually Editable)!");
    }

    private const string Quest2RightControllerPath = "Packages/com.meta.xr.sdk.core/Meshes/OculusTouchForQuest2/OculusTouchForQuest2_Right.fbx";
    private const string UniversalControllerPath   = "Assets/VRTemplateAssets/Models/Controllers/UniversalController.fbx";
    private const string FresnelShaderName         = "SpatialFramework/Textured Fresnel/Standard";
    private const string CircleRingSpritePath      = "Assets/VRTemplateAssets/Sprites/UI/Circle_60x60 Outline.png";
    private const string CircleInnerSpritePath     = "Assets/VRTemplateAssets/Sprites/UI/CircleMask.png";
    private const string RoundOutlineSpritePath    = "Assets/VRTemplateAssets/Sprites/UI/Round Radius 10 Outline.png";

    // Newly downloaded Unity UI Samples 9-sliced sprites
    private const string SfWindowSpritePath        = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Window.psd";
    private const string SfButtonSpritePath        = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Button.psd";
    private const string SfTitleSpritePath         = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Title.psd";
    private const string UiPanelSpritePath         = "Assets/Unity UI Samples/Textures and Sprites/Rounded UI/UIPanel.png";
    private const string UiButtonDefaultSpritePath = "Assets/Unity UI Samples/Textures and Sprites/Rounded UI/UIButtonDefault.png";
    private const string DottedRectSpritePath      = "Assets/Unity UI Samples/Textures and Sprites/Decoration/DottedRoundedRect.psd";

    private const string StepChimeClipPath         = "Assets/VRTemplateAssets/Audio/Button_22_click.wav";
    private const string HoverClipPath             = "Assets/VRTemplateAssets/Audio/Button_14_hover.wav";

    private const string InterBoldFontPath = "Assets/Fonts/Inter_Bold_SDF.asset";
    private const string FallbackFontPath  = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Build 4-Scene Flow (Menu, Tutorial, Main, Summary)")]
    public static void BuildAllScenes()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterBoldFontPath)
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);

        SetupMainSceneTrashCanDisposal();
        BuildMenuScene(font);
        BuildTutorialScene(font);
        BuildSummaryScene(font);
        UpdateEditorBuildSettings();

        EditorSceneManager.OpenScene(TutorialScenePath);
        Debug.Log("[SceneFlow] All 4 scenes (Menu, Holographic Tutorial, Main, Summary) updated and verified!");
    }

    // ══════════════════════════════════════════════════════════════════
    // MAIN SCENE: ENSURE INTERIOR TRASH CAN DISPOSAL ZONE (0 PENALTY, 0 FORCEPS INTERFERENCE)
    // ══════════════════════════════════════════════════════════════════
    public static void SetupMainSceneTrashCanDisposal()
    {
        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        EnsureInteriorTrashCanDisposalInCurrentScene();
        EditorSceneManager.SaveScene(scene, MainScenePath);
        Debug.Log("[SceneFlow] Configured TrashCan_InteriorDisposalZone in MainScene.");
    }

    private static TrashCanDisposalZone EnsureInteriorTrashCanDisposalInCurrentScene()
    {
        // Remove any legacy oversized collider directly on the scaled TrashCan mesh
        var trashCanGo = GameObject.Find("TrashCan");
        if (trashCanGo != null)
        {
            var oldWb = trashCanGo.GetComponent<WasteBasin>();
            if (oldWb != null) Object.DestroyImmediate(oldWb);
            var oldCol = trashCanGo.GetComponent<BoxCollider>();
            if (oldCol != null && oldCol.isTrigger) Object.DestroyImmediate(oldCol);
        }

        // Remove any previous standalone zone before recreating cleanly
        var existingZone = GameObject.Find("TrashCan_InteriorDisposalZone") ?? GameObject.Find("TrashCan_WasteBasinZone");
        if (existingZone != null) Object.DestroyImmediate(existingZone);

        Vector3 trashCenter = new Vector3(0.59f, 0.94f, 0.60f);
        if (trashCanGo != null)
        {
            var mr = trashCanGo.GetComponentInChildren<MeshRenderer>();
            if (mr != null) trashCenter = mr.bounds.center;
            else trashCenter = new Vector3(trashCanGo.transform.position.x, 0.94f, trashCanGo.transform.position.z);
        }

        // Dedicated interior-only trigger zone strictly inside the mouth/cavity of the trash can
        // (Smaller than exterior walls of TrashCan [0.32 x 0.37 x 0.35] so exterior contact never triggers it)
        var zoneObj = new GameObject("TrashCan_InteriorDisposalZone");
        zoneObj.transform.position = new Vector3(trashCenter.x, trashCenter.y + 0.04f, trashCenter.z);
        zoneObj.transform.rotation = Quaternion.identity;
        zoneObj.transform.localScale = Vector3.one;

        var box = zoneObj.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(0.22f, 0.28f, 0.22f);

        var audioSrc = zoneObj.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0.5f;

        var disposalZone = zoneObj.AddComponent<TrashCanDisposalZone>();
        disposalZone.audioSource = audioSrc;
        disposalZone.discardSound = AssetDatabase.LoadAssetAtPath<AudioClip>(StepChimeClipPath);
        if (trashCanGo != null) disposalZone.visualMeshSlot = trashCanGo;

        return disposalZone;
    }

    // ══════════════════════════════════════════════════════════════════
    // SCENE 1: MENU SCENE (FUTURISTIC CLINICAL COMMAND INTERFACE USING UNITY UI SAMPLES)
    // ══════════════════════════════════════════════════════════════════
    public static void BuildMenuScene(TMP_FontAsset font)
    {
        AssetDatabase.CopyAsset(MainScenePath, MenuScenePath);
        var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        foreach (var root in scene.GetRootGameObjects())
        {
            if (!root.name.Contains("PLAYER"))
            {
                Object.DestroyImmediate(root);
            }
        }

        SetupDarkEnvironment();
        AlignPlayerInScene(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
        SetupEventSystem();

        var sfWindowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SfWindowSpritePath);
        var sfButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SfButtonSpritePath);
        var sfTitleSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(SfTitleSpritePath);
        var uiPanelSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(UiPanelSpritePath);
        var dottedSprite   = AssetDatabase.LoadAssetAtPath<Sprite>(DottedRectSpritePath);

        // Menu Canvas
        var canvasObj = new GameObject("MenuCanvas");
        canvasObj.transform.position = new Vector3(0f, 1.50f, 1.38f);
        canvasObj.transform.rotation = Quaternion.identity;

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();
        canvasObj.AddComponent<VRHeadFollower>();

        var rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(860f, 520f);
        rt.localScale = Vector3.one * 0.0020f;

        var menuController = canvasObj.AddComponent<MenuScreenController>();
        var menuAudio = canvasObj.AddComponent<AudioSource>();
        menuAudio.playOnAwake = false;
        menuController.audioSource = menuAudio;
        menuController.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(StepChimeClipPath);

        // ──────────────────────────────────────────────────────────────────
        // SCREEN 1: TITLE SCREEN (3-LAYER STEREOSCOPIC MEDICAL WORKSTATION)
        // ──────────────────────────────────────────────────────────────────
        var titlePanel = CreatePanel("TitleScreenPanel", canvasObj.transform, Vector2.zero, Vector2.one);
        menuController.titleScreenPanel = titlePanel;

        // Layer 0: Deep Obsidian Glass Backplate + SF Window Beveled Frame
        var backplate = CreatePanel("ObsidianGlassBackplate", titlePanel.transform, Vector2.zero, Vector2.one);
        AddSlicedImage(backplate, uiPanelSprite, new Color(0.02f, 0.05f, 0.10f, 0.92f));
        AddOutline(backplate, new Color(0.18f, 0.72f, 0.96f, 0.65f), new Vector2(1.5f, -1.5f));

        var sfFrame = CreatePanel("SF_BeveledCommandFrame", titlePanel.transform, new Vector2(-0.02f, -0.02f), new Vector2(1.02f, 1.02f));
        AddSlicedImage(sfFrame, sfWindowSprite, new Color(0.28f, 0.82f, 0.98f, 0.55f));

        // Top Clinical Header Banner (using SF Title Sprite)
        var headerBanner = CreatePanel("TopTelemetryBanner", titlePanel.transform, new Vector2(0.14f, 0.85f), new Vector2(0.86f, 0.95f));
        AddSlicedImage(headerBanner, sfTitleSprite ?? uiPanelSprite, new Color(0.06f, 0.24f, 0.38f, 0.85f));
        var headerText = CreateTMP(headerBanner.transform, "HeaderLabel", "OBSTETRIC VR SIMULATION SYSTEM  •  CLINICAL TRAINING MODULE", 11.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.45f, 0.92f, 1.0f), font);
        SetRect(headerText.rectTransform, Vector2.zero, Vector2.one);

        // Main Title & Subtitle
        var titleText = CreateTMP(titlePanel.transform, "TitleText", "VR CHILDBIRTH SIMULATOR", 30f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        SetRect(titleText.rectTransform, new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.83f));

        var subText = CreateTMP(titlePanel.transform, "SubText", "Aseptic Perineal Care, Dual-Forceps 9-Ball Technique & Decision Matrix Evaluation", 13.5f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.62f, 0.84f, 0.96f), font);
        SetRect(subText.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.68f));

        // Layer 1: 3 Recessed Clinical Module Status Cards
        CreateModuleInfoCard(titlePanel.transform, "Card1_Communication", new Vector2(0.07f, 0.40f), new Vector2(0.35f, 0.55f),
            "MODULE 01", "Patient Consent & Sterile Draping", dottedSprite ?? uiPanelSprite, font);
        CreateModuleInfoCard(titlePanel.transform, "Card2_Aseptic", new Vector2(0.36f, 0.40f), new Vector2(0.64f, 0.55f),
            "MODULE 02", "9-Cotton Ball Aseptic Technique", dottedSprite ?? uiPanelSprite, font);
        CreateModuleInfoCard(titlePanel.transform, "Card3_Evaluation", new Vector2(0.65f, 0.40f), new Vector2(0.93f, 0.55f),
            "MODULE 03", "Real-Time Decision Matrix & POV", dottedSprite ?? uiPanelSprite, font);

        // Layer 2: Elevated Interactive Action Buttons (Floating 8mm Forward in Z for 3D Depth)
        var playBtnObj = CreateMedicalButton("PlayButton", titlePanel.transform, "▶   LAUNCH SIMULATION",
            sfButtonSprite ?? uiPanelSprite, new Color(0.08f, 0.58f, 0.84f, 0.96f), new Color(0.38f, 0.92f, 1.0f, 0.95f), 16f, font);
        SetRect(playBtnObj.GetComponent<RectTransform>(), new Vector2(0.24f, 0.21f), new Vector2(0.76f, 0.35f));
        playBtnObj.transform.localPosition = new Vector3(0f, 0f, -4f);
        menuController.playButton = playBtnObj.GetComponent<Button>();

        var exitBtnObj = CreateMedicalButton("ExitButton", titlePanel.transform, "EXIT APPLICATION",
            sfButtonSprite ?? uiPanelSprite, new Color(0.10f, 0.16f, 0.24f, 0.90f), new Color(0.35f, 0.50f, 0.68f, 0.70f), 13f, font);
        SetRect(exitBtnObj.GetComponent<RectTransform>(), new Vector2(0.34f, 0.08f), new Vector2(0.66f, 0.18f));
        exitBtnObj.transform.localPosition = new Vector3(0f, 0f, -4f);
        menuController.exitButton = exitBtnObj.GetComponent<Button>();

        // ──────────────────────────────────────────────────────────────────
        // SCREEN 2: TUTORIAL BRIEFING MODAL
        // ──────────────────────────────────────────────────────────────────
        var promptPanel = CreatePanel("TutorialPromptPanel", canvasObj.transform, Vector2.zero, Vector2.one);
        menuController.tutorialPromptPanel = promptPanel;

        var promptBack = CreatePanel("ObsidianGlassBackplate", promptPanel.transform, Vector2.zero, Vector2.one);
        AddSlicedImage(promptBack, uiPanelSprite, new Color(0.02f, 0.05f, 0.10f, 0.94f));
        AddOutline(promptBack, new Color(0.20f, 0.86f, 0.68f, 0.75f), new Vector2(1.5f, -1.5f));

        var promptSfFrame = CreatePanel("SF_BeveledModalFrame", promptPanel.transform, new Vector2(-0.02f, -0.02f), new Vector2(1.02f, 1.02f));
        AddSlicedImage(promptSfFrame, sfWindowSprite, new Color(0.22f, 0.85f, 0.72f, 0.55f));

        var promptHeader = CreatePanel("ModalHeaderBanner", promptPanel.transform, new Vector2(0.18f, 0.84f), new Vector2(0.82f, 0.94f));
        AddSlicedImage(promptHeader, sfTitleSprite ?? uiPanelSprite, new Color(0.06f, 0.28f, 0.26f, 0.88f));
        var promptHeaderTxt = CreateTMP(promptHeader.transform, "HeaderText", "PRE-SIMULATION ORIENTATION", 12f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.34f, 0.95f, 0.76f), font);
        SetRect(promptHeaderTxt.rectTransform, Vector2.zero, Vector2.one);

        var promptTitle = CreateTMP(promptPanel.transform, "PromptTitle", "HOLOGRAPHIC MECHANICS TUTORIAL", 25f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        SetRect(promptTitle.rectTransform, new Vector2(0.06f, 0.67f), new Vector2(0.94f, 0.81f));

        var promptWell = CreatePanel("BriefingContentWell", promptPanel.transform, new Vector2(0.10f, 0.43f), new Vector2(0.90f, 0.65f));
        AddSlicedImage(promptWell, dottedSprite ?? uiPanelSprite, new Color(0.04f, 0.11f, 0.18f, 0.80f));

        var promptQuestion = CreateTMP(promptWell.transform, "PromptQuestion",
            "Would you like to complete a guided <b>3D Holographic Tutorial</b> before entering the delivery room?\n" +
            "<color=#7DD3FC>• Step 1: Instrument Grip   • Step 2: Clinical UI Trigger   • Step 3: Forceps Cotton Transfer</color>",
            13.5f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.90f, 0.96f, 1.0f), font);
        SetRect(promptQuestion.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));

        var yesBtnObj = CreateMedicalButton("YesTutorialButton", promptPanel.transform, "✓  START HOLOGRAPHIC TUTORIAL",
            sfButtonSprite ?? uiPanelSprite, new Color(0.08f, 0.62f, 0.48f, 0.96f), new Color(0.34f, 0.96f, 0.78f, 0.95f), 14.5f, font);
        SetRect(yesBtnObj.GetComponent<RectTransform>(), new Vector2(0.09f, 0.23f), new Vector2(0.48f, 0.37f));
        yesBtnObj.transform.localPosition = new Vector3(0f, 0f, -4f);
        menuController.yesTutorialButton = yesBtnObj.GetComponent<Button>();

        var noBtnObj = CreateMedicalButton("NoSkipToMainButton", promptPanel.transform, "▶  SKIP TO DELIVERY ROOM",
            sfButtonSprite ?? uiPanelSprite, new Color(0.08f, 0.50f, 0.78f, 0.96f), new Color(0.38f, 0.88f, 1.0f, 0.95f), 14.5f, font);
        SetRect(noBtnObj.GetComponent<RectTransform>(), new Vector2(0.52f, 0.23f), new Vector2(0.91f, 0.37f));
        noBtnObj.transform.localPosition = new Vector3(0f, 0f, -4f);
        menuController.noTutorialButton = noBtnObj.GetComponent<Button>();

        var backBtnObj = CreateMedicalButton("BackToTitleButton", promptPanel.transform, "◀  RETURN TO MAIN MENU",
            sfButtonSprite ?? uiPanelSprite, new Color(0.12f, 0.18f, 0.26f, 0.90f), new Color(0.38f, 0.55f, 0.72f, 0.70f), 12.5f, font);
        SetRect(backBtnObj.GetComponent<RectTransform>(), new Vector2(0.33f, 0.08f), new Vector2(0.67f, 0.18f));
        backBtnObj.transform.localPosition = new Vector3(0f, 0f, -4f);
        menuController.backToMenuButton = backBtnObj.GetComponent<Button>();

        promptPanel.SetActive(false);

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        Debug.Log("[SceneFlow] Built redesigned medical MenuScene at: " + MenuScenePath);
    }

    // ══════════════════════════════════════════════════════════════════
    // SCENE 2: TUTORIAL SCENE (MODULAR, 100% EDITABLE STEP HIERARCHY)
    // ══════════════════════════════════════════════════════════════════
    public static void BuildTutorialScene(TMP_FontAsset font)
    {
        AssetDatabase.CopyAsset(MainScenePath, TutorialScenePath);
        var scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

        GameObject tablesRoot = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            string n = root.name;
            if (n.Contains("TABLES AND TOOLS"))
            {
                tablesRoot = root;
            }
            else if (n.Contains("PLAYER") || n.Contains("ENVIRONMENT"))
            {
                // Preserve exact Player rig and Hospital Room environment
            }
            else
            {
                Object.DestroyImmediate(root);
            }
        }

        // Keep ONLY Table 3 and CottonBall_Model_Template
        GameObject table3Obj = null;
        if (tablesRoot != null)
        {
            var toDelete = new List<GameObject>();
            for (int i = 0; i < tablesRoot.transform.childCount; i++)
            {
                var ch = tablesRoot.transform.GetChild(i).gameObject;
                if (ch.name == "Table 3")
                {
                    table3Obj = ch;
                }
                else if (ch.name.Contains("CottonBall"))
                {
                    // Keep cotton template
                }
                else
                {
                    toDelete.Add(ch);
                }
            }
            foreach (var d in toDelete) Object.DestroyImmediate(d);
        }

        // Position Player directly in front of Table 3
        AlignPlayerInScene(new Vector3(-0.08f, 0.0f, 1.32f), new Vector3(0f, 180f, 0f));
        SetupEventSystem();
        EnsureSceneFader();

        // Setup interior-only TrashCan disposal zone
        var wasteBasinComp = EnsureInteriorTrashCanDisposalInCurrentScene();
        Transform trashCanTransform = wasteBasinComp != null ? wasteBasinComp.transform : null;

        XRGrabInteractable pitcherGrab = null;
        ForcepsController pickupForceps = null;
        ForcepsController handlingForceps = null;
        Transform iodineJarTransform = null;

        if (table3Obj != null)
        {
            var pitcherTr = FindChildRecursive(table3Obj.transform, "Pitcher");
            if (pitcherTr != null) pitcherGrab = pitcherTr.GetComponent<XRGrabInteractable>();

            foreach (var fc in table3Obj.GetComponentsInChildren<ForcepsController>(true))
            {
                if (fc.role == ForcepsRole.Pickup) pickupForceps = fc;
                else if (fc.role == ForcepsRole.Handling) handlingForceps = fc;
            }

            iodineJarTransform = FindChildRecursive(table3Obj.transform, "7.5_iodine")
                              ?? FindChildRecursive(table3Obj.transform, "10_iodine");
        }

        var uiPanelSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(UiPanelSpritePath);
        var sfButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SfButtonSpritePath);
        var sfWindowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SfWindowSpritePath);
        var roundSprite    = AssetDatabase.LoadAssetAtPath<Sprite>(RoundOutlineSpritePath);
        var ringSprite     = AssetDatabase.LoadAssetAtPath<Sprite>(CircleRingSpritePath);
        var innerSprite    = AssetDatabase.LoadAssetAtPath<Sprite>(CircleInnerSpritePath);

        // ── Holographic Materials ──
        Material holoBodyMat = GetOrCreateHologramMaterial(
            "Assets/Materials/HolographicControllerBody.mat",
            new Color(0.75f, 0.88f, 0.96f, 0.18f),
            new Color(0.42f, 0.90f, 1.0f, 0.88f),
            new Vector4(0.52f, 0.95f, 0.65f, 1.0f)
        );
        Material holoHighlightMat = GetOrCreateHologramMaterial(
            "Assets/Materials/HolographicButtonHighlight.mat",
            new Color(0.20f, 0.95f, 1.0f, 0.78f),
            new Color(0.85f, 1.0f, 1.0f, 1.0f),
            new Vector4(0.25f, 0.90f, 0.65f, 1.0f)
        );
        Material targetSilhouetteMat = GetOrCreateHologramMaterial(
            "Assets/Materials/HolographicTargetSilhouette.mat",
            new Color(0.25f, 0.90f, 1.0f, 0.04f),
            new Color(0.38f, 0.95f, 1.0f, 0.85f),
            new Vector4(0.60f, 0.96f, 0.70f, 1.0f)
        );
        Material holoLineMat = GetOrCreateLineMaterial("Assets/Materials/HolographicLeaderLine.mat");

        var controllerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Quest2RightControllerPath)
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>(UniversalControllerPath);

        // ── Build HolographicTutorialGuide System ──
        var guideRoot = new GameObject("----- HOLOGRAPHIC TUTORIAL SYSTEM");
        var holoGuide = guideRoot.AddComponent<HolographicTutorialGuide>();
        var tutorialManager = guideRoot.AddComponent<TutorialManager>();
        var audioSrc = guideRoot.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;

        holoGuide.targetSilhouetteMaterial = targetSilhouetteMat;
        holoGuide.lockControllerToHeadset = true; // Smoothly follows player view in VR
        holoGuide.autoSnapRingToTargetInPlayMode = true;
        holoGuide.billboardTargetRingInPlayMode = true;
        holoGuide.controllerViewOffset = new Vector3(0.28f, -0.07f, 0.54f);

        tutorialManager.holographicGuide = holoGuide;
        tutorialManager.audioSource = audioSrc;
        tutorialManager.stepCompleteClip = AssetDatabase.LoadAssetAtPath<AudioClip>(StepChimeClipPath);
        tutorialManager.tutorialCompleteClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HoverClipPath) ?? tutorialManager.stepCompleteClip;
        tutorialManager.demoTool = pitcherGrab;
        tutorialManager.pickupForceps = pickupForceps;
        tutorialManager.handlingForceps = handlingForceps;
        tutorialManager.antisepticJarTransform = iodineJarTransform;
        tutorialManager.wasteBasinTransform = trashCanTransform;
        tutorialManager.wasteBasin = wasteBasinComp;

        // 1. Floating 3D Controller Rig (Right Periphery — Freely Movable/Rotatable in Scene View)
        var rigObj = new GameObject("HolographicControllerRig");
        rigObj.transform.SetParent(guideRoot.transform, false);
        rigObj.transform.position = new Vector3(-0.36f, 1.38f, 0.82f);
        rigObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        holoGuide.controllerRigRoot = rigObj.transform;

        if (controllerPrefab != null)
        {
            var controllerModel = (GameObject)PrefabUtility.InstantiatePrefab(controllerPrefab, rigObj.transform);
            controllerModel.name = "HolographicController_3DModel";
            controllerModel.transform.localPosition = Vector3.zero;
            controllerModel.transform.localRotation = Quaternion.Euler(18f, -145f, 12f);
            controllerModel.transform.localScale = Vector3.one * 1.45f;

            foreach (var r in controllerModel.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = holoBodyMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            holoGuide.controllerModelRoot = controllerModel.transform;
        }

        // Active Button Glowing Cyan Pill Highlight
        var highlightObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        highlightObj.name = "ActiveButtonHighlight_Glow";
        highlightObj.transform.SetParent(rigObj.transform, false);
        highlightObj.transform.localScale = new Vector3(0.024f, 0.016f, 0.027f);
        var hlCol = highlightObj.GetComponent<Collider>();
        if (hlCol != null) Object.DestroyImmediate(hlCol);
        var hlRenderer = highlightObj.GetComponent<MeshRenderer>();
        if (hlRenderer != null)
        {
            hlRenderer.sharedMaterial = holoHighlightMat;
            hlRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        holoGuide.activeButtonHighlight = highlightObj.transform;

        // Dedicated Movable Button Highlight Anchors (Drag/Scale in Scene View!)
        var buttonAnchorsRoot = new GameObject("Button Highlight Anchors (Move to Adjust Glow)");
        buttonAnchorsRoot.transform.SetParent(rigObj.transform, false);

        // 1. Grip Button Glow Anchor (Outer grip pad on handle)
        var gripAnchorObj = new GameObject("GripButton_GlowAnchor");
        gripAnchorObj.transform.SetParent(buttonAnchorsRoot.transform, false);
        gripAnchorObj.transform.localPosition = new Vector3(-0.018f, -0.028f, -0.012f);
        gripAnchorObj.transform.localRotation = Quaternion.Euler(15f, 90f, 0f);
        gripAnchorObj.transform.localScale = new Vector3(0.026f, 0.018f, 0.028f);
        holoGuide.gripHighlightAnchor = gripAnchorObj.transform;

        // 2. Index Trigger Glow Anchor (Front trigger curve)
        var triggerAnchorObj = new GameObject("TriggerButton_GlowAnchor");
        triggerAnchorObj.transform.SetParent(buttonAnchorsRoot.transform, false);
        triggerAnchorObj.transform.localPosition = new Vector3(0.002f, -0.038f, 0.034f);
        triggerAnchorObj.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
        triggerAnchorObj.transform.localScale = new Vector3(0.022f, 0.016f, 0.026f);
        holoGuide.triggerHighlightAnchor = triggerAnchorObj.transform;

        // 3. Primary Button A Glow Anchor (Faceplate Button A cap)
        var buttonAAnchorObj = new GameObject("ButtonA_GlowAnchor");
        buttonAAnchorObj.transform.SetParent(buttonAnchorsRoot.transform, false);
        buttonAAnchorObj.transform.localPosition = new Vector3(0.012f, 0.012f, -0.018f);
        buttonAAnchorObj.transform.localRotation = Quaternion.Euler(30f, 25f, 0f);
        buttonAAnchorObj.transform.localScale = new Vector3(0.018f, 0.014f, 0.018f);
        holoGuide.buttonAHighlightAnchor = buttonAAnchorObj.transform;

        // Orthogonal L-Shaped Leader Lines (Thin, Crisp Cyan)
        holoGuide.upperLeaderLine  = CreateHolographicLine(rigObj.transform, "UpperLeaderLine", holoLineMat, 0.0018f);
        holoGuide.lowerLeaderLine  = CreateHolographicLine(rigObj.transform, "LowerLeaderLine", holoLineMat, 0.0018f);
        holoGuide.laserPointerLine = CreateHolographicLine(rigObj.transform, "LaserPointerRayLine", holoLineMat, 0.0024f);

        // Dark Semi-Transparent Holographic Callout Badges (Beside the 3D Controller - Matches Screenshot media_1794131219639_42d16bc9.jpg)
        var calloutCanvasObj = new GameObject("ControllerCalloutCanvas");
        calloutCanvasObj.transform.SetParent(rigObj.transform, false);
        calloutCanvasObj.transform.localPosition = new Vector3(-0.15f, 0.02f, 0f);
        calloutCanvasObj.transform.localRotation = Quaternion.identity;

        var calloutCanvas = calloutCanvasObj.AddComponent<Canvas>();
        calloutCanvas.renderMode = RenderMode.WorldSpace;
        calloutCanvasObj.AddComponent<CanvasScaler>();
        var calloutCg = calloutCanvasObj.AddComponent<CanvasGroup>();
        holoGuide.controllerCalloutCanvasGroup = calloutCg;

        var calloutRt = calloutCanvasObj.GetComponent<RectTransform>();
        calloutRt.sizeDelta = new Vector2(330f, 250f);
        calloutRt.localScale = Vector3.one * 0.00095f;

        // Upper Dark Glass Callout Pill
        var upperGroup = CreatePanel("UpperCalloutGroup", calloutCanvasObj.transform, new Vector2(0.02f, 0.64f), new Vector2(0.98f, 0.96f));
        AddSlicedImage(upperGroup, uiPanelSprite, new Color(0.01f, 0.03f, 0.07f, 0.78f));
        AddOutline(upperGroup, new Color(0.36f, 0.90f, 1.0f, 0.85f), new Vector2(1.2f, -1.2f));

        var upperAnchor = new GameObject("UpperCalloutLineAnchor");
        upperAnchor.transform.SetParent(upperGroup.transform, false);
        SetRect(upperAnchor.AddComponent<RectTransform>(), new Vector2(0.12f, 0f), new Vector2(0.12f, 0f));
        holoGuide.upperCalloutAnchor = upperAnchor.transform;

        var badgeBox = CreatePanel("ButtonBadge", upperGroup.transform, new Vector2(0.04f, 0.18f), new Vector2(0.24f, 0.82f));
        AddSlicedImage(badgeBox, roundSprite ?? uiPanelSprite, new Color(0.08f, 0.38f, 0.56f, 0.85f));
        AddOutline(badgeBox, new Color(0.45f, 0.95f, 1.0f, 1.0f), new Vector2(1.2f, -1.2f));

        var badgeTxt = CreateTMP(badgeBox.transform, "BadgeLabel", "GRIP", 13.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.65f, 0.98f, 1.0f), font);
        SetRect(badgeTxt.rectTransform, Vector2.zero, Vector2.one);
        holoGuide.buttonBadgeText = badgeTxt;

        var upperTxt = CreateTMP(upperGroup.transform, "UpperCalloutText", "HOLD [GRAB] BUTTON\nTO PICK UP", 15.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.92f, 0.98f, 1.0f), font);
        SetRect(upperTxt.rectTransform, new Vector2(0.28f, 0.08f), new Vector2(0.96f, 0.92f));
        holoGuide.upperCalloutText = upperTxt;

        // Lower Dark Glass Callout Pill
        var lowerGroup = CreatePanel("LowerCalloutGroup", calloutCanvasObj.transform, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.34f));
        AddSlicedImage(lowerGroup, uiPanelSprite, new Color(0.01f, 0.03f, 0.07f, 0.78f));
        AddOutline(lowerGroup, new Color(0.36f, 0.90f, 1.0f, 0.85f), new Vector2(1.2f, -1.2f));

        var lowerAnchor = new GameObject("LowerCalloutLineAnchor");
        lowerAnchor.transform.SetParent(lowerGroup.transform, false);
        SetRect(lowerAnchor.AddComponent<RectTransform>(), new Vector2(0.50f, 1f), new Vector2(0.50f, 1f));
        holoGuide.lowerCalloutAnchor = lowerAnchor.transform;

        var lowerTxt = CreateTMP(lowerGroup.transform, "LowerCalloutText", "HOLD [GRAB] BUTTON\nTO PICK UP", 15f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.84f, 0.96f, 1.0f), font);
        SetRect(lowerTxt.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
        holoGuide.lowerCalloutText = lowerTxt;

        // 2. Target Object Holographic Ring + Dark Semi-Transparent Pill Label Over Table
        var targetRingObj = new GameObject("TargetObjectHolographicRing");
        targetRingObj.transform.SetParent(guideRoot.transform, false);
        Vector3 pitcherPos = new Vector3(0.13f, 1.15f, 0.75f);
        if (pitcherGrab != null)
        {
            var mr = pitcherGrab.GetComponentInChildren<MeshRenderer>();
            pitcherPos = mr != null ? mr.bounds.center : pitcherGrab.transform.position;
        }
        targetRingObj.transform.position = pitcherGrab != null ? pitcherGrab.transform.position + new Vector3(0f, 0.08f, 0f) : pitcherPos + new Vector3(0.11f, -0.02f, -0.04f);
        targetRingObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        var ringCanvas = targetRingObj.AddComponent<Canvas>();
        ringCanvas.renderMode = RenderMode.WorldSpace;
        targetRingObj.AddComponent<CanvasScaler>();
        var ringCg = targetRingObj.AddComponent<CanvasGroup>();
        holoGuide.targetRingRoot = targetRingObj.transform;
        holoGuide.targetRingCanvasGroup = ringCg;

        var ringRt = targetRingObj.GetComponent<RectTransform>();
        ringRt.sizeDelta = new Vector2(240f, 240f);
        ringRt.localScale = Vector3.one * 0.00105f;

        var innerDisc = CreatePanel("InnerGlowDisc", targetRingObj.transform, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.72f));
        var innerImg = AddImage(innerDisc, new Color(0.55f, 0.94f, 1.0f, 0.18f));
        if (innerSprite != null) innerImg.sprite = innerSprite;
        holoGuide.targetRingInnerImage = innerImg;

        var outerRing = CreatePanel("OuterCyanRing", targetRingObj.transform, new Vector2(0.14f, 0.04f), new Vector2(0.86f, 0.76f));
        var outerImg = AddImage(outerRing, new Color(0.45f, 0.94f, 1.0f, 0.92f));
        if (ringSprite != null) outerImg.sprite = ringSprite;
        holoGuide.targetRingOuterImage = outerImg;

        // Dark Semi-Transparent Pill Behind Label Text for High Contrast Readability
        var labelBanner = CreatePanel("TargetLabelBanner", targetRingObj.transform, new Vector2(-0.12f, 0.78f), new Vector2(1.12f, 0.98f));
        AddSlicedImage(labelBanner, uiPanelSprite, new Color(0.01f, 0.03f, 0.07f, 0.80f));
        AddOutline(labelBanner, new Color(0.36f, 0.90f, 1.0f, 0.88f), new Vector2(1.2f, -1.2f));

        var ringLabelTxt = CreateTMP(labelBanner.transform, "TargetLabelText", "GRAB PITCHER", 17.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.92f, 0.99f, 1.0f), font);
        SetRect(ringLabelTxt.rectTransform, Vector2.zero, Vector2.one);
        holoGuide.targetRingLabel = ringLabelTxt;

        holoGuide.CacheRenderers();
        holoGuide.AutoResolveAnchors();
        holoGuide.PositionActiveButtonHighlight();
        holoGuide.UpdateLeaderLines(holoGuide.gripButtonAnchor);
        holoGuide.SetAlphaImmediate(1f);

        if (holoGuide.laserPointerLine != null)
        {
            holoGuide.laserPointerLine.gameObject.SetActive(false);
        }

        // ── 3. Dedicated Target Ring Positions (Move These Freely in the Scene View!) ──
        var ringPositionsRoot = new GameObject("Target Ring Positions (Move These in Scene)");
        ringPositionsRoot.transform.SetParent(guideRoot.transform, false);

        var ringPosPitcher = new GameObject("RingPos_1_Pitcher");
        ringPosPitcher.transform.SetParent(ringPositionsRoot.transform, false);
        ringPosPitcher.transform.position = pitcherPos + new Vector3(0.11f, -0.02f, -0.04f);

        var ringPosUIButton = new GameObject("RingPos_2_UIButton");
        ringPosUIButton.transform.SetParent(ringPositionsRoot.transform, false);
        ringPosUIButton.transform.position = new Vector3(-0.08f, 1.38f, 0.74f);

        var ringPosForceps = new GameObject("RingPos_3A_PickupForceps");
        ringPosForceps.transform.SetParent(ringPositionsRoot.transform, false);
        Vector3 forcepsPos = pickupForceps != null ? pickupForceps.transform.position : new Vector3(-0.16f, 1.13f, 0.75f);
        ringPosForceps.transform.position = forcepsPos + new Vector3(0f, 0.06f, -0.02f);

        var ringPosJar = new GameObject("RingPos_3B_IodineJar");
        ringPosJar.transform.SetParent(ringPositionsRoot.transform, false);
        Vector3 jarPos = iodineJarTransform != null ? iodineJarTransform.position : new Vector3(-0.25f, 1.15f, 0.75f);
        ringPosJar.transform.position = jarPos + new Vector3(0f, 0.08f, -0.02f);

        var ringPosTransfer = new GameObject("RingPos_3C_TransferCotton");
        ringPosTransfer.transform.SetParent(ringPositionsRoot.transform, false);
        Vector3 handlingPos = handlingForceps != null ? handlingForceps.transform.position : new Vector3(-0.06f, 1.13f, 0.75f);
        ringPosTransfer.transform.position = handlingPos + new Vector3(0f, 0.06f, -0.02f);

        var ringPosTrash = new GameObject("RingPos_3D_TrashBasin");
        ringPosTrash.transform.SetParent(ringPositionsRoot.transform, false);
        Vector3 binPos = trashCanTransform != null ? trashCanTransform.position : new Vector3(0.59f, 1.05f, 0.60f);
        ringPosTrash.transform.position = binPos + new Vector3(0f, 0.12f, 0f);

        tutorialManager.ringPosition_Pitcher = ringPosPitcher.transform;
        tutorialManager.ringPosition_UIButton = ringPosUIButton.transform;
        tutorialManager.ringPosition_PickupForceps = ringPosForceps.transform;
        tutorialManager.ringPosition_IodineJar = ringPosJar.transform;
        tutorialManager.ringPosition_TransferCotton = ringPosTransfer.transform;
        tutorialManager.ringPosition_TrashBasin = ringPosTrash.transform;

        // ── 4. Step 2 Compact Dark-Glass Sample Button Panel (Independent & Freely Movable in Scene View) ──
        var samplePanelCanvasObj = new GameObject("SampleTutorialButtonCanvas");
        samplePanelCanvasObj.transform.SetParent(guideRoot.transform, false);
        samplePanelCanvasObj.transform.position = new Vector3(-0.08f, 1.38f, 0.74f);
        samplePanelCanvasObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        var spCanvas = samplePanelCanvasObj.AddComponent<Canvas>();
        spCanvas.renderMode = RenderMode.WorldSpace;
        samplePanelCanvasObj.AddComponent<CanvasScaler>();
        samplePanelCanvasObj.AddComponent<GraphicRaycaster>();
        samplePanelCanvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();
        var spCg = samplePanelCanvasObj.AddComponent<CanvasGroup>();

        var spRt = samplePanelCanvasObj.GetComponent<RectTransform>();
        spRt.sizeDelta = new Vector2(340f, 180f);
        spRt.localScale = Vector3.one * 0.0011f;

        var spBg = CreatePanel("SamplePanelCard", samplePanelCanvasObj.transform, Vector2.zero, Vector2.one);
        AddSlicedImage(spBg, uiPanelSprite, new Color(0.01f, 0.03f, 0.07f, 0.78f));
        AddOutline(spBg, new Color(0.36f, 0.90f, 1.0f, 0.85f), new Vector2(1.2f, -1.2f));

        var spFrame = CreatePanel("SF_OverlayFrame", spBg.transform, Vector2.zero, Vector2.one);
        AddSlicedImage(spFrame, sfWindowSprite, new Color(0.36f, 0.90f, 1.0f, 0.40f));

        var spTitle = CreateTMP(spBg.transform, "Header", "UI RAY INTERACTION", 15f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.55f, 0.94f, 1.0f), font);
        SetRect(spTitle.rectTransform, new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.92f));

        var spDesc = CreateTMP(spBg.transform, "Desc", "Point your ray and squeeze <b>[TRIGGER]</b> to click:", 12f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.86f, 0.95f, 1.0f), font);
        SetRect(spDesc.rectTransform, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.70f));

        var spBtnObj = CreateMedicalButton("SampleInteractiveButton", spBg.transform, "PRESS [TRIGGER] TO CONFIRM",
            sfButtonSprite ?? uiPanelSprite, new Color(0.06f, 0.46f, 0.68f, 0.88f), new Color(0.45f, 0.95f, 1.0f, 0.95f), 13f, font);
        SetRect(spBtnObj.GetComponent<RectTransform>(), new Vector2(0.12f, 0.14f), new Vector2(0.88f, 0.40f));

        tutorialManager.sampleButtonPanel = samplePanelCanvasObj;
        tutorialManager.sampleButtonCanvasGroup = spCg;
        tutorialManager.sampleActionButton = spBtnObj.GetComponent<Button>();
        tutorialManager.sampleButtonLabel = spBtnObj.GetComponentInChildren<TextMeshProUGUI>();
        samplePanelCanvasObj.SetActive(false);

        // ── 5A. Standalone Top-Right Skip Button Canvas (Independently Movable in Scene View) ──
        var skipCanvasObj = new GameObject("TutorialSkipCanvas");
        skipCanvasObj.transform.SetParent(guideRoot.transform, false);
        skipCanvasObj.transform.position = new Vector3(-0.42f, 1.66f, 0.74f);
        skipCanvasObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        var skipCanvas = skipCanvasObj.AddComponent<Canvas>();
        skipCanvas.renderMode = RenderMode.WorldSpace;
        skipCanvasObj.AddComponent<CanvasScaler>();
        skipCanvasObj.AddComponent<GraphicRaycaster>();
        skipCanvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

        var skipRt = skipCanvasObj.GetComponent<RectTransform>();
        skipRt.sizeDelta = new Vector2(140f, 36f);
        skipRt.localScale = Vector3.one * 0.0012f;

        var skipBtnObj = CreateMedicalButton("SkipTutorialButton", skipCanvasObj.transform, "Skip Tutorial ⏭",
            uiPanelSprite, new Color(0.02f, 0.05f, 0.10f, 0.68f), new Color(0.36f, 0.88f, 0.96f, 0.70f), 11.5f, font);
        SetRect(skipBtnObj.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        tutorialManager.skipTutorialButton = skipBtnObj.GetComponent<Button>();

        // ── 5B. Step 4 Standalone Completion Card Canvas (Independently Movable in Scene View) ──
        var hudCanvasObj = new GameObject("TutorialCompletionCanvas");
        hudCanvasObj.transform.SetParent(guideRoot.transform, false);
        hudCanvasObj.transform.position = new Vector3(-0.08f, 1.46f, 0.68f);
        hudCanvasObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        var hudCanvas = hudCanvasObj.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.WorldSpace;
        hudCanvasObj.AddComponent<CanvasScaler>();
        hudCanvasObj.AddComponent<GraphicRaycaster>();
        hudCanvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

        var hudRt = hudCanvasObj.GetComponent<RectTransform>();
        hudRt.sizeDelta = new Vector2(520f, 280f);
        hudRt.localScale = Vector3.one * 0.0013f;

        var compObj = CreatePanel("CompletionCard", hudCanvasObj.transform, Vector2.zero, Vector2.one);
        AddSlicedImage(compObj, uiPanelSprite, new Color(0.01f, 0.04f, 0.08f, 0.86f));
        AddOutline(compObj, new Color(0.20f, 0.88f, 0.64f, 0.90f), new Vector2(1.5f, -1.5f));
        tutorialManager.completionCard = compObj;

        var compTitle = CreateTMP(compObj.transform, "Title", "✓ HOLOGRAPHIC TUTORIAL COMPLETE", 18.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.28f, 0.94f, 0.72f), font);
        SetRect(compTitle.rectTransform, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.90f));

        var compDesc = CreateTMP(compObj.transform, "Desc",
            "You have completed:\n• <b>Instrument Grip</b>   • <b>UI Trigger Ray</b>   • <b>Aseptic Cotton Transfer & Disposal</b>",
            12.5f, FontStyles.Normal, TextAlignmentOptions.Center, Color.white, font);
        SetRect(compDesc.rectTransform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.68f));

        var enterBtn = CreateMedicalButton("ProceedButton", compObj.transform, "ENTER DELIVERY ROOM ▶",
            sfButtonSprite ?? uiPanelSprite, new Color(0.08f, 0.58f, 0.44f, 0.92f), new Color(0.34f, 0.96f, 0.78f, 0.95f), 13.5f, font);
        SetRect(enterBtn.GetComponent<RectTransform>(), new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.30f));
        tutorialManager.proceedToMainSceneButton = enterBtn.GetComponent<Button>();
        compObj.SetActive(false);

        EditorSceneManager.SaveScene(scene, TutorialScenePath);
        Debug.Log("[SceneFlow] Built reference-aligned Holographic TutorialScene with Movable Rings at: " + TutorialScenePath);
    }

    // ══════════════════════════════════════════════════════════════════
    // SCENE 3: SUMMARY SCENE
    // ══════════════════════════════════════════════════════════════════
    public static void BuildSummaryScene(TMP_FontAsset font)
    {
        AssetDatabase.CopyAsset(MainScenePath, SummaryScenePath);
        var scene = EditorSceneManager.OpenScene(SummaryScenePath, OpenSceneMode.Single);

        foreach (var root in scene.GetRootGameObjects())
        {
            if (!root.name.Contains("PLAYER"))
            {
                Object.DestroyImmediate(root);
            }
        }

        SetupDarkEnvironment();
        AlignPlayerInScene(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
        SetupEventSystem();

        var canvasObj = new GameObject("SummaryCanvas");
        canvasObj.transform.position = new Vector3(0f, 1.50f, 1.35f);
        canvasObj.transform.rotation = Quaternion.identity;

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();
        canvasObj.AddComponent<VRHeadFollower>();

        var rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800f, 520f);
        rt.localScale = Vector3.one * 0.0020f;

        var summaryScreenRoot = BuildSimulationUI.BuildSummaryScreen(canvasObj.transform, font);
        var controller = summaryScreenRoot.GetComponent<SummaryScreenController>();

        var footer = summaryScreenRoot.transform.Find("FooterBar");
        if (footer != null)
        {
            var sfButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SfButtonSpritePath);
            var menuBtnObj = CreateMedicalButton("Button_MainMenu", footer, "⌂ Main Menu",
                sfButtonSprite, new Color(0.14f, 0.22f, 0.34f, 0.92f), new Color(0.38f, 0.82f, 0.98f, 0.80f), 13f, font);
            SetRect(menuBtnObj.GetComponent<RectTransform>(), new Vector2(0.26f, 0.15f), new Vector2(0.48f, 0.85f));
            if (controller != null) controller.buttonMainMenu = menuBtnObj.GetComponent<Button>();
        }

        summaryScreenRoot.SetActive(true);

        EditorSceneManager.SaveScene(scene, SummaryScenePath);
        Debug.Log("[SceneFlow] Built SummaryScene at: " + SummaryScenePath);
    }

    // ══════════════════════════════════════════════════════════════════
    // BUILD SETTINGS REGISTRATION
    // ══════════════════════════════════════════════════════════════════
    public static void UpdateEditorBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(TutorialScenePath, true),
            new EditorBuildSettingsScene(MainScenePath, true),
            new EditorBuildSettingsScene(SummaryScenePath, true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[SceneFlow] Updated EditorBuildSettings with 4 scenes: Menu, Tutorial, Main, Summary.");
    }

    // ══════════════════════════════════════════════════════════════════
    // UI & SCENE HELPERS
    // ══════════════════════════════════════════════════════════════════

    private static void CreateModuleInfoCard(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string badge, string desc, Sprite bgSprite, TMP_FontAsset font)
    {
        var card = CreatePanel(name, parent, anchorMin, anchorMax);
        AddSlicedImage(card, bgSprite, new Color(0.04f, 0.11f, 0.19f, 0.82f));
        AddOutline(card, new Color(0.24f, 0.74f, 0.94f, 0.45f), new Vector2(1f, -1f));

        var bTxt = CreateTMP(card.transform, "ModuleBadge", badge, 10.5f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.20f, 0.88f, 0.70f), font);
        SetRect(bTxt.rectTransform, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.92f));

        var dTxt = CreateTMP(card.transform, "ModuleDesc", desc, 11.5f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.84f, 0.94f, 1.0f), font);
        SetRect(dTxt.rectTransform, new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.62f));
    }

    private static GameObject CreateMedicalButton(string name, Transform parent, string label, Sprite slicedSprite, Color bgColor, Color outlineColor, float fontSize, TMP_FontAsset font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = AddSlicedImage(go, slicedSprite, bgColor);
        AddOutline(go, outlineColor, new Vector2(1.5f, -1.5f));

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.80f, 0.92f, 1.0f, 1f);
        btn.colors = colors;

        var txt = CreateTMP(go.transform, "Text", label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
        SetRect(txt.rectTransform, Vector2.zero, Vector2.one);

        return go;
    }

    private static Image AddSlicedImage(GameObject go, Sprite sprite, Color color)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    private static void SetupDarkEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.04f, 0.04f, 0.06f);

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.02f, 0.04f, 1f);
        }

        EnsureSceneFader();
    }

    private static void EnsureSceneFader()
    {
        var fader = Object.FindFirstObjectByType<VRSceneTransitionFader>();
        if (fader == null)
        {
            var faderObj = new GameObject("VRSceneTransitionFader");
            faderObj.AddComponent<VRSceneTransitionFader>();
        }
    }

    private static void AlignPlayerInScene(Vector3 position, Vector3 eulerRotation)
    {
        var aligner = Object.FindFirstObjectByType<XRPlayerSpawnAligner>();
        if (aligner != null)
        {
            aligner.transform.position = position;
            aligner.transform.rotation = Quaternion.Euler(eulerRotation);
            aligner.eyeHeight = 1.546f;
            aligner.heightOffset = 0f;
            aligner.spawnAtCurrentScenePosition = true;
            if (aligner.customSpawnPoint != null)
            {
                aligner.customSpawnPoint.position = new Vector3(position.x, 1.546f, position.z);
                aligner.customSpawnPoint.rotation = Quaternion.Euler(eulerRotation);
            }
        }
    }

    private static void SetupEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    private static Material GetOrCreateHologramMaterial(string path, Color baseColor, Color edgeColor, Vector4 edgeData)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(FresnelShaderName)
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Sprites/Default");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetColor("_Color", baseColor);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_EdgeColor", edgeColor);
        mat.SetVector("_EdgeData", edgeData);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void AddHolographicTextShadow(GameObject go)
    {
        var sh = go.GetComponent<Shadow>() ?? go.AddComponent<Shadow>();
        sh.effectColor = new Color(0.01f, 0.04f, 0.09f, 0.88f);
        sh.effectDistance = new Vector2(1.4f, -1.4f);
    }

    private static Material GetOrCreateLineMaterial(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = new Color(0.50f, 0.94f, 1.0f, 0.92f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static LineRenderer CreateHolographicLine(Transform parent, string name, Material mat, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = mat;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 2;
        return lr;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (var tr in parent.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name == name) return tr;
        }
        return null;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float size, FontStyles style, TextAlignmentOptions align, Color color, TMP_FontAsset font)
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
#endif
