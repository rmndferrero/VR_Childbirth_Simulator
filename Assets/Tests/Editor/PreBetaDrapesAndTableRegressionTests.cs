using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PreBetaDrapesAndTableRegressionTests
{
    private readonly List<UnityEngine.Object> trackedObjects = new List<UnityEngine.Object>();

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null) trackedObjects.Add(obj);
        return obj;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = trackedObjects.Count - 1; i >= 0; i--)
        {
            if (trackedObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(trackedObjects[i]);
            }
        }
        trackedObjects.Clear();

        SetStaticInstance(typeof(MotherDrapingManager), null);
        SetStaticInstance(typeof(DrapingGuideUI), null);
        SetStaticInstance(typeof(TablePrepManager), null);
        SetStaticInstance(typeof(MayoStandDrapeSocket), null);
        SetStaticInstance(typeof(VRHeadsetVisualFeedback), null);
        SetStaticInstance(typeof(VRDemoGameManager), null);
        SetStaticInstance(typeof(PerinealCareManager), null);

        EvaluationLogger.Instance.Bind(null);
        EvaluationLogger.Instance.ResetSession();
    }

    private static void SetStaticInstance(Type type, object value)
    {
        var prop = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        prop?.GetSetMethod(true)?.Invoke(null, new[] { value });
    }

    private MotherDrapingManager CreateMotherDrapingFixture(
        out List<GameObject> whitePlates,
        out List<GameObject> adjustedChildren,
        out GameObject adjustedRoot,
        out GameObject mayoDrape,
        bool startAdjustedRootInactive = false)
    {
        var root = Track(new GameObject("TestMotherDrapingManager"));
        var mgr = root.AddComponent<MotherDrapingManager>();
        SetStaticInstance(typeof(MotherDrapingManager), mgr);

        adjustedRoot = Track(new GameObject("Adjusted_Drapes"));
        adjustedChildren = new List<GameObject>();
        string[] childNames = { "Drape_Buttocks", "Drape_Abdomen", "Drape_LeftLeg", "Drape_RightLeg" };

        whitePlates = new List<GameObject>();
        string[] slotNames = { "1. Under Buttocks", "2. Abdominal", "3. Left Leg", "4. Right Leg" };

        mgr.drapeSlots = new List<DrapeSlot>();
        for (int i = 0; i < 4; i++)
        {
            var whitePlate = Track(new GameObject($"WhitePlate_{i + 1}"));
            whitePlate.SetActive(false);
            whitePlates.Add(whitePlate);

            var ghost = Track(new GameObject($"Ghost_{i + 1}"));
            var targetPt = Track(new GameObject($"Target_{i + 1}"));

            var adjChild = Track(new GameObject(childNames[i]));
            adjChild.transform.SetParent(adjustedRoot.transform, false);
            adjChild.transform.localPosition = new Vector3(0.1f * (i + 1), -0.05f * (i + 1), 0.2f * (i + 1));
            adjChild.transform.localRotation = Quaternion.Euler(5f * (i + 1), 15f * (i + 1), -3f * (i + 1));
            adjChild.transform.localScale = new Vector3(1.1f + 0.05f * i, 0.9f, 1.05f);
            adjChild.SetActive(true);
            adjustedChildren.Add(adjChild);

            mgr.drapeSlots.Add(new DrapeSlot
            {
                slotName = slotNames[i],
                targetPoint = targetPt.transform,
                ghostIndicatorObject = ghost,
                placeholderGreenDrape = whitePlate,
                final3DModelPrefabOrObject = null,
                isDraped = false
            });
        }

        if (startAdjustedRootInactive)
        {
            adjustedRoot.SetActive(false);
        }

        mayoDrape = Track(new GameObject("MayoStand_Drape"));
        mayoDrape.SetActive(false);
        var mayoGhost = Track(new GameObject("Ghost_Mayo"));
        var mayoTarget = Track(new GameObject("Target_Mayo"));

        mgr.drapeSlots.Add(new DrapeSlot
        {
            slotName = "5. Mayo Stand",
            targetPoint = mayoTarget.transform,
            ghostIndicatorObject = mayoGhost,
            placeholderGreenDrape = mayoDrape,
            final3DModelPrefabOrObject = mayoDrape, // Mirrors MainScene Slot 4 where both point to MayoStand_Drape
            isDraped = false
        });

        return mgr;
    }

    private DrapingGuideUI CreateGuideUIFixture(
        out TMP_Text currentTitle,
        out TMP_Text instructionText,
        out Image[] dotImages,
        out TMP_Text[] dotTexts)
    {
        var uiGo = Track(new GameObject("TestDrapingGuideUI"));
        var ui = uiGo.AddComponent<DrapingGuideUI>();
        SetStaticInstance(typeof(DrapingGuideUI), ui);

        var titleGo = Track(new GameObject("CurrentTitle"));
        currentTitle = titleGo.AddComponent<TextMeshProUGUI>();
        var instrGo = Track(new GameObject("DirectionInstruction"));
        instructionText = instrGo.AddComponent<TextMeshProUGUI>();

        dotImages = new Image[4];
        dotTexts = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            var imgGo = Track(new GameObject($"DotImg_{i}"));
            dotImages[i] = imgGo.AddComponent<Image>();
            var txtGo = Track(new GameObject($"DotTxt_{i}"));
            dotTexts[i] = txtGo.AddComponent<TextMeshProUGUI>();
        }

        ui.currentDrapeTitleText = currentTitle;
        ui.directionInstructionText = instructionText;
        ui.stepDotImages = dotImages;
        ui.stepDotTexts = dotTexts;
        ui.completedColor = Color.green;
        ui.activeColor = Color.cyan;
        ui.upcomingColor = Color.gray;
        return ui;
    }

    [Test]
    public void MotherDraping_FlexibleLegSequence_AcceptsRightThenLeft_AndRejectsOutOfOrder()
    {
        EvaluationLogger.Instance.ResetSession();
        var mgr = CreateMotherDrapingFixture(out var whitePlates, out var adjustedChildren, out _, out _, false);
        var ui = CreateGuideUIFixture(out var titleText, out _, out var dotImages, out var dotTexts);

        var linen = Track(new GameObject("DryLinen"));
        mgr.BeginDrapingPhase();

        Assert.IsTrue(mgr.IsSlotValidNext(0));
        Assert.IsFalse(mgr.IsSlotValidNext(1));
        Assert.IsFalse(mgr.IsSlotValidNext(2));
        Assert.IsFalse(mgr.IsSlotValidNext(3));

        // 1. Buttocks
        Assert.IsTrue(mgr.TryPlaceDrape(0, linen));
        Assert.AreEqual(1, mgr.GetDrapedCount());

        // Attempting Left or Right Leg before Abdominal must fail and record mistake
        Assert.IsFalse(mgr.TryPlaceDrape(2, linen));
        Assert.IsFalse(mgr.TryPlaceDrape(3, linen));
        Assert.IsFalse(mgr.drapeSlots[2].isDraped);
        Assert.IsFalse(mgr.drapeSlots[3].isDraped);
        Assert.GreaterOrEqual(EvaluationLogger.Instance.events.FindAll(e => !e.isCorrect).Count, 2);

        // 2. Abdominal unlocks BOTH Left Leg (2) and Right Leg (3)
        Assert.IsTrue(mgr.TryPlaceDrape(1, linen));
        Assert.AreEqual(2, mgr.GetDrapedCount());
        Assert.IsTrue(mgr.IsSlotValidNext(2));
        Assert.IsTrue(mgr.IsSlotValidNext(3));
        Assert.IsFalse(mgr.IsSlotValidNext(4));

        ui.RefreshUI();
        Assert.AreEqual(ui.activeColor, dotImages[2].color);
        Assert.AreEqual(ui.activeColor, dotImages[3].color);
        Assert.AreEqual("3/4", dotTexts[2].text);
        Assert.AreEqual("3/4", dotTexts[3].text);
        Assert.IsTrue(titleText.text.IndexOf("Left or Right", StringComparison.OrdinalIgnoreCase) >= 0,
            $"Expected flexible prompt, got: {titleText.text}");

        // Mayo Stand cannot finish while both legs remain un-draped
        Assert.IsFalse(mgr.TryPlaceDrape(4, linen));

        // 3. Place Right Leg FIRST (slot 3)
        Assert.IsTrue(mgr.TryPlaceDrape(3, linen));
        Assert.AreEqual(3, mgr.GetDrapedCount());
        Assert.IsTrue(adjustedChildren[3].activeSelf);
        Assert.IsFalse(whitePlates[3].activeSelf);
        Assert.IsTrue(mgr.IsSlotValidNext(2));
        Assert.IsFalse(mgr.IsSlotValidNext(3));
        Assert.IsFalse(mgr.IsSlotValidNext(4), "Mayo Stand must stay locked until remaining leg is draped.");

        ui.RefreshUI();
        Assert.AreEqual(ui.completedColor, dotImages[3].color);
        Assert.AreEqual(ui.activeColor, dotImages[2].color);
        Assert.IsTrue(mgr.GetExpectedStepDescription().IndexOf("Left Leg", StringComparison.OrdinalIgnoreCase) >= 0);

        // 4. Place remaining Left Leg (slot 2) -> now unlocks Mayo Stand (4)
        Assert.IsTrue(mgr.TryPlaceDrape(2, linen));
        Assert.AreEqual(4, mgr.GetDrapedCount());
        Assert.IsTrue(mgr.IsSlotValidNext(4));

        // 5. Mayo Stand completes sequence cleanly
        Assert.IsTrue(mgr.TryPlaceDrape(4, linen));
        Assert.AreEqual(5, mgr.GetDrapedCount());
        Assert.IsTrue(mgr.isDrapingComplete);
    }

    [Test]
    public void MotherDraping_FlexibleLegSequence_AlsoAcceptsLeftThenRight()
    {
        var mgr = CreateMotherDrapingFixture(out _, out var adjustedChildren, out _, out _, false);
        var linen = Track(new GameObject("DryLinen"));
        mgr.BeginDrapingPhase();

        Assert.IsTrue(mgr.TryPlaceDrape(0, linen));
        Assert.IsTrue(mgr.TryPlaceDrape(1, linen));
        Assert.IsTrue(mgr.TryPlaceDrape(2, linen));
        Assert.IsFalse(mgr.IsSlotValidNext(4));
        Assert.IsTrue(mgr.GetExpectedStepDescription().IndexOf("Right Leg", StringComparison.OrdinalIgnoreCase) >= 0);

        Assert.IsTrue(mgr.TryPlaceDrape(3, linen));
        Assert.IsTrue(adjustedChildren[2].activeSelf);
        Assert.IsTrue(adjustedChildren[3].activeSelf);
        Assert.IsTrue(mgr.IsSlotValidNext(4));
    }

    [Test]
    public void MotherDraping_AdjustedDrapesResolution_PreservesUserTransformsAndHidesWhiteCubes()
    {
        var mgr = CreateMotherDrapingFixture(out var whitePlates, out var adjustedChildren, out var adjustedRoot, out var mayoDrape, startAdjustedRootInactive: true);

        Vector3[] originalLocalPos = new Vector3[4];
        Quaternion[] originalLocalRot = new Quaternion[4];
        Vector3[] originalLocalScale = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            originalLocalPos[i] = adjustedChildren[i].transform.localPosition;
            originalLocalRot[i] = adjustedChildren[i].transform.localRotation;
            originalLocalScale[i] = adjustedChildren[i].transform.localScale;
        }

        mgr.BeginDrapingPhase();

        // Resolving from an initially inactive Adjusted_Drapes parent must NOT reveal unplaced drapes,
        // and must wire Adjusted_Drapes children without altering any transforms.
        Assert.IsTrue(adjustedRoot.activeSelf);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(adjustedChildren[i], mgr.drapeSlots[i].final3DModelPrefabOrObject);
            Assert.IsFalse(adjustedChildren[i].activeSelf, $"Adjusted child {i} should start hidden before placement.");
            Assert.IsFalse(whitePlates[i].activeSelf, $"White cube placeholder {i} should remain inactive.");
            Assert.Less(Vector3.Distance(originalLocalPos[i], adjustedChildren[i].transform.localPosition), 0.0001f);
            Assert.Less(Quaternion.Angle(originalLocalRot[i], adjustedChildren[i].transform.localRotation), 0.01f);
            Assert.Less(Vector3.Distance(originalLocalScale[i], adjustedChildren[i].transform.localScale), 0.0001f);
        }

        var linen = Track(new GameObject("DryLinen"));
        Assert.IsTrue(mgr.TryPlaceDrape(0, linen));
        Assert.IsTrue(adjustedChildren[0].activeSelf);
        Assert.IsFalse(whitePlates[0].activeSelf, "White plate must stay hidden after placing drape.");
        Assert.IsFalse(adjustedChildren[1].activeSelf, "Unplaced drape must remain hidden.");

        Assert.IsTrue(mgr.TryPlaceDrape(1, linen));
        Assert.IsTrue(mgr.TryPlaceDrape(2, linen));
        Assert.IsTrue(mgr.TryPlaceDrape(3, linen));
        Assert.IsTrue(mgr.TryPlaceDrape(4, linen));

        // Slot 4 shares placeholderGreenDrape == final3DModelPrefabOrObject (MayoStand_Drape) and must remain visible
        Assert.IsTrue(mayoDrape.activeSelf, "MayoStand_Drape must remain active after Mayo placement.");
    }

    [Test]
    public void MotherDraping_PerZoneChildDrapesBelowGhost_AreResolvedAndShownWithoutMovingTransforms()
    {
        var root = Track(new GameObject("TestZoneChildrenDrapingManager"));
        var mgr = root.AddComponent<MotherDrapingManager>();
        SetStaticInstance(typeof(MotherDrapingManager), mgr);
        mgr.drapeSlots = new List<DrapeSlot>();

        string[] zoneNames = { "1. Under Buttocks", "2. Abdominal", "3. Left Leg", "4. Right Leg" };
        string[] customChildNames = { "Buttocks", "Drape_Abdominal", "Drape_Left leg", "Drape_Left Leg" };
        var placedMeshes = new GameObject[4];
        var savedPositions = new Vector3[4];
        var savedRotations = new Quaternion[4];
        var savedScales = new Vector3[4];

        for (int i = 0; i < 4; i++)
        {
            var zone = Track(new GameObject(zoneNames[i]));
            zone.transform.SetParent(root.transform, false);

            var ghost = Track(new GameObject("GhostIndicator"));
            ghost.transform.SetParent(zone.transform, false);

            var placed = GameObject.CreatePrimitive(PrimitiveType.Quad);
            placed.name = customChildNames[i];
            Track(placed);
            placed.transform.SetParent(zone.transform, false);
            placed.transform.localPosition = new Vector3(0.05f * (i + 1), -0.15f, 0.02f);
            placed.transform.localRotation = Quaternion.Euler(10f * i, 25f, -5f);
            placed.transform.localScale = new Vector3(1.2f, 0.95f, 1.1f);
            placed.SetActive(false);

            placedMeshes[i] = placed;
            savedPositions[i] = placed.transform.localPosition;
            savedRotations[i] = placed.transform.localRotation;
            savedScales[i] = placed.transform.localScale;

            mgr.drapeSlots.Add(new DrapeSlot
            {
                slotName = zoneNames[i],
                targetPoint = zone.transform,
                ghostIndicatorObject = ghost,
                placeholderGreenDrape = null,
                final3DModelPrefabOrObject = null,
                isDraped = false
            });
        }

        mgr.BeginDrapingPhase();

        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(placedMeshes[i], mgr.drapeSlots[i].final3DModelPrefabOrObject);
            Assert.IsFalse(placedMeshes[i].activeSelf);
            Assert.IsTrue(mgr.drapeSlots[i].ghostIndicatorObject.activeSelf);
        }

        var linen = Track(new GameObject("DryLinen"));
        Assert.IsTrue(mgr.TryPlaceDrape(0, linen));
        Assert.IsTrue(placedMeshes[0].activeSelf, "Placed drape below ghost must appear on lock.");
        Assert.IsFalse(mgr.drapeSlots[0].ghostIndicatorObject.activeSelf);

        Assert.IsTrue(mgr.TryPlaceDrape(1, linen));
        Assert.IsTrue(placedMeshes[1].activeSelf);
        Assert.IsTrue(mgr.TryPlaceDrape(3, linen));
        Assert.IsTrue(placedMeshes[3].activeSelf);
        Assert.IsTrue(mgr.TryPlaceDrape(2, linen));
        Assert.IsTrue(placedMeshes[2].activeSelf);

        for (int i = 0; i < 4; i++)
        {
            Assert.Less(Vector3.Distance(savedPositions[i], placedMeshes[i].transform.localPosition), 0.0001f);
            Assert.Less(Quaternion.Angle(savedRotations[i], placedMeshes[i].transform.localRotation), 0.01f);
            Assert.Less(Vector3.Distance(savedScales[i], placedMeshes[i].transform.localScale), 0.0001f);
        }
    }

    [Test]
    public void TablePrep_AbsorbsTable2Duplicates_AndPlacesForcepsScalpelTongsCuretteFlatInAnySlot()
    {
        var tablePrepRoot = Track(new GameObject("TestTablePrepRoot"));
        var manager = tablePrepRoot.AddComponent<TablePrepManager>();
        SetStaticInstance(typeof(TablePrepManager), manager);

        var table2Root = Track(new GameObject("Table 2"));
        manager.table2Root = table2Root.transform;

        // Primaries on Table 1 (outside Table 2)
        var tForceps = CreateTool("Artery_Forcep", "Artery_Forcep");
        var tScalpel = CreateTool("Scalpel_01", "Scalpel_01");
        var tTongs = CreateTool("Thumb_Forceps", "Thumb_Forceps");
        var tCurette = CreateTool("Curette_01", "Curette_01");
        var tBasin = CreateTool("KidneyBasin_01", "KidneyBasin_01");

        // 6 Slots under Table2_Slots matching MainScene layout (including rotated Slot_4 and scaled Slot_6)
        var slotsParent = Track(new GameObject("Table2_Slots"));
        slotsParent.transform.SetParent(table2Root.transform, false);
        var slot1 = CreateSlot(slotsParent.transform, 1, "Slot_1", "Forceps", tForceps, new Vector3(-0.076f, 0.205f, -0.251f), Quaternion.identity, Vector3.one);
        slot1.snapEulerAngles = new Vector3(90f, 92.5f, 0f);
        var slot2 = CreateSlot(slotsParent.transform, 2, "Slot_2", "Umbi_Clamp", null, new Vector3(-0.081f, 0.205f, -0.105f), Quaternion.identity, Vector3.one);
        var slot3 = CreateSlot(slotsParent.transform, 3, "Slot_3", "Scissor", null, new Vector3(-0.08f, 0.205f, 0.031f), Quaternion.identity, Vector3.one);
        var slot4 = CreateSlot(slotsParent.transform, 4, "Slot_4", "Kidney_Basin", tBasin, new Vector3(0.088f, 0.205f, 0.044f), Quaternion.Euler(0f, 1.749f, 0f), Vector3.one);
        var slot5 = CreateSlot(slotsParent.transform, 5, "Slot_5", "straight_catheter", null, new Vector3(0.083f, 0.205f, -0.114f), Quaternion.identity, Vector3.one);
        var slot6 = CreateSlot(slotsParent.transform, 6, "Slot_6", "gauze", null, new Vector3(0.082f, 0.205f, -0.273f), Quaternion.identity, new Vector3(0.87626f, 0.87626f, 0.87626f));

        manager.slots = new List<TablePrepSlot> { slot1, slot2, slot3, slot4, slot5, slot6 };
        manager.isMayoDraped = true;
        manager.isTablePrepActive = true;

        // Saved reference duplicates under Table 2 (matching user poses from MainScene)
        var forcepsDup = CreateDuplicate(table2Root.transform, "Forceps (1)", "", new Vector3(-0.07876f, 0.20008f, -0.26804f), Quaternion.Euler(83.4171f, 177.61214f, 0f));
        var scalpelDup = CreateDuplicate(table2Root.transform, "Scalpel (1)", "Scalpel_01", new Vector3(0.1508f, 0.1918f, -0.25076f), Quaternion.Euler(346.28345f, 271.66104f, 357.06912f));
        var tongsDup = CreateDuplicate(table2Root.transform, "Tongs (1)", "Thumb_Forceps", new Vector3(0.1492f, 0.1852f, 0.0462f), Quaternion.Euler(276.02505f, 11.001455f, 171.72925f));
        var curetteDup = CreateDuplicate(table2Root.transform, "Curette  (1)", "Curette_01", new Vector3(-0.0838f, 0.186f, -0.2486f), Quaternion.Euler(0f, 352.07773f, 0f));

        // Capture expected poses before absorption destroys/disables runtime duplicates
        Vector3 expectedForcepsPos = forcepsDup.transform.position;
        Quaternion expectedForcepsRot = forcepsDup.transform.rotation;
        Quaternion expectedScalpelRot = scalpelDup.transform.rotation;
        Quaternion expectedTongsRot = tongsDup.transform.rotation;
        Quaternion expectedCuretteRot = curetteDup.transform.rotation;

        manager.AbsorbTable2ReferenceDuplicatesIfPresent();

        // Canonicalization maps decoy names without colliding with TongueForceps or Artery_Forcep
        Assert.AreEqual("scalpel", TablePrepSlot.CanonicalizeToolKey("Scalpel_01"));
        Assert.AreEqual("scalpel", TablePrepSlot.CanonicalizeToolKey("Scalpel (1)"));
        Assert.AreEqual("curette", TablePrepSlot.CanonicalizeToolKey("Curette_01"));
        Assert.AreEqual("curette", TablePrepSlot.CanonicalizeToolKey("Curette  (1)"));
        Assert.AreEqual("thumb_forceps", TablePrepSlot.CanonicalizeToolKey("Thumb_Forceps"));
        Assert.AreEqual("thumb_forceps", TablePrepSlot.CanonicalizeToolKey("Tongs (1)"));
        Assert.AreNotEqual(TablePrepSlot.CanonicalizeToolKey("Thumb_Forceps"), TablePrepSlot.CanonicalizeToolKey("TongueForceps"));
        Assert.AreNotEqual(TablePrepSlot.CanonicalizeToolKey("Thumb_Forceps"), TablePrepSlot.CanonicalizeToolKey("Artery_Forcep"));

        // Slot 1 matches Forceps (1) exactly
        slot1.GetTargetSnapPose(tForceps, out Vector3 snappedForcepsPos, out Quaternion snappedForcepsRot);
        Assert.Less(Vector3.Distance(expectedForcepsPos, snappedForcepsPos), 0.0005f);
        Assert.Less(Quaternion.Angle(expectedForcepsRot, snappedForcepsRot), 0.05f);

        // Scalpel, Tongs, and Curette get perToolReferences in every slot (including rotated Slot_4 & scaled Slot_6)
        foreach (var s in new[] { slot1, slot2, slot4, slot6 })
        {
            Assert.IsTrue(s.HasPerToolReference("Scalpel_01"));
            Assert.IsTrue(s.HasPerToolReference("Thumb_Forceps"));
            Assert.IsTrue(s.HasPerToolReference("Curette_01"));

            s.GetTargetSnapPose(tScalpel, out Vector3 sPos, out Quaternion sRot);
            Assert.Less(Mathf.Abs(sPos.y - scalpelDup.transform.position.y), 0.001f);
            Assert.Less(Quaternion.Angle(expectedScalpelRot, sRot), 0.1f, $"Scalpel rotation mismatch in {s.name}");

            s.GetTargetSnapPose(tTongs, out Vector3 tPos, out Quaternion tRot);
            Assert.Less(Mathf.Abs(tPos.y - tongsDup.transform.position.y), 0.001f);
            Assert.Less(Quaternion.Angle(expectedTongsRot, tRot), 0.1f, $"Tongs rotation mismatch in {s.name}");

            s.GetTargetSnapPose(tCurette, out Vector3 cPos, out Quaternion cRot);
            Assert.Less(Mathf.Abs(cPos.y - curetteDup.transform.position.y), 0.001f);
            Assert.Less(Quaternion.Angle(expectedCuretteRot, cRot), 0.1f, $"Curette rotation mismatch in {s.name}");
        }

        // Duplicates are excluded from active scene tool list
        var remainingTools = new HashSet<ToolItem>(ToolItem.AllToolsInScene);
        Assert.IsFalse(remainingTools.Contains(forcepsDup));
        Assert.IsFalse(remainingTools.Contains(scalpelDup));
        Assert.IsFalse(remainingTools.Contains(tongsDup));
        Assert.IsFalse(remainingTools.Contains(curetteDup));
    }

    [Test]
    public void MainScene_ContainsUpdatedSlot1PerToolRefsAndDisabledDuplicates()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sceneText = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Scenes", "MainScene.unity"));

        Assert.IsTrue(sceneText.Contains("final3DModelPrefabOrObject: {fileID: 2999229479311139435}"), "Slot 0 must reference Buttocks drape.");
        Assert.IsTrue(sceneText.Contains("final3DModelPrefabOrObject: {fileID: 4219802813229297144}"), "Slot 1 must reference Drape_Abdominal.");
        Assert.IsTrue(sceneText.Contains("final3DModelPrefabOrObject: {fileID: 6254090265089958346}"), "Slot 2 must reference Drape_Left leg.");
        Assert.IsTrue(sceneText.Contains("final3DModelPrefabOrObject: {fileID: 4952325537795323014}"), "Slot 3 must reference Drape_Left Leg.");
        Assert.IsTrue(Regex.IsMatch(sceneText, @"snapPositionOffset:\s*\{x:\s*-0\.0027601\d*,\s*y:\s*-0\.00491988\d*,\s*z:\s*-0\.0170401\d*\}"),
            "Slot 1 must contain updated Forceps (1) snapPositionOffset.");
        Assert.IsTrue(Regex.IsMatch(sceneText, @"snapEulerAngles:\s*\{x:\s*83\.4171\d*,\s*y:\s*177\.6121\d*,\s*z:\s*0\}"),
            "Slot 1 must contain updated Forceps (1) snapEulerAngles.");
        Assert.AreEqual(6, Regex.Matches(sceneText, @"- toolID: Scalpel_01").Count);
        Assert.AreEqual(6, Regex.Matches(sceneText, @"- toolID: Thumb_Forceps").Count);
        Assert.AreEqual(6, Regex.Matches(sceneText, @"- toolID: Curette_01").Count);

        foreach (string dupName in new[] { "Tongs (1)", "Scalpel (1)", "Forceps (1)", "Curette  (1)" })
        {
            Assert.IsTrue(Regex.IsMatch(sceneText,
                Regex.Escape($"value: {dupName}") + @"[\s\S]{1,3500}?propertyPath: m_IsActive\r?\n\s+value: 0"),
                $"Reference duplicate '{dupName}' must be deactivated (m_IsActive: 0) in MainScene.unity.");
        }
    }

    private ToolItem CreateTool(string goName, string toolId)
    {
        var go = Track(new GameObject(goName, typeof(Rigidbody)));
        var rb = go.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        var tool = go.AddComponent<ToolItem>();
        tool.toolID = toolId;
        typeof(ToolItem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tool, null);
        return tool;
    }

    private ToolItem CreateDuplicate(Transform table2Root, string goName, string toolId, Vector3 localPos, Quaternion localRot)
    {
        var go = Track(new GameObject(goName, typeof(Rigidbody)));
        go.transform.SetParent(table2Root, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        var rb = go.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        var tool = go.AddComponent<ToolItem>();
        tool.toolID = toolId;
        typeof(ToolItem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tool, null);
        return tool;
    }

    private TablePrepSlot CreateSlot(Transform parent, int slotNum, string slotName, string assignedId, ToolItem assignedTool, Vector3 localPos, Quaternion localRot, Vector3 localScale)
    {
        var go = Track(new GameObject(slotName));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        var slot = go.AddComponent<TablePrepSlot>();
        slot.slotNumber = slotNum;
        slot.slotLabel = $"Slot {slotNum}";
        slot.assignedToolID = assignedId;
        slot.assignedTool = assignedTool;
        slot.snapTransform = go.transform;
        slot.snapRadius = 0.16f;
        slot.useAbsoluteSnapRotation = true;
        return slot;
    }
}
