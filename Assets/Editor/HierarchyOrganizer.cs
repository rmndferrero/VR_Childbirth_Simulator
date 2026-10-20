using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HierarchyOrganizer
{
    [MenuItem("Tools/Sort Scene Hierarchy")]
    public static void SortHierarchy()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "MainScene")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            scene = EditorSceneManager.GetActiveScene();
        }

        var existingRoots = scene.GetRootGameObjects().ToDictionary(g => g.name, g => g);

        string[] hierarchyOrder = new string[]
        {
            // --- SECTION 1: CORE MANAGERS ---
            "=== [1] CORE MANAGERS ===",
            "GameManager",
            "PerinealCareManager",
            "StrokeTrackingManager",

            // --- SECTION 2: PLAYER & VR RIG ---
            "=== [2] PLAYER & VR RIG ===",
            "Player",
            "PlayerSpawnPoint",
            "XR Interaction Manager",
            "EventSystem",

            // --- SECTION 3: PATIENT & DRAPING ---
            "=== [3] PATIENT & CLINICAL MODEL ===",
            "Mother",

            // --- SECTION 4: CLINICAL TABLES & TOOLS ---
            "=== [4] CLINICAL TABLES & TOOLS ===",
            "Table 1",
            "Table 2",
            "Table 3",
            "Temporary_WasteBasin",
            "CottonBall",
            "NewBorn_Table",
            "baby_scale",

            // --- SECTION 5: UI & HUD CANVASES ---
            "=== [5] UI & HUD CANVASES ===",
            "DialogueCanvas",
            "CleaningProgressCanvas",
            "StrokeGuideCanvas",
            "VISUAL UI WARNINGS",

            // --- SECTION 6: ENVIRONMENT & LIGHTING ---
            "=== [6] ENVIRONMENT & LIGHTING ===",
            "ENVIRONMENT",
            "Directional Light",
            "Global_PostProcess_Volume"
        };

        // Clean up any old dividers
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name.StartsWith("--- [") || r.name.StartsWith("=== ["))
            {
                Object.DestroyImmediate(r);
            }
        }

        existingRoots = scene.GetRootGameObjects().ToDictionary(g => g.name, g => g);

        int siblingIndex = 0;
        foreach (var itemName in hierarchyOrder)
        {
            GameObject go = null;

            if (itemName.StartsWith("==="))
            {
                go = new GameObject(itemName);
                go.tag = "EditorOnly";
            }
            else if (existingRoots.TryGetValue(itemName, out var foundGo))
            {
                go = foundGo;
            }

            if (go != null)
            {
                go.transform.SetSiblingIndex(siblingIndex++);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[HierarchyOrganizer] Scene Hierarchy successfully sorted into clean logical sections!");
    }
}
