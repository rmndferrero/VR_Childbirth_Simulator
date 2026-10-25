using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to each phrase choice pill UI GameObject in the 3-Column Phrase Board.
/// Features:
/// - Clean "Choice 1", "Choice 2", "Choice 3" high-contrast bold white labels (no tiny text).
/// - VR raycast hover detection (IPointerEnter / IPointerExit) for additive live preview in speech card.
/// - 1-click tap selection into its column's top slot / tap slotted chip to eject back to bank.
/// - Drag-to-slot and drag-to-swap between choices in the column.
/// - Strict 2D planar locking (Z = 0, Rotation = 0, Scale = 1) in LateUpdate to eliminate VR mid-air float.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggablePhraseChip : MonoBehaviour, 
    IBeginDragHandler, IDragHandler, IEndDragHandler, 
    IPointerClickHandler, IDropHandler, 
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("Token Data")]
    public DialoguePhraseTokenSO tokenData;

    [Header("Choice Number")]
    [Range(1, 3)]
    public int choiceNumber = 1;

    [Header("UI Visual Components")]
    public TMP_Text labelText;
    public Image chipBackgroundImage;
    public Outline chipOutline;

    [Header("UbiSim VR Medical Colors")]
    public Color normalBgColor = new Color(0.10f, 0.16f, 0.24f, 0.95f);     // #1A2638 Dark Slate
    public Color normalBorderColor = new Color(0.20f, 0.32f, 0.48f, 0.85f);  // #33527A Subtle Slate Border
    public Color hoverBgColor = new Color(0.04f, 0.30f, 0.48f, 0.98f);       // #0B4D7B Hovered Cyan Slate
    public Color hoverBorderColor = new Color(0.22f, 0.74f, 0.97f, 1.0f);    // #38BDF8 Glowing Cyan
    public Color slottedBgColor = new Color(0.02f, 0.40f, 0.62f, 0.98f);     // #0369A1 Active Medical Cyan
    public Color slottedBorderColor = new Color(0.50f, 0.88f, 1.0f, 1.0f);   // #80E0FF Bright Cyan

    [HideInInspector] public bool isSelected = false;
    [HideInInspector] public PhraseDropSlot currentSlot = null;
    [HideInInspector] public Transform originalPoolParent;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private CanvasGroup canvasGroup;
    private Vector3 startDragLocalPosition;
    private Transform startDragParent;
    private int startSiblingIndex;
    private bool isDragging = false;
    private bool isHovered = false;

    public event Action<DraggablePhraseChip> OnChipClicked;

    private void Awake()
    {
        EnsureComponents();
    }

    private void Start()
    {
        EnsureComponents();
        if (originalPoolParent == null)
        {
            originalPoolParent = transform.parent;
        }
        ApplyVisuals();
    }

    private void LateUpdate()
    {
        // Enforce strict 2D planar lock at all times - prevents any floating or tilting in VR
        if (rectTransform != null)
        {
            Vector3 pos = rectTransform.localPosition;
            if (pos.z != 0f)
            {
                rectTransform.localPosition = new Vector3(pos.x, pos.y, 0f);
            }
            if (rectTransform.localRotation != Quaternion.identity)
            {
                rectTransform.localRotation = Quaternion.identity;
            }
            if (rectTransform.localScale != Vector3.one)
            {
                rectTransform.localScale = Vector3.one;
            }
        }
    }

    public void EnsureComponents()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && canvasRect == null) canvasRect = parentCanvas.GetComponent<RectTransform>();
        if (chipOutline == null) chipOutline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        if (chipBackgroundImage == null) chipBackgroundImage = GetComponent<Image>();
        if (labelText == null) labelText = GetComponentInChildren<TMP_Text>();
    }

    public void Initialize(DialoguePhraseTokenSO data, int number, Transform poolParent)
    {
        tokenData = data;
        choiceNumber = number;
        originalPoolParent = poolParent;
        currentSlot = null;
        isSelected = false;
        EnsureComponents();
        ApplyVisuals();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        ApplyVisuals();
    }

    public void ApplyVisuals()
    {
        EnsureComponents();

        string label = $"Choice {choiceNumber}";
        if (labelText != null)
        {
            labelText.text = isSelected ? $"<b>✓ {label}</b>" : $"<b>{label}</b>";
            labelText.color = isSelected ? Color.white : (isHovered ? Color.white : new Color(0.85f, 0.92f, 1f, 1f));
        }

        if (chipBackgroundImage != null)
        {
            if (isSelected) chipBackgroundImage.color = slottedBgColor;
            else if (isHovered) chipBackgroundImage.color = hoverBgColor;
            else chipBackgroundImage.color = normalBgColor;
        }

        if (chipOutline != null)
        {
            if (isSelected)
            {
                chipOutline.effectColor = slottedBorderColor;
                chipOutline.effectDistance = new Vector2(2f, -2f);
            }
            else if (isHovered)
            {
                chipOutline.effectColor = hoverBorderColor;
                chipOutline.effectDistance = new Vector2(1.8f, -1.8f);
            }
            else
            {
                chipOutline.effectColor = normalBorderColor;
                chipOutline.effectDistance = new Vector2(1f, -1f);
            }
        }

        if (rectTransform != null)
        {
            Vector3 pos = rectTransform.localPosition;
            rectTransform.localPosition = new Vector3(pos.x, pos.y, 0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // VR RAYCAST HOVER PREVIEW
    // ══════════════════════════════════════════════════════════════════

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging) return;
        isHovered = true;
        ApplyVisuals();

        if (PhraseDecisionMatrix.Instance != null)
        {
            PhraseDecisionMatrix.Instance.SetHoveredChip(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;
        isHovered = false;
        ApplyVisuals();

        if (PhraseDecisionMatrix.Instance != null)
        {
            PhraseDecisionMatrix.Instance.ClearHoveredChip(this);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // DRAG AND DROP
    // ══════════════════════════════════════════════════════════════════

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (tokenData == null) return;
        isDragging = true;
        isHovered = false;
        EnsureComponents();

        startDragParent = transform.parent;
        startDragLocalPosition = rectTransform.localPosition;
        startSiblingIndex = transform.GetSiblingIndex();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.85f;
        }

        if (parentCanvas != null)
        {
            transform.SetParent(parentCanvas.transform, false);
            transform.SetAsLastSibling();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            // Strict 2D planar enforcement - Z is strictly zero
            rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1.0f;
        }

        // If not dropped on a valid target, return to previous parent & position
        if (transform.parent == (parentCanvas != null ? parentCanvas.transform : null))
        {
            transform.SetParent(startDragParent, false);
            transform.SetSiblingIndex(startSiblingIndex);
            rectTransform.localPosition = new Vector3(startDragLocalPosition.x, startDragLocalPosition.y, 0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj == null || droppedObj == gameObject) return;

        DraggablePhraseChip draggedChip = droppedObj.GetComponent<DraggablePhraseChip>();
        if (draggedChip == null || draggedChip.tokenData == null) return;

        // Perform clean swap if in same category
        if (PhraseDecisionMatrix.Instance != null && draggedChip.tokenData.category == tokenData.category)
        {
            PhraseDecisionMatrix.Instance.HandleChipSwap(draggedChip, this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;

        if (PhraseDecisionMatrix.Instance != null)
        {
            PhraseDecisionMatrix.Instance.HandlePillClicked(this);
        }
        else
        {
            OnChipClicked?.Invoke(this);
        }
    }

    public void SnapIntoSlot(PhraseDropSlot slot)
    {
        EnsureComponents();
        currentSlot = slot;
        transform.SetParent(slot.chipContainer != null ? slot.chipContainer : slot.transform, false);
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }
        SetSelected(true);
    }

    public void ReturnToPool()
    {
        EnsureComponents();
        if (currentSlot != null)
        {
            currentSlot.ClearSlot();
            currentSlot = null;
        }

        Transform targetParent = originalPoolParent != null ? originalPoolParent : transform.parent;
        transform.SetParent(targetParent, false);
        if (rectTransform != null)
        {
            rectTransform.localPosition = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, 0f);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }
        SetSelected(false);
    }
}
