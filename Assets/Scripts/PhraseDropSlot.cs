using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to each of the 3 top target drop slots on the Phrase Board.
/// Features:
/// - Category validation (Greeting, PatientIdentification, ProcedureExplanation).
/// - Clean snapping of chips into slot with zero local Z-depth.
/// - Tap to eject chip back to its home bank.
/// - Drop detection for drag-to-slot and drag-to-swap.
/// - Sleek UbiSim dark-slate styling and cyan highlight when occupied.
/// </summary>
public class PhraseDropSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("Slot Configuration")]
    public PhraseSlotCategory targetCategory;
    [Range(0f, 1f)] public float stageWeight = 0.33f;
    public string slotTitle = "1. Greeting";

    [Header("UI Visual Components")]
    public Transform chipContainer;
    public TMP_Text slotHeaderLabel;
    public TMP_Text placeholderText;
    public Image slotBackground;
    public Outline slotOutline;

    [Header("UbiSim Medical Colors")]
    public Color normalBgColor = new Color(0.08f, 0.12f, 0.18f, 0.85f);     // #141F2E Dark Slate
    public Color normalBorderColor = new Color(0.20f, 0.30f, 0.45f, 0.70f);  // #334D73 Subtle Slate
    public Color occupiedBorderColor = new Color(0.22f, 0.74f, 0.97f, 0.95f);// #38BDF8 Glowing Cyan
    public Color rejectColor = new Color(0.95f, 0.25f, 0.25f, 0.95f);        // #EF4444 Rejection Red

    [HideInInspector] public DraggablePhraseChip currentSlottedChip = null;

    public bool IsOccupied => currentSlottedChip != null && currentSlottedChip.tokenData != null;

    public event Action<PhraseDropSlot, DraggablePhraseChip> OnChipSlotted;
    public event Action<PhraseDropSlot> OnChipRemoved;

    private void Start()
    {
        UpdateVisualState();
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj == null) return;

        DraggablePhraseChip chip = droppedObj.GetComponent<DraggablePhraseChip>();
        if (chip == null || chip.tokenData == null) return;

        // 1. Category Validation: Ensure chip matches slot category
        if (chip.tokenData.category != targetCategory)
        {
            Debug.LogWarning($"[PhraseDropSlot] Category mismatch: Slot '{slotTitle}' requires {targetCategory}, but chip was {chip.tokenData.category}.");
            FlashRejection();
            chip.ReturnToPool();
            return;
        }

        // 2. Accept and Slot Chip
        AcceptChip(chip);
    }

    public bool AcceptChip(DraggablePhraseChip newChip)
    {
        if (newChip == null || newChip.tokenData == null) return false;

        if (newChip.tokenData.category != targetCategory)
        {
            FlashRejection();
            return false;
        }

        // If slot is already occupied, return old chip to its home bank
        if (currentSlottedChip != null && currentSlottedChip != newChip)
        {
            currentSlottedChip.ReturnToPool();
        }

        // If new chip was in another slot, clear that slot
        if (newChip.currentSlot != null && newChip.currentSlot != this)
        {
            newChip.currentSlot.currentSlottedChip = null;
            newChip.currentSlot.UpdateVisualState();
        }

        currentSlottedChip = newChip;
        newChip.SnapIntoSlot(this);

        UpdateVisualState();
        OnChipSlotted?.Invoke(this, newChip);
        return true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentSlottedChip != null)
        {
            currentSlottedChip.ReturnToPool();
            if (PhraseDecisionMatrix.Instance != null)
            {
                PhraseDecisionMatrix.Instance.UpdateLivePreview();
            }
        }
    }

    public void ClearSlot()
    {
        currentSlottedChip = null;
        UpdateVisualState();
        OnChipRemoved?.Invoke(this);
    }

    public void UpdateVisualState()
    {
        if (placeholderText != null)
        {
            placeholderText.gameObject.SetActive(!IsOccupied);
        }

        if (slotBackground != null)
        {
            slotBackground.color = normalBgColor;
        }

        if (slotOutline != null)
        {
            slotOutline.effectColor = IsOccupied ? occupiedBorderColor : normalBorderColor;
            slotOutline.effectDistance = IsOccupied ? new Vector2(1.8f, -1.8f) : new Vector2(1f, -1f);
        }
    }

    private void FlashRejection()
    {
        if (slotOutline != null)
        {
            slotOutline.effectColor = rejectColor;
            CancelInvoke(nameof(ResetBorderColor));
            Invoke(nameof(ResetBorderColor), 0.65f);
        }
    }

    private void ResetBorderColor()
    {
        UpdateVisualState();
    }
}
