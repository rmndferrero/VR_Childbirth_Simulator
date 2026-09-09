using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CleaningProgressUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject rootCanvas;
    public TMP_Text titleText;
    public TMP_Text percentageText;

    [Header("Moving Progress Bar")]
    public Image continuousFillImage;

    [Header("Bottom Target Indicator")]
    public TMP_Text currentTargetText;

    [Header("Settings")]
    public float fillRate = 0.5f;

    private float targetProgress = 0f;
    private float displayedFill = 0f;
    private bool isComplete = false;
    private bool isRinsePhase = false;
    private string currentTitle = "Water Wash Progress";

    private void Awake()
    {
        targetProgress = 0f;
        displayedFill = 0f;
        if (continuousFillImage != null)
        {
            continuousFillImage.fillAmount = 0f;
        }
    }

    private void OnEnable()
    {
        displayedFill = targetProgress;
        if (continuousFillImage != null)
        {
            continuousFillImage.fillAmount = displayedFill;
        }
    }

    private void Update()
    {
        // Smoothly animate the fill bar movement towards target progress
        displayedFill = Mathf.MoveTowards(displayedFill, targetProgress, Time.deltaTime * 3.0f);

        if (continuousFillImage != null)
        {
            continuousFillImage.fillAmount = displayedFill;
        }

        if (percentageText != null)
        {
            percentageText.text = $"{Mathf.RoundToInt(displayedFill * 100f)}%";
        }
    }

    public void Show()
    {
        if (rootCanvas != null) rootCanvas.SetActive(true);
    }

    public void Hide()
    {
        if (rootCanvas != null) rootCanvas.SetActive(false);
    }

    /// <summary>
    /// Configures the UI for Step 1: Preliminary Water Wash.
    /// </summary>
    public void ResetProgress(string title = "Step 1: Preliminary Water Wash")
    {
        currentTitle = title;
        isRinsePhase = false;
        targetProgress = 0f;
        displayedFill = 0f;
        isComplete = false;

        if (continuousFillImage != null) continuousFillImage.fillAmount = 0f;
        if (titleText != null) titleText.text = currentTitle;
        if (currentTargetText != null)
            currentTargetText.text = "Pour water from the pitcher over the perineal area to begin wash.";
        if (percentageText != null) percentageText.text = "0%";
    }

    /// <summary>
    /// Configures the UI for Step 3: Antiseptic Water Rinse.
    /// </summary>
    public void ResetForRinse(string title = "Step 3: Antiseptic Rinse")
    {
        currentTitle = title;
        isRinsePhase = true;
        targetProgress = 0f;
        displayedFill = 0f;
        isComplete = false;

        if (continuousFillImage != null) continuousFillImage.fillAmount = 0f;
        if (titleText != null) titleText.text = currentTitle;
        if (currentTargetText != null)
            currentTargetText.text = "Pour water from the pitcher to rinse off all <color=#4ADE80><b>light green antiseptic</b></color>.";
        if (percentageText != null) percentageText.text = "0%";
    }

    /// <summary>
    /// Reports water pouring during Step 1 (Preliminary Water Wash).
    /// </summary>
    public void ReportWashStep1(float deltaTime)
    {
        if (isComplete || isRinsePhase) return;

        targetProgress = Mathf.Clamp01(targetProgress + fillRate * deltaTime);

        if (currentTargetText != null)
            currentTargetText.text = "Washing perineal area... <color=#38BDF8><b>" + Mathf.RoundToInt(targetProgress * 100f) + "%</b></color>";

        if (targetProgress >= 0.98f && !isComplete)
        {
            isComplete = true;
            targetProgress = 1f;
            if (currentTargetText != null)
                currentTargetText.text = "<color=#34D399><b>Preliminary Water Wash Complete (100%)</b></color>";

            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnWaterWashCompleted();
            }
        }
    }

    /// <summary>
    /// Updates progress during Step 3 (Rinse) based directly on light-green puddles washed away.
    /// </summary>
    public void UpdateRinseProgress(float progress01)
    {
        if (isComplete || !isRinsePhase) return;

        targetProgress = Mathf.Clamp01(progress01);

        if (currentTargetText != null)
            currentTargetText.text = "Rinsing antiseptic... <color=#4ADE80><b>" + Mathf.RoundToInt(targetProgress * 100f) + "%</b></color>";

        if (targetProgress >= 0.98f && !isComplete)
        {
            isComplete = true;
            targetProgress = 1f;
            if (currentTargetText != null)
                currentTargetText.text = "<color=#34D399><b>All Antiseptic Washed Away (100%)</b></color>";

            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnWaterWashCompleted();
            }
        }
    }
}
