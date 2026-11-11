using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages Step 7 (Mayo Table Setup / Tool Preparation).
/// Handles the 6 placement slots on Table 2, integrates with MayoSetupConfirmationUI,
/// allows students to arrange instruments, validates order upon confirmation ("Perfect" vs "One is wrong"),
/// and resets misplaced tools back to Table 1 until the student perfects the order.
/// </summary>
public class TablePrepManager : MonoBehaviour
{
    public static TablePrepManager Instance { get; private set; }

    [Header("Table 2 Slots (Slots 1 to 6)")]
    [Tooltip("All Table 2 slots in order.")]
    public List<TablePrepSlot> slots = new List<TablePrepSlot>();

    [Header("Table 2 Root / Parent")]
    public Transform table2Root;

    [Header("Mayo Setup Confirmation UI")]
    public MayoSetupConfirmationUI confirmationUI;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip correctSlotClip;
    public AudioClip wrongSlotClip;
    public AudioClip allSlotsCompletedClip;

    [Header("UI Feedback (Optional legacy)")]
    public TMP_Text statusText;

    [Header("State")]
    public bool isTablePrepActive = false;
    public bool isTablePrepCompleted = false;

    [Header("Mayo Stand Sterile Field State")]
    [Tooltip("Must be true (Mayo Stand draped with sterile linen) before any surgical instrument can be placed.")]
    public bool isMayoDraped = false;

    public event Action<int, int> OnSlotCountUpdated; // (filledCount, totalCount)
    public event Action OnTablePrepCompleted;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        CacheSlots();
        AbsorbTable2ReferenceDuplicatesIfPresent();
    }

    private void Start()
    {
        isMayoDraped = MayoStandDrapeSocket.Instance != null && MayoStandDrapeSocket.Instance.isDraped;
        CacheSlots();
    }

    private void Update()
    {
        if (!isTablePrepActive || isTablePrepCompleted)
        {
            foreach (var slot in slots)
            {
                if (slot != null) slot.SetHoverState(false, false);
            }
            return;
        }

        // Find the single closest slot to any currently held tool
        TablePrepSlot bestSlot = null;
        float bestDist = 0.14f; // Tight radius so only the slot directly underneath triggers

        foreach (var tool in ToolItem.AllToolsInScene)
        {
            if (tool != null && tool.IsBeingHeld)
            {
                foreach (var slot in slots)
                {
                    if (slot == null || (slot.isFilled && slot.currentPlacedTool != tool)) continue;
                    float d = Vector3.Distance(tool.transform.position, slot.GetTargetSnapPosition());
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestSlot = slot;
                    }
                }
            }
        }

        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.SetHoverState(slot == bestSlot, true);
            }
        }
    }

    public void CacheSlots()
    {
        if (slots == null || slots.Count == 0)
        {
            var found = GetComponentsInChildren<TablePrepSlot>(true);
            if (found != null && found.Length > 0)
            {
                slots = new List<TablePrepSlot>(found);
                slots.Sort((a, b) => a.slotNumber.CompareTo(b.slotNumber));
            }
        }
    }

    /// <summary>
    /// Called when the simulation enters Step 7 (Setup Table / Mayo Prep).
    /// Activates slots on Table 2 and reveals the confirmation UI.
    /// </summary>
    public void StartTablePrepPhase()
    {
        isTablePrepActive = true;
        isTablePrepCompleted = false;

        CacheSlots();

        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.ClearSlot();
                slot.ShowGhost();
            }
        }

        if (confirmationUI != null)
        {
            confirmationUI.Show();
            confirmationUI.ResetFeedback();
            confirmationUI.UpdateProgress(0, slots.Count);
        }

        UpdateStatusUI();
        Debug.Log($"[TablePrepManager] Step 7 Started! {slots.Count} tool slots active on Table 2.");
    }

    /// <summary>
    /// Checks if a tool released near Table 2 should snap to a slot or be rejected immediately (Option 2A).
    /// Returns true if handled by TablePrepManager.
    /// </summary>
    public bool TryHandleToolRelease(ToolItem tool)
    {
        if (tool == null || tool.GetComponent<TwoHandedLinenCloth>() != null) return false;

        CacheSlots();

        TablePrepSlot closestAvailableSlot = null;
        float closestAvailableDist = float.MaxValue;
        TablePrepSlot closestAnySlot = null;
        float closestAnyDist = float.MaxValue;

        Vector3 toolPos = tool.transform.position;

        foreach (var slot in slots)
        {
            if (slot == null) continue;

            float dist = Vector3.Distance(toolPos, slot.GetTargetSnapPosition());
            if (dist > slot.snapRadius) continue;

            if (dist < closestAnyDist)
            {
                closestAnyDist = dist;
                closestAnySlot = slot;
            }

            bool slotFreeOrSameTool = !slot.isFilled || slot.currentPlacedTool == null || slot.currentPlacedTool == tool;
            if (slotFreeOrSameTool && dist < closestAvailableDist)
            {
                closestAvailableDist = dist;
                closestAvailableSlot = slot;
            }
        }

        if (closestAnySlot == null)
        {
            // Not near any Table 2 slot
            return false;
        }

        // Gatekeeping: Enforce sterile field on Mayo Stand before accepting any instrument
        bool draped = isMayoDraped || (MayoStandDrapeSocket.Instance != null && MayoStandDrapeSocket.Instance.isDraped);
        if (!draped)
        {
            if (VRHeadsetVisualFeedback.Instance != null)
            {
                VRHeadsetVisualFeedback.Instance.TriggerUnsterileMayoViolation(tool.toolID);
            }
            PlaySound(wrongSlotClip);
            closestAnySlot.RejectWrongTool(tool);
            return true;
        }

        if (!isTablePrepActive || isTablePrepCompleted) return false;

        if (closestAvailableSlot == null)
        {
            // All nearby slots within snapRadius are already occupied; let release proceed without spoiling validity
            return false;
        }

        // Ensure a moved tool cannot remain registered in an old slot simultaneously
        foreach (var slot in slots)
        {
            if (slot != null && slot != closestAvailableSlot && slot.currentPlacedTool == tool)
            {
                slot.ClearSlot();
            }
        }

        // Blind slotting: snap ANY dropped surgical instrument neutrally with zero pass/fail feedback until Submit
        closestAvailableSlot.SnapCorrectTool(tool);
        PlaySound(correctSlotClip);

        UpdateStatusUI();
        return true;
    }

    public void NotifyToolPickedUp(ToolItem tool)
    {
        if (tool == null) return;
        CacheSlots();
        bool changed = false;
        foreach (var slot in slots)
        {
            if (slot != null && slot.isFilled && slot.currentPlacedTool == tool)
            {
                slot.ClearSlot();
                changed = true;
            }
        }
        if (changed) UpdateStatusUI();
    }

    private void RecordWrongSlotAttempt(TablePrepSlot slot, ToolItem tool)
    {
        string expected = slot != null
            ? (slot.assignedTool != null ? VRDemoGameManager.NormalizeToolDisplayName(slot.assignedTool.toolID) : (!string.IsNullOrEmpty(slot.assignedToolID) ? slot.assignedToolID : $"Slot {slot.slotNumber}"))
            : "Designated Slot";
        string actual = tool != null ? VRDemoGameManager.NormalizeToolDisplayName(tool.toolID) : "Instrument";
        RecordMistake($"Slot {slot?.slotNumber}: placed {actual} instead of {expected}");
    }

    /// <summary>
    /// Validates the full Mayo Table layout when the player presses [ Confirm Tool Setup ].
    /// Says "Perfect" if all 6 tools are in the correct slots;
    /// Says "One or more tools are wrong" and resets misplaced tools to Table 1 if any are wrong.
    /// </summary>
    public void ValidateAndConfirmSetup()
    {
        if (!isTablePrepActive || isTablePrepCompleted) return;

        CacheSlots();

        // 1. Check if all slots have an instrument placed
        int filledCount = 0;
        foreach (var slot in slots)
        {
            if (slot != null && slot.isFilled && slot.currentPlacedTool != null)
            {
                filledCount++;
            }
        }

        if (filledCount < slots.Count)
        {
            PlaySound(wrongSlotClip);
            if (confirmationUI != null)
            {
                confirmationUI.ShowIncompleteFeedback(filledCount, slots.Count);
            }
            return;
        }

        // 2. Check each slot for correct instrument assignment
        int correctCount = 0;
        List<TablePrepSlot> wrongSlots = new List<TablePrepSlot>();

        foreach (var slot in slots)
        {
            if (slot != null && slot.CanAccept(slot.currentPlacedTool))
            {
                correctCount++;
                if (slot.currentPlacedTool != null)
                {
                    slot.currentPlacedTool.MarkConfirmedCorrect();
                }
            }
            else if (slot != null)
            {
                wrongSlots.Add(slot);
            }
        }

        // Case A: 100% PERFECT!
        if (wrongSlots.Count == 0 && correctCount == slots.Count)
        {
            PlaySound(allSlotsCompletedClip != null ? allSlotsCompletedClip : correctSlotClip);
            if (confirmationUI != null)
            {
                confirmationUI.ShowPerfectFeedback();
            }

            foreach (var slot in slots)
            {
                if (slot != null && slot.currentPlacedTool != null)
                {
                    slot.currentPlacedTool.MarkConfirmedCorrect();
                    slot.currentPlacedTool.LockPermanentlyOnTable();
                }
            }

            StartCoroutine(WaitAndCompleteRoutine(2.5f));
        }
        // Case B: 1 or more instruments are wrong!
        else
        {
            PlaySound(wrongSlotClip);
            RecordMistake($"Incorrect Mayo Table Setup: {wrongSlots.Count} instruments misplaced on the tray.");

            if (VRHeadsetVisualFeedback.Instance != null)
            {
                VRHeadsetVisualFeedback.Instance.PlayFeedbackCuesOnly();
            }

            if (confirmationUI != null)
            {
                confirmationUI.ShowWrongFeedback(wrongSlots.Count);
            }

            // Reset only the misplaced tools back to Table 1 so student can retry and perfect the order
            foreach (var ws in wrongSlots)
            {
                if (ws.currentPlacedTool != null)
                {
                    var misplacedTool = ws.currentPlacedTool;
                    ws.ClearSlot();
                    misplacedTool.RestoreColors();
                    misplacedTool.TeleportToTable1();
                }
            }

            UpdateStatusUI();
        }
    }

    private IEnumerator WaitAndCompleteRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        CompleteTablePrep();
    }

    public void UpdateStatusUI()
    {
        int filledCount = 0;
        foreach (var slot in slots)
        {
            if (slot != null && slot.isFilled) filledCount++;
        }

        if (confirmationUI != null)
        {
            confirmationUI.UpdateProgress(filledCount, slots.Count);
        }

        if (statusText != null)
        {
            statusText.text = $"Mayo Table Setup: <color=#38BDF8><b>{filledCount}/{slots.Count} Tools Arranged</b></color>";
        }

        OnSlotCountUpdated?.Invoke(filledCount, slots.Count);
    }

    private void CompleteTablePrep()
    {
        if (isTablePrepCompleted) return;
        isTablePrepCompleted = true;
        isTablePrepActive = false;

        Debug.Log("[TablePrepManager] All tools verified and perfected on Mayo Table! Step 7 Completed.");
        EvaluationLogger.Instance.LogCorrect("Mayo table setup confirmed", MatrixCriterion.Technique);

        OnTablePrepCompleted?.Invoke();

        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.AdvanceFromStep7_SetupTable();
        }
        else if (PerinealCareManager.Instance != null)
        {
            PerinealCareManager.Instance.SetState(PerinealCareState.STATE_8_FINAL_SUMMARY);
        }
    }

    /// <summary>
    /// If the scene still contains active Table 2 duplicate reference tools (e.g. "Forceps (1)",
    /// "Tongs (1)", "Scalpel (1)", "Curette  (1)") alongside primary Table 1 tools, extracts
    /// their exact slot poses and hides/destroys the duplicates so only real tools remain grabbable.
    /// </summary>
    public void AbsorbTable2ReferenceDuplicatesIfPresent()
    {
        CacheSlots();
        if (slots == null || slots.Count == 0) return;

        Transform trayParent = table2Root;
        if (trayParent == null)
        {
            Transform cur = transform.parent;
            while (cur != null)
            {
                if (cur.name.IndexOf("Table 2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cur.name.IndexOf("Table2", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    trayParent = cur;
                    break;
                }
                cur = cur.parent;
            }
        }
        if (trayParent == null) return;

        // Collect all ToolItems in scene to distinguish genuine primary tools from Table 2 duplicates
        ToolItem[] allTools = FindObjectsByType<ToolItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var duplicateRefs = new List<ToolItem>();
        float meanSlotY = 0f;
        int validSlotCount = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            Transform root = slots[i].snapTransform != null ? slots[i].snapTransform : slots[i].transform;
            meanSlotY += root.position.y;
            validSlotCount++;
        }
        if (validSlotCount > 0) meanSlotY /= validSlotCount;

        foreach (var candidate in allTools)
        {
            if (candidate == null || !candidate.transform.IsChildOf(trayParent)) continue;
            if (!candidate.gameObject.name.Contains("(")) continue;

            string key = TablePrepSlot.CanonicalizeToolKey(candidate.toolID);
            if (string.IsNullOrEmpty(key)) key = TablePrepSlot.CanonicalizeToolKey(candidate.gameObject.name);
            if (string.IsNullOrEmpty(key)) continue;

            // Only treat as a reference duplicate if an unparented/other-table primary tool with same key exists
            bool hasPrimaryOutsideTable2 = false;
            foreach (var other in allTools)
            {
                if (other == null || other == candidate || other.transform.IsChildOf(trayParent)) continue;
                string otherKey = TablePrepSlot.CanonicalizeToolKey(other.toolID);
                if (string.IsNullOrEmpty(otherKey)) otherKey = TablePrepSlot.CanonicalizeToolKey(other.gameObject.name);
                if (otherKey == key)
                {
                    hasPrimaryOutsideTable2 = true;
                    break;
                }
            }

            if (hasPrimaryOutsideTable2)
            {
                duplicateRefs.Add(candidate);
            }
        }

        foreach (var refTool in duplicateRefs)
        {
            string key = TablePrepSlot.CanonicalizeToolKey(refTool.toolID);
            if (string.IsNullOrEmpty(key)) key = TablePrepSlot.CanonicalizeToolKey(refTool.gameObject.name);

            if (key == "artery_forceps")
            {
                TablePrepSlot slot1 = slots.Find(s => s != null && (s.slotNumber == 1 || TablePrepSlot.CanonicalizeToolKey(s.assignedToolID) == "artery_forceps"));
                if (slot1 != null)
                {
                    Transform slotRoot = slot1.snapTransform != null ? slot1.snapTransform : slot1.transform;
                    slot1.snapPositionOffset = slotRoot.InverseTransformDirection(refTool.transform.position - slotRoot.position);
                    slot1.snapEulerAngles = (Quaternion.Inverse(slotRoot.rotation) * refTool.transform.rotation).eulerAngles;
                    slot1.useAbsoluteSnapRotation = false;
                }
            }
            else if (key == "scalpel" || key == "thumb_forceps" || key == "curette")
            {
                float yOffset = refTool.transform.position.y - meanSlotY;
                string idToStore = !string.IsNullOrEmpty(refTool.toolID) ? refTool.toolID : refTool.gameObject.name;
                for (int i = 0; i < slots.Count; i++)
                {
                    TablePrepSlot slot = slots[i];
                    if (slot == null || slot.HasPerToolReference(idToStore)) continue;
                    Transform slotRoot = slot.snapTransform != null ? slot.snapTransform : slot.transform;
                    Vector3 localEuler = (Quaternion.Inverse(slotRoot.rotation) * refTool.transform.rotation).eulerAngles;
                    slot.SetPerToolReference(idToStore, new Vector3(0f, yOffset, 0f), localEuler, true);
                }
            }

            refTool.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(refTool.gameObject);
            }
            else
            {
                DestroyImmediate(refTool.gameObject);
            }
        }
    }

    private void RecordMistake(string reason)
    {
        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.RecordViolation(ClinicalViolationType.Incorrect_Table_Slot, reason);
        }
        else if (PerinealCareManager.Instance != null)
        {
            PerinealCareManager.Instance.RecordClinicalViolation(reason, 5);
        }
        else
        {
            EvaluationLogger.Instance.LogViolation(ClinicalViolationType.Incorrect_Table_Slot, "Incorrect table slot", reason, 5, true);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
