using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the 9 scene-based 3D On-Skin Holographic Stroke Sliders during gameplay.
/// - Keeps all 9 sliders sorted (1 to 9) in the Hierarchy under Perineal_Stroke_Sliders.
/// - When NOT holding cotton in the handling forceps: All slider guides hide so the view is completely clean (applied Betadine puddles remain visible).
/// - When holding soaked cotton in the handling forceps:
///   1. Completed Strokes (i < currentActiveIndex): Visual guide disappears immediately upon stroke completion, replaced by the washable Betadine puddle on the skin!
///   2. Remaining Candidate Strokes (Decision Stage, < 3 mistakes): Shown as subtle semi-transparent holographic blueprint (hollow concentric reticles with 100% transparent centers + delicate dashed line, no directional spoilers) so the student decides where to stroke.
///   3. Active Wiping: Lights up in glowing cyan with progress beam and sparkle trail.
///   4. 3-Mistake Remedial Hint: If the student fails 3 times on the active stroke, the correct stroke pulses/glows rhythmically in bright cyan.
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

    [Header("Decision Stage & Remedial Hint Configuration")]
    [Tooltip("When true, all remaining candidate strokes appear as subtle semi-transparent blueprints until 3 mistakes occur (which triggers a glowing pulse hint on the correct stroke).")]
    public bool hideGuidesUnlessError = true;

    [Tooltip("Number of mistakes on the current active stroke before triggering the bright glowing cyan pulse hint.")]
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

            // Keep Hierarchy cleanly sorted 1 through 9
            for (int i = 0; i < sliders.Length; i++)
            {
                if (sliders[i] != null && sliders[i].transform.parent == slidersParent)
                {
                    sliders[i].transform.SetSiblingIndex(i);
                }
            }
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
        if (trackingManager != null && trackingManager.IsStrokeCompleted(mistakeIndex))
        {
            return;
        }

        int threshold = trackingManager != null ? trackingManager.GetMistakesRequiredForHint() : mistakesRequiredForHint;
        activeStrokeMistakeCount++;
        currentMistakeIndex = mistakeIndex;
        mistakeFlashTimer = (trackingManager != null) ? trackingManager.mistakeLockoutDuration : 1.25f;

        if (activeStrokeMistakeCount >= threshold)
        {
            Debug.Log($"[StrokeAreaVisualGuide] Warm-Gold Hint Triggered ({activeStrokeMistakeCount} mistakes). Highlighting valid zone(s): {(trackingManager != null ? trackingManager.GetValidChoicesSummary() : $"Stroke {currentActiveIndex + 1}")}");
        }
    }

    private void HandleStrokeProgressUpdated(int strokeIndex, float progress, Vector3 handlePos)
    {
        bool isValidOrActive = strokeIndex == currentActiveIndex
            || (trackingManager != null && trackingManager.IsStrokeValidNow(strokeIndex));
        if (isValidOrActive && sliders != null && strokeIndex >= 0 && strokeIndex < sliders.Length)
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

    private bool IsInPerinealStrokePhase()
    {
        if (PerinealCareManager.Instance == null) return true;
        var st = PerinealCareManager.Instance.currentState;
        return st == PerinealCareState.STATE_3_IODINE_7_5_SCRUB || st == PerinealCareState.STATE_5_IODINE_10_PAINT;
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

        if (trackingManager != null)
        {
            currentActiveIndex = trackingManager.GetCurrentStrokeIndex();
            activeStrokeMistakeCount = trackingManager.GetActiveStrokeMistakeCount();
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Refreshes slider visual states immediately from StrokeTrackingManager (useful after programmatic resets or in tests).
    /// </summary>
    public void RefreshVisualsImmediate()
    {
        if (trackingManager != null)
        {
            currentActiveIndex = trackingManager.GetCurrentStrokeIndex();
            activeStrokeMistakeCount = trackingManager.GetActiveStrokeMistakeCount();
        }
        UpdateVisuals();
    }

    /// <summary>
    /// Updates all 9 sliders:
    /// - When NOT holding cotton in handling forceps (or outside stroke phases): All guides are hidden.
    /// - Completed strokes (i < currentActiveIndex): Guides disappear immediately, replaced by the washable Betadine puddle on the skin.
    /// - Remaining strokes (i >= currentActiveIndex):
    ///   • Default (< 3 mistakes): Subtle semi-transparent holographic blueprint (hollow concentric rings + dashed lines) so the player decides which zone to stroke.
    ///   • Active wiping: Bright cyan glowing beam + sparkle dust trail.
    ///   • 3 Mistakes made on active stroke: Rhythmic breathing cyan pulse on the active stroke to guide the student.
    /// </summary>
    private void UpdateVisuals()
    {
        if (sliders == null || sliders.Length == 0) return;

        bool procedureIncomplete = trackingManager == null
            ? currentActiveIndex < 9
            : trackingManager.GetCompletedStrokeCount() < 9;
        bool showGuides = IsCottonInHandlingForceps() && IsInPerinealStrokePhase() && procedureIncomplete;
        int requiredMistakes = trackingManager != null ? trackingManager.GetMistakesRequiredForHint() : mistakesRequiredForHint;
        bool shouldPulseActiveHint = activeStrokeMistakeCount >= requiredMistakes;
        float activeProgress = (trackingManager != null) ? trackingManager.GetCurrentStrokeProgress() : 0f;
        bool isActivelyWiping = activeProgress > 0.01f;
        int activeAttemptZone = trackingManager != null ? trackingManager.GetActiveAttemptZoneIndex() : currentActiveIndex;

        for (int i = 0; i < sliders.Length; i++)
        {
            var slider = sliders[i];
            if (slider == null) continue;

            // Keep GameObject active in Hierarchy so the user can always inspect/edit transforms in Scene View
            if (!slider.gameObject.activeSelf)
            {
                slider.gameObject.SetActive(true);
            }

            bool isCompleted = trackingManager != null ? trackingManager.IsStrokeCompleted(i) : i < currentActiveIndex;

            // 1. Not holding cotton or stroke already completed -> Hide guide (leaving only the Betadine puddle on completed strokes)
            if (!showGuides || isCompleted)
            {
                slider.SetVisualElementsVisible(false);
                continue;
            }

            // 2. Remaining uncompleted stroke while holding cotton in handling forceps
            slider.SetVisualElementsVisible(true);

            bool isZoneWiping = isActivelyWiping && (i == activeAttemptZone || (activeAttemptZone < 0 && i == currentActiveIndex));
            bool isCandidateValidNow = trackingManager != null ? trackingManager.IsStrokeValidNow(i) : (i == currentActiveIndex);

            if (isZoneWiping)
            {
                slider.SetVisualState(SliderVisualState.Active, force: slider.IsShowingHintTracer);
            }
            else if (currentMistakeIndex == i && mistakeFlashTimer > 0f)
            {
                slider.SetVisualState(SliderVisualState.Mistake);
            }
            else if (isCandidateValidNow && (shouldPulseActiveHint || !hideGuidesUnlessError))
            {
                slider.SetVisualState(SliderVisualState.Active);
                slider.UpdatePulseHint(Time.time);
            }
            else if (shouldPulseActiveHint || !hideGuidesUnlessError)
            {
                slider.SetVisualState(SliderVisualState.Dimmed);
            }
            else
            {
                // Decision Stage (< 3 mistakes): Render identically to upcoming strokes so the player decides from memory!
                slider.SetVisualState(SliderVisualState.Upcoming);
            }
        }
    }

    [ContextMenu("Toggle Test Mode (Hide Guides Unless Error)")]
    public void ToggleTestMode()
    {
        hideGuidesUnlessError = !hideGuidesUnlessError;
        Debug.Log($"[StrokeAreaVisualGuide] Decision Mode (Pulse Only After 3 Mistakes) is now: {(hideGuidesUnlessError ? "ENABLED" : "DISABLED (Always Highlight Active)")}");
    }
}
