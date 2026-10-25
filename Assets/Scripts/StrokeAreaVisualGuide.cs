using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the 9 scene-based 3D On-Skin Stroke Sliders during gameplay.
/// When holding the handling forceps with cotton:
/// - Active Stroke: Highlighted in Glowing Sky Blue with live handle bead, fill line, and direction arrow.
/// - Upcoming Strokes: Clean, subtle roadmap tracks showing where future strokes will be placed.
/// - Completed Strokes: Slider guides hide completely, leaving only the applied betadine on the mother's skin!
/// - Mistake Out-of-Order Touch: The touched slider flashes Glowing Neon Red for 1.25s.
/// When not holding forceps + cotton: All slider guides hide to keep the view clean (applied betadine remains visible).
/// </summary>
public class StrokeAreaVisualGuide : MonoBehaviour
{
    [Header("Sliders Parent / Collection")]
    [Tooltip("The parent GameObject containing all 9 StrokeSliderTrack children.")]
    public Transform slidersParent;
    public StrokeSliderTrack[] sliders = new StrokeSliderTrack[9];

    [Header("Visual Materials")]
    public Material activeBoxMaterial;
    public Material upcomingBoxMaterial;
    public Material completedBoxMaterial;
    public Material mistakeBoxMaterial;

    [Header("Handling Forceps Reference")]
    [Tooltip("Reference to the Handling Forceps. Auto-cached if null.")]
    public ForcepsController handlingForceps;

    [Header("Blind Test / Examination Mode")]
    [Tooltip("When true, hides the 9-ball stroke guide lines and nodes during normal wiping so the user must identify the anatomical zones from memory. Keeps 100% full stroke tracking logic active, and ONLY flashes Glowing Neon Red when a mistake occurs!")]
    public bool hideGuidesUnlessError = true;

    [Header("Hint Configuration")]
    [Tooltip("Number of mistakes on the current active stroke before triggering the bright glowing blue visual hint.")]
    public int mistakesRequiredForHint = 3;

    private StrokeTrackingManager trackingManager;
    private int currentActiveIndex = 0;
    private int currentMistakeIndex = -1;
    private float mistakeFlashTimer = 0f;
    private int activeStrokeMistakeCount = 0;

    private void Awake()
    {
        trackingManager = FindFirstObjectByType<StrokeTrackingManager>();
        if (trackingManager != null)
        {
            trackingManager.OnStrokeAdvanced += HandleStrokeAdvanced;
            trackingManager.OnMistakeZoneTriggered += HandleMistakeZoneTriggered;
            trackingManager.OnStrokeProgressUpdated += HandleStrokeProgressUpdated;
        }

        LoadMaterialsIfNull();
        CacheHandlingForceps();
        CacheSliders();
    }

    private void Start()
    {
        CacheHandlingForceps();
        CacheSliders();
        RefreshActiveZoneDisplay();
    }

    private void OnEnable()
    {
        CacheHandlingForceps();
        CacheSliders();
        RefreshActiveZoneDisplay();
    }

    private void OnDestroy()
    {
        if (trackingManager != null)
        {
            trackingManager.OnStrokeAdvanced -= HandleStrokeAdvanced;
            trackingManager.OnMistakeZoneTriggered -= HandleMistakeZoneTriggered;
            trackingManager.OnStrokeProgressUpdated -= HandleStrokeProgressUpdated;
        }
    }

    public void CacheSliders()
    {
        if (slidersParent == null)
        {
            var found = GameObject.Find("Perineal_Stroke_Sliders");
            if (found != null) slidersParent = found.transform;
        }

        if (slidersParent != null)
        {
            var foundSliders = slidersParent.GetComponentsInChildren<StrokeSliderTrack>(true);
            var list = new List<StrokeSliderTrack>(foundSliders);
            list.Sort((a, b) => a.strokeIndex.CompareTo(b.strokeIndex));
            sliders = list.ToArray();
        }
        else
        {
            var foundSliders = FindObjectsByType<StrokeSliderTrack>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var list = new List<StrokeSliderTrack>(foundSliders);
            list.Sort((a, b) => a.strokeIndex.CompareTo(b.strokeIndex));
            sliders = list.ToArray();
        }

        foreach (var s in sliders)
        {
            if (s != null)
            {
                if (s.activeMaterial == null) s.activeMaterial = activeBoxMaterial;
                if (s.upcomingMaterial == null) s.upcomingMaterial = upcomingBoxMaterial;
                if (s.completedMaterial == null) s.completedMaterial = completedBoxMaterial;
                if (s.mistakeMaterial == null) s.mistakeMaterial = mistakeBoxMaterial;
            }
        }
    }

    private void CacheHandlingForceps()
    {
        if (handlingForceps != null) return;
        var allForceps = FindObjectsByType<ForcepsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var f in allForceps)
        {
            if (f != null && f.role == ForcepsRole.Handling)
            {
                handlingForceps = f;
                break;
            }
        }
    }

    private void LoadMaterialsIfNull()
    {
        if (activeBoxMaterial == null)
        {
#if UNITY_EDITOR
            activeBoxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Active_Mat.mat");
            if (activeBoxMaterial == null)
                activeBoxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Mesh_Mat.mat");
#endif
        }

        if (upcomingBoxMaterial == null)
        {
#if UNITY_EDITOR
            upcomingBoxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Inactive_Mat.mat");
            if (upcomingBoxMaterial == null) upcomingBoxMaterial = activeBoxMaterial;
#endif
        }

        if (completedBoxMaterial == null)
        {
#if UNITY_EDITOR
            completedBoxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GREEN GLOW.mat");
            if (completedBoxMaterial == null) completedBoxMaterial = upcomingBoxMaterial;
#endif
        }

        if (mistakeBoxMaterial == null)
        {
#if UNITY_EDITOR
            mistakeBoxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Red_Mistake_Mat.mat");
#endif
        }
    }

    private void HandleStrokeAdvanced(int strokeIndex, StrokeZoneDefinition activeZone)
    {
        currentActiveIndex = strokeIndex;
        activeStrokeMistakeCount = 0; // Reset mistake counter for the new stroke
        mistakeFlashTimer = 0f;
        currentMistakeIndex = -1;

        if (sliders != null)
        {
            foreach (var s in sliders)
            {
                if (s != null)
                {
                    s.ResetProgress();
                }
            }
        }

        RefreshActiveZoneDisplay();
    }

    private void HandleMistakeZoneTriggered(int mistakeIndex)
    {
        if (trackingManager != null && mistakeIndex < trackingManager.GetCurrentStrokeIndex())
        {
            return;
        }

        activeStrokeMistakeCount++;
        currentMistakeIndex = mistakeIndex;
        mistakeFlashTimer = (trackingManager != null) ? trackingManager.mistakeLockoutDuration : 1.25f;

        // NOTE: Sliders DO NOT turn red! Only the VR Red POV effect flashes on mistakes.
        // If the user fails 3 times on the active stroke, the active stroke slider will be revealed as a guide.

        if (activeStrokeMistakeCount >= mistakesRequiredForHint)
        {
            Debug.Log($"[StrokeAreaVisualGuide] Hint Triggered on Stroke {currentActiveIndex + 1}! ({activeStrokeMistakeCount} mistakes made). Revealing active stroke slider as guide.");
        }
    }

    private void HandleStrokeProgressUpdated(int strokeIndex, float progress, Vector3 handlePos)
    {
        if (strokeIndex == currentActiveIndex && sliders != null && strokeIndex >= 0 && strokeIndex < sliders.Length)
        {
            var slider = sliders[strokeIndex];
            if (slider != null)
            {
                slider.UpdateProgress(progress, handlePos);
            }
        }
    }

    private void RefreshActiveZoneDisplay()
    {
        if (trackingManager != null)
        {
            currentActiveIndex = trackingManager.GetCurrentStrokeIndex();
        }
    }

    public bool IsCottonInHandlingForceps()
    {
        if (handlingForceps == null) CacheHandlingForceps();

        if (handlingForceps != null && handlingForceps.HasCotton)
        {
            return true;
        }

        if (CottonState.activeCottons != null)
        {
            foreach (var cotton in CottonState.activeCottons)
            {
                if (cotton != null && cotton.currentHolder == ForcepsRole.Handling && !cotton.isUsed)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (mistakeFlashTimer > 0f)
        {
            mistakeFlashTimer -= Time.deltaTime;
            if (mistakeFlashTimer <= 0f)
            {
                currentMistakeIndex = -1;
            }
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Updates all 9 sliders:
    /// - Normal memory testing mode: Sliders are invisible by default so the user must identify the anatomical zones from memory.
    /// - Mistakes trigger ONLY the POV red flash in the VR headset (sliders never turn red).
    /// - 3 Mistakes Rule: If the user fails 3 times on the active stroke, that stroke's slider guide is revealed in Glowing Sky Blue.
    /// - Completed strokes: Slider guide disappears completely, leaving only applied betadine on the skin.
    /// </summary>
    private void UpdateVisuals()
    {
        if (sliders == null || sliders.Length == 0) return;

        bool isHoldingCotton = IsCottonInHandlingForceps();
        bool showActiveGuide = (activeStrokeMistakeCount >= mistakesRequiredForHint) && isHoldingCotton;

        for (int i = 0; i < sliders.Length; i++)
        {
            var slider = sliders[i];
            if (slider == null) continue;

            // 1. Active Stroke Guide (only shown if failed 3 times and holding cotton)
            if (i == currentActiveIndex && currentActiveIndex < 9)
            {
                if (showActiveGuide)
                {
                    slider.gameObject.SetActive(true);
                    slider.SetVisualElementsVisible(true);
                    slider.SetVisualState(SliderVisualState.Active);
                }
                else
                {
                    slider.SetVisualElementsVisible(false);
                    slider.gameObject.SetActive(false);
                }
                continue;
            }

            // 2. All other strokes (completed or upcoming): Always hidden
            slider.SetVisualElementsVisible(false);
            slider.gameObject.SetActive(false);
        }
    }

    [ContextMenu("Toggle Test Mode (Hide Guides Unless Error)")]
    public void ToggleTestMode()
    {
        hideGuidesUnlessError = !hideGuidesUnlessError;
        Debug.Log($"[StrokeAreaVisualGuide] Test Mode (Hide Guides Unless Error) is now: {(hideGuidesUnlessError ? "ENABLED (Invisible until error)" : "DISABLED (Guided)")}");
    }
}
