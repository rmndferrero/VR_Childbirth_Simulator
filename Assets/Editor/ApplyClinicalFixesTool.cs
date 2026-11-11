using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Idempotent Editor utility that applies and verifies the scene-side configuration across MainScene and SummaryScene:
/// 1. Standardizes Table 1 instruments to Forceps-parity rigid hold + handle AttachPoints (preserving Forceps, Dry_Linen, Pitcher).
/// 2. Wires Table 2 customSnapPoints to manual (1) visual reference duplicates, disables/strips all 18 duplicates, and resets isMayoDraped.
/// 3. Wires PerinealCareManager to active Pitcher, builds/calibrates Perineal_Wash_Zone strictly to the perineal field, and links PitcherPour.
/// 4. Rebuilds DialogueCanvas/SummaryScreen and SummaryScene.unity with the redesigned multi-page Assessment UI.
/// </summary>
public static class ApplyClinicalFixesTool
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string InterBoldFontPath = "Assets/Fonts/Inter_Bold_SDF.asset";
    private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly HashSet<string> PreservedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Pickup Forceps",
        "Handling Forceps",
        "Dry_Linen",
        "Pitcher",
        "Old Pitcher"
    };

    private static readonly HashSet<string> Table1InstrumentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Forceps",
        "Tongs",
        "Scissor",
        "Mayo_Scissors",
        "Bandage_Scissors",
        "Umbi_Clamp",
        "Scalpel",
        "Curette",
        "Kidney_Basin",
        "straight_catheter",
        "gauze"
    };

    private static readonly (int slotNumber, string refName)[] CustomSnapMappings =
    {
        (4, "Kidney_Basin (1)"),
        (5, "straight_catheter (1)"),
        (6, "gauze (1)")
    };

    [MenuItem("Tools/Apply Clinical Fixes & Rebuild Summary Scenes")]
    public static void ApplyAllClinicalFixes()
    {
        if (!File.Exists(MainScenePath))
        {
            Debug.LogError($"[ApplyClinicalFixesTool] Cannot find {MainScenePath}");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene activeSceneBefore = SceneManager.GetActiveScene();
        string returnScenePath = activeSceneBefore.IsValid() ? activeSceneBefore.path : MainScenePath;

        Scene mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        Transform table2SlotsTf = FindTransformByName("Table2_Slots");
        int toolsCalibrated = ConfigureTable1Instruments(table2SlotsTf);
        int duplicatesCleaned = ConfigureTable2Preparation(table2SlotsTf);
        bool washZoneConfigured = ConfigurePerinealWashZoneAndPitcher();
        bool mainSummaryRebuilt = RebuildMainSceneSummaryAndWireManagers();

        EditorSceneManager.MarkSceneDirty(mainScene);
        EditorSceneManager.SaveScene(mainScene);

        TMP_FontAsset font = LoadPreferredTMPFont();
        SetupSimulationSceneFlow.BuildSummaryScene(font);
        SetupSimulationSceneFlow.UpdateEditorBuildSettings();

        if (!string.IsNullOrEmpty(returnScenePath) && File.Exists(returnScenePath))
        {
            EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);
        }
        else
        {
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[ApplyClinicalFixesTool] Complete! " +
            $"Table1Tools={toolsCalibrated}, Table2DuplicatesCleaned={duplicatesCleaned}, " +
            $"WashZoneConfigured={washZoneConfigured}, MainSummaryRebuilt={mainSummaryRebuilt}.");
    }

    [MenuItem("Tools/Create & Select Perineal Wash Zone Collider")]
    public static void CreateAndSelectPerinealWashZone()
    {
        if (!File.Exists(MainScenePath))
        {
            Debug.LogError($"[ApplyClinicalFixesTool] Cannot find {MainScenePath}");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != MainScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            activeScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }

        bool resetBounds = FindTransformByName("Perineal_Wash_Zone") == null;
        ConfigurePerinealWashZoneAndPitcher(resetBounds, out GameObject washZoneGo);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        if (washZoneGo != null)
        {
            Selection.activeGameObject = washZoneGo;
            EditorGUIUtility.PingObject(washZoneGo);
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
            Debug.Log(resetBounds
                ? "[ApplyClinicalFixesTool] Created, wired, and selected Mother/Perineal_Wash_Zone."
                : "[ApplyClinicalFixesTool] Verified wiring and selected existing Mother/Perineal_Wash_Zone (preserved manual transform/collider size).");
        }
    }

    private static int ConfigureTable1Instruments(Transform table2SlotsTf)
    {
        int configured = 0;
        var allTools = UnityEngine.Object.FindObjectsByType<ToolItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (ToolItem tool in allTools)
        {
            if (tool == null || tool.gameObject.scene.path != MainScenePath) continue;
            if (IsDescendantOf(tool.transform, table2SlotsTf)) continue;

            string trimmedName = tool.gameObject.name.Trim();
            if (PreservedToolNames.Contains(trimmedName)) continue;
            if (!Table1InstrumentNames.Contains(trimmedName)) continue;

            ConfigureSingleTable1Tool(tool);
            configured++;
        }

        return configured;
    }

    private static void ConfigureSingleTable1Tool(ToolItem tool)
    {
        Undo.RecordObject(tool, "Configure Table 1 Tool Grip");
        tool.autoAlignHandleGrip = true;
        tool.attachPositionOffset = Vector3.zero;
        tool.attachEulerAngles = Vector3.zero;
        tool.RebuildHandAttachPoint();

        XRGrabInteractable grab = tool.GetComponent<XRGrabInteractable>()
            ?? tool.GetComponentInParent<XRGrabInteractable>()
            ?? tool.GetComponentInChildren<XRGrabInteractable>(true);
        if (grab != null)
        {
            Undo.RecordObject(grab, "Apply Forceps XRGrabInteractable Parity");
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.attachEaseInTime = 0f;
            grab.useDynamicAttach = false;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.trackScale = false;
            grab.throwOnDetach = false;
            if (tool.handAttachPoint != null)
            {
                grab.attachTransform = tool.handAttachPoint;
            }
            grab.interactionLayers = grab.interactionLayers.value | 2048;
            EditorUtility.SetDirty(grab);
        }

        var transformers = tool.GetComponentsInChildren<XRGeneralGrabTransformer>(true);
        foreach (XRGeneralGrabTransformer transformer in transformers)
        {
            if (transformer == null) continue;
            Undo.RecordObject(transformer, "Disable Scaling on XRGeneralGrabTransformer");
            transformer.allowOneHandedScaling = false;
            transformer.allowTwoHandedScaling = false;
            EditorUtility.SetDirty(transformer);
        }

        Rigidbody rb = tool.GetComponent<Rigidbody>() ?? tool.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            Undo.RecordObject(rb, "Set Rigid Tool Rigidbody Settings");
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            EditorUtility.SetDirty(rb);
        }

        EditorUtility.SetDirty(tool);
        if (tool.handAttachPoint != null)
        {
            EditorUtility.SetDirty(tool.handAttachPoint.gameObject);
        }
    }

    private static int ConfigureTable2Preparation(Transform table2SlotsTf)
    {
        TablePrepManager prepManager = UnityEngine.Object.FindFirstObjectByType<TablePrepManager>(FindObjectsInactive.Include);
        if (prepManager != null)
        {
            Undo.RecordObject(prepManager, "Reset Table2_Prep_Manager Initial State");
            prepManager.isMayoDraped = false;
            prepManager.isTablePrepActive = false;
            prepManager.isTablePrepCompleted = false;
            EditorUtility.SetDirty(prepManager);
        }

        if (table2SlotsTf == null)
        {
            Debug.LogWarning("[ApplyClinicalFixesTool] Table2_Slots transform not found in MainScene.");
            return 0;
        }

        TablePrepSlot[] slots = table2SlotsTf.GetComponentsInChildren<TablePrepSlot>(true);
        var slotByNumber = new Dictionary<int, TablePrepSlot>();
        foreach (TablePrepSlot slot in slots)
        {
            if (slot == null) continue;
            Undo.RecordObject(slot, "Normalize TablePrepSlot");
            slot.isFilled = false;
            slot.currentPlacedTool = null;
            if (slot.snapTransform == null)
            {
                slot.snapTransform = slot.transform;
            }
            slotByNumber[slot.slotNumber] = slot;
            EditorUtility.SetDirty(slot);
        }

        // Wire manual reference duplicates in Slot 4, 5, 6 as customSnapPoint before deactivating them.
        Transform[] allTable2Transforms = table2SlotsTf.GetComponentsInChildren<Transform>(true);
        foreach ((int slotNumber, string refName) in CustomSnapMappings)
        {
            if (!slotByNumber.TryGetValue(slotNumber, out TablePrepSlot slot) || slot == null) continue;

            Transform match = FindExactChildTransform(allTable2Transforms, refName);
            if (match != null)
            {
                Undo.RecordObject(slot, $"Wire Slot {slotNumber} CustomSnapPoint");
                slot.customSnapPoint = match;
                EditorUtility.SetDirty(slot);
            }
            else
            {
                Debug.LogWarning($"[ApplyClinicalFixesTool] Reference '{refName}' not found under Table2_Slots for Slot_{slotNumber}.");
            }
        }

        // Identify duplicate visual-reference roots (top-most matching objects inside Table2_Slots).
        var duplicateRoots = new List<GameObject>();
        foreach (Transform tf in allTable2Transforms)
        {
            if (tf == null || tf == table2SlotsTf) continue;
            if (!IsTable2ReferenceDuplicateRoot(tf)) continue;
            if (HasAncestorInList(tf, duplicateRoots)) continue;
            duplicateRoots.Add(tf.gameObject);
        }

        int cleaned = 0;
        foreach (GameObject dup in duplicateRoots)
        {
            if (dup == null) continue;
            StripInteractableAndPhysicsComponents(dup);
            if (dup.activeSelf)
            {
                Undo.RecordObject(dup, "Deactivate Table2 Visual Reference Duplicate");
                dup.SetActive(false);
            }
            EditorUtility.SetDirty(dup);
            cleaned++;
        }

        return cleaned;
    }

    private static bool IsTable2ReferenceDuplicateRoot(Transform tf)
    {
        if (tf.GetComponent<TablePrepSlot>() != null) return false;
        string name = tf.gameObject.name;
        if (name.StartsWith("Slot_", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.EndsWith("GhostPlate", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.IndexOf("SnapPoint", StringComparison.OrdinalIgnoreCase) >= 0) return false;

        if (tf.GetComponent<ToolItem>() != null || tf.GetComponent<XRGrabInteractable>() != null)
        {
            return true;
        }

        return name.StartsWith("Kidney_Basin", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("straight_catheter", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("gauze", StringComparison.OrdinalIgnoreCase);
    }

    private static void StripInteractableAndPhysicsComponents(GameObject root)
    {
        foreach (var comp in root.GetComponentsInChildren<XRGeneralGrabTransformer>(true)) RemoveOrDisableComponent(comp);
        foreach (var comp in root.GetComponentsInChildren<XRGrabInteractable>(true)) RemoveOrDisableComponent(comp);
        foreach (var comp in root.GetComponentsInChildren<ToolItem>(true)) RemoveOrDisableComponent(comp);
        foreach (var comp in root.GetComponentsInChildren<Rigidbody>(true)) RemoveOrDisableComponent(comp);
        foreach (var comp in root.GetComponentsInChildren<Collider>(true)) RemoveOrDisableComponent(comp);
    }

    private static void RemoveOrDisableComponent(Component comp)
    {
        if (comp == null) return;
        if (!PrefabUtility.IsPartOfPrefabInstance(comp) || PrefabUtility.IsAddedComponentOverride(comp))
        {
            Undo.DestroyObjectImmediate(comp);
            return;
        }

        if (comp is Behaviour behaviour) behaviour.enabled = false;
        else if (comp is Collider col) col.enabled = false;
        else if (comp is Rigidbody rb) rb.isKinematic = true;
    }

    private static bool ConfigurePerinealWashZoneAndPitcher()
    {
        return ConfigurePerinealWashZoneAndPitcher(false, out _);
    }

    private static bool ConfigurePerinealWashZoneAndPitcher(bool resetBoundsIfExists, out GameObject washZoneGo)
    {
        PerinealCareManager careManager = UnityEngine.Object.FindFirstObjectByType<PerinealCareManager>(FindObjectsInactive.Include);
        PitcherPour activePitcher = FindActivePitcher();

        if (careManager != null && activePitcher != null)
        {
            Undo.RecordObject(careManager, "Wire Active PitcherPour to PerinealCareManager");
            careManager.pitcherPour = activePitcher;
            EditorUtility.SetDirty(careManager);
        }

        Transform existingWashZoneTf = FindTransformByName("Perineal_Wash_Zone");
        washZoneGo = existingWashZoneTf != null ? existingWashZoneTf.gameObject : null;
        Transform motherTf = FindTransformByName("Mother");

        bool createdNew = false;
        if (washZoneGo == null)
        {
            washZoneGo = new GameObject("Perineal_Wash_Zone");
            Undo.RegisterCreatedObjectUndo(washZoneGo, "Create Perineal_Wash_Zone");
            if (motherTf != null)
            {
                washZoneGo.transform.SetParent(motherTf, false);
            }
            washZoneGo.transform.localPosition = new Vector3(-0.002f, 0.284f, 0.906f);
            washZoneGo.transform.localRotation = Quaternion.identity;
            washZoneGo.transform.localScale = Vector3.one;
            createdNew = true;
        }
        else if (resetBoundsIfExists)
        {
            Undo.RecordObject(washZoneGo.transform, "Reset Perineal_Wash_Zone Transform");
            if (motherTf != null && washZoneGo.transform.parent != motherTf)
            {
                washZoneGo.transform.SetParent(motherTf, false);
            }
            washZoneGo.transform.localPosition = new Vector3(-0.002f, 0.284f, 0.906f);
            washZoneGo.transform.localRotation = Quaternion.identity;
            washZoneGo.transform.localScale = Vector3.one;
        }

        washZoneGo.layer = 0;
        washZoneGo.SetActive(true);

        BoxCollider box = washZoneGo.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider>(washZoneGo);
            createdNew = true;
        }
        Undo.RecordObject(box, "Configure Perineal_Wash_Zone Collider");
        box.isTrigger = true;
        if (createdNew || resetBoundsIfExists)
        {
            box.center = Vector3.zero;
            box.size = new Vector3(0.14f, 0.18f, 0.18f);
        }

        if (washZoneGo.GetComponent<PerinealWashZoneGizmo>() == null)
        {
            Undo.AddComponent<PerinealWashZoneGizmo>(washZoneGo);
        }

        EditorUtility.SetDirty(box);
        EditorUtility.SetDirty(washZoneGo);

        if (activePitcher != null)
        {
            Undo.RecordObject(activePitcher, "Wire Perineal_Wash_Zone to PitcherPour");
            activePitcher.perinealWashZoneCollider = box;
            EditorUtility.SetDirty(activePitcher);
        }

        return activePitcher != null;
    }

    private static bool RebuildMainSceneSummaryAndWireManagers()
    {
        TMP_FontAsset font = LoadPreferredTMPFont();
        Transform dialogueCanvas = FindTransformByName("DialogueCanvas");
        if (dialogueCanvas == null)
        {
            Debug.LogWarning("[ApplyClinicalFixesTool] DialogueCanvas not found in MainScene; skipping fallback SummaryScreen rebuild.");
            return false;
        }

        Transform oldSummary = dialogueCanvas.Find("SummaryScreen");
        if (oldSummary != null)
        {
            Undo.DestroyObjectImmediate(oldSummary.gameObject);
        }

        GameObject newSummary = BuildSimulationUI.BuildSummaryScreen(dialogueCanvas, font);
        if (newSummary == null) return false;

        Undo.RegisterCreatedObjectUndo(newSummary, "Build Redesigned SummaryScreen");
        RectTransform rt = newSummary.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(820f, 620f);
            rt.localScale = Vector3.one;
        }
        newSummary.SetActive(false);

        VRDemoGameManager gm = UnityEngine.Object.FindFirstObjectByType<VRDemoGameManager>(FindObjectsInactive.Include);
        if (gm != null)
        {
            Undo.RecordObject(gm, "Wire VRDemoGameManager References");
            gm.summaryScreenController = newSummary.GetComponent<SummaryScreenController>();
            if (gm.perinealCareManager == null)
            {
                gm.perinealCareManager = UnityEngine.Object.FindFirstObjectByType<PerinealCareManager>(FindObjectsInactive.Include);
            }
            if (gm.motherDrapingManager == null)
            {
                gm.motherDrapingManager = UnityEngine.Object.FindFirstObjectByType<MotherDrapingManager>(FindObjectsInactive.Include);
            }
            EditorUtility.SetDirty(gm);
        }

        EditorUtility.SetDirty(newSummary);
        return true;
    }

    [MenuItem("Tools/Rebuild Beta Summary Screens Only")]
    public static void RebuildBetaSummaryScreensOnly()
    {
        const string SummaryScenePath = "Assets/Scenes/SummaryScene.unity";
        TMP_FontAsset font = LoadPreferredTMPFont();

        if (File.Exists(MainScenePath))
        {
            Scene mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            bool mainSummaryRebuilt = RebuildMainSceneSummaryAndWireManagers();
            if (mainSummaryRebuilt)
            {
                EditorSceneManager.MarkSceneDirty(mainScene);
                EditorSceneManager.SaveScene(mainScene);
            }
            Debug.Log($"[ApplyClinicalFixesTool] MainScene Beta Summary rebuilt: {mainSummaryRebuilt}");
        }

        if (File.Exists(SummaryScenePath))
        {
            Scene summaryScene = EditorSceneManager.OpenScene(SummaryScenePath, OpenSceneMode.Single);
            Transform summaryCanvas = null;
            foreach (Transform tf in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tf != null && tf.gameObject.scene == summaryScene && tf.gameObject.name == "SummaryCanvas")
                {
                    summaryCanvas = tf;
                    break;
                }
            }

            if (summaryCanvas != null)
            {
                RectTransform canvasRt = summaryCanvas.GetComponent<RectTransform>();
                if (canvasRt != null)
                {
                    canvasRt.sizeDelta = new Vector2(820f, 620f);
                    canvasRt.localScale = Vector3.one * 0.0019f;
                }

                for (int i = summaryCanvas.childCount - 1; i >= 0; i--)
                {
                    Transform child = summaryCanvas.GetChild(i);
                    if (child != null && child.name == "SummaryScreen")
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }

                GameObject newSummary = BuildSimulationUI.BuildSummaryScreen(summaryCanvas, font);
                if (newSummary != null)
                {
                    newSummary.SetActive(true);
                    EditorUtility.SetDirty(newSummary);
                }

                EditorSceneManager.MarkSceneDirty(summaryScene);
                EditorSceneManager.SaveScene(summaryScene);
                Debug.Log("[ApplyClinicalFixesTool] SummaryScene Beta Summary rebuilt and saved.");
            }
        }

        if (File.Exists(MainScenePath))
        {
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }
        AssetDatabase.SaveAssets();
    }

    private static PitcherPour FindActivePitcher()
    {
        PitcherPour fallback = null;
        foreach (PitcherPour p in UnityEngine.Object.FindObjectsByType<PitcherPour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p == null || p.gameObject.scene.path != MainScenePath) continue;
            if (p.gameObject.name.IndexOf("Old", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (p.gameObject.activeSelf && p.gameObject.name.Equals("Pitcher", StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
            if (p.gameObject.activeInHierarchy) fallback = p;
        }
        return fallback;
    }

    private static TMP_FontAsset LoadPreferredTMPFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterBoldFontPath);
        if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        return font;
    }

    private static Transform FindTransformByName(string exactName)
    {
        foreach (Transform tf in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tf != null && tf.gameObject.scene.path == MainScenePath && tf.gameObject.name == exactName)
            {
                return tf;
            }
        }
        return null;
    }

    private static Transform FindExactChildTransform(Transform[] transforms, string exactName)
    {
        foreach (Transform tf in transforms)
        {
            if (tf != null && tf.gameObject.name == exactName) return tf;
        }
        return null;
    }

    private static bool IsDescendantOf(Transform candidate, Transform ancestor)
    {
        if (candidate == null || ancestor == null) return false;
        return candidate == ancestor || candidate.IsChildOf(ancestor);
    }

    private static bool HasAncestorInList(Transform tf, List<GameObject> roots)
    {
        foreach (GameObject root in roots)
        {
            if (root != null && tf != root.transform && tf.IsChildOf(root.transform))
            {
                return true;
            }
        }
        return false;
    }
}
