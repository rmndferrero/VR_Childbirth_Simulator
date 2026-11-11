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
/// - Drag-to-slot and drag-to-swap between choices in the column, safe under XR World-Space canvases and interrupted drags.
/// - Strict 2D planar locking (Z = 0, Rotation = 0, Scale = 1) in LateUpdate to eliminate VR mid-air float.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggablePhraseChip : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    private static DraggablePhraseChip activeDraggedChip;

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
    private Button chipButton;
    private Vector3 startDragLocalPosition;
    private Transform startDragParent;
    private int startSiblingIndex;
    private bool isDragging = false;
    private bool isHovered = false;
    private bool dropHandled = false;

    public bool IsDragging => isDragging;
    public CanvasGroup ChipCanvasGroup => canvasGroup;

    public event Action<DraggablePhraseChip> OnChipClicked;

    public static void CancelAnyActiveDrag()
    {
        if (activeDraggedChip != null)
        {
            activeDraggedChip.ForceCancelDrag();
        }
    }

    private void Awake()
    {
        EnsureComponents();
    }

    private void Start()
    {
        EnsureComponents();
        if (originalPoolParent == null && parentCanvas != null && transform.parent != parentCanvas.transform)
        {
            originalPoolParent = transform.parent;
        }
        else if (originalPoolParent == null && parentCanvas == null)
        {
            originalPoolParent = transform.parent;
        }
        ApplyVisuals();
    }

    private void OnDisable()
    {
        if (isDragging || activeDraggedChip == this || (canvasGroup != null && !canvasGroup.blocksRaycasts))
        {
            ForceCancelDrag();
        }
    }

    private void OnDestroy()
    {
        if (activeDraggedChip == this)
        {
            activeDraggedChip = null;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && isDragging)
        {
            ForceCancelDrag();
        }
    }

    private void LateUpdate()
    {
        // Enforce strict 2D planar lock at all times - prevents any floating or tilting in VR
        EnforcePlanarTransform();
    }

    private void EnforcePlanarTransform()
    {
        if (rectTransform == null) return;

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

    public void EnsureComponents()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        Canvas resolvedCanvas = GetComponentInParent<Canvas>();
        if (resolvedCanvas != null)
        {
            parentCanvas = resolvedCanvas.rootCanvas != null ? resolvedCanvas.rootCanvas : resolvedCanvas;
            canvasRect = parentCanvas.GetComponent<RectTransform>();
        }
        else if (parentCanvas != null && canvasRect == null)
        {
            canvasRect = parentCanvas.GetComponent<RectTransform>();
        }

        if (chipOutline == null) chipOutline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        if (chipBackgroundImage == null) chipBackgroundImage = GetComponent<Image>();
        if (labelText == null) labelText = GetComponentInChildren<TMP_Text>();

        // Prevent child TMP label mesh from intercepting pointer raycasts away from the root pill collider/graphic
        if (labelText != null && labelText.raycastTarget)
        {
            labelText.raycastTarget = false;
        }

        // Prevent EventSystem keyboard/controller directional focus from locking onto a chip button after click/drag
        if (chipButton == null) chipButton = GetComponent<Button>();
        if (chipButton != null && chipButton.navigation.mode != Navigation.Mode.None)
        {
            Navigation nav = chipButton.navigation;
            nav.mode = Navigation.Mode.None;
            chipButton.navigation = nav;
        }
    }

    public void Initialize(DialoguePhraseTokenSO data, int number, Transform poolParent)
    {
        tokenData = data;
        choiceNumber = number;
        originalPoolParent = poolParent;
        currentSlot = null;
        isSelected = false;
        EnsureComponents();
        ResetDragVisualState();
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

        EnforcePlanarTransform();
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

    public void NotifyDroppedSuccessfully()
    {
        dropHandled = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (tokenData == null) return;

        if (activeDraggedChip != null && activeDraggedChip != this)
        {
            activeDraggedChip.ForceCancelDrag();
        }

        EnsureComponents();
        activeDraggedChip = this;
        isDragging = true;
        isHovered = false;
        dropHandled = false;

        Transform homeSlotTransform = currentSlot != null
            ? (currentSlot.chipContainer != null ? currentSlot.chipContainer : currentSlot.transform)
            : originalPoolParent;
        startDragParent = (parentCanvas != null && transform.parent == parentCanvas.transform && homeSlotTransform != null)
            ? homeSlotTransform
            : transform.parent;
        startDragLocalPosition = rectTransform.localPosition;
        startSiblingIndex = transform.GetSiblingIndex();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.85f;
        }

        if (parentCanvas != null)
        {
            transform.SetParent(parentCanvas.transform, true);
            transform.SetAsLastSibling();
            EnforcePlanarTransform();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || eventData == null) return;

        if (TryGetCanvasLocalPoint(eventData, out Vector2 localPoint))
        {
            rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        bool wasDropHandled = dropHandled;
        ResetDragVisualState();

        Transform overlayTransform = parentCanvas != null ? parentCanvas.transform : null;
        if (!wasDropHandled && (transform.parent == overlayTransform || transform.parent == null))
        {
            RestoreToParentBeforeDrag();
        }

        ApplyVisuals();
    }

    public void ForceCancelDrag()
    {
        Transform overlayTransform = parentCanvas != null ? parentCanvas.transform : null;
        bool needsRestore = isDragging && (transform.parent == overlayTransform || transform.parent == null);

        ResetDragVisualState();

        if (needsRestore)
        {
            RestoreToParentBeforeDrag();
        }

        ApplyVisuals();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;
        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj == null || droppedObj == gameObject) return;

        DraggablePhraseChip draggedChip = droppedObj.GetComponent<DraggablePhraseChip>();
        if (draggedChip == null || draggedChip.tokenData == null || tokenData == null) return;

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
        ResetDragVisualState();
        currentSlot = slot;
        Transform slotParent = slot.chipContainer != null ? slot.chipContainer : slot.transform;
        transform.SetParent(slotParent, false);
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }
        SetSelected(true);
        RebuildParentLayout(slotParent);
    }

    public void ReturnToPool()
    {
        EnsureComponents();
        ResetDragVisualState();
        if (currentSlot != null)
        {
            currentSlot.ClearSlot();
            currentSlot = null;
        }

        Transform targetParent = originalPoolParent != null ? originalPoolParent : transform.parent;
        if (targetParent != null)
        {
            transform.SetParent(targetParent, false);
        }
        EnforcePlanarTransform();
        SetSelected(false);
        RebuildParentLayout(targetParent);
    }

    private void ResetDragVisualState()
    {
        isDragging = false;
        isHovered = false;
        dropHandled = false;
        if (activeDraggedChip == this)
        {
            activeDraggedChip = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1.0f;
        }
    }

    private void RestoreToParentBeforeDrag()
    {
        Transform restoreParent = startDragParent;
        if (restoreParent == null || (parentCanvas != null && restoreParent == parentCanvas.transform))
        {
            restoreParent = currentSlot != null
                ? (currentSlot.chipContainer != null ? currentSlot.chipContainer : currentSlot.transform)
                : originalPoolParent;
        }

        if (restoreParent == null) return;

        transform.SetParent(restoreParent, false);
        int maxIdx = Mathf.Max(0, restoreParent.childCount - 1);
        transform.SetSiblingIndex(Mathf.Clamp(startSiblingIndex, 0, maxIdx));
        rectTransform.localPosition = new Vector3(startDragLocalPosition.x, startDragLocalPosition.y, 0f);
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        RebuildParentLayout(restoreParent);
    }

    private static void RebuildParentLayout(Transform targetParent)
    {
        if (targetParent is RectTransform parentRect && targetParent.gameObject.activeInHierarchy)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
    }

    private bool TryGetCanvasLocalPoint(PointerEventData eventData, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (canvasRect == null) return false;

        Camera cam = eventData.pressEventCamera != null
            ? eventData.pressEventCamera
            : (eventData.enterEventCamera != null
                ? eventData.enterEventCamera
                : (parentCanvas != null && parentCanvas.worldCamera != null ? parentCanvas.worldCamera : Camera.main));

        if (eventData.pointerCurrentRaycast.isValid && eventData.pointerCurrentRaycast.worldPosition != Vector3.zero)
        {
            Plane canvasPlane = new Plane(canvasRect.forward, canvasRect.position);
            Vector3 projectedWorld = canvasPlane.ClosestPointOnPlane(eventData.pointerCurrentRaycast.worldPosition);
            Vector3 local3 = canvasRect.InverseTransformPoint(projectedWorld);
            localPoint = new Vector2(local3.x, local3.y);
            return true;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, eventData.position, cam, out Vector3 worldPt))
        {
            Vector3 local3 = canvasRect.InverseTransformPoint(worldPt);
            localPoint = new Vector2(local3.x, local3.y);
            return true;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, cam, out localPoint);
    }
}
