using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to every phrase token chip UI GameObject.
/// Implements Unity UI drag-and-drop interfaces compatible with XR Ray Interactors,
/// and includes an accessible 1-click Tap-to-Slot fallback for VR ergonomics.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggablePhraseChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Token Data")]
    public DialoguePhraseTokenSO tokenData;

    [Header("UI Visual Components")]
    public TMP_Text labelText;
    public Image chipBackgroundImage;
    public Outline chipOutline;

    [Header("Visual Colors by Category")]
    public Color introColor = new Color(0.20f, 0.60f, 0.90f, 0.95f);   // Sky Blue
    public Color identColor = new Color(0.85f, 0.55f, 0.15f, 0.95f);   // Amber
    public Color explainColor = new Color(0.25f, 0.75f, 0.50f, 0.95f); // Emerald Green

    [HideInInspector] public PhraseDropSlot currentSlot = null;
    [HideInInspector] public Transform originalPoolParent;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private CanvasGroup canvasGroup;
    private Vector3 startDragLocalPosition;
    private Transform startDragParent;
    private bool isDragging = false;

    public event Action<DraggablePhraseChip> OnChipClicked;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            canvasRect = parentCanvas.GetComponent<RectTransform>();
        }
    }

    private void Start()
    {
        if (originalPoolParent == null)
        {
            originalPoolParent = transform.parent;
        }
        ApplyVisuals();
    }

    public void Initialize(DialoguePhraseTokenSO data, Transform poolParent)
    {
        tokenData = data;
        originalPoolParent = poolParent;
        currentSlot = null;
        ApplyVisuals();
    }

    public void ApplyVisuals()
    {
        if (labelText != null && tokenData != null)
        {
            labelText.text = !string.IsNullOrEmpty(tokenData.chipShortLabel)
                ? tokenData.chipShortLabel
                : tokenData.fullPhraseText;
        }

        if (chipBackgroundImage != null && tokenData != null)
        {
            switch (tokenData.category)
            {
                case PhraseSlotCategory.Introduction:
                    chipBackgroundImage.color = introColor;
                    break;
                case PhraseSlotCategory.PatientIdentification:
                    chipBackgroundImage.color = identColor;
                    break;
                case PhraseSlotCategory.ProcedureExplanation:
                    chipBackgroundImage.color = explainColor;
                    break;
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (tokenData == null) return;
        isDragging = true;
        startDragParent = transform.parent;
        startDragLocalPosition = rectTransform.localPosition;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false; // Allow ray to pass through chip so DropSlot detects OnDrop
            canvasGroup.alpha = 0.85f;
        }

        // Reparent to top canvas root so it renders on top of all UI during drag
        if (parentCanvas != null)
        {
            transform.SetParent(parentCanvas.transform, true);
            transform.SetAsLastSibling();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            rectTransform.localPosition = localPoint;
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

        // If not accepted by any slot, return back to original position/parent
        if (currentSlot == null)
        {
            ReturnToPool();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;

        // Trigger 1-click Tap-to-Slot / Tap-to-Return
        OnChipClicked?.Invoke(this);
    }

    public void SnapIntoSlot(PhraseDropSlot slot)
    {
        currentSlot = slot;
        transform.SetParent(slot.chipContainer != null ? slot.chipContainer : slot.transform, false);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    public void ReturnToPool()
    {
        if (currentSlot != null)
        {
            currentSlot.ClearSlot();
            currentSlot = null;
        }

        Transform targetParent = originalPoolParent != null ? originalPoolParent : transform.parent;
        transform.SetParent(targetParent, false);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }
}
