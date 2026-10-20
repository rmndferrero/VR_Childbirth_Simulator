using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages Step 7 (Mayo Table Setup / Tool Preparation).
/// Handles the 5 placement slots on Table 2, enforces correct tool matching,
/// shows uniform small blue box ghosts, logs clinical mistake deductions on wrong placement,
/// and advances to Step 8 (Final Evaluation) when all 5 slots are filled.
/// </summary>
public class TablePrepManager : MonoBehaviour
{
    public static TablePrepManager Instance { get; private set; }

    [Header("Table 2 Slots (Slots 1 to 5)")]
    [Tooltip("All 5 Table 2 slots in order.")]
    public List<TablePrepSlot> slots = new List<TablePrepSlot>();

    [Header("Table 2 Root / Parent")]
    public Transform table2Root;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip correctSlotClip;
    public AudioClip wrongSlotClip;
    public AudioClip allSlotsCompletedClip;

    [Header("UI Feedback (Optional)")]
    public TMP_Text statusText;

    [Header("State")]
    public bool isTablePrepActive = false;
    public bool isTablePrepCompleted = false;

    public event Action<int, int> OnSlotCountUpdated; // (filledCount, totalCount)
    public event Action OnTablePrepCompleted;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        CacheSlots();
    }

    private void Start()
    {
        CacheSlots();
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
    /// Activates all 5 blue box ghosts on Table 2.
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

        UpdateStatusUI();
        Debug.Log($"[TablePrepManager] Step 7 Started! {slots.Count} tool slots active on Table 2.");
    }

    /// <summary>
    /// Checks if a tool released near Table 2 should snap to a slot or be rejected.
    /// Returns true if handled by TablePrepManager.
    /// </summary>
    public bool TryHandleToolRelease(ToolItem tool)
    {
        if (!isTablePrepActive || tool == null || isTablePrepCompleted) return false;

        CacheSlots();

        TablePrepSlot closestSlot = null;
        float closestDist = float.MaxValue;

        Vector3 toolPos = tool.transform.position;

        foreach (var slot in slots)
        {
            if (slot == null || slot.snapTransform == null) continue;

            float dist = Vector3.Distance(toolPos, slot.snapTransform.position);
            if (dist <= slot.snapRadius && dist < closestDist)
            {
                closestDist = dist;
                closestSlot = slot;
            }
        }

        if (closestSlot == null)
        {
            // Not near any Table 2 slot
            return false;
        }

        // Check if this slot is already filled by another tool
        if (closestSlot.isFilled && closestSlot.currentPlacedTool != null && closestSlot.currentPlacedTool != tool)
        {
            PlaySound(wrongSlotClip);
            RecordMistake($"Slot {closestSlot.slotNumber} already contains an instrument!");
            closestSlot.RejectWrongTool(tool);
            return true;
        }

        // Validate if the tool matches this slot
        if (closestSlot.CanAccept(tool))
        {
            // CORRECT PLACEMENT!
            closestSlot.SnapCorrectTool(tool);
            PlaySound(correctSlotClip);
            CheckAllSlotsFilled();
            return true;
        }
        else
        {
            // WRONG PLACEMENT!
            PlaySound(wrongSlotClip);
            string toolName = !string.IsNullOrEmpty(tool.toolID) ? tool.toolID : tool.gameObject.name;
            RecordMistake($"Incorrect placement: {toolName} does not belong in Slot {closestSlot.slotNumber} ({closestSlot.slotLabel})!");
            closestSlot.RejectWrongTool(tool);
            return true;
        }
    }

    public void CheckAllSlotsFilled()
    {
        int filledCount = 0;
        foreach (var slot in slots)
        {
            if (slot != null && slot.isFilled)
            {
                filledCount++;
            }
        }

        OnSlotCountUpdated?.Invoke(filledCount, slots.Count);
        UpdateStatusUI();

        if (filledCount >= slots.Count && slots.Count > 0)
        {
            CompleteTablePrep();
        }
    }

    private void UpdateStatusUI()
    {
        int filledCount = 0;
        foreach (var slot in slots)
        {
            if (slot != null && slot.isFilled) filledCount++;
        }

        if (statusText != null)
        {
            statusText.text = $"Mayo Table Setup: <color=#38BDF8><b>{filledCount}/{slots.Count} Tools Arranged</b></color>";
        }
    }

    private void CompleteTablePrep()
    {
        if (isTablePrepCompleted) return;
        isTablePrepCompleted = true;
        isTablePrepActive = false;

        PlaySound(allSlotsCompletedClip != null ? allSlotsCompletedClip : correctSlotClip);
        Debug.Log("[TablePrepManager] All 5 tools correctly arranged on Table 2! Step 7 Completed.");

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
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
