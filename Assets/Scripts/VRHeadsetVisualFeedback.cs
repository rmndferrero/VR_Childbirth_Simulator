using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Controls VR Headset Point-Of-View (POV) visual feedback:
/// 1. Glowing Red Vignette border in player's vision on protocol violations / unauthorized tool grabbing.
/// 2. Close-range, "in-your-face" floating HUD banners for critical feedback (Step 1 tool warnings, Glove unlocks, etc.).
/// 3. Controller haptic feedback.
/// </summary>
public class VRHeadsetVisualFeedback : MonoBehaviour
{
    public static VRHeadsetVisualFeedback Instance { get; private set; }

    [Header("In-Face HUD Settings")]
    [Tooltip("Distance in meters from the VR headset to the HUD (close-range in-your-face).")]
    public float hudDistance = 0.48f;

    [Tooltip("Vertical offset relative to eye level.")]
    public float hudHeightOffset = -0.02f;

    [Tooltip("Default display duration in seconds.")]
    public float defaultDuration = 2.8f;

    [Header("Step 1 Violation UI Text")]
    [Tooltip("Header text shown when player tries to grab a tool before obtaining consent.")]
    public string step1WarningTitle = "Consent Required!";

    [Tooltip("Description text. Use {0} to insert the grabbed tool's name dynamically.")]
    [TextArea(2, 4)]
    public string step1WarningSubtitle = "Talk to the mother and obtain informed consent before handling instruments ({0}).";

    [Tooltip("Mistake log entry recorded into the simulation scoring summary.")]
    public string step1MistakeLogText = "Handled '{0}' before obtaining informed consent";

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip warningBuzzerClip;

    private Camera targetCamera;

    // Red Vignette Components
    private GameObject vignetteCanvasObj;
    private CanvasGroup vignetteCanvasGroup;
    private Image vignetteImage;
    private Coroutine vignetteCoroutine;

    // In-Face HUD Components
    private GameObject hudCanvasObj;
    private CanvasGroup hudCanvasGroup;
    private TextMeshProUGUI hudTitleTMP;
    private TextMeshProUGUI hudSubtitleTMP;
    private Image hudPanelBg;
    private Outline hudOutline;
    private Coroutine hudCoroutine;

    private float lastStep1WarningTime = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureInstance()
    {
        if (Instance == null)
        {
            var existing = FindFirstObjectByType<VRHeadsetVisualFeedback>();
            if (existing == null)
            {
                var go = new GameObject("VRHeadsetVisualFeedback");
                go.AddComponent<VRHeadsetVisualFeedback>();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        EnsureVignetteOverlayExists();
        EnsureInFaceHUDExists();
    }

    private void Start()
    {
        targetCamera = Camera.main;
        EnsureVignetteOverlayExists();
        EnsureInFaceHUDExists();
    }

    private void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        // Position In-Face HUD directly in front of the player's view
        if (hudCanvasObj != null && hudCanvasGroup != null && hudCanvasGroup.alpha > 0.001f)
        {
            Vector3 targetPos = targetCamera.transform.position + targetCamera.transform.forward * hudDistance + targetCamera.transform.up * hudHeightOffset;
            hudCanvasObj.transform.position = Vector3.Lerp(hudCanvasObj.transform.position, targetPos, Time.deltaTime * 14f);
            hudCanvasObj.transform.rotation = Quaternion.LookRotation(hudCanvasObj.transform.position - targetCamera.transform.position);
        }

        // Keep Vignette locked directly to camera orientation
        if (vignetteCanvasObj != null)
        {
            vignetteCanvasObj.transform.position = targetCamera.transform.position + targetCamera.transform.forward * 0.28f;
            vignetteCanvasObj.transform.rotation = targetCamera.transform.rotation;
        }
    }

    /// <summary>
    /// Triggered when the player attempts to grab or use any tool in Step 1 before obtaining consent.
    /// </summary>
    public void TriggerStep1ToolViolation(string toolName)
    {
        if (Time.time - lastStep1WarningTime < 1.5f) return;
        lastStep1WarningTime = Time.time;

        string cleanName = toolName.Replace('_', ' ');
        string title = string.IsNullOrEmpty(step1WarningTitle) ? "Consent Required!" : step1WarningTitle;
        string subtitle = string.IsNullOrEmpty(step1WarningSubtitle)
            ? $"Talk to the mother and obtain informed consent before handling instruments ({cleanName})."
            : string.Format(step1WarningSubtitle, cleanName);

        FlashRedVignette(1.8f);
        ShowInFaceHUD(title, subtitle, new Color(0.95f, 0.20f, 0.20f, 1f), 3.0f);

        if (VRDemoGameManager.Instance != null)
        {
            string mistakeLog = string.IsNullOrEmpty(step1MistakeLogText)
                ? $"Handled '{cleanName}' before obtaining informed consent"
                : string.Format(step1MistakeLogText, cleanName);
            VRDemoGameManager.Instance.RecordMistake(mistakeLog, 5);
        }

        PlayWarningAudio();
        SendHapticsToControllers(0.6f, 0.35f);
    }

    /// <summary>
    /// Flashes the red vignette around the perimeter of the player's VR field of view.
    /// </summary>
    public void FlashRedVignette(float duration = 1.6f)
    {
        EnsureVignetteOverlayExists();
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(RedVignetteRoutine(duration));
    }

    private IEnumerator RedVignetteRoutine(float duration)
    {
        if (vignetteCanvasGroup == null) yield break;

        // Pulse in
        float elapsed = 0f;
        float inDuration = 0.2f;
        while (elapsed < inDuration)
        {
            elapsed += Time.deltaTime;
            vignetteCanvasGroup.alpha = Mathf.Lerp(0f, 0.85f, elapsed / inDuration);
            yield return null;
        }

        // Pulse heartbeat effect while active
        float stayElapsed = 0f;
        float stayDuration = duration - 0.4f;
        while (stayElapsed < stayDuration)
        {
            stayElapsed += Time.deltaTime;
            float pulse = 0.70f + Mathf.Sin(stayElapsed * 10f) * 0.15f;
            vignetteCanvasGroup.alpha = pulse;
            yield return null;
        }

        // Fade out
        elapsed = 0f;
        float outDuration = 0.35f;
        while (elapsed < outDuration)
        {
            elapsed += Time.deltaTime;
            vignetteCanvasGroup.alpha = Mathf.Lerp(0.70f, 0f, elapsed / outDuration);
            yield return null;
        }

        vignetteCanvasGroup.alpha = 0f;
        vignetteCoroutine = null;
    }

    /// <summary>
    /// Displays a prominent, close-range floating banner directly in front of the player's vision.
    /// </summary>
    public void ShowInFaceHUD(string title, string subtitle, Color themeColor, float duration = 2.8f)
    {
        EnsureInFaceHUDExists();
        if (hudCoroutine != null) StopCoroutine(hudCoroutine);
        hudCoroutine = StartCoroutine(InFaceHUDRoutine(title, subtitle, themeColor, duration));
    }

    private IEnumerator InFaceHUDRoutine(string title, string subtitle, Color themeColor, float duration)
    {
        if (hudTitleTMP != null)
        {
            hudTitleTMP.text = title;
            hudTitleTMP.color = themeColor;
        }

        if (hudSubtitleTMP != null)
        {
            hudSubtitleTMP.text = subtitle;
        }

        if (hudOutline != null)
        {
            hudOutline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.9f);
        }

        // Snap position immediately in front of camera
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null && hudCanvasObj != null)
        {
            hudCanvasObj.transform.position = targetCamera.transform.position + targetCamera.transform.forward * hudDistance + targetCamera.transform.up * hudHeightOffset;
            hudCanvasObj.transform.rotation = Quaternion.LookRotation(hudCanvasObj.transform.position - targetCamera.transform.position);
        }

        // Fade In & Scale Punch
        float elapsed = 0f;
        float fadeInDuration = 0.25f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            if (hudCanvasGroup != null) hudCanvasGroup.alpha = t;
            if (hudCanvasObj != null) hudCanvasObj.transform.localScale = Vector3.one * Mathf.Lerp(0.00085f, 0.0010f, t);
            yield return null;
        }

        if (hudCanvasGroup != null) hudCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(duration);

        // Fade Out
        elapsed = 0f;
        float fadeOutDuration = 0.35f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(1f - (elapsed / fadeOutDuration));
            if (hudCanvasGroup != null) hudCanvasGroup.alpha = t;
            yield return null;
        }

        if (hudCanvasGroup != null) hudCanvasGroup.alpha = 0f;
        hudCoroutine = null;
    }

    private void EnsureVignetteOverlayExists()
    {
        if (vignetteCanvasObj != null) return;

        vignetteCanvasObj = new GameObject("VR_RedVignette_Overlay");
        vignetteCanvasObj.transform.SetParent(transform, false);

        var canvas = vignetteCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        vignetteCanvasObj.AddComponent<CanvasScaler>();

        vignetteCanvasGroup = vignetteCanvasObj.AddComponent<CanvasGroup>();
        vignetteCanvasGroup.alpha = 0f;
        vignetteCanvasGroup.blocksRaycasts = false;
        vignetteCanvasGroup.interactable = false;

        var rect = vignetteCanvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(900, 600);
        rect.localScale = new Vector3(0.00065f, 0.00065f, 0.00065f);

        // Procedural radial vignette image
        GameObject imgGo = new GameObject("VignetteImage");
        imgGo.transform.SetParent(vignetteCanvasObj.transform, false);
        var imgRect = imgGo.AddComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.sizeDelta = Vector2.zero;

        vignetteImage = imgGo.AddComponent<Image>();
        vignetteImage.sprite = CreateVignetteSprite();
        vignetteImage.color = new Color(1f, 0.08f, 0.08f, 1f);
        vignetteImage.raycastTarget = false;
    }

    private Sprite CreateVignetteSprite()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2((float)x / size, (float)y / size);
                float dist = Vector2.Distance(uv, center) * 1.4142f; // 0 at center, 1 at corner
                float alpha = Mathf.Clamp01((dist - 0.35f) / 0.65f);
                alpha = Mathf.Pow(alpha, 1.8f); // Soft radial exponential falloff
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void EnsureInFaceHUDExists()
    {
        if (hudCanvasObj != null) return;

        hudCanvasObj = new GameObject("VR_InFaceHUD_Canvas");
        hudCanvasObj.transform.SetParent(transform, false);

        var canvas = hudCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        hudCanvasObj.AddComponent<CanvasScaler>();

        hudCanvasGroup = hudCanvasObj.AddComponent<CanvasGroup>();
        hudCanvasGroup.alpha = 0f;
        hudCanvasGroup.blocksRaycasts = false;
        hudCanvasGroup.interactable = false;

        var rect = hudCanvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(460, 130);
        rect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Panel Background
        GameObject panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(hudCanvasObj.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        hudPanelBg = panelGo.AddComponent<Image>();
        hudPanelBg.color = new Color(0.05f, 0.08f, 0.14f, 0.94f); // Dark translucent glass

        hudOutline = panelGo.AddComponent<Outline>();
        hudOutline.effectColor = new Color(0.95f, 0.20f, 0.20f, 0.85f);
        hudOutline.effectDistance = new Vector2(2f, -2f);

        // Title
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.52f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(12, 0);
        titleRect.offsetMax = new Vector2(-12, -8);

        hudTitleTMP = titleGo.AddComponent<TextMeshProUGUI>();
        hudTitleTMP.text = "Protocol Alert";
        hudTitleTMP.fontSize = 24;
        hudTitleTMP.fontStyle = FontStyles.Bold;
        hudTitleTMP.alignment = TextAlignmentOptions.Center;
        hudTitleTMP.color = new Color(0.95f, 0.25f, 0.25f, 1f);

        // Subtitle
        GameObject subGo = new GameObject("Subtitle");
        subGo.transform.SetParent(panelGo.transform, false);
        var subRect = subGo.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.52f);
        subRect.offsetMin = new Vector2(12, 8);
        subRect.offsetMax = new Vector2(-12, 0);

        hudSubtitleTMP = subGo.AddComponent<TextMeshProUGUI>();
        hudSubtitleTMP.text = "Please follow the clinical procedure.";
        hudSubtitleTMP.fontSize = 15;
        hudSubtitleTMP.alignment = TextAlignmentOptions.Center;
        hudSubtitleTMP.color = new Color(0.92f, 0.95f, 0.98f, 0.95f);
        hudSubtitleTMP.textWrappingMode = TextWrappingModes.Normal;
    }

    private void PlayWarningAudio()
    {
        if (audioSource != null && warningBuzzerClip != null)
        {
            audioSource.PlayOneShot(warningBuzzerClip);
        }
    }

    private void SendHapticsToControllers(float amplitude, float duration)
    {
        var interactors = FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None);
        foreach (var interactor in interactors)
        {
            if (interactor is XRDirectInteractor direct)
            {
                direct.SendHapticImpulse(amplitude, duration);
            }
            else if (interactor is XRRayInteractor ray)
            {
                ray.SendHapticImpulse(amplitude, duration);
            }
        }
    }
}
