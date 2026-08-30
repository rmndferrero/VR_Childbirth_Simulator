using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum WashZone
{
    Center,
    LeftGroin,
    RightGroin
}

public class CleaningProgressUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject rootCanvas;
    public TMP_Text titleText;
    public TMP_Text percentageText;

    [Header("Moving Progress Bar")]
    public Image continuousFillImage; // Moving green fill bar (0% to 100% per zone)

    [Header("Bottom Target Indicator")]
    public TMP_Text currentTargetText; // Text at the bottom showing active part

    [Header("Settings")]
    public float fillRate = 0.6f; // Speed of fill per zone

    private WashZone currentActiveZone = WashZone.Center;
    private float currentZoneProgress = 0f;
    private float displayedFill = 0f;
    private bool isComplete = false;
    private string baseTitle = "Water Wash Progress";

    private void Awake()
    {
        currentZoneProgress = 0f;
        displayedFill = 0f;
        if (continuousFillImage != null)
        {
            continuousFillImage.fillAmount = 0f;
        }
    }

    private void OnEnable()
    {
        displayedFill = currentZoneProgress;
        if (continuousFillImage != null)
        {
            continuousFillImage.fillAmount = displayedFill;
        }
    }

    private void Update()
    {
        // Smoothly animate the fill bar movement towards current zone progress
        displayedFill = Mathf.MoveTowards(displayedFill, currentZoneProgress, Time.deltaTime * 2.0f);

        if (continuousFillImage != null)
        {
            continuousFillImage.fillAmount = displayedFill;
        }

        if (percentageText != null)
        {
            percentageText.text = $"{Mathf.RoundToInt(displayedFill * 100f)}%";
        }

        UpdateVisuals();
    }

    public void Show()
    {
        if (rootCanvas != null) rootCanvas.SetActive(true);
    }

    public void Hide()
    {
        if (rootCanvas != null) rootCanvas.SetActive(false);
    }

    public void ResetProgress(string title = "Water Wash Progress")
    {
        baseTitle = title;
        currentActiveZone = WashZone.Center;
        currentZoneProgress = 0f;
        displayedFill = 0f;
        isComplete = false;

        if (continuousFillImage != null) continuousFillImage.fillAmount = 0f;
        UpdateVisuals();
    }

    public void ReportWaterPour(WashZone zone, float deltaTime)
    {
        if (isComplete) return;

        // Only increase progress if pouring on the current active zone
        if (zone == currentActiveZone)
        {
            currentZoneProgress = Mathf.Clamp01(currentZoneProgress + fillRate * deltaTime);

            // Check if current active zone reached 100%
            if (currentZoneProgress >= 0.98f)
            {
                AdvanceToNextZone();
            }
        }
        else
        {
            // Poured on wrong zone / out of order
            if (Time.frameCount % 90 == 0 && PerinealCareManager.Instance != null)
            {
                string expectedName = GetZoneName(currentActiveZone);
                string wrongName = GetZoneName(zone);
                PerinealCareManager.Instance.RecordClinicalViolation($"Wash sequence violation: Wash {expectedName} before {wrongName}.", 2);
            }
        }
    }

    private void AdvanceToNextZone()
    {
        currentZoneProgress = 1f;

        if (currentActiveZone == WashZone.Center)
        {
            Debug.Log("[CleaningProgressUI] Center wash complete! Resetting bar for Left Groin.");
            currentActiveZone = WashZone.LeftGroin;
            currentZoneProgress = 0f;
            displayedFill = 0f;
        }
        else if (currentActiveZone == WashZone.LeftGroin)
        {
            Debug.Log("[CleaningProgressUI] Left Groin wash complete! Resetting bar for Right Groin.");
            currentActiveZone = WashZone.RightGroin;
            currentZoneProgress = 0f;
            displayedFill = 0f;
        }
        else if (currentActiveZone == WashZone.RightGroin)
        {
            Debug.Log("[CleaningProgressUI] Right Groin wash complete! All 3 zones finished.");
            isComplete = true;
            currentZoneProgress = 1f;
            displayedFill = 1f;

            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnWaterWashCompleted();
            }
        }
    }

    private void UpdateVisuals()
    {
        if (isComplete)
        {
            if (titleText != null) titleText.text = $"{baseTitle} (Complete)";
            if (currentTargetText != null) currentTargetText.text = "<color=#34D399><b>✓ All Zones Washed (100%)</b></color>";
            return;
        }

        switch (currentActiveZone)
        {
            case WashZone.Center:
                if (titleText != null) titleText.text = $"{baseTitle} (1 of 3: Center)";
                if (currentTargetText != null)
                    currentTargetText.text = $"Current Target: <color=#38BDF8><b>1. Center</b> (Labia to Perineum)</color> [{Mathf.RoundToInt(displayedFill * 100f)}%]";
                break;

            case WashZone.LeftGroin:
                if (titleText != null) titleText.text = $"{baseTitle} (2 of 3: Left Groin)";
                if (currentTargetText != null)
                    currentTargetText.text = $"Current Target: <color=#38BDF8><b>2. Left Groin</b> (Inguinal Fold)</color> [{Mathf.RoundToInt(displayedFill * 100f)}%]";
                break;

            case WashZone.RightGroin:
                if (titleText != null) titleText.text = $"{baseTitle} (3 of 3: Right Groin)";
                if (currentTargetText != null)
                    currentTargetText.text = $"Current Target: <color=#38BDF8><b>3. Right Groin</b> (Inguinal Fold)</color> [{Mathf.RoundToInt(displayedFill * 100f)}%]";
                break;
        }
    }

    private string GetZoneName(WashZone zone)
    {
        switch (zone)
        {
            case WashZone.Center: return "Center";
            case WashZone.LeftGroin: return "Left Groin";
            case WashZone.RightGroin: return "Right Groin";
            default: return "Current Zone";
        }
    }
}
