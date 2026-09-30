#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class Table2SlotsSetupHelper : MonoBehaviour
{
    [MenuItem("Tools/Setup Table 2 Slots (6 Slots)")]
    public static void SetupTable2Slots()
    {
        // 1. Find Table 2 or Mayo Table in scene
        GameObject table2Obj = GameObject.Find("Table 2") 
                            ?? GameObject.Find("Table2") 
                            ?? GameObject.Find("MayoTable") 
                            ?? GameObject.Find("Table_2");

        Transform parentTransform = table2Obj != null ? table2Obj.transform : null;

        if (parentTransform == null)
        {
            var allObjects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in allObjects)
            {
                if (t.name.Contains("Table") && t.name.Contains("2"))
                {
                    table2Obj = t.gameObject;
                    parentTransform = t;
                    break;
                }
            }
        }

        // 2. Find or Create TablePrepManager
        TablePrepManager manager = Object.FindFirstObjectByType<TablePrepManager>();
        if (manager == null)
        {
            GameObject managerObj = new GameObject("Table2_Prep_Manager");
            if (parentTransform != null) managerObj.transform.SetParent(parentTransform, true);
            manager = managerObj.AddComponent<TablePrepManager>();
        }

        if (parentTransform != null) manager.table2Root = parentTransform;

        // 3. Find or Create 6 Slot GameObjects
        Transform slotsContainer = manager.transform.Find("Table2_Slots");
        if (slotsContainer == null)
        {
            GameObject containerObj = new GameObject("Table2_Slots");
            containerObj.transform.SetParent(manager.transform, false);
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;
            slotsContainer = containerObj.transform;
        }

        List<TablePrepSlot> slotsList = new List<TablePrepSlot>();

        // Default tool IDs for the 6 slots (can be freely edited by user in Inspector)
        string[] defaultToolIDs = new string[6]
        {
            "Mayo_Scissors",
            "Kelly_Clamp_1",
            "Kelly_Clamp_2",
            "Cord_Clamp",
            "Needle_Holder",
            "Thumb_Forceps"
        };

        // If creating new slots, calculate default offset
        float spacing = 0.10f;
        float startOffset = -0.25f;

        for (int i = 1; i <= 6; i++)
        {
            Transform existingSlot = slotsContainer.Find($"Slot_{i}");
            GameObject slotObj;
            if (existingSlot != null)
            {
                // PRESERVE user's adjusted position!
                slotObj = existingSlot.gameObject;
            }
            else
            {
                slotObj = new GameObject($"Slot_{i}");
                slotObj.transform.SetParent(slotsContainer, false);
                slotObj.transform.localPosition = new Vector3(startOffset + (i - 1) * spacing, 0f, 0f);
                slotObj.transform.localRotation = Quaternion.identity;
            }

            TablePrepSlot slot = slotObj.GetComponent<TablePrepSlot>();
            if (slot == null) slot = slotObj.AddComponent<TablePrepSlot>();

            slot.slotNumber = i;
            slot.slotLabel = $"Slot {i}";
            if (string.IsNullOrEmpty(slot.assignedToolID) && slot.assignedTool == null)
            {
                slot.assignedToolID = defaultToolIDs[i - 1];
            }
            slot.snapTransform = slotObj.transform;
            slot.snapRadius = 0.18f;
            slot.ghostPlateSize = new Vector3(0.09f, 0.002f, 0.09f);

            // Clean up old box ghost if present
            Transform oldBox = slot.transform.Find($"Slot_{i}_GhostBox");
            if (oldBox != null)
            {
                Object.DestroyImmediate(oldBox.gameObject);
            }

            slot.CreateGhostPlateIfNull();
            slotsList.Add(slot);
        }

        manager.slots = slotsList;

        EditorUtility.SetDirty(manager.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        Debug.Log($"[Table2SlotsSetupHelper] Successfully configured TablePrepManager with {slotsList.Count} slots (Slots 1 to 6) and circular plate ghosts!");
        Selection.activeGameObject = manager.gameObject;
    }
}
#endif
