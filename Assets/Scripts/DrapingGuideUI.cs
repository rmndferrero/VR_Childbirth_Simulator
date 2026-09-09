using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Modern floating HUD banner tracking the 4-step sterile draping sequence.
/// Matches the visual styling and architecture of StrokeGuideUI with 4 step nodes:
/// 1. Under Buttocks -> 2. Abdomen -> 3. Left Thigh -> 4. Right Thigh.
/// Shows progress bar (0% to 100%), active target instructions, and checkmarks (✓).
/// </summary>
public class DrapingGuideUI : MonoBehaviour
{
    public static DrapingGuideUI Instance { get; private set; }

    [Header("Root Canvas")]
    public GameObject rootCanvas;

    [Header("Header & Instruction Text")]
    public TMP_Text phaseTitleText;
    public TMP_Text currentDrapeTitleText;
    public TMP_Text directionInstructionText;
    public TMP_Text liveFeedbackBanner;
    public TMP_Text progressPercentageText;

    [Header("Progress Bar")]
    public Image progressBarFill;

    [Header("4-Step Dots / Cards")]
    public Transform dotsContainer;
    public Image[] stepDotImages = new Image[4];
    public TMP_Text[] stepDotTexts = new TMP_Text[4];
    public TMP_Text[] stepDotSubTexts = new TMP_Text[4];

    [Header("Colors")]
    public Color completedColor = new Color(0.10f, 0.78f, 0.45f, 1f);   // Vibrant Emerald Green
    public Color activeColor = new Color(0.15f, 0.75f, 1.00f, 1f);      // Glowing Cyan Blue
    public Color upcomingColor = new Color(0.18f, 0.24f, 0.32f, 0.85f);  // Dark Slate

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip drapeSuccessClip;
    public AudioClip mistakeWarningClip;

    private static readonly string[] DrapeLabels = new string[4]
    {
        "1. Under Buttocks",
        "2. Abdomen",
        "3. Left Thigh",
        "4. Right Thigh"
    };

    private static readonly string[] DrapeInstructions = new string[4]
    {
        "Technique: <color=#38BDF8><b>Unfold with BOTH hands</b></color> -> Place underneath patient's buttocks.",
        "Technique: <color=#38BDF8><b>Unfold with BOTH hands</b></color> -> Place across lower abdomen / suprapubic.",
        "Technique: <color=#38BDF8><b>Unfold with BOTH hands</b></color> -> Cover the patient's left thigh & leg.",
        "Technique: <color=#38BDF8><b>Unfold with BOTH hands</b></color> -> Cover the patient's right thigh & leg."
    };

    private MotherDrapingManager drapingManager;
    private Coroutine feedbackCoroutine;
    private bool isInitialized = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        drapingManager = FindFirstObjectByType<MotherDrapingManager>();
        if (drapingManager != null)
        {
            drapingManager.OnDrapePlaced += HandleDrapePlaced;
            drapingManager.OnAllDrapesCompleted += HandleAllDrapesCompleted;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        EnsureUIHierarchy();
    }

    private void Start()
    {
        EnsureUIHierarchy();
    }

    private void OnDestroy()
    {
        if (drapingManager != null)
        {
            drapingManager.OnDrapePlaced -= HandleDrapePlaced;
            drapingManager.OnAllDrapesCompleted -= HandleAllDrapesCompleted;
        }
    }

    public void Show()
    {
        EnsureUIHierarchy();
        if (rootCanvas != null) rootCanvas.SetActive(true);
        RefreshUI();
    }

    public void Hide()
    {
        if (rootCanvas != null) rootCanvas.SetActive(false);
    }

    public void RefreshUI()
    {
        if (drapingManager == null) drapingManager = FindFirstObjectByType<MotherDrapingManager>();
        UpdateUIStatus();
    }

    private void HandleDrapePlaced(int slotIndex, DrapeSlot slot)
    {
        ShowFeedback($"<color=#34D399><b>✓ {slot.slotName} Draped Successfully!</b></color>", 2.0f);
        if (audioSource != null && drapeSuccessClip != null)
        {
            audioSource.PlayOneShot(drapeSuccessClip);
        }

        UpdateUIStatus();
    }

    private void HandleAllDrapesCompleted()
    {
        UpdateUIStatus();
    }

    public void ShowMistakeFeedback(string message)
    {
        ShowFeedback($"<color=#EF4444><b>{message}</b></color>", 2.5f);
        if (audioSource != null && mistakeWarningClip != null)
        {
            audioSource.PlayOneShot(mistakeWarningClip);
        }
    }

    private void UpdateUIStatus()
    {
        if (drapingManager == null) drapingManager = FindFirstObjectByType<MotherDrapingManager>();
        int count = (drapingManager != null) ? drapingManager.GetDrapedCount() : 0;
        float progress01 = Mathf.Clamp01((float)count / 4f);

        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = progress01;
        }

        if (progressPercentageText != null)
        {
            progressPercentageText.text = $"{Mathf.RoundToInt(progress01 * 100f)}% ({count}/4 Done)";
        }

        if (count < 4)
        {
            if (currentDrapeTitleText != null)
            {
                currentDrapeTitleText.text = $"Draping Progress: <color=#38BDF8><b>{count}/4 Drapes Placed</b></color>";
            }

            if (directionInstructionText != null)
            {
                directionInstructionText.text = "Technique: <color=#38BDF8><b>Unfold with BOTH hands</b></color> -> Place drape on any remaining blue zone.";
            }
        }
        else
        {
            if (currentDrapeTitleText != null)
            {
                currentDrapeTitleText.text = "<color=#34D399><b>✓ All 4 Drapes Placed Successfully!</b></color>";
            }
            if (directionInstructionText != null)
            {
                directionInstructionText.text = "Sterile field prepared. Notify mother and proceed to delivery.";
            }
            if (progressBarFill != null) progressBarFill.fillAmount = 1f;
            if (progressPercentageText != null) progressPercentageText.text = "100% (4/4 Complete)";
        }

        UpdateStepDots();
    }

    private void UpdateStepDots()
    {
        if (stepDotImages == null) return;
        if (drapingManager == null) drapingManager = FindFirstObjectByType<MotherDrapingManager>();

        for (int i = 0; i < stepDotImages.Length; i++)
        {
            if (stepDotImages[i] == null) continue;

            bool isDraped = (drapingManager != null && i < drapingManager.drapeSlots.Count && drapingManager.drapeSlots[i].isDraped);

            if (isDraped)
            {
                // Completed -> Vibrant Emerald Green with Checkmark
                stepDotImages[i].color = completedColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = "<b>✓</b>";
                    stepDotTexts[i].color = Color.white;
                }
            }
            else
            {
                // Available / Undraped -> Clean Dark Slate
                stepDotImages[i].color = upcomingColor;
                if (stepDotTexts != null && i < stepDotTexts.Length && stepDotTexts[i] != null)
                {
                    stepDotTexts[i].text = $"{i + 1}";
                    stepDotTexts[i].color = new Color(0.70f, 0.80f, 0.90f, 0.65f);
                }
            }

            if (stepDotSubTexts != null && i < stepDotSubTexts.Length && stepDotSubTexts[i] != null && i < DrapeLabels.Length)
            {
                stepDotSubTexts[i].text = DrapeLabels[i];
                stepDotSubTexts[i].color = isDraped ? Color.white : new Color(0.6f, 0.7f, 0.8f, 0.5f);
            }
        }
    }

    public void ShowFeedback(string text, float duration)
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

    public void EnsureUIHierarchy()
    {
        if (isInitialized) return;

        var strokeUI = FindFirstObjectByType<StrokeGuideUI>(FindObjectsInactive.Include);
        if (strokeUI != null && (rootCanvas == null || rootCanvas == gameObject))
        {
            // Clone StrokeGuideCanvas to create pixel-perfect DrapingGuideCanvas
            GameObject canvasTemplate = strokeUI.rootCanvas != null ? strokeUI.rootCanvas : strokeUI.gameObject;
            GameObject drapingCanvas = Instantiate(canvasTemplate, canvasTemplate.transform.parent);
            drapingCanvas.name = "DrapingGuideCanvas";
            drapingCanvas.transform.position = canvasTemplate.transform.position;
            drapingCanvas.transform.rotation = canvasTemplate.transform.rotation;
            drapingCanvas.transform.localScale = canvasTemplate.transform.localScale;

            // Remove cloned StrokeGuideUI component on the clone
            var oldComp = drapingCanvas.GetComponent<StrokeGuideUI>();
            if (oldComp != null) Destroy(oldComp);

            rootCanvas = drapingCanvas;

            // Wire up UI references from the clone
            var texts = drapingCanvas.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                string tName = t.gameObject.name.ToLower();
                if (tName.Contains("phase") || tName.Contains("header"))
                {
                    phaseTitleText = t;
                    phaseTitleText.text = "Step 5: Sterile Draping of the Mother";
                }
                else if (tName.Contains("stroke") || tName.Contains("title") || tName.Contains("target"))
                {
                    currentDrapeTitleText = t;
                }
                else if (tName.Contains("direction") || tName.Contains("instruction"))
                {
                    directionInstructionText = t;
                }
                else if (tName.Contains("feedback") || tName.Contains("banner"))
                {
                    liveFeedbackBanner = t;
                }
                else if (tName.Contains("percent") || tName.Contains("progress"))
                {
                    progressPercentageText = t;
                }
            }

            if (strokeUI.progressBarFill != null)
            {
                var fills = drapingCanvas.GetComponentsInChildren<Image>(true);
                foreach (var img in fills)
                {
                    if (img.gameObject.name.ToLower().Contains("fill") || img.type == Image.Type.Filled)
                    {
                        progressBarFill = img;
                        break;
                    }
                }
            }

            // Adapt 4 dots from the 9 dots container
            if (strokeUI.dotsContainer != null)
            {
                Transform clonedContainer = drapingCanvas.transform.Find(GetRelativePath(canvasTemplate.transform, strokeUI.dotsContainer));
                if (clonedContainer == null) clonedContainer = drapingCanvas.GetComponentInChildren<HorizontalLayoutGroup>(true)?.transform;

                if (clonedContainer != null)
                {
                    dotsContainer = clonedContainer;
                    int childCount = clonedContainer.childCount;
                    List<Image> dotImgs = new List<Image>();
                    List<TMP_Text> dotTxts = new List<TMP_Text>();
                    List<TMP_Text> dotSubTxts = new List<TMP_Text>();

                    for (int i = 0; i < childCount; i++)
                    {
                        Transform child = clonedContainer.GetChild(i);
                        if (i < 4)
                        {
                            child.gameObject.SetActive(true);
                            var img = child.GetComponent<Image>() ?? child.GetComponentInChildren<Image>(true);
                            var txts = child.GetComponentsInChildren<TMP_Text>(true);
                            dotImgs.Add(img);
                            dotTxts.Add(txts.Length > 0 ? txts[0] : null);
                            dotSubTxts.Add(txts.Length > 1 ? txts[1] : null);
                        }
                        else
                        {
                            // Hide excess dots (5 through 9)
                            child.gameObject.SetActive(false);
                        }
                    }

                    stepDotImages = dotImgs.ToArray();
                    stepDotTexts = dotTxts.ToArray();
                    stepDotSubTexts = dotSubTxts.ToArray();
                }
            }

            if (audioSource == null) audioSource = drapingCanvas.GetComponent<AudioSource>();
            if (strokeUI.audioSource != null && audioSource == null)
            {
                audioSource = drapingCanvas.AddComponent<AudioSource>();
            }

            drapeSuccessClip = strokeUI.strokeSuccessClip;
            mistakeWarningClip = strokeUI.mistakeWarningClip;

            drapingCanvas.SetActive(false);
            isInitialized = true;
        }
    }

    private string GetRelativePath(Transform root, Transform target)
    {
        if (target == root) return "";
        string path = target.name;
        Transform parent = target.parent;
        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
