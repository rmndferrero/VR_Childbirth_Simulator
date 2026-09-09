using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages player hand visual materials (Human Skin vs Sterile Surgical Gloves)
/// and triggers a smooth camera-tracking floating notification HUD when gloves are equipped.
/// </summary>
public class PlayerHandMaterialManager : MonoBehaviour
{
    public static PlayerHandMaterialManager Instance { get; private set; }

    [Header("Hand Mesh Renderers")]
    public SkinnedMeshRenderer leftHandMesh;
    public SkinnedMeshRenderer rightHandMesh;

    [Header("Materials")]
    [Tooltip("Brown human skin material used initially.")]
    public Material skinMaterial;

    [Tooltip("Blue sterile surgical glove material used after perineal care.")]
    public Material gloveMaterial;

    [Header("Notification HUD Settings")]
    [Tooltip("Distance from headset where the notification floats.")]
    public float hudDistance = 0.75f;

    [Tooltip("Vertical height offset relative to headset eye level.")]
    public float hudHeightOffset = -0.05f;

    [Tooltip("Duration in seconds the notification stays visible.")]
    public float notificationDuration = 3.5f;

    public bool IsWearingGloves { get; private set; } = false;

    private GameObject hudCanvasObject;
    private CanvasGroup hudCanvasGroup;
    private TextMeshProUGUI hudTitleText;
    private TextMeshProUGUI hudSubtitleText;
    private Coroutine notificationCoroutine;
    private Camera targetCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        FindHandMeshes();
        CreateNotificationHUD();
    }

    private void Start()
    {
        targetCamera = Camera.main;
        // Default to Brown Human Skin at simulation start
        ApplyMaterial(skinMaterial);
        IsWearingGloves = false;
    }

    private void LateUpdate()
    {
        // Smoothly position and orient notification HUD in front of player's camera
        if (hudCanvasObject != null && hudCanvasGroup != null && hudCanvasGroup.alpha > 0.001f)
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                Vector3 targetPos = targetCamera.transform.position + targetCamera.transform.forward * hudDistance + targetCamera.transform.up * hudHeightOffset;
                hudCanvasObject.transform.position = Vector3.Lerp(hudCanvasObject.transform.position, targetPos, Time.deltaTime * 10f);
                hudCanvasObject.transform.rotation = Quaternion.LookRotation(hudCanvasObject.transform.position - targetCamera.transform.position);
            }
        }
    }

    public void FindHandMeshes()
    {
        if (leftHandMesh == null)
        {
            var left = transform.Find("Camera Offset/LEFT HAND/Left Hand Model/hands:hands_geom/hands:Lhand");
            if (left != null) leftHandMesh = left.GetComponent<SkinnedMeshRenderer>();
        }

        if (rightHandMesh == null)
        {
            var right = transform.Find("Camera Offset/RIGHT HAND/Right Hand Model/hands:hands_geom/hands:Rhand");
            if (right != null) rightHandMesh = right.GetComponent<SkinnedMeshRenderer>();
        }
    }

    /// <summary>
    /// Transitions both hands to blue sterile gloves and displays the camera-tracking HUD notification.
    /// </summary>
    public void EquipGloves()
    {
        if (IsWearingGloves) return;

        IsWearingGloves = true;
        ApplyMaterial(gloveMaterial);

        ShowNotification("Sterile Gloves Equipped", "You are now wearing sterile medical gloves for the delivery procedure.");
        Debug.Log("[PlayerHandMaterialManager] Player equipped sterile gloves.");
    }

    /// <summary>
    /// Reverts hands back to human skin.
    /// </summary>
    public void RemoveGloves()
    {
        IsWearingGloves = false;
        ApplyMaterial(skinMaterial);
    }

    private void ApplyMaterial(Material mat)
    {
        if (mat == null) return;

        if (leftHandMesh != null)
        {
            leftHandMesh.material = mat;
        }

        if (rightHandMesh != null)
        {
            rightHandMesh.material = mat;
        }
    }

    public void ShowNotification(string title, string subtitle)
    {
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }

        notificationCoroutine = StartCoroutine(NotificationRoutine(title, subtitle));
    }

    private IEnumerator NotificationRoutine(string title, string subtitle)
    {
        if (hudTitleText != null) hudTitleText.text = title;
        if (hudSubtitleText != null) hudSubtitleText.text = subtitle;

        // Position immediately in front of camera
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null && hudCanvasObject != null)
        {
            hudCanvasObject.transform.position = targetCamera.transform.position + targetCamera.transform.forward * hudDistance + targetCamera.transform.up * hudHeightOffset;
            hudCanvasObject.transform.rotation = Quaternion.LookRotation(hudCanvasObject.transform.position - targetCamera.transform.position);
        }

        // Fade In
        float elapsed = 0f;
        float fadeInDuration = 0.35f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            if (hudCanvasGroup != null) hudCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        if (hudCanvasGroup != null) hudCanvasGroup.alpha = 1f;

        // Display Stay Duration
        yield return new WaitForSeconds(notificationDuration);

        // Fade Out
        elapsed = 0f;
        float fadeOutDuration = 0.5f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            if (hudCanvasGroup != null) hudCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeOutDuration));
            yield return null;
        }
        if (hudCanvasGroup != null) hudCanvasGroup.alpha = 0f;

        notificationCoroutine = null;
    }

    private void CreateNotificationHUD()
    {
        if (hudCanvasObject != null) return;

        hudCanvasObject = new GameObject("GloveNotificationHUD_Canvas");
        hudCanvasObject.transform.SetParent(transform, false);

        var canvas = hudCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = hudCanvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        hudCanvasGroup = hudCanvasObject.AddComponent<CanvasGroup>();
        hudCanvasGroup.alpha = 0f;

        var rect = hudCanvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(480, 140);
        rect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Background Panel
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(hudCanvasObject.transform, false);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.04f, 0.10f, 0.18f, 0.88f); // Deep navy translucent background
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Outline Border
        var outline = panelGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.0f, 0.85f, 1.0f, 0.85f); // Vibrant cyan border
        outline.effectDistance = new Vector2(2f, -2f);

        // Title Text
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(panelGo.transform, false);
        hudTitleText = titleGo.AddComponent<TextMeshProUGUI>();
        hudTitleText.text = "Sterile Gloves Equipped";
        hudTitleText.fontSize = 26;
        hudTitleText.fontStyle = FontStyles.Bold;
        hudTitleText.alignment = TextAlignmentOptions.Center;
        hudTitleText.color = new Color(0.1f, 0.95f, 1.0f, 1.0f); // Bright Cyan
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(15, 0);
        titleRect.offsetMax = new Vector2(-15, -10);

        // Subtitle Text
        var subGo = new GameObject("SubtitleText");
        subGo.transform.SetParent(panelGo.transform, false);
        hudSubtitleText = subGo.AddComponent<TextMeshProUGUI>();
        hudSubtitleText.text = "You are now wearing sterile medical gloves.";
        hudSubtitleText.fontSize = 17;
        hudSubtitleText.alignment = TextAlignmentOptions.Center;
        hudSubtitleText.color = new Color(0.9f, 0.95f, 1.0f, 0.95f);
        var subRect = subGo.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.55f);
        subRect.offsetMin = new Vector2(15, 8);
        subRect.offsetMax = new Vector2(-15, 0);
    }
}
