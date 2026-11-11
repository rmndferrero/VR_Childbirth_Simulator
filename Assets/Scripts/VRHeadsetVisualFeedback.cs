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

    // Red Vignette Overlay Components (Single-Pass Instanced URP Stereo Shader Quad)
    private GameObject vignetteOverlayObj;
    private MeshFilter vignetteMeshFilter;
    private MeshRenderer vignetteMeshRenderer;
    private Mesh vignetteMesh;
    private Material vignetteMaterial;
    private bool usingCustomVignetteShader;
    private Coroutine vignetteCoroutine;
    private float currentVignetteIntensity = 0f;
    private float lastVignetteFlashTime = -99f;
    private const float VignetteOverlayDistance = 0.25f;
    private const float VignetteMinRetriggerInterval = 0.15f;

    private static readonly int ColorShaderId = Shader.PropertyToID("_Color");
    private static readonly int IntensityShaderId = Shader.PropertyToID("_Intensity");
    private static readonly int InnerRadiusShaderId = Shader.PropertyToID("_InnerRadius");
    private static readonly int OuterRadiusShaderId = Shader.PropertyToID("_OuterRadius");
    private static readonly int SoftnessShaderId = Shader.PropertyToID("_Softness");
    private static readonly Color VignetteTintColor = new Color(0.88f, 0.015f, 0.015f, 0.78f);

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

    private void OnEnable()
    {
        EvaluationLogger.Instance.OnEventLogged -= HandleEvaluationEventLogged;
        EvaluationLogger.Instance.OnEventLogged += HandleEvaluationEventLogged;
    }

    private void OnDisable()
    {
        EvaluationLogger.Instance.OnEventLogged -= HandleEvaluationEventLogged;
    }

    private void OnDestroy()
    {
        EvaluationLogger.Instance.OnEventLogged -= HandleEvaluationEventLogged;
        if (Instance == this) Instance = null;

        if (vignetteMaterial != null)
        {
            if (Application.isPlaying) Destroy(vignetteMaterial);
            else DestroyImmediate(vignetteMaterial);
            vignetteMaterial = null;
        }

        if (vignetteMesh != null)
        {
            if (Application.isPlaying) Destroy(vignetteMesh);
            else DestroyImmediate(vignetteMesh);
            vignetteMesh = null;
        }
    }

    private void HandleEvaluationEventLogged(EvaluationEvent e)
    {
        if (e == null || e.isCorrect) return;
        float duration = e.severity == MistakeSeverity.Severe ? 1.45f : 1.15f;
        FlashRedVignette(duration);
    }

    private void Start()
    {
        ResolveActiveCamera();
        EnsureVignetteOverlayExists();
        EnsureInFaceHUDExists();
    }

    private void LateUpdate()
    {
        Camera cam = ResolveActiveCamera();
        if (cam == null) return;

        // Position In-Face HUD directly in front of the player's view
        if (hudCanvasObj != null && hudCanvasGroup != null && hudCanvasGroup.alpha > 0.001f)
        {
            Vector3 targetPos = cam.transform.position + cam.transform.forward * hudDistance + cam.transform.up * hudHeightOffset;
            hudCanvasObj.transform.position = Vector3.Lerp(hudCanvasObj.transform.position, targetPos, Time.unscaledDeltaTime * 14f);
            hudCanvasObj.transform.rotation = Quaternion.LookRotation(hudCanvasObj.transform.position - cam.transform.position);
        }

        // Keep Vignette overlay parented to the active stereo VR camera
        UpdateVignetteOverlayTransform();
    }

    private Camera ResolveActiveCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled && targetCamera.gameObject.activeInHierarchy)
        {
            return targetCamera;
        }

        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.isActiveAndEnabled && mainCam.gameObject.activeInHierarchy)
        {
            targetCamera = mainCam;
            return targetCamera;
        }

        Camera[] allCameras = Camera.allCameras;
        Camera fallbackGameCam = null;
        for (int i = 0; i < allCameras.Length; i++)
        {
            Camera cam = allCameras[i];
            if (cam == null || !cam.isActiveAndEnabled || !cam.gameObject.activeInHierarchy) continue;
            if (cam.cameraType != CameraType.Game) continue;
            if (cam.stereoTargetEye != StereoTargetEyeMask.None)
            {
                targetCamera = cam;
                return targetCamera;
            }
            if (fallbackGameCam == null) fallbackGameCam = cam;
        }

        if (fallbackGameCam != null)
        {
            targetCamera = fallbackGameCam;
        }

        return targetCamera;
    }

    private void UpdateVignetteOverlayTransform()
    {
        Camera cam = ResolveActiveCamera();
        if (vignetteOverlayObj == null || cam == null) return;

        if (vignetteOverlayObj.transform.parent != cam.transform)
        {
            vignetteOverlayObj.transform.SetParent(cam.transform, false);
        }

        vignetteOverlayObj.transform.localPosition = new Vector3(0f, 0f, VignetteOverlayDistance);
        vignetteOverlayObj.transform.localRotation = Quaternion.identity;
        vignetteOverlayObj.transform.localScale = Vector3.one;

        EnsureOverlayLayerVisibleToCamera(cam);
    }

    private void EnsureOverlayLayerVisibleToCamera(Camera cam)
    {
        if (vignetteOverlayObj == null || cam == null) return;
        int mask = cam.cullingMask;
        if ((mask & (1 << vignetteOverlayObj.layer)) != 0) return;

        if ((mask & 1) != 0)
        {
            vignetteOverlayObj.layer = 0;
            return;
        }

        for (int bit = 0; bit < 32; bit++)
        {
            if ((mask & (1 << bit)) != 0)
            {
                vignetteOverlayObj.layer = bit;
                return;
            }
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

        if (VRDemoGameManager.Instance == null) FlashRedVignette(1.45f);
        ShowInFaceHUD(title, subtitle, new Color(0.95f, 0.20f, 0.20f, 1f), 3.0f);

        // Scoring is done once by the caller (ToolItem -> RecordViolation(Tool_Handled_Before_Consent));
        // this method only gives HUD / audio / haptic feedback while EvaluationLogger triggers the vignette.

        PlayWarningAudio();
        SendHapticsToControllers(0.6f, 0.35f);
    }

    private float lastMayoViolationTime = 0f;

    /// <summary>
    /// Triggered when the player attempts to place or arrange tools onto Table 2 before draping the Mayo Stand.
    /// Matches the visual and audio feedback of the Step 1 pitcher/consent lockout.
    /// </summary>
    public void TriggerUnsterileMayoViolation(string toolName)
    {
        if (Time.time - lastMayoViolationTime < 1.5f) return;
        lastMayoViolationTime = Time.time;

        string cleanName = string.IsNullOrEmpty(toolName) ? "Instrument" : toolName.Replace('_', ' ');
        string title = "Sterile Field Breach!";
        string subtitle = $"Drape the Mayo Stand with sterile linen before setting up instruments ({cleanName}).";

        ShowInFaceHUD(title, subtitle, new Color(0.95f, 0.20f, 0.20f, 1f), 3.2f);

        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.RecordViolation(
                ClinicalViolationType.Unsterile_Mayo_Placement_Violation,
                $"Placed '{cleanName}' on un-draped Mayo Stand"
            );
        }
        else
        {
            FlashRedVignette(1.45f);
        }

        PlayWarningAudio();
        SendHapticsToControllers(0.7f, 0.4f);
    }

    [ContextMenu("Test Red Vignette Flash")]
    public void TestRedVignetteFlash()
    {
        TriggerMistakeFlash(1.25f);
    }

    /// <summary>
    /// Smoothly pulses a crimson border vignette around the perimeter of the player's VR field of view.
    /// </summary>
    public void FlashRedVignette(float duration = 1.15f)
    {
        if (vignetteCoroutine != null && Time.unscaledTime - lastVignetteFlashTime < VignetteMinRetriggerInterval)
        {
            return;
        }

        lastVignetteFlashTime = Time.unscaledTime;
        EnsureVignetteOverlayExists();
        UpdateVignetteOverlayTransform();
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(RedVignetteRoutine(Mathf.Max(0.85f, duration)));
    }

    /// <summary>
    /// Triggers warning buzzer audio and controller haptics (and red vignette).
    /// </summary>
    public void TriggerMistakeFlash(float duration = 1.15f)
    {
        FlashRedVignette(duration);
        PlayFeedbackCuesOnly();
    }

    public void PlayFeedbackCuesOnly(float hapticAmplitude = 0.55f, float hapticDuration = 0.28f)
    {
        PlayWarningAudio();
        SendHapticsToControllers(hapticAmplitude, hapticDuration);
    }

    private IEnumerator RedVignetteRoutine(float duration)
    {
        if (vignetteMaterial == null || vignetteMeshRenderer == null) yield break;

        const float inDuration = 0.14f;
        const float outDuration = 0.50f;
        float holdDuration = Mathf.Max(0.20f, duration - inDuration - outDuration);

        // Smooth ease-in from current intensity using unscaled time
        float startIntensity = currentVignetteIntensity;
        float elapsed = 0f;
        while (elapsed < inDuration)
        {
            float dt = Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.02f;
            elapsed += dt;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / inDuration));
            ApplyVignetteIntensity(Mathf.Lerp(startIntensity, 1f, t));
            yield return null;
        }

        ApplyVignetteIntensity(1f);
        float holdElapsed = 0f;
        while (holdElapsed < holdDuration)
        {
            float dt = Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.02f;
            holdElapsed += dt;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < outDuration)
        {
            float dt = Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.02f;
            elapsed += dt;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / outDuration));
            ApplyVignetteIntensity(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        ApplyVignetteIntensity(0f);
        vignetteCoroutine = null;
    }

    private void ApplyVignetteIntensity(float normalizedIntensity)
    {
        currentVignetteIntensity = Mathf.Clamp01(normalizedIntensity);

        if (vignetteMaterial != null)
        {
            if (usingCustomVignetteShader)
            {
                vignetteMaterial.SetFloat(IntensityShaderId, currentVignetteIntensity);
            }
            else
            {
                Color c = VignetteTintColor;
                c.a = VignetteTintColor.a * currentVignetteIntensity;
                if (vignetteMaterial.HasProperty("_BaseColor")) vignetteMaterial.SetColor("_BaseColor", c);
                if (vignetteMaterial.HasProperty("_Color")) vignetteMaterial.SetColor("_Color", c);
            }
        }

        if (vignetteMeshRenderer != null)
        {
            vignetteMeshRenderer.enabled = currentVignetteIntensity > 0.001f;
        }
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
        if (vignetteOverlayObj != null) return;

        Camera cam = ResolveActiveCamera();

        vignetteOverlayObj = new GameObject("VR_RedVignette_Overlay");
        if (cam != null)
        {
            vignetteOverlayObj.transform.SetParent(cam.transform, false);
            vignetteOverlayObj.transform.localPosition = new Vector3(0f, 0f, VignetteOverlayDistance);
            vignetteOverlayObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            vignetteOverlayObj.transform.SetParent(transform, false);
        }

        vignetteMesh = CreateVignetteQuadMesh();
        vignetteMeshFilter = vignetteOverlayObj.AddComponent<MeshFilter>();
        vignetteMeshFilter.sharedMesh = vignetteMesh;

        vignetteMaterial = CreateVignetteMaterial(out usingCustomVignetteShader);

        vignetteMeshRenderer = vignetteOverlayObj.AddComponent<MeshRenderer>();
        vignetteMeshRenderer.sharedMaterial = vignetteMaterial;
        vignetteMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        vignetteMeshRenderer.receiveShadows = false;
        vignetteMeshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        vignetteMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        vignetteMeshRenderer.allowOcclusionWhenDynamic = false;
        vignetteMeshRenderer.sortingOrder = 32000;

        ApplyVignetteIntensity(currentVignetteIntensity);
        UpdateVignetteOverlayTransform();
    }

    private static Mesh CreateVignetteQuadMesh()
    {
        var mesh = new Mesh { name = "VR_RedVignette_Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        // Generous bounds prevent CPU frustum culling across any OpenXR IPD, canted frustum, or nearClipPlane.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));
        return mesh;
    }

    private static Material CreateVignetteMaterial(out bool isCustomShader)
    {
        Shader customShader = Shader.Find("VRSim/VRHeadsetVignetteOverlay");
        if (customShader != null)
        {
            isCustomShader = true;
            var mat = new Material(customShader);
            ConfigureCustomVignetteMaterial(mat);
            return mat;
        }

        Material resourceMat = Resources.Load<Material>("VRHeadsetVignetteMat");
        if (resourceMat != null && resourceMat.shader != null && resourceMat.shader.name == "VRSim/VRHeadsetVignetteOverlay")
        {
            isCustomShader = true;
            var mat = new Material(resourceMat);
            ConfigureCustomVignetteMaterial(mat);
            return mat;
        }

        isCustomShader = false;
        Shader fallback = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (fallback != null)
        {
            var mat = new Material(fallback);
            return mat;
        }

        return null;
    }

    private static void ConfigureCustomVignetteMaterial(Material mat)
    {
        if (mat == null) return;
        mat.SetColor(ColorShaderId, VignetteTintColor);
        mat.SetFloat(IntensityShaderId, 0f);
        mat.SetFloat(InnerRadiusShaderId, 0.44f);
        mat.SetFloat(OuterRadiusShaderId, 0.86f);
        mat.SetFloat(SoftnessShaderId, 1.25f);
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

    public void SendHapticsToControllers(float amplitude, float duration)
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
