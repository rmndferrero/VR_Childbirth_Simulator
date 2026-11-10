using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sleek, modern floating HUD banner tracking the 9-ball perineal sequence.
/// Preserves 100% visual styling consistency with DrapingGuideUI and the rest of the simulation.
/// Supports Memory-Based Assessment Mode:
/// - By default (0-2 mistakes), masks the active anatomical target name and direction so the student decides from memory.
/// - Upon 3 mistakes on the active stroke, reveals the exact anatomical target name (with Left/Right notation) and technique.
/// - Completed steps lock in with vibrant emerald green checkmarks (✓) and their confirmed anatomical names.
/// </summary>
public class StrokeGuideUI : MonoBehaviour
{
    [Header("Root Canvas")]
    public GameObject rootCanvas;

    [Header("Header & Instruction Text")]
    public TMP_Text phaseTitleText;
    public TMP_Text currentStrokeTitleText;
    public TMP_Text directionInstructionText;
    public TMP_Text liveFeedbackBanner;
    public TMP_Text progressPercentageText;

    [Header("Progress Bar")]
    public Image progressBarFill;

    [Header("9-Stroke Step Dots / Cards")]
    public Transform dotsContainer;
    public Image[] stepDotImages;
    public TMP_Text[] stepDotTexts;
    public TMP_Text[] stepDotSubTexts; // Zone short names (masked until completed or 3 mistakes)

    [Header("Assessment & Hint Settings")]
    [Tooltip("When true, hides the target zone name and direction in the UI until the student makes 3 mistakes on the current stroke.")]
    public bool hideTargetUntilMistakes = true;
    [Tooltip("Number of mistakes on the current stroke before the UI reveals the anatomical target name and direction hint.")]
    public int mistakesToRevealTarget = 3;

    [Header("Colors")]
    public Color completedColor = new Color(0.10f, 0.78f, 0.45f, 1f); // Vibrant Emerald Green
    public Color activeColor = new Color(0.15f, 0.75f, 1.00f, 1f);   // Glowing Cyan Blue
    public Color upcomingColor = new Color(0.18f, 0.24f, 0.32f, 0.85f); // Dark Slate

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip strokeSuccessClip;
    public AudioClip mistakeWarningClip;

    private static readonly string[] ShortZoneLabels = new string[9]
    {
        "1. R. Majus",
        "2. Mons Pubis",
        "3. L. Majus",
        "4. Labia Minora",
        "5. R. Thigh",
        "6. L. Thigh",
        "7. L. Buttock",
        "8. R. Buttock",
        "9. Anus"
    };

    private static readonly string[] ClinicalZoneNames = new string[9]
    {
        "Right Labium Majus",
        "Mons Pubis",
        "Left Labium Majus",
        "Labia Minora (Vestibule)",
        "Right Inner Thigh",
        "Left Inner Thigh",
        "Left Buttock",
        "Right Buttock",
        "Anus"
    };

    private StrokeTrackingManager trackingManager;
    private Coroutine feedbackCoroutine;

    private void Awake()
    {
        trackingManager = FindFirstObjectByType<StrokeTrackingManager>();
        if (trackingManager != null)
        {
            trackingManager.OnStrokeAdvanced += HandleStrokeAdvanced;
            trackingManager.OnStrokeValidated += HandleStrokeValidated;
            trackingManager.OnMistakeZoneTriggered += HandleMistakeZoneTriggered;
        }
    }

    private void OnDestroy()
    {
        if (trackingManager != null)
        {
            trackingManager.OnStrokeAdvanced -= HandleStrokeAdvanced;
            trackingManager.OnStrokeValidated -= HandleStrokeValidated;
            trackingManager.OnMistakeZoneTriggered -= HandleMistakeZoneTriggered;
        }
    }

    public void Show()
    {
        if (rootCanvas != null) rootCanvas.SetActive(true);
        RefreshUI();
    }

    public void Hide()
    {
        if (rootCanvas != null) rootCanvas.SetActive(false);
    }

    public void RefreshUI()
    {
        if (trackingManager == null) trackingManager = FindFirstObjectByType<StrokeTrackingManager>();
        if (trackingManager == null) return;

        int activeIdx = trackingManager.GetCurrentStrokeIndex();
        StrokeZoneDefinition activeZone = trackingManager.GetCurrentActiveZone();

        HandleStrokeAdvanced(activeIdx, activeZone);
    }

    private void HandleMistakeZoneTriggered(int mistakeZoneIndex)
    {
        if (trackingManager == null) return;
        int activeIdx = trackingManager.GetCurrentStrokeIndex();
        StrokeZoneDefinition activeZone = trackingManager.GetCurrentActiveZone();
        UpdateActiveDisplay(activeIdx, activeZone);
    }

    private void HandleStrokeAdvanced(int strokeIndex, StrokeZoneDefinition activeZone)
    {
        UpdateActiveDisplay(strokeIndex, activeZone);
    }

    private void UpdateActiveDisplay(int strokeIndex, StrokeZoneDefinition activeZone)
    {
        float progress01 = Mathf.Clamp01((float)strokeIndex / 9f);
        int mistakesOnCurrent = (trackingManager != null) ? trackingManager.GetActiveStrokeMistakeCount() : 0;
        bool revealHint = !hideTargetUntilMistakes || (mistakesOnCurrent >= mistakesToRevealTarget);

        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = progress01;
        }

        if (progressPercentageText != null)
        {
            progressPercentageText.text = $"Progress: {strokeIndex} of 9 Cotton Balls Used ({Mathf.RoundToInt(progress01 * 100f)}%)";
        }

        if (strokeIndex >= 0 && strokeIndex < 9)
        {
            string displayZoneName = (strokeIndex < ClinicalZoneNames.Length)
                ? ClinicalZoneNames[strokeIndex]
                : (activeZone != null ? activeZone.zoneName : $"Zone {strokeIndex + 1}");

            if (revealHint)
            {
                if (currentStrokeTitleText != null)
                {
                    string prefix = (mistakesOnCurrent >= mistakesToRevealTarget)
                        ? $"Target {strokeIndex + 1}/9 (Remedial Hint)"
                        : $"Target {strokeIndex + 1}/9";
                    currentStrokeTitleText.text = $"{prefix}: <color=#38BDF8><b>{displayZoneName}</b></color>";
                }

                if (directionInstructionText != null)
                {
                    directionInstructionText.text = GetTechniqueDescription(strokeIndex);
                }
            }
            else
            {
                if (currentStrokeTitleText != null)
                {
                    currentStrokeTitleText.text = $"Cotton Ball {strokeIndex + 1}/9: <color=#94A3B8><b>Identify Next Target Area from Memory</b></color>";
                }

                if (directionInstructionText != null)
                {
                    directionInstructionText.text = "Select the appropriate anatomical zone following the cleanest-to-dirtiest sequence.";
                }
            }
        }
        else
        {
            if (currentStrokeTitleText != null)
            {
                currentStrokeTitleText.text = "<color=#34D399><b>✓ All 9 Cotton Ball Strokes Completed Successfully!</b></color>";
            }
            if (directionInstructionText != null)
            {
                directionInstructionText.text = "Antiseptic application complete. Proceed to next step.";
            }
            if (progressBarFill != null) progressBarFill.fillAmount = 1f;
            if (progressPercentageText != null) progressPercentageText.text = "Progress: 9 of 9 Cotton Balls Used (100%)";
        }

        UpdateStepDots(strokeIndex, revealHint);
    }

    private string GetTechniqueDescription(int strokeIndex)
    {
        if (strokeIndex == 1)
        {
            return "Technique: <color=#38BDF8><b>Transverse Wipe</b></color> (Wipe across Mons Pubis from Left or Right)";
        }
        if (strokeIndex == 8)
        {
            return "Technique: <color=#FCD34D><b>Direct Contact Tap</b></color> (Single tap contact on Anus)";
        }
        return "Technique: <color=#38BDF8><b>Downward Wipe</b></color> (Slide cotton top to bottom along track)";
    }

    private void UpdateStepDots(int activeIndex, bool revealActiveHint)
    {
        if (stepDotImages == null) return;

        for (int i = 0; i < stepDotImages.Length; i++)
        {
            if (stepDotImages[i] == null) continue;

            if (i < activeIndex)
            {
                // Completed -> Vibrant Emerald Green with Checkmark (✓) and Confirmed Name
                stepDotImages[i].color = completedColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = "<b>✓</b>";
                    stepDotTexts[i].color = Color.white;
                }
                if (stepDotSubTexts != null && i < stepDotSubTexts.Length && stepDotSubTexts[i] != null && i < ShortZoneLabels.Length)
                {
                    stepDotSubTexts[i].text = ShortZoneLabels[i];
                    stepDotSubTexts[i].color = Color.white;
                }
            }
            else if (i == activeIndex)
            {
                // Active Target -> Glowing Sky Blue / Cyan with Step Number
                stepDotImages[i].color = activeColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = $"<b>{i + 1}</b>";
                    stepDotTexts[i].color = Color.white;
                }
                if (stepDotSubTexts != null && i < stepDotSubTexts.Length && stepDotSubTexts[i] != null && i < ShortZoneLabels.Length)
                {
                    stepDotSubTexts[i].text = revealActiveHint ? ShortZoneLabels[i] : $"Ball {i + 1}";
                    stepDotSubTexts[i].color = Color.white;
                }
            }
            else
            {
                // Upcoming -> Clean Dark Slate (Masked until reached/completed)
                stepDotImages[i].color = upcomingColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = $"{i + 1}";
                    stepDotTexts[i].color = new Color(0.70f, 0.80f, 0.90f, 0.65f);
                }
                if (stepDotSubTexts != null && i < stepDotSubTexts.Length && stepDotSubTexts[i] != null && i < ShortZoneLabels.Length)
                {
                    stepDotSubTexts[i].text = hideTargetUntilMistakes ? $"Ball {i + 1}" : ShortZoneLabels[i];
                    stepDotSubTexts[i].color = new Color(0.6f, 0.7f, 0.8f, 0.5f);
                }
            }
        }
    }

    private void HandleStrokeValidated(string message, bool isSuccess)
    {
        if (isSuccess)
        {
            PlaySound(strokeSuccessClip);
            ShowFeedback($"<color=#34D399><b>✓ {message}</b></color>", 1.8f);
        }
        else
        {
            PlaySound(mistakeWarningClip);
            ShowFeedback($"<color=#EF4444><b>{message}</b></color>", 2.2f);
        }
    }

    private void ShowFeedback(string text, float duration)
    {
        if (liveFeedbackBanner == null) return;

        if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
        feedbackCoroutine = StartCoroutine(FeedbackRoutine(text, duration));
    }

    private IEnumerator FeedbackRoutine(string text, float duration)
    {
        liveFeedbackBanner.gameObject.SetActive(true);
        liveFeedbackBanner.text = text;

        yield return new WaitForSeconds(duration);

        liveFeedbackBanner.text = "";
        liveFeedbackBanner.gameObject.SetActive(false);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
