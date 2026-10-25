using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// WorldSpace floating UI mounted directly above the Mayo Table (Table 2).
/// Features:
/// - "MAYO STAND INSTRUMENT SETUP" header with UbiSim VR medical aesthetic.
/// - Live count of instruments arranged onto the tray.
/// - "Confirm Tool Setup" button.
/// - Clear feedback banner:
///   - Incomplete: "Please place all instruments on the table before confirming."
///   - One or more wrong: "Setup Incorrect: One or more instruments are misplaced! Resetting misplaced tools..."
///   - Perfect: "Perfect! All instruments arranged in correct surgical order."
/// </summary>
public class MayoSetupConfirmationUI : MonoBehaviour
{
    public static MayoSetupConfirmationUI Instance { get; private set; }

    [Header("UI Text Components")]
    public TMP_Text titleText;
    public TMP_Text progressText;
    public TMP_Text feedbackBannerText;

    [Header("Interactive Button")]
    public Button confirmButton;
    public TMP_Text confirmButtonText;

    [Header("Styling Components")]
    public Image panelBg;
    public Outline panelOutline;

    [Header("Colors")]
    public Color normalBorderColor = new Color(0.20f, 0.35f, 0.50f, 0.85f);  // Slate
    public Color perfectColor = new Color(0.20f, 0.83f, 0.60f, 1f);          // Emerald Green
    public Color wrongColor = new Color(0.94f, 0.27f, 0.27f, 1f);            // Red
    public Color warningColor = new Color(0.96f, 0.62f, 0.04f, 1f);          // Amber

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }
    }

    private void Start()
    {
        // Only show once Mayo Stand is draped and Step 7 begins
        bool shouldShow = TablePrepManager.Instance != null 
            && TablePrepManager.Instance.isMayoDraped 
            && TablePrepManager.Instance.isTablePrepActive;

        if (!shouldShow)
        {
            Hide();
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        var panel = transform.Find("Panel");
        if (panel != null) panel.gameObject.SetActive(true);
    }

    public void Hide()
    {
        var panel = transform.Find("Panel");
        if (panel != null) panel.gameObject.SetActive(false);
        else gameObject.SetActive(false);
    }

    public void OnConfirmClicked()
    {
        if (TablePrepManager.Instance != null)
        {
            TablePrepManager.Instance.ValidateAndConfirmSetup();
        }
    }

    public void UpdateProgress(int placedCount, int totalCount)
    {
        if (progressText != null)
        {
            progressText.text = $"Instruments Arranged: <color=#38BDF8><b>{placedCount} / {totalCount}</b></color>";
        }
    }

    public void ShowIncompleteFeedback(int placedCount, int totalCount)
    {
        if (feedbackBannerText != null)
        {
            feedbackBannerText.text = $"<color=#F59E0B>Please arrange all instruments on the table before confirming ({placedCount}/{totalCount} placed).</color>";
        }
        if (panelOutline != null)
        {
            panelOutline.effectColor = warningColor;
        }
    }

    public void ShowWrongFeedback(int wrongCount)
    {
        if (feedbackBannerText != null)
        {
            string msg = wrongCount == 1
                ? "<b>One instrument is wrong!</b> Resetting misplaced tool to Table 1..."
                : $"<b>{wrongCount} instruments are wrong!</b> Resetting misplaced tools to Table 1...";
            feedbackBannerText.text = $"<color=#EF4444>[X] {msg}</color>";
        }
        if (panelOutline != null)
        {
            panelOutline.effectColor = wrongColor;
        }
    }

    public void ShowPerfectFeedback()
    {
        if (feedbackBannerText != null)
        {
            feedbackBannerText.text = "<color=#34D399><b>[OK] Perfect! All instruments arranged in correct surgical order.</b></color>";
        }
        if (panelOutline != null)
        {
            panelOutline.effectColor = perfectColor;
        }
        if (confirmButton != null)
        {
            confirmButton.interactable = false;
        }
        if (confirmButtonText != null)
        {
            confirmButtonText.text = "✓ Order Verified";
        }
    }

    public void ResetFeedback()
    {
        if (feedbackBannerText != null)
        {
            feedbackBannerText.text = "<color=#94A3B8>Arrange instruments into slots, then tap Confirm below.</color>";
        }
        if (panelOutline != null)
        {
            panelOutline.effectColor = normalBorderColor;
        }
        if (confirmButton != null)
        {
            confirmButton.interactable = true;
        }
        if (confirmButtonText != null)
        {
            confirmButtonText.text = "Confirm Tool Setup";
        }
    }
}
