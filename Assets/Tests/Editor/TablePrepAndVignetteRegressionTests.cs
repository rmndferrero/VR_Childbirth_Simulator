using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TablePrepAndVignetteRegressionTests
{
    private readonly List<Object> trackedObjects = new List<Object>();

    private T Track<T>(T obj) where T : Object
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
                Object.DestroyImmediate(trackedObjects[i]);
            }
        }
        trackedObjects.Clear();

        SetStaticInstance(typeof(TablePrepManager), null);
        SetStaticInstance(typeof(MayoStandDrapeSocket), null);
        SetStaticInstance(typeof(VRHeadsetVisualFeedback), null);
        SetStaticInstance(typeof(VRDemoGameManager), null);
        SetStaticInstance(typeof(PerinealCareManager), null);

        EvaluationLogger.Instance.Bind(null);
        EvaluationLogger.Instance.ResetSession();
    }

    private static void SetStaticInstance(System.Type type, object value)
    {
        var prop = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        prop?.GetSetMethod(true)?.Invoke(null, new[] { value });
    }

    private static void SetMember(object target, string memberName, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var field = target.GetType().GetField(memberName, flags);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }
        var prop = target.GetType().GetProperty(memberName, flags);
        prop?.SetValue(target, value);
    }

    private ToolItem CreateMockTool(string goName, string toolId, Vector3 worldPos, Quaternion worldRot, Vector3 localScale)
    {
        var go = Track(new GameObject(goName, typeof(Rigidbody)));
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;
        go.transform.localScale = localScale;
        var rb = go.GetComponent<Rigidbody>();
        rb.isKinematic = true;

        var tool = go.AddComponent<ToolItem>();
        tool.toolID = toolId;
        typeof(ToolItem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tool, null);
        return tool;
    }

    private TablePrepSlot CreateSlot(Transform parent, int slotNum, string slotName, string assignedId, ToolItem assignedTool, Vector3 localPos, Quaternion localRot, Vector3 localScale)
    {
        var slotGo = Track(new GameObject(slotName));
        slotGo.transform.SetParent(parent, false);
        slotGo.transform.localPosition = localPos;
        slotGo.transform.localRotation = localRot;
        slotGo.transform.localScale = localScale;

        var slot = slotGo.AddComponent<TablePrepSlot>();
        slot.slotNumber = slotNum;
        slot.slotLabel = $"Slot {slotNum}";
        slot.assignedToolID = assignedId;
        slot.assignedTool = assignedTool;
        slot.snapTransform = slotGo.transform;
        slot.snapRadius = 0.16f;
        slot.useAbsoluteSnapRotation = true;
        return slot;
    }

    private Transform CreateInactiveReferenceChild(Transform slotParent, string refName, string toolId, Vector3 localPos, Quaternion localRot)
    {
        var refGo = Track(new GameObject(refName));
        refGo.transform.SetParent(slotParent, false);
        refGo.transform.localPosition = localPos;
        refGo.transform.localRotation = localRot;
        var refTool = refGo.AddComponent<ToolItem>();
        refTool.toolID = toolId;
        refGo.SetActive(false);
        return refGo.transform;
    }

    [Test]
    public void TryHandleToolRelease_BlindSlotting_AcceptsAnyToolUntilSubmit_ThenResetsWrongOnly()
    {
        EvaluationLogger.Instance.ResetSession();

        var root = Track(new GameObject("TestTablePrepRoot"));
        var manager = root.AddComponent<TablePrepManager>();
        SetStaticInstance(typeof(TablePrepManager), manager);

        var tForceps = CreateMockTool("Artery_Forcep", "Artery_Forcep", new Vector3(-1f, 0.8f, 0f), Quaternion.identity, Vector3.one);
        var tClamp = CreateMockTool("UmbiClamp_01", "UmbiClamp_01", new Vector3(-1.1f, 0.8f, 0f), Quaternion.identity, Vector3.one);
        var tScissor = CreateMockTool("Sponge_Forceps", "Sponge_Forceps", new Vector3(-1.2f, 0.8f, 0f), Quaternion.identity, Vector3.one);
        var tBasin = CreateMockTool("KidneyBasin_01", "KidneyBasin_01", new Vector3(-1.3f, 0.8f, 0f), Quaternion.identity, Vector3.one);
        var tCatheter = CreateMockTool("Straight_Catheter", "Straight_Catheter", new Vector3(-1.4f, 0.8f, 0f), Quaternion.identity, Vector3.one);
        var tGauze = CreateMockTool("Gauze_Pack", "Gauze_Pack", new Vector3(-1.5f, 0.8f, 0f), Quaternion.identity, Vector3.one);
        var tDecoyScalpel = CreateMockTool("Scalpel_01", "Scalpel_01", new Vector3(-1.6f, 0.8f, 0f), Quaternion.identity, Vector3.one);

        var slots = new[]
        {
            CreateSlot(root.transform, 1, "Slot_1", "Forceps", tForceps, new Vector3(0.0f, 0f, 0.0f), Quaternion.identity, Vector3.one),
            CreateSlot(root.transform, 2, "Slot_2", "Umbi_Clamp", tClamp, new Vector3(0.4f, 0f, 0.0f), Quaternion.identity, Vector3.one),
            CreateSlot(root.transform, 3, "Slot_3", "Scissor", tScissor, new Vector3(0.8f, 0f, 0.0f), Quaternion.identity, Vector3.one),
            CreateSlot(root.transform, 4, "Slot_4", "Kidney_Basin", tBasin, new Vector3(0.0f, 0f, 0.4f), Quaternion.Euler(0f, 1.749f, 0f), Vector3.one),
            CreateSlot(root.transform, 5, "Slot_5", "straight_catheter", tCatheter, new Vector3(0.4f, 0f, 0.4f), Quaternion.identity, Vector3.one),
            CreateSlot(root.transform, 6, "Slot_6", "gauze", tGauze, new Vector3(0.8f, 0f, 0.4f), Quaternion.identity, new Vector3(0.87626f, 0.87626f, 0.87626f)),
        };

        SetMember(manager, "slots", slots);
        SetMember(manager, "isMayoDraped", true);
        SetMember(manager, "isTablePrepActive", true);
        SetMember(manager, "isTablePrepCompleted", false);

        // Place 4 right tools + 1 wrong swap (Gauze on Slot 4) + 1 decoy tool (Scalpel on Slot 1)
        tDecoyScalpel.transform.position = slots[0].GetTargetSnapPosition();
        tClamp.transform.position = slots[1].GetTargetSnapPosition();
        tScissor.transform.position = slots[2].GetTargetSnapPosition();
        tGauze.transform.position = slots[3].GetTargetSnapPosition();
        tCatheter.transform.position = slots[4].GetTargetSnapPosition();
        tBasin.transform.position = slots[5].GetTargetSnapPosition();

        Assert.IsTrue(manager.TryHandleToolRelease(tDecoyScalpel));
        Assert.IsTrue(manager.TryHandleToolRelease(tClamp));
        Assert.IsTrue(manager.TryHandleToolRelease(tScissor));
        Assert.IsTrue(manager.TryHandleToolRelease(tGauze));
        Assert.IsTrue(manager.TryHandleToolRelease(tCatheter));
        Assert.IsTrue(manager.TryHandleToolRelease(tBasin));

        // All 6 slots filled blindly, with ZERO mistake violations before Submit!
        for (int i = 0; i < 6; i++) Assert.IsTrue(slots[i].isFilled, $"Slot {i + 1} should accept blind drop.");
        Assert.AreEqual(tDecoyScalpel, slots[0].currentPlacedTool);
        Assert.AreEqual(tGauze, slots[3].currentPlacedTool);
        Assert.AreEqual(0, EvaluationLogger.Instance.events.FindAll(e => !e.isCorrect).Count,
            "Blind slotting must never trigger mistake spoilers before clicking Submit.");

        // Now release an extra tool near already-occupied Slot 2 (no open slot in radius) -> returns false cleanly without penalty
        tForceps.transform.position = slots[1].GetTargetSnapPosition();
        Assert.IsFalse(manager.TryHandleToolRelease(tForceps));
        Assert.AreEqual(tClamp, slots[1].currentPlacedTool);
        Assert.AreEqual(0, EvaluationLogger.Instance.events.FindAll(e => !e.isCorrect).Count);

        // Click Submit -> strict evaluation logs Incorrect_Table_Slot, clears ONLY slots 1, 4, 6 (wrong), preserves slots 2, 3, 5!
        manager.ValidateAndConfirmSetup();

        var violations = EvaluationLogger.Instance.events.FindAll(e => !e.isCorrect);
        Assert.AreEqual(1, violations.Count, "Pressing Submit with wrong instruments must log Incorrect_Table_Slot.");
        Assert.AreEqual(ClinicalViolationType.Incorrect_Table_Slot, violations[0].violationType);
        Assert.IsFalse(slots[0].isFilled);
        Assert.IsTrue(slots[1].isFilled);
        Assert.IsTrue(slots[2].isFilled);
        Assert.IsFalse(slots[3].isFilled);
        Assert.IsTrue(slots[4].isFilled);
        Assert.IsFalse(slots[5].isFilled);

        // Fix the 3 cleared slots and Submit again -> completes cleanly!
        tForceps.transform.position = slots[0].GetTargetSnapPosition();
        tBasin.transform.position = slots[3].GetTargetSnapPosition();
        tGauze.transform.position = slots[5].GetTargetSnapPosition();
        Assert.IsTrue(manager.TryHandleToolRelease(tForceps));
        Assert.IsTrue(manager.TryHandleToolRelease(tBasin));
        Assert.IsTrue(manager.TryHandleToolRelease(tGauze));

        manager.ValidateAndConfirmSetup();
        Assert.IsTrue(EvaluationLogger.Instance.events.Exists(e => e.isCorrect && e.action == "Mayo table setup confirmed"));
    }

    [Test]
    public void GetTargetSnapPose_UsesInactiveDuplicateReferencesAndCrossSlotHomeFallback()
    {
        var root = Track(new GameObject("TestSlotsRoot"));

        var tForceps = CreateMockTool("Artery_Forcep", "Artery_Forcep", Vector3.zero, Quaternion.identity, Vector3.one);
        var tBasin = CreateMockTool("KidneyBasin_01", "KidneyBasin_01", Vector3.zero, Quaternion.identity, Vector3.one);
        var tCatheter = CreateMockTool("Straight_Catheter", "Straight_Catheter", Vector3.zero, Quaternion.identity, Vector3.one);
        var tGauze = CreateMockTool("Gauze_Pack", "Gauze_Pack", Vector3.zero, Quaternion.identity, Vector3.one);
        var tTongueForceps = CreateMockTool("TongueForceps", "TongueForceps", Vector3.zero, Quaternion.identity, Vector3.one);
        var tMayoScissors = CreateMockTool("Mayo_01", "Mayo_01", Vector3.zero, Quaternion.identity, Vector3.one);

        var slot1 = CreateSlot(root.transform, 1, "Slot_1", "Forceps", tForceps, new Vector3(-0.16f, 0f, -0.15f), Quaternion.identity, Vector3.one);
        slot1.snapEulerAngles = new Vector3(90f, 92.5f, 0f);

        var slot4 = CreateSlot(root.transform, 4, "Slot_4", "Kidney_Basin", tBasin, new Vector3(-0.058f, 0f, 0.125f), new Quaternion(0f, 0.015263491f, 0f, 0.99988353f), Vector3.one);
        var slot5 = CreateSlot(root.transform, 5, "Slot_5", "straight_catheter", tCatheter, new Vector3(-0.052f, 0f, -0.014f), Quaternion.identity, Vector3.one);
        var slot6 = CreateSlot(root.transform, 6, "Slot_6", "gauze", tGauze, new Vector3(0.193f, 0f, -0.006f), Quaternion.identity, new Vector3(0.87626f, 0.87626f, 0.87626f));

        // User-placed inactive duplicate children matching MainScene.unity hierarchy
        Transform refKB1 = CreateInactiveReferenceChild(slot4.transform, "Kidney_Basin (1)", "KidneyBasin_01",
            new Vector3(0.00815052f, 0.009960055f, -0.0074347407f),
            new Quaternion(0.4850529f, -0.48505312f, 0.5145129f, -0.514513f));
        Transform refCat1 = CreateInactiveReferenceChild(slot5.transform, "straight_catheter (1)", "Straight_Catheter",
            new Vector3(-0.10560019f, -0.01859963f, 0.018400135f),
            new Quaternion(-0.70413196f, -0.7100335f, -0.0050592446f, -0.005017104f));
        Transform refGauze1 = CreateInactiveReferenceChild(slot6.transform, "gauze (1)", "Gauze_Pack",
            new Vector3(0.0025104408f, -0.019857014f, 0.003651674f),
            new Quaternion(0.49228984f, -0.50759315f, 0.5075929f, 0.4922899f));
        Transform refGauzeInSlot4 = CreateInactiveReferenceChild(slot4.transform, "gauze (6)", "Gauze_Pack",
            new Vector3(-0.009789416f, -0.0061000586f, 0.025713079f),
            new Quaternion(0.49228984f, -0.50759315f, 0.5075929f, 0.4922899f));

        // 1) Inactive duplicate children match to sub-millimeter precision (even under Slot_6 scale 0.87626 and Slot_4 rotation!)
        slot4.GetTargetSnapPose(tBasin, out Vector3 kbPos, out Quaternion kbRot);
        Assert.Less(Vector3.Distance(refKB1.position, kbPos), 0.0001f);
        Assert.Less(Quaternion.Angle(refKB1.rotation, kbRot), 0.05f);

        slot5.GetTargetSnapPose(tCatheter, out Vector3 catPos, out Quaternion catRot);
        Assert.Less(Vector3.Distance(refCat1.position, catPos), 0.0001f);
        Assert.Less(Quaternion.Angle(refCat1.rotation, catRot), 0.05f);

        slot6.GetTargetSnapPose(tGauze, out Vector3 gauzePos, out Quaternion gauzeRot);
        Assert.Less(Vector3.Distance(refGauze1.position, gauzePos), 0.0001f);
        Assert.Less(Quaternion.Angle(refGauze1.rotation, gauzeRot), 0.05f);

        // 2) Wrong-slot Gauze on Slot_4 matches gauze (6) duplicate inside Slot_4!
        slot4.GetTargetSnapPose(tGauze, out Vector3 gauzeIn4Pos, out Quaternion gauzeIn4Rot);
        Assert.Less(Vector3.Distance(refGauzeInSlot4.position, gauzeIn4Pos), 0.0001f);
        Assert.Less(Quaternion.Angle(refGauzeInSlot4.rotation, gauzeIn4Rot), 0.05f);

        // 3) Artery_Forcep dropped on Slot_4 (no Forceps duplicate in Slot_4) derives relative pose from Slot_1 home pose!
        slot4.GetTargetSnapPose(tForceps, out Vector3 forcepsIn4Pos, out Quaternion forcepsIn4Rot);
        Assert.Less(Vector3.Distance(slot4.transform.position, forcepsIn4Pos), 0.0001f);
        Assert.Less(Quaternion.Angle(slot4.transform.rotation * Quaternion.Euler(90f, 92.5f, 0f), forcepsIn4Rot), 0.05f);

        // 4) CanonicalizeToolKey never matches decoy TongueForceps -> Forceps or Mayo_01 -> Scissor
        Assert.AreNotEqual(TablePrepSlot.CanonicalizeToolKey("Forceps"), TablePrepSlot.CanonicalizeToolKey("TongueForceps"));
        Assert.AreNotEqual(TablePrepSlot.CanonicalizeToolKey("Scissor"), TablePrepSlot.CanonicalizeToolKey("Mayo_01"));
        Assert.IsFalse(slot1.CanAccept(tTongueForceps));
        Assert.IsFalse(slot1.CanAccept(tMayoScissors));

        // Decoy tool in Slot_4 sits flat at Slot_4 tray origin rather than KidneyBasin's vertical rotation
        slot4.GetTargetSnapPose(tTongueForceps, out Vector3 decoyPos, out Quaternion decoyRot);
        Assert.Less(Vector3.Distance(slot4.transform.position, decoyPos), 0.0001f);
        Assert.Less(Quaternion.Angle(slot4.transform.rotation, decoyRot), 0.05f);
    }

    [Test]
    public void VRHeadsetVisualFeedback_ShaderAndStereoVignettePulse_WorkCleanly()
    {
        Shader shader = Shader.Find("VRSim/VRHeadsetVignetteOverlay");
        Assert.IsNotNull(shader, "VRSim/VRHeadsetVignetteOverlay shader must be compiled and discoverable.");

        Material resourceMat = Resources.Load<Material>("VRHeadsetVignetteMat");
        Assert.IsNotNull(resourceMat, "Resources/VRHeadsetVignetteMat.mat must exist for player builds.");
        Assert.AreEqual(shader, resourceMat.shader);

        var camGo = Track(new GameObject("TestStereoCam", typeof(Camera)));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.stereoTargetEye = StereoTargetEyeMask.Both;

        var fbGo = Track(new GameObject("TestVRFeedback", typeof(VRHeadsetVisualFeedback)));
        var fb = fbGo.GetComponent<VRHeadsetVisualFeedback>();
        SetMember(fb, "targetCamera", cam);
        typeof(VRHeadsetVisualFeedback).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(fb, null);

        EvaluationLogger.Instance.ResetSession();
        EvaluationLogger.Instance.LogViolation(ClinicalViolationType.Incorrect_Table_Slot, "Wrong table slot", "Slot 4", 5, true);

        Transform overlayTf = cam.transform.Find("VR_RedVignette_Overlay");
        Assert.IsNotNull(overlayTf);
        Track(overlayTf.gameObject);

        var mf = overlayTf.GetComponent<MeshFilter>();
        var mr = overlayTf.GetComponent<MeshRenderer>();
        Assert.IsNotNull(mf?.sharedMesh);
        Assert.GreaterOrEqual(mf.sharedMesh.bounds.extents.x, 1.5f);
        Assert.IsNotNull(mr?.sharedMaterial);

        // Step the fade coroutine from 0 -> peak intensity (1) -> fade back down to 0
        var routine = typeof(VRHeadsetVisualFeedback)
            .GetMethod("RedVignetteRoutine", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(fb, new object[] { 1.0f }) as IEnumerator;
        Assert.IsNotNull(routine);

        float maxObservedIntensity = 0f;
        int steps = 0;
        while (routine.MoveNext() && steps++ < 200)
        {
            if (mr.sharedMaterial.HasProperty("_Intensity"))
            {
                maxObservedIntensity = Mathf.Max(maxObservedIntensity, mr.sharedMaterial.GetFloat("_Intensity"));
            }
        }

        Assert.GreaterOrEqual(maxObservedIntensity, 0.99f, "Vignette must reach full peak intensity smoothly.");
        Assert.IsFalse(mr.enabled, "Vignette MeshRenderer must disable itself once faded out to save draw calls.");
    }

    [Test]
    public void MainScene_PreservesVisualUiWarningsAndSyncedSlotDefaults()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sceneText = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Scenes", "MainScene.unity"));
        string graphicsText = File.ReadAllText(Path.Combine(projectRoot, "ProjectSettings", "GraphicsSettings.asset"));

        Assert.IsTrue(graphicsText.Contains("091ca51ac5d948039565f5cfb0fe6d3e"),
            "GraphicsSettings.asset must include VRHeadsetVignetteOverlay shader GUID in AlwaysIncludedShaders.");
        Assert.IsTrue(sceneText.Contains("m_Name: VISUAL UI WARNINGS"), "VISUAL UI WARNINGS must remain in MainScene.");
        Assert.IsTrue(sceneText.Contains("snapPositionOffset: {x: 0.00815052, y: 0.009960055, z: -0.007434741}"));
        Assert.IsTrue(sceneText.Contains("snapPositionOffset: {x: -0.10560019, y: -0.01859963, z: 0.018400135}"));
        Assert.IsTrue(sceneText.Contains("snapPositionOffset: {x: 0.0022, y: -0.0174, z: 0.0032}"));
    }
}
