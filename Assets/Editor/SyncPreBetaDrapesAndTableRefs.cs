#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Idempotent Editor utility to wire Adjusted_Drapes child meshes into MotherDrapingManager
/// (preserving all user transforms) and extract Table 2 reference poses from duplicated
/// Forceps, Scalpel, Tongs, and Curette objects into TablePrepSlot snap references.
/// </summary>
public static class SyncPreBetaDrapesAndTableRefs
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const int MotherDrapeSlotCount = 4;

    [MenuItem("Tools/Perineal Care/Sync Pre-Beta Drapes and Table References")]
    public static void RunInteractiveSync()
    {
        bool changed = ApplySyncToActiveScene();
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log($"[SyncPreBetaDrapesAndTableRefs] Sync complete (changed={changed}).");
    }

    public static bool SyncMainSceneInBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        bool changed = ApplySyncToActiveScene();
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"[SyncPreBetaDrapesAndTableRefs] Batch sync on {MainScenePath} complete (changed={changed}).");
        return changed;
    }

    public static bool ApplySyncToActiveScene()
    {
        bool changed = false;
        changed |= SyncAdjustedDrapes();
        changed |= SyncTable2ReferenceDuplicates();
        return changed;
    }

    private static bool SyncAdjustedDrapes()
    {
        MotherDrapingManager mgr = UnityEngine.Object.FindFirstObjectByType<MotherDrapingManager>(FindObjectsInactive.Include);
        if (mgr == null) return false;

        Transform adjustedRoot = MotherDrapingManager.FindAdjustedDrapesRoot();
        bool changed = false;
        Undo.RecordObject(mgr, "Sync Placed Drape Slots");

        int limit = Mathf.Min(MotherDrapeSlotCount, mgr.drapeSlots.Count);
        for (int i = 0; i < limit; i++)
        {
            DrapeSlot slot = mgr.drapeSlots[i];
            if (slot == null) continue;

            GameObject resolved = slot.final3DModelPrefabOrObject != null
                ? slot.final3DModelPrefabOrObject
                : MotherDrapingManager.ResolvePlacedDrapeForSlot(slot, mgr.drapeSlots, i, adjustedRoot);

            if (resolved != null)
            {
                if (resolved.activeSelf)
                {
                    Undo.RecordObject(resolved, $"Hide {resolved.name} Initially");
                    resolved.SetActive(false);
                    EditorUtility.SetDirty(resolved);
                    changed = true;
                }

                if (slot.final3DModelPrefabOrObject != resolved)
                {
                    slot.final3DModelPrefabOrObject = resolved;
                    changed = true;
                }
            }

            if (slot.placeholderGreenDrape != null && slot.placeholderGreenDrape != slot.final3DModelPrefabOrObject)
            {
                if (slot.placeholderGreenDrape.activeSelf)
                {
                    Undo.RecordObject(slot.placeholderGreenDrape, "Disable White Placeholder Drape");
                    slot.placeholderGreenDrape.SetActive(false);
                    EditorUtility.SetDirty(slot.placeholderGreenDrape);
                    changed = true;
                }
            }
        }

        if (adjustedRoot != null && !adjustedRoot.gameObject.activeSelf)
        {
            Undo.RecordObject(adjustedRoot.gameObject, "Activate Adjusted_Drapes Root");
            adjustedRoot.gameObject.SetActive(true);
            EditorUtility.SetDirty(adjustedRoot.gameObject);
            changed = true;
        }

        if (changed) EditorUtility.SetDirty(mgr);
        return changed;
    }

    private static bool SyncTable2ReferenceDuplicates()
    {
        TablePrepManager prepMgr = UnityEngine.Object.FindFirstObjectByType<TablePrepManager>(FindObjectsInactive.Include);
        if (prepMgr == null) return false;

        prepMgr.CacheSlots();
        List<TablePrepSlot> slots = prepMgr.slots;
        if (slots == null || slots.Count == 0) return false;

        Transform table2Root = prepMgr.table2Root != null ? prepMgr.table2Root : FindSceneTransformByName("Table 2");
        if (table2Root == null) return false;

        float meanSlotY = 0f;
        int validSlots = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            Transform root = slots[i].snapTransform != null ? slots[i].snapTransform : slots[i].transform;
            meanSlotY += root.position.y;
            validSlots++;
        }
        if (validSlots > 0) meanSlotY /= validSlots;

        bool changed = false;
        ToolItem[] allTools = UnityEngine.Object.FindObjectsByType<ToolItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (ToolItem candidate in allTools)
        {
            if (candidate == null || !candidate.transform.IsChildOf(table2Root)) continue;
            if (!candidate.gameObject.name.Contains("(")) continue;

            string key = TablePrepSlot.CanonicalizeToolKey(candidate.toolID);
            if (string.IsNullOrEmpty(key)) key = TablePrepSlot.CanonicalizeToolKey(candidate.gameObject.name);
            if (string.IsNullOrEmpty(key)) continue;

            bool hasPrimaryOutsideTable2 = false;
            foreach (ToolItem other in allTools)
            {
                if (other == null || other == candidate || other.transform.IsChildOf(table2Root)) continue;
                string otherKey = TablePrepSlot.CanonicalizeToolKey(other.toolID);
                if (string.IsNullOrEmpty(otherKey)) otherKey = TablePrepSlot.CanonicalizeToolKey(other.gameObject.name);
                if (otherKey == key)
                {
                    hasPrimaryOutsideTable2 = true;
                    break;
                }
            }

            if (!hasPrimaryOutsideTable2) continue;

            if (key == "artery_forceps")
            {
                TablePrepSlot slot1 = slots.Find(s => s != null && (s.slotNumber == 1 || TablePrepSlot.CanonicalizeToolKey(s.assignedToolID) == "artery_forceps"));
                if (slot1 != null)
                {
                    Undo.RecordObject(slot1, "Sync Slot 1 Forceps Pose");
                    Transform slotRoot = slot1.snapTransform != null ? slot1.snapTransform : slot1.transform;
                    slot1.snapPositionOffset = slotRoot.InverseTransformDirection(candidate.transform.position - slotRoot.position);
                    slot1.snapEulerAngles = (Quaternion.Inverse(slotRoot.rotation) * candidate.transform.rotation).eulerAngles;
                    slot1.useAbsoluteSnapRotation = false;
                    EditorUtility.SetDirty(slot1);
                    changed = true;
                }
            }
            else if (key == "scalpel" || key == "thumb_forceps" || key == "curette")
            {
                float yOffset = candidate.transform.position.y - meanSlotY;
                string idToStore = !string.IsNullOrEmpty(candidate.toolID) ? candidate.toolID : candidate.gameObject.name;

                for (int i = 0; i < slots.Count; i++)
                {
                    TablePrepSlot slot = slots[i];
                    if (slot == null) continue;
                    Undo.RecordObject(slot, $"Sync PerToolReference {idToStore} in Slot {slot.slotNumber}");
                    Transform slotRoot = slot.snapTransform != null ? slot.snapTransform : slot.transform;
                    Vector3 localEuler = (Quaternion.Inverse(slotRoot.rotation) * candidate.transform.rotation).eulerAngles;
                    slot.SetPerToolReference(idToStore, new Vector3(0f, yOffset, 0f), localEuler, true);
                    EditorUtility.SetDirty(slot);
                    changed = true;
                }
            }

            StripReferenceObject(candidate.gameObject);
            changed = true;
        }

        return changed;
    }

    private static void StripReferenceObject(GameObject go)
    {
        if (go == null) return;

        foreach (XRGrabInteractable grab in go.GetComponentsInChildren<XRGrabInteractable>(true))
            if (grab != null) Undo.DestroyObjectImmediate(grab);
        foreach (ToolItem toolItem in go.GetComponentsInChildren<ToolItem>(true))
            if (toolItem != null) Undo.DestroyObjectImmediate(toolItem);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) Undo.DestroyObjectImmediate(rb);
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
            if (col != null) Undo.DestroyObjectImmediate(col);

        if (go.activeSelf)
        {
            Undo.RecordObject(go, $"Deactivate Reference {go.name}");
            go.SetActive(false);
        }
        EditorUtility.SetDirty(go);
    }

    private static GameObject FindSceneGameObjectByName(string name)
    {
        Transform t = FindSceneTransformByName(name);
        return t != null ? t.gameObject : null;
    }

    private static Transform FindSceneTransformByName(string name)
    {
        Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
            {
                return all[i];
            }
        }
        return null;
    }
}
#endif
