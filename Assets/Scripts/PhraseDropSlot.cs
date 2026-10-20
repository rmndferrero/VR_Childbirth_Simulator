using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to each of the 3 drop slots on the clinical clipboard UI.
/// Enforces category validation (Introduction, PatientIdentification, ProcedureExplanation),
/// handles chip snapping, swapping, and ejection.
/// </summary>
public class PhraseDropSlot : MonoBehaviour, IDropHandler
{
    [Header("Slot Configuration")]
    public PhraseSlotCategory targetCategory;
    [Range(0f, 1f)] public float stageWeight = 0.33f;
    public string slotTitle = "1. Introduction";

    [Header("UI Visual Components")]
    public Transform chipContainer;
    public TMP_Text slotHeaderLabel;
    public TMP_Text placeholderText;
    public Image slotBackground;
    public Outline slotOutline;

    [Header("Feedback Colors")]
    public Color normalBorderColor = new Color(0.35f, 0.45f, 0.55f, 0.6f);
    public Color hoverValidColor = new Color(0.20f, 0.85f, 0.50f, 0.9f);
    public Color rejectColor = new Color(0.95f, 0.25f, 0.25f, 0.9f);

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

        // 1. Category Validation: Ensure chip matches slot's required clinical stage
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

        // If slot is already occupied, return the old chip back to the pool
        if (currentSlottedChip != null && currentSlottedChip != newChip)
        {
            currentSlottedChip.ReturnToPool();
        }

        // If chip was in another slot, clear that slot
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

        if (slotOutline != null)
        {
            slotOutline.effectColor = IsOccupied ? hoverValidColor : normalBorderColor;
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
