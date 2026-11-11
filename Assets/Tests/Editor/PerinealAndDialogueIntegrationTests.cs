using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PerinealAndDialogueIntegrationTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    private T Track<T>(T obj) where T : Object
    {
        createdObjects.Add(obj);
        return obj;
    }

    [TearDown]
    public void TearDown()
    {
        DraggablePhraseChip.CancelAnyActiveDrag();
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
        CottonState.activeCottons.Clear();

        typeof(PhraseDecisionMatrix).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, null);
        typeof(StrokeTrackingManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, null);
        typeof(VRDemoGameManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        typeof(TablePrepManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        typeof(SummaryScreenController).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        typeof(VRHeadsetVisualFeedback).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        typeof(VRDemoGameManager).GetProperty("LastCompletedRecord", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        typeof(VRDemoGameManager).GetProperty("LastDebrief", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        typeof(VRDemoGameManager).GetProperty("LastRubric", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        EvaluationLogger.Instance.Bind(null);
        EvaluationLogger.Instance.ResetSession();
    }

    private (Canvas canvas, RectTransform bank, PhraseDropSlot slot, PhraseDecisionMatrix matrix, List<DraggablePhraseChip> chips) BuildDialogueRig()
    {
        var canvasGo = Track(new GameObject("TestDialogueCanvas", typeof(RectTransform), typeof(Canvas)));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var bankGo = Track(new GameObject("TestChoicesBank", typeof(RectTransform)));
        bankGo.transform.SetParent(canvasGo.transform, false);

        var slotGo = Track(new GameObject("TestIntroSlot", typeof(RectTransform), typeof(PhraseDropSlot)));
        slotGo.transform.SetParent(canvasGo.transform, false);
        var slot = slotGo.GetComponent<PhraseDropSlot>();
        slot.targetCategory = PhraseSlotCategory.Introduction;
        slot.chipContainer = slotGo.GetComponent<RectTransform>();

        var matrixGo = Track(new GameObject("TestMatrix", typeof(PhraseDecisionMatrix)));
        matrixGo.transform.SetParent(canvasGo.transform, false);
        var matrix = matrixGo.GetComponent<PhraseDecisionMatrix>();
        matrix.introSlot = slot;
        typeof(PhraseDecisionMatrix).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
            ?.SetValue(null, matrix);

        var chips = new List<DraggablePhraseChip>();
        for (int i = 0; i < 3; i++)
        {
            var token = Track(ScriptableObject.CreateInstance<DialoguePhraseTokenSO>());
            token.category = PhraseSlotCategory.Introduction;
            token.fullPhraseText = $"Test intro {i + 1}";

            var chipGo = Track(new GameObject($"Chip_{i}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button), typeof(DraggablePhraseChip)));
            chipGo.transform.SetParent(bankGo.transform, false);

            var labelGo = Track(new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI)));
            labelGo.transform.SetParent(chipGo.transform, false);
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.raycastTarget = true;

            var chip = chipGo.GetComponent<DraggablePhraseChip>();
            chip.labelText = tmp;
            chip.Initialize(token, i + 1, bankGo.transform);
            chips.Add(chip);
        }

        return (canvas, bankGo.GetComponent<RectTransform>(), slot, matrix, chips);
    }

    private (StrokeTrackingManager manager, StrokeSliderTrack[] tracks) BuildStrokeRig()
    {
        var managerGo = Track(new GameObject("TestStrokeManager", typeof(StrokeTrackingManager)));
        var manager = managerGo.GetComponent<StrokeTrackingManager>();
        manager.mistakeLockoutDuration = 0f;

        var tracks = new StrokeSliderTrack[9];
        for (int i = 0; i < 9; i++)
        {
            var tGo = Track(new GameObject($"StrokeSlider_{i + 1}", typeof(StrokeSliderTrack)));
            var startGo = Track(new GameObject("StartPoint"));
            var endGo = Track(new GameObject("EndPoint"));
            var beadGo = Track(new GameObject("HandleBead"));
            var trackLineGo = Track(new GameObject("TrackLine", typeof(LineRenderer)));
            var fillLineGo = Track(new GameObject("FillLine", typeof(LineRenderer)));

            startGo.transform.SetParent(tGo.transform, false);
            endGo.transform.SetParent(tGo.transform, false);
            beadGo.transform.SetParent(tGo.transform, false);
            trackLineGo.transform.SetParent(tGo.transform, false);
            fillLineGo.transform.SetParent(tGo.transform, false);

            // Space tracks cleanly apart so zones never overlap in X
            startGo.transform.position = new Vector3(i * 0.25f, 0.10f, 0f);
            endGo.transform.position = new Vector3(i * 0.25f, 0.00f, 0f);

            var track = tGo.GetComponent<StrokeSliderTrack>();
            track.strokeIndex = i;
            track.strokeName = $"Stroke {i + 1}";
            track.isTapOnly = (i == 8);
            track.isBidirectional = (i == 1);
            track.startPointTransform = startGo.transform;
            track.endPointTransform = endGo.transform;
            track.startNode = startGo;
            track.endNode = endGo;
            track.handleBead = beadGo;
            track.trackLine = trackLineGo.GetComponent<LineRenderer>();
            track.fillLine = fillLineGo.GetComponent<LineRenderer>();
            tracks[i] = track;
        }

        manager.sliderTracks = tracks;
        return (manager, tracks);
    }

    private CottonState CreateCotton(ForcepsRole role = ForcepsRole.Handling)
    {
        var cGo = Track(new GameObject("TestCotton", typeof(CottonState)));
        var c = cGo.GetComponent<CottonState>();
        c.isSoaked = true;
        c.isUsed = false;
        c.currentHolder = role;
        return c;
    }

    [Test]
    public void DialogueChips_InterruptedCancelAndBankSwap_NeverLeavePanelStuck()
    {
        var (canvas, bank, slot, matrix, chips) = BuildDialogueRig();
        var eventData = new PointerEventData(null);

        Assert.IsFalse(chips[0].labelText.raycastTarget);
        Assert.AreEqual(Navigation.Mode.None, chips[0].GetComponent<Button>().navigation.mode);

        // Repeated drag + abrupt disable recovery
        for (int round = 0; round < 3; round++)
        {
            chips[0].OnBeginDrag(eventData);
            Assert.IsTrue(chips[0].IsDragging);
            Assert.IsFalse(chips[0].ChipCanvasGroup.blocksRaycasts);
            Assert.AreEqual(canvas.transform, chips[0].transform.parent);

            chips[0].gameObject.SetActive(false);
            Assert.IsFalse(chips[0].IsDragging);
            Assert.IsTrue(chips[0].ChipCanvasGroup.blocksRaycasts);
            Assert.AreEqual(bank, chips[0].transform.parent);
            chips[0].gameObject.SetActive(true);
        }

        // Starting drag on chip 1 auto-cancels active drag on chip 0
        chips[0].OnBeginDrag(eventData);
        chips[1].OnBeginDrag(eventData);
        Assert.IsFalse(chips[0].IsDragging);
        Assert.IsTrue(chips[0].ChipCanvasGroup.blocksRaycasts);
        Assert.AreEqual(bank, chips[0].transform.parent);

        // Case 3 bank swap while chip 1 is dragged onto chip 2
        int rootCountBefore = canvas.transform.childCount;
        chips[2].OnDrop(new PointerEventData(null) { pointerDrag = chips[1].gameObject });
        chips[1].OnEndDrag(eventData);

        Assert.AreEqual(bank, chips[1].transform.parent);
        Assert.AreEqual(bank, chips[2].transform.parent);
        Assert.IsTrue(chips[1].ChipCanvasGroup.blocksRaycasts);
        Assert.IsTrue(chips[2].ChipCanvasGroup.blocksRaycasts);
        Assert.AreEqual(rootCountBefore - 1, canvas.transform.childCount, "No chip may stay orphaned on root DialogueCanvas.");

        // Slot accept + ClearFormulation during active drag
        slot.AcceptChip(chips[0]);
        Assert.AreEqual(slot, chips[0].currentSlot);
        chips[1].OnBeginDrag(eventData);
        matrix.ClearFormulation();
        Assert.IsNull(chips[0].currentSlot);
        Assert.IsFalse(chips[1].IsDragging);
        Assert.IsTrue(chips[1].ChipCanvasGroup.blocksRaycasts);
        Assert.AreEqual(bank, chips[1].transform.parent);
    }

    [Test]
    public void StrokeTrackingManager_AlternatePairedOrderAndMonsPubisFix_WorkEndToEnd()
    {
        var (manager, tracks) = BuildStrokeRig();
        manager.ResetForPhase(AntisepticType.Iodine_7_5_Scrub);

        void CompleteDirectional(int zone, bool reverse = false)
        {
            var c = CreateCotton();
            Vector3 a = tracks[zone].GetStartWorldPosition();
            Vector3 b = tracks[zone].GetEndWorldPosition();
            if (reverse) (a, b) = (b, a);

            float[] ts = { 0.10f, 0.35f, 0.55f, 0.78f, 0.92f, 0.96f, 0.99f };
            foreach (float t in ts)
            {
                if (manager.IsStrokeCompleted(zone)) break;
                manager.ProcessCottonTouch(c, zone, Vector3.Lerp(a, b, t));
                if (!manager.IsStrokeCompleted(zone) && t >= 0.92f)
                {
                    for (int f = 0; f < 30 && !manager.IsStrokeCompleted(zone); f++)
                    {
                        manager.ProcessCottonTouch(c, zone, Vector3.Lerp(a, b, 0.96f));
                    }
                }
            }
            Assert.IsTrue(manager.IsStrokeCompleted(zone), $"Zone {zone} should have completed.");
            Assert.IsTrue(c.isUsed);
        }

        // Stage 0: [0] Right Labia Majora
        CompleteDirectional(0);
        Assert.AreEqual(1, manager.GetCurrentStageIndex());

        // Stage 1: Mons Pubis tap at Start must NOT complete
        var tapCotton = CreateCotton();
        Vector3 monsStart = tracks[1].GetStartWorldPosition();
        for (int f = 0; f < 25; f++)
        {
            manager.ProcessCottonTouch(tapCotton, 1, Vector3.Lerp(monsStart, tracks[1].GetEndWorldPosition(), 0.15f));
        }
        Assert.IsFalse(manager.IsStrokeCompleted(1), "Tapping Mons Pubis start must never prematurely complete.");

        // Complete Mons Pubis in reverse (End -> Start)
        CompleteDirectional(1, reverse: true);
        Assert.AreEqual(2, manager.GetCurrentStageIndex());

        // Stage 2: complete [3] BEFORE [2], verify IsStrokeCompleted tracks exact zones
        CompleteDirectional(3);
        Assert.IsTrue(manager.IsStrokeCompleted(3));
        Assert.IsFalse(manager.IsStrokeCompleted(2));
        Assert.IsFalse(manager.IsStrokeValidNow(4));
        CompleteDirectional(2);

        // Stage 3: [5] then [4]
        CompleteDirectional(5);
        Assert.IsTrue(manager.IsStrokeCompleted(5));
        Assert.IsFalse(manager.IsStrokeCompleted(4));
        CompleteDirectional(4);

        // Stage 4: [7] then [6]
        CompleteDirectional(7);
        Assert.IsFalse(manager.IsStrokeValidNow(8));
        CompleteDirectional(6);

        // Stage 5: [8] Anus tap completion last
        Assert.IsTrue(manager.IsStrokeValidNow(8));
        var anusCotton = CreateCotton();
        manager.ProcessCottonTouch(anusCotton, 8, tracks[8].GetStartWorldPosition());
        Assert.IsTrue(manager.IsStrokeCompleted(8));
        Assert.AreEqual(9, manager.GetCompletedStrokeCount());
    }

    [Test]
    public void VisualGuideAndUI_ShowWarmGoldHintsAfter3MistakesAndExactPairedDots()
    {
        var (manager, tracks) = BuildStrokeRig();
        manager.ResetForPhase(AntisepticType.Iodine_7_5_Scrub);

        // Hold an unused cotton in Handling forceps so visual guide activates
        CreateCotton(ForcepsRole.Handling);

        var guideGo = Track(new GameObject("TestVisualGuide", typeof(StrokeAreaVisualGuide)));
        var guide = guideGo.GetComponent<StrokeAreaVisualGuide>();
        typeof(StrokeAreaVisualGuide).GetField("trackingManager", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(guide, manager);
        guide.sliders = tracks;
        guide.hideGuidesUnlessError = true;
        guide.mistakesRequiredForHint = 3;

        // Wire StrokeGuideUI step dots to verify exact paired zone display
        var uiGo = Track(new GameObject("TestGuideUI", typeof(StrokeGuideUI)));
        var guideUI = uiGo.GetComponent<StrokeGuideUI>();
        typeof(StrokeGuideUI).GetField("trackingManager", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(guideUI, manager);
        guideUI.stepDotImages = new Image[9];
        guideUI.stepDotTexts = new TMP_Text[9];
        for (int i = 0; i < 9; i++)
        {
            var dotGo = Track(new GameObject($"Dot_{i}", typeof(RectTransform), typeof(Image)));
            var txtGo = Track(new GameObject($"DotTxt_{i}", typeof(RectTransform), typeof(TextMeshProUGUI)));
            guideUI.stepDotImages[i] = dotGo.GetComponent<Image>();
            guideUI.stepDotTexts[i] = txtGo.GetComponent<TextMeshProUGUI>();
        }

        // Before 3 mistakes: all tracks show subtle Upcoming blueprint
        guide.RefreshVisualsImmediate();
        Assert.AreEqual(SliderVisualState.Upcoming, tracks[0].CurrentVisualState);
        Assert.IsFalse(tracks[0].IsShowingHintTracer);

        // Trigger 3 out-of-order mistakes -> stage 0 ([0]) pulses Warm Gold & shows tracer, others Dimmed
        var mistakeCotton = CreateCotton();
        Vector3 wrongPos = tracks[4].GetStartWorldPosition();
        manager.ProcessCottonTouch(mistakeCotton, 4, wrongPos);
        manager.ProcessCottonTouch(mistakeCotton, 4, wrongPos);
        manager.ProcessCottonTouch(mistakeCotton, 4, wrongPos);

        guide.RefreshVisualsImmediate();
        Assert.AreEqual(3, manager.GetActiveStrokeMistakeCount());
        Assert.IsTrue(tracks[0].IsShowingHintTracer, "Target zone 0 must activate Warm-Gold hint tracer after 3 mistakes.");
        Assert.AreEqual(SliderVisualState.Dimmed, tracks[1].CurrentVisualState, "Non-target upcoming zone must dim while hint displays.");

        // Advance cleanly to paired Stage 2 and finish zone 3 before zone 2
        void FinishZone(int z)
        {
            var fc = CreateCotton();
            Vector3 s = tracks[z].GetStartWorldPosition();
            Vector3 e = tracks[z].GetEndWorldPosition();
            manager.ProcessCottonTouch(fc, z, Vector3.Lerp(s, e, 0.1f));
            manager.ProcessCottonTouch(fc, z, Vector3.Lerp(s, e, 0.5f));
            for (int f = 0; f < 30 && !manager.IsStrokeCompleted(z); f++)
            {
                manager.ProcessCottonTouch(fc, z, Vector3.Lerp(s, e, 0.96f));
            }
            Assert.IsTrue(manager.IsStrokeCompleted(z));
        }

        FinishZone(0);
        FinishZone(1);
        FinishZone(3); // Complete [3] before [2]

        guideUI.RefreshUI();
        Assert.AreEqual("<b>✓</b>", guideUI.stepDotTexts[3].text, "Completed zone 3 dot must show checkmark.");
        Assert.AreEqual("<b>3</b>", guideUI.stepDotTexts[2].text, "Remaining valid paired zone 2 dot must remain active, never falsely completed.");
    }

    private ToolItem CreateTestTool(string goName, string toolId, Vector3 worldPos)
    {
        var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        go.name = goName;
        go.transform.position = worldPos;
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        var grab = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        Assert.IsNotNull(grab);
        var tool = go.AddComponent<ToolItem>();
        tool.toolID = toolId;
        tool.CacheHomeTransformsNow();
        return tool;
    }

    [Test]
    public void TablePrepSlot_And_TablePrepManager_EnforceStrictValidationAndRegrabClearing()
    {
        var managerGo = Track(new GameObject("TestTablePrepManager", typeof(TablePrepManager)));
        var prepManager = managerGo.GetComponent<TablePrepManager>();
        prepManager.isMayoDraped = true;
        prepManager.isTablePrepActive = true;

        var slot1Go = Track(new GameObject("Slot_1", typeof(TablePrepSlot)));
        slot1Go.transform.position = new Vector3(0f, 1f, 0f);
        var slot1 = slot1Go.GetComponent<TablePrepSlot>();
        slot1.slotNumber = 1;
        slot1.snapRadius = 0.25f;

        var slot4Go = Track(new GameObject("Slot_4", typeof(TablePrepSlot)));
        slot4Go.transform.position = new Vector3(1f, 1f, 0f);
        var slot4 = slot4Go.GetComponent<TablePrepSlot>();
        slot4.slotNumber = 4;
        slot4.snapRadius = 0.25f;

        var customRefGo = Track(new GameObject("Kidney_Basin (1)"));
        customRefGo.transform.SetParent(slot4Go.transform, false);
        customRefGo.transform.position = new Vector3(1.03f, 1.02f, -0.02f);
        customRefGo.transform.rotation = Quaternion.Euler(0f, 90f, 270f);
        customRefGo.SetActive(false); // Confirm inactive visual reference transform still drives exact snap pose
        slot4.customSnapPoint = customRefGo.transform;

        ToolItem forceps = CreateTestTool("Forceps", "Artery_Forcep", new Vector3(-1f, 1f, 0f));
        ToolItem tongs = CreateTestTool("Tongs", "Thumb_Forceps", slot1Go.transform.position);
        ToolItem kidney = CreateTestTool("Kidney_Basin", "KidneyBasin_01", slot4Go.transform.position);

        var linenGo = Track(new GameObject("Dry_Linen", typeof(BoxCollider), typeof(Rigidbody), typeof(ToolItem), typeof(TwoHandedLinenCloth)));
        var linenTool = linenGo.GetComponent<ToolItem>();
        linenTool.toolID = "DryLinen_01";

        slot1.assignedTool = forceps;
        slot1.assignedToolID = "Forceps";
        slot4.assignedTool = kidney;
        slot4.assignedToolID = "Kidney_Basin";
        prepManager.slots = new List<TablePrepSlot> { slot1, slot4 };

        // Exact validation checks (must reject wrong forceps and linen drape)
        Assert.IsTrue(slot1.CanAccept(forceps));
        Assert.IsFalse(slot1.CanAccept(tongs), "Wrong forceps instrument must never pass slot CanAccept.");
        Assert.IsFalse(slot1.CanAccept(linenTool), "Linen cloth must be blocked from instrument slots.");

        // Release wrong tool inside Slot 1 radius -> Option 2A must reject without snapping
        Vector3 wrongPosBefore = tongs.transform.position;
        bool snappedWrong = prepManager.TryHandleToolRelease(tongs);
        Assert.IsFalse(snappedWrong, "Wrong instrument inside Slot 1 trigger must not snap.");
        Assert.IsFalse(slot1.isFilled);
        Assert.IsNull(slot1.currentPlacedTool);
        Assert.Less(Vector3.Distance(tongs.transform.position, wrongPosBefore), 0.001f);

        // Release correct Kidney Basin inside Slot 4 -> must match inactive customSnapPoint (1) pose exactly
        bool snappedCorrect = prepManager.TryHandleToolRelease(kidney);
        Assert.IsTrue(snappedCorrect, "Assigned Kidney Basin inside Slot 4 must snap.");
        Assert.IsTrue(slot4.isFilled);
        Assert.AreEqual(kidney, slot4.currentPlacedTool);
        Assert.Less(Vector3.Distance(kidney.transform.position, customRefGo.transform.position), 0.001f);
        Assert.Less(Quaternion.Angle(kidney.transform.rotation, customRefGo.transform.rotation), 0.5f);

        // Re-grabbing the placed tool clears the slot immediately
        prepManager.NotifyToolPickedUp(kidney);
        Assert.IsFalse(slot4.isFilled, "Re-grabbing a placed tool must clear its slot.");
        Assert.IsNull(slot4.currentPlacedTool);
    }

    [Test]
    public void CommunicationDebriefData_PreservesNonBlockingEvaluationsAndRecommendedAnswers()
    {
        DialoguePhraseTokenSO CreateToken(PhraseSlotCategory cat, string text, float score, string critique)
        {
            var tok = Track(ScriptableObject.CreateInstance<DialoguePhraseTokenSO>());
            tok.category = cat;
            tok.fullPhraseText = text;
            tok.professionalism = score;
            tok.empathy = score;
            tok.clarity = score;
            tok.clinicalCritiqueNote = critique;
            return tok;
        }

        var introPoor = CreateToken(PhraseSlotCategory.Introduction, "I am going to start now.", 1.5f, "");
        var identPoor = CreateToken(PhraseSlotCategory.PatientIdentification, "Are you ready?", 1.5f, "");
        var explainPoor = CreateToken(PhraseSlotCategory.ProcedureExplanation, "Hold still.", 1.0f, "");

        var introBest = CreateToken(PhraseSlotCategory.Introduction, "Good morning, I am Nurse Elena, caring for you today.", 5.0f, "Introduce your name and clinical role.");
        var identBest = CreateToken(PhraseSlotCategory.PatientIdentification, "May I verify your full name and date of birth?", 5.0f, "Verify two patient identifiers.");
        var explainBest = CreateToken(PhraseSlotCategory.ProcedureExplanation, "I will wash your perineal area gently with warm water.", 5.0f, "Explain each step and expected sensations.");

        var allTokens = new[] { introPoor, identPoor, explainPoor, introBest, identBest, explainBest };
        CommunicationDebriefData debrief = CommunicationDebriefData.CreateFromEvaluation(
            30f, 30f, 20f, 27f, introPoor, identPoor, explainPoor, allTokens, deductionApplied: 10);

        Assert.Less(debrief.compositeScore, 50f);
        Assert.AreEqual(10, debrief.dialogueDeduction);
        Assert.AreEqual(3, debrief.decisionEntries.Count);

        for (int i = 0; i < debrief.decisionEntries.Count; i++)
        {
            Assert.IsTrue(debrief.decisionEntries[i].isSuboptimalOrWrong);
        }
        Assert.AreEqual(introBest.fullPhraseText, debrief.decisionEntries[0].recommendedPhrase);
        Assert.AreEqual(identBest.fullPhraseText, debrief.decisionEntries[1].recommendedPhrase);
        Assert.AreEqual(explainBest.fullPhraseText, debrief.decisionEntries[2].recommendedPhrase);
        Assert.AreEqual("Introduce your name and clinical role.", debrief.decisionEntries[0].clinicalRationale);
    }

    [Test]
    public void GameManager_CottonBallAndToolDropTiers_ComputeExactDeductionLedger()
    {
        var gmGo = Track(new GameObject("TestGameManager", typeof(VRDemoGameManager)));
        var gm = gmGo.GetComponent<VRDemoGameManager>();
        typeof(VRDemoGameManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { gm });
        gm.scoringRubric = Track(ScriptableObject.CreateInstance<SimulationScoringRubricSO>());
        gm.InitializeSimulation();

        // Cotton ball tiers: 0->0, 1-2->5, 3-5->10, >5->15 with per-cotton ID dedup
        Assert.AreEqual(0, gm.CottonBallDeduction);
        gm.RecordCottonBallMistake(1001, ClinicalViolationType.Reused_Cotton_Ball, "Reused cotton 1001");
        gm.RecordCottonBallMistake(1001, ClinicalViolationType.Contaminated_Drop_Outside_Basin, "Dropped cotton 1001");
        Assert.AreEqual(1, gm.AffectedCottonBallCount, "Multiple mistakes on the same cotton ball ID must count only once.");
        Assert.AreEqual(5, gm.CottonBallDeduction);

        gm.RecordCottonBallMistake(1002, ClinicalViolationType.Reused_Cotton_Ball, "Reused cotton 1002");
        Assert.AreEqual(2, gm.AffectedCottonBallCount);
        Assert.AreEqual(5, gm.CottonBallDeduction);

        gm.RecordCottonBallMistake(1003, ClinicalViolationType.Contaminated_Drop_Outside_Basin, "Dropped cotton 1003");
        Assert.AreEqual(3, gm.AffectedCottonBallCount);
        Assert.AreEqual(10, gm.CottonBallDeduction);

        gm.RecordCottonBallMistake(1004, ClinicalViolationType.Reused_Cotton_Ball, "Reused cotton 1004");
        gm.RecordCottonBallMistake(1005, ClinicalViolationType.Reused_Cotton_Ball, "Reused cotton 1005");
        Assert.AreEqual(10, gm.CottonBallDeduction);

        gm.RecordCottonBallMistake(1006, ClinicalViolationType.Contaminated_Drop_Outside_Basin, "Dropped cotton 1006");
        gm.RecordCottonBallMistake(1007, ClinicalViolationType.Contaminated_Drop_Outside_Basin, "Dropped cotton 1007");
        Assert.AreEqual(7, gm.AffectedCottonBallCount);
        Assert.AreEqual(15, gm.CottonBallDeduction, "Cotton ball deduction must cap at -15.");

        // Tool drop Option 9B tiers: 0->0, 1->5, 2-3->10, >=4->15
        gm.RecordDropPenalty("Pitcher_01");
        Assert.AreEqual(1, gm.TotalToolDropCount);
        Assert.AreEqual(5, gm.ToolDropDeduction);
        Assert.AreEqual(1, gm.ToolDropCounts["Pitcher"]);

        gm.RecordDropPenalty("Artery_Forcep");
        Assert.AreEqual(2, gm.TotalToolDropCount);
        Assert.AreEqual(10, gm.ToolDropDeduction);

        gm.RecordDropPenalty("Straight_Catheter");
        Assert.AreEqual(3, gm.TotalToolDropCount);
        Assert.AreEqual(10, gm.ToolDropDeduction);

        gm.RecordDropPenalty("Pitcher_01");
        gm.RecordDropPenalty("KidneyBasin_01");
        Assert.AreEqual(5, gm.TotalToolDropCount);
        Assert.AreEqual(15, gm.ToolDropDeduction, "Tool drop deduction must cap at -15 under Option 9B.");
        Assert.AreEqual(2, gm.ToolDropCounts["Pitcher"]);

        Assert.AreEqual(100 - gm.TotalDeductions, gm.currentScore, "Current score must equal 100 - TotalDeductions.");
        Assert.AreEqual(70, gm.currentScore);
    }

    [Test]
    public void PitcherPour_ClassifyStep2Collider_StrictlySeparatesPerineumFromLegsAndAbdomen()
    {
        var motherRoot = Track(new GameObject("Mother"));
        var perinealZoneGo = Track(new GameObject("Perineal_Wash_Zone", typeof(BoxCollider)));
        perinealZoneGo.transform.SetParent(motherRoot.transform, false);
        var perinealCol = perinealZoneGo.GetComponent<Collider>();

        var leftLegGo = Track(new GameObject("3. Left Leg", typeof(BoxCollider)));
        leftLegGo.transform.SetParent(motherRoot.transform, false);
        var leftLegCol = leftLegGo.GetComponent<Collider>();

        var abdominalGo = Track(new GameObject("2. Abdominal", typeof(BoxCollider)));
        abdominalGo.transform.SetParent(motherRoot.transform, false);
        var abdominalCol = abdominalGo.GetComponent<Collider>();

        PitcherPour.ClassifyStep2Collider(perinealCol, perinealCol, out bool perinealHit, out bool wrongMotherHit);
        Assert.IsTrue(perinealHit);
        Assert.IsFalse(wrongMotherHit);

        PitcherPour.ClassifyStep2Collider(leftLegCol, perinealCol, out bool legPerinealHit, out bool legWrongMotherHit);
        Assert.IsFalse(legPerinealHit, "Left leg must never classify as valid perineal washing area.");
        Assert.IsTrue(legWrongMotherHit, "Left leg must register as non-perineal mother contact in Step 2.");

        PitcherPour.ClassifyStep2Collider(abdominalCol, perinealCol, out bool abdPerinealHit, out bool abdWrongMotherHit);
        Assert.IsFalse(abdPerinealHit);
        Assert.IsTrue(abdWrongMotherHit);

        perinealZoneGo.AddComponent<PerinealWashZoneGizmo>();
        PitcherPour.ClassifyStep2Collider(perinealCol, null, out bool gizmoPerinealHit, out bool gizmoWrongMotherHit);
        Assert.IsTrue(gizmoPerinealHit, "PerinealWashZoneGizmo-tagged collider must classify as valid perineal area.");
        Assert.IsFalse(gizmoWrongMotherHit);

        string[] outerZoneNames =
        {
            "Puddle_7_5_StrokeZone_5_RightInnerThigh",
            "Coating_10_StrokeZone_6_LeftInnerThigh",
            "StrokeZone_7_LeftButtock",
            "Coating_10_StrokeZone_8_RightButtock"
        };

        foreach (string zoneName in outerZoneNames)
        {
            var outerGo = Track(new GameObject(zoneName, typeof(BoxCollider)));
            outerGo.transform.SetParent(motherRoot.transform, false);
            var outerCol = outerGo.GetComponent<Collider>();

            PitcherPour.ClassifyStep2Collider(outerCol, perinealCol, out bool outerPerineal, out bool outerWrongMother);
            Assert.IsFalse(outerPerineal, $"{zoneName} must NOT count as valid Step 2 perineal washing.");
            Assert.IsTrue(outerWrongMother, $"{zoneName} must be classified as non-perineal mother contact in Step 2.");
        }
    }

    [Test]
    public void ForcepsController_PickupRole_HoldButtonClampsAndReleaseDrops()
    {
        var xrManagerGo = Track(new GameObject("TestXRManager", typeof(XRInteractionManager)));
        var xrManager = xrManagerGo.GetComponent<XRInteractionManager>();

        var forcepsGo = Track(new GameObject("Pickup Forceps", typeof(Rigidbody), typeof(BoxCollider), typeof(XRGrabInteractable), typeof(ForcepsController)));
        var socketGo = Track(new GameObject("CottonSocket", typeof(XRSocketInteractor)));
        socketGo.transform.SetParent(forcepsGo.transform, false);

        var forcepsGrab = forcepsGo.GetComponent<XRGrabInteractable>();
        var cottonSocket = socketGo.GetComponent<XRSocketInteractor>();
        cottonSocket.interactionManager = xrManager;
        cottonSocket.socketActive = true;

        var forceps = forcepsGo.GetComponent<ForcepsController>();
        forceps.role = ForcepsRole.Pickup;
        forceps.cottonSocket = cottonSocket;
        forceps.forcepsGrabInteractable = forcepsGrab;

        var cottonGo = Track(new GameObject("TestCotton", typeof(Rigidbody), typeof(SphereCollider), typeof(XRGrabInteractable), typeof(CottonState)));
        var cottonGrab = cottonGo.GetComponent<XRGrabInteractable>();
        cottonGrab.interactionManager = xrManager;

        var gamepad = InputSystem.AddDevice<Gamepad>("TestForcepsGamepad");
        var primaryAction = new InputAction("TestPrimaryButton", InputActionType.Button, "<Gamepad>/buttonSouth");
        var primaryActionRef = Track(InputActionReference.Create(primaryAction));
        forceps.rightPrimaryButton = primaryActionRef;
        primaryAction.Enable();

        try
        {
            xrManager.SelectEnter((IXRSelectInteractor)cottonSocket, (IXRSelectInteractable)cottonGrab);
            Assert.IsTrue(forceps.HasCotton, "Precondition: cotton must initially be socketed in Pickup Forceps.");

            typeof(ForcepsController).GetField("isHeldByRightHand", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(forceps, true);
            MethodInfo updateMethod = typeof(ForcepsController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(updateMethod);

            // While primary button is held, Pickup Forceps retains clamped cotton (matching Handling Forceps).
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South, true));
            InputSystem.Update();
            Assert.IsTrue(primaryAction.IsPressed());

            updateMethod.Invoke(forceps, null);
            Assert.IsTrue(forceps.HasCotton, "Pickup Forceps must retain cotton while holding the primary button.");
            Assert.IsTrue(cottonSocket.socketActive);

            // Upon releasing primary button, Pickup Forceps drops the cotton ball and deactivates socket.
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South, false));
            InputSystem.Update();
            Assert.IsFalse(primaryAction.IsPressed());

            updateMethod.Invoke(forceps, null);
            Assert.IsFalse(forceps.HasCotton, "Pickup Forceps must drop cotton when primary button is released.");
            Assert.IsFalse(cottonSocket.hasSelection);
            Assert.IsFalse(cottonSocket.socketActive);
        }
        finally
        {
            primaryAction.Disable();
            primaryAction.Dispose();
            if (gamepad != null && gamepad.added)
            {
                InputSystem.RemoveDevice(gamepad);
            }
        }
    }

    [Test]
    public void SummaryScreenController_BuildsAndPopulatesOverviewAndAllStagesWithoutNulls()
    {
        var canvasGo = Track(new GameObject("TestSummaryCanvas", typeof(RectTransform), typeof(Canvas)));
        var rootGo = Track(BuildSimulationUI.BuildSummaryScreen(canvasGo.transform, null));
        Assert.IsNotNull(rootGo);

        var controller = rootGo.GetComponent<SummaryScreenController>();
        Assert.IsNotNull(controller);

        var rubric = Track(ScriptableObject.CreateInstance<SimulationScoringRubricSO>());
        var record = new SimulationRecord
        {
            playerName = "Student Nurse",
            dateCompleted = "2026-11-11",
            finalScore = 85,
            affectedCottonBallCount = 3,
            cottonBallDeduction = 10,
            totalToolDropCount = 1,
            toolDropDeduction = 5,
            totalDeductions = 15,
            toolDropBreakdown = new List<ToolDropSummaryEntry>
            {
                new ToolDropSummaryEntry { toolName = "Pitcher", dropCount = 1 }
            },
            evaluationEvents = new List<EvaluationEvent>
            {
                new EvaluationEvent
                {
                    step = 2,
                    action = "Poured water onto left thigh/leg instead of perineum",
                    detail = "left thigh/leg",
                    isCorrect = false,
                    violationType = ClinicalViolationType.Wrong_Washing_Area.ToString(),
                    criterion = MatrixCriterion.Technique,
                    severity = MistakeSeverity.Moderate,
                    time = 12.5f,
                    count = 1
                }
            }
        };

        var intro = Track(ScriptableObject.CreateInstance<DialoguePhraseTokenSO>());
        intro.fullPhraseText = "Hello, I am Nurse Elena.";
        var ident = Track(ScriptableObject.CreateInstance<DialoguePhraseTokenSO>());
        ident.fullPhraseText = "Could you share your full name?";
        var explain = Track(ScriptableObject.CreateInstance<DialoguePhraseTokenSO>());
        explain.fullPhraseText = "I will gently wash the perineal area with warm water.";
        var debrief = CommunicationDebriefData.CreateFromEvaluation(90f, 90f, 90f, 90f, intro, ident, explain, new[] { intro, ident, explain }, 0);

        typeof(VRDemoGameManager).GetProperty("LastCompletedRecord", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { record });
        typeof(VRDemoGameManager).GetProperty("LastDebrief", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { debrief });
        typeof(VRDemoGameManager).GetProperty("LastRubric", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { rubric });

        controller.OpenSummaryScreen();

        Assert.AreEqual("85/100", controller.ovHeroScoreText.text);
        Assert.AreEqual("3", controller.ovCottonAffectedValueText.text);
        Assert.AreEqual("-10", controller.ovCottonDeductionValueText.text);
        Assert.IsTrue(controller.ovToolRowObjects[0].activeSelf);
        Assert.AreEqual("Pitcher dropped", controller.ovToolRowLabelTexts[0].text);
        Assert.AreEqual("1", controller.ovToolRowValueTexts[0].text);
        Assert.IsFalse(controller.ovToolRowObjects[1].activeSelf);
        Assert.AreEqual("-5", controller.ovToolDeductionValueText.text);
        Assert.AreEqual("-15", controller.ovTotalDeductionsValueText.text);
        Assert.IsTrue(controller.ovPerinealWashSubText.text.Contains("Perineal Washing"));
        Assert.IsTrue(controller.ovPerinealCottonSubText.text.Contains("9-Cotton-Ball"));
        Assert.IsTrue(controller.ovDrapeMotherSubText.text.Contains("Draping the Mother"));
        Assert.IsTrue(controller.ovDrapeTableSubText.text.Contains("Draping the Table"));

        Assert.IsTrue(controller.page0Overview.activeSelf);
        controller.ovStageButtons[0].onClick.Invoke();
        Assert.IsFalse(controller.page0Overview.activeSelf);
        Assert.IsTrue(controller.page1Dialogue.activeSelf);

        controller.ShowPage(2);
        Assert.IsTrue(controller.page2Perineal.activeSelf);
        controller.ShowPage(3);
        Assert.IsTrue(controller.page3Draping.activeSelf);
        controller.ShowPage(4);
        Assert.IsTrue(controller.page4TableAndFinal.activeSelf);
        controller.ShowPage(5);
        Assert.IsTrue(controller.page5Matrix.activeSelf);
        controller.ShowPage(6);
        Assert.IsTrue(controller.page6Gallery.activeSelf);
    }

    [Test]
    public void VRHeadsetVisualFeedback_CreatesFittedCameraOverlay_AndRespondsToModerateEvaluationEvents()
    {
        var camGo = Track(new GameObject("TestMainCamera", typeof(Camera)));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 90f;

        var feedbackGo = Track(new GameObject("TestVisualFeedback", typeof(VRHeadsetVisualFeedback)));
        var feedback = feedbackGo.GetComponent<VRHeadsetVisualFeedback>();
        typeof(VRHeadsetVisualFeedback).GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(feedback, cam);
        typeof(VRHeadsetVisualFeedback).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(feedback, null);

        // Log a Moderate mistake -> single-source-of-truth observer must create VR_RedVignette_Overlay at z = 0.25f
        EvaluationLogger.Instance.ResetSession();
        EvaluationLogger.Instance.LogViolation(ClinicalViolationType.Wrong_Washing_Area, "Poured onto left leg", "left leg", 5, true);

        Transform overlayTf = camGo.transform.Find("VR_RedVignette_Overlay");
        Assert.IsNotNull(overlayTf, "VR_RedVignette_Overlay must be parented to target camera after logged mistake.");
        Track(overlayTf.gameObject);
        Assert.AreEqual(0.25f, overlayTf.localPosition.z, 0.001f);

        MeshFilter overlayMesh = overlayTf.GetComponent<MeshFilter>();
        MeshRenderer overlayRenderer = overlayTf.GetComponent<MeshRenderer>();
        Assert.IsNotNull(overlayMesh, "VR_RedVignette_Overlay must use a MeshFilter stereo quad for OpenXR URP.");
        Assert.IsNotNull(overlayMesh.sharedMesh);
        Assert.IsNotNull(overlayRenderer, "VR_RedVignette_Overlay must use a MeshRenderer stereo quad for OpenXR URP.");
        Assert.IsNotNull(overlayRenderer.sharedMaterial);
        Assert.AreNotEqual(0, cam.cullingMask & (1 << overlayTf.gameObject.layer));
    }
}
