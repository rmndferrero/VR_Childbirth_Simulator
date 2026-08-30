using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StrokeGuideUI : MonoBehaviour
{
    [Header("Root Canvas")]
    public GameObject rootCanvas;

    [Header("Header & Instruction Text")]
    public TMP_Text phaseTitleText;
    public TMP_Text currentStrokeTitleText;
    public TMP_Text directionInstructionText;
    public TMP_Text liveFeedbackBanner;

    [Header("9-Stroke Step Dots Container")]
    public Transform dotsContainer;
    public Image[] stepDotImages;
    public TMP_Text[] stepDotTexts;

    [Header("Colors")]
    public Color completedColor = new Color(0.15f, 0.85f, 0.40f, 1f); // Vibrant Green
    public Color activeColor = new Color(0.20f, 0.75f, 1f, 1f);       // Cyan / Sky Blue
    public Color upcomingColor = new Color(0.20f, 0.28f, 0.38f, 0.8f); // Dark Slate

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip strokeSuccessClip;
    public AudioClip mistakeWarningClip;

    private StrokeTrackingManager trackingManager;
    private Coroutine feedbackCoroutine;

    private void Awake()
    {
        trackingManager = FindObjectOfType<StrokeTrackingManager>();
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
        if (trackingManager == null) trackingManager = FindObjectOfType<StrokeTrackingManager>();
        if (trackingManager == null) return;

        int activeIdx = trackingManager.GetCurrentStrokeIndex();
        StrokeZoneDefinition activeZone = trackingManager.GetCurrentActiveZone();

        HandleStrokeAdvanced(activeIdx, activeZone);
    }

    private void HandleStrokeAdvanced(int strokeIndex, StrokeZoneDefinition activeZone)
    {
        if (activeZone != null)
        {
            if (currentStrokeTitleText != null)
            {
                currentStrokeTitleText.text = $"Stroke {strokeIndex + 1} of 9: <color=#38BDF8><b>{activeZone.zoneName}</b></color>";
            }

            if (directionInstructionText != null)
            {
                directionInstructionText.text = "Direction: <b>Downward stroke</b> (One continuous swipe top to bottom)";
            }
        }
        else
        {
            if (currentStrokeTitleText != null)
            {
                currentStrokeTitleText.text = "<color=#34D399><b>✓ All 9 Strokes Completed!</b></color>";
            }
            if (directionInstructionText != null)
            {
                directionInstructionText.text = "All zones prepared. Ready for water rinse.";
            }
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
                // Completed
                stepDotImages[i].color = completedColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = "✓";
                    stepDotTexts[i].color = Color.white;
                }
            }
            else if (i == activeIndex)
            {
                // Active Target
                stepDotImages[i].color = activeColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = $"{i + 1}";
                    stepDotTexts[i].color = Color.white;
                }
            }
            else
            {
                // Upcoming
                stepDotImages[i].color = upcomingColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = $"{i + 1}";
                    stepDotTexts[i].color = new Color(0.7f, 0.8f, 0.9f, 0.6f);
                }
            }
        }
    }

    private void HandleStrokeValidated(string message, bool isSuccess)
    {
        if (isSuccess)
        {
            PlaySound(strokeSuccessClip);
            ShowFeedback($"<color=#34D399><b>{message}</b></color>", 2.0f);
        }
        else
        {
            PlaySound(mistakeWarningClip);
            ShowFeedback($"<color=#EF4444><b>{message} (Paint will auto-fade)</b></color>", 2.5f);
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
