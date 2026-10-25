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
    }

    private void Start()
    {
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
    /// Checks if a tool released near Table 2 should snap to a slot or be rejected.
    /// Returns true if handled by TablePrepManager.
    /// </summary>
    public bool TryHandleToolRelease(ToolItem tool)
    {
        if (tool == null) return false;

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

        // Gatekeeping: Enforce sterile field on Mayo Stand before accepting any instrument
        bool draped = isMayoDraped || (MayoStandDrapeSocket.Instance != null && MayoStandDrapeSocket.Instance.isDraped);
        if (!draped)
        {
            if (VRHeadsetVisualFeedback.Instance != null)
            {
                VRHeadsetVisualFeedback.Instance.TriggerUnsterileMayoViolation(tool.toolID);
            }
            PlaySound(wrongSlotClip);
            closestSlot.RejectWrongTool(tool);
            return true;
        }

        if (!isTablePrepActive || isTablePrepCompleted) return false;

        // If the tool was already placed in this slot, re-snap it cleanly into place
        if (closestSlot.isFilled && closestSlot.currentPlacedTool != null)
        {
            if (closestSlot.currentPlacedTool == tool)
            {
                closestSlot.SnapCorrectTool(tool);
                PlaySound(correctSlotClip);
                return true;
            }
            PlaySound(wrongSlotClip);
            return false;
        }

        // Snap tool into this slot
        closestSlot.SnapCorrectTool(tool);
        PlaySound(correctSlotClip);

        UpdateStatusUI();
        return true;
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
