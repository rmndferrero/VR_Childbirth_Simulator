using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sleek, modern floating HUD banner tracking the 9-ball perineal sequence.
/// Shows progress bar (0% to 100%), active stroke instructions, and 9 step nodes
/// with checkmarks (✓) for completed zones, glowing borders for active target, and clean upcoming states.
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
    public TMP_Text[] stepDotSubTexts; // Optional zone short names (e.g. "R Majora", "Mons", etc.)

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
        "1. R Majora",
        "2. Mons",
        "3. L Majora",
        "4. Minora",
        "5. R Thigh",
        "6. L Thigh",
        "7. L Buttock",
        "8. R Buttock",
        "9. Anus"
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
        }
    }

    private void OnDestroy()
    {
        if (trackingManager != null)
        {
            trackingManager.OnStrokeAdvanced -= HandleStrokeAdvanced;
            trackingManager.OnStrokeValidated -= HandleStrokeValidated;
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

    private void HandleStrokeAdvanced(int strokeIndex, StrokeZoneDefinition activeZone)
    {
        float progress01 = Mathf.Clamp01((float)strokeIndex / 9f);

        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = progress01;
        }

        if (progressPercentageText != null)
        {
            progressPercentageText.text = $"{Mathf.RoundToInt(progress01 * 100f)}% ({strokeIndex}/9 Done)";
        }

        if (activeZone != null)
        {
            if (currentStrokeTitleText != null)
            {
                currentStrokeTitleText.text = $"Target {strokeIndex + 1}/9: <color=#38BDF8><b>{activeZone.zoneName}</b></color>";
            }

            if (directionInstructionText != null)
            {
                if (strokeIndex == 8)
                {
                    directionInstructionText.text = "Technique: <color=#FCD34D><b>Direct Dab / Pat</b></color> (Steady contact dwell on Anus)";
                }
                else if (strokeIndex == 1)
                {
                    directionInstructionText.text = "Technique: <color=#38BDF8><b>Horizontal swipe</b></color> (Lateral pass across Mons Pubis)";
                }
                else
                {
                    directionInstructionText.text = "Technique: <color=#38BDF8><b>Downward stroke</b></color> (Continuous swipe clean to dirty)";
                }
            }
        }
        else
        {
            if (currentStrokeTitleText != null)
            {
                currentStrokeTitleText.text = "<color=#34D399><b>✓ All 9 Strokes Completed Successfully!</b></color>";
            }
            if (directionInstructionText != null)
            {
                directionInstructionText.text = "Antiseptic application complete. Proceed to next step.";
            }
            if (progressBarFill != null) progressBarFill.fillAmount = 1f;
            if (progressPercentageText != null) progressPercentageText.text = "100% (9/9 Complete)";
        }

        UpdateStepDots(strokeIndex);
    }

    private void UpdateStepDots(int activeIndex)
    {
        if (stepDotImages == null) return;

        for (int i = 0; i < stepDotImages.Length; i++)
        {
            if (stepDotImages[i] == null) continue;

            if (i < activeIndex)
            {
                // Completed -> Vibrant Emerald Green with Step Number
                stepDotImages[i].color = completedColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = $"<b>{i + 1}</b>";
                    stepDotTexts[i].color = Color.white;
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
            }
            else
            {
                // Upcoming -> Clean Dark Slate
                stepDotImages[i].color = upcomingColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = $"{i + 1}";
                    stepDotTexts[i].color = new Color(0.70f, 0.80f, 0.90f, 0.65f);
                }
            }

            if (stepDotSubTexts != null && i < stepDotSubTexts.Length && stepDotSubTexts[i] != null && i < ShortZoneLabels.Length)
            {
                stepDotSubTexts[i].text = ShortZoneLabels[i];
                stepDotSubTexts[i].color = (i <= activeIndex) ? Color.white : new Color(0.6f, 0.7f, 0.8f, 0.5f);
            }
        }
    }

    private void HandleStrokeValidated(string message, bool isSuccess)
    {
        if (isSuccess)
        {
            PlaySound(strokeSuccessClip);
            ShowFeedback($"<color=#34D399><b>{message}</b></color>", 1.8f);
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
