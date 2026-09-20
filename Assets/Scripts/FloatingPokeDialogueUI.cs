using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls floating poke UI dialogues across the 8-step simulation:
/// - Step 1: Patient communication & informed consent.
/// - Step 7: Setup table transition prompt.
/// - Step 8: Comprehensive final score calculation & mistake summary evaluation with restart button.
/// </summary>
public class FloatingPokeDialogueUI : MonoBehaviour
{
    public static FloatingPokeDialogueUI Instance { get; private set; }

    [Header("Root Canvas")]
    public GameObject rootCanvasObject;

    [Header("1. Start Prompt Container (Single Button)")]
    public GameObject promptContainer;
    public Button promptButton;
    public TMP_Text promptButtonText;

    [Header("2. Dialogue Choice Container")]
    public GameObject dialogueOptionsContainer;
    public TMP_Text stepHeaderTitle;
    public TMP_Text stepInstructionText;
    public Button[] choiceButtons;
    public TMP_Text[] choiceButtonTexts;

    [Header("3. Mother Response Container")]
    public GameObject responseContainer;
    public TMP_Text responseSpeakerText;
    public TMP_Text responseBodyText;

    [Header("4. Step 8: Summary & Mistakes Evaluation Container")]
    public GameObject summaryContainer;
    public TMP_Text summaryTitleText;
    public TMP_Text summaryScoreText;
    public TMP_Text summaryMistakesListText;
    public Button summaryRestartButton;

    [Header("Audio & Feedback")]
    public AudioSource audioSource;
    public AudioClip pokeClickSound;

    [Header("Dialogue Scenarios")]
    public DialogueScenario initialExplanationScenario;
    public DialogueScenario postProcedureScenario;

    private DialogueScenario activeScenario;
    private int currentDialogueNode = 0;
    private bool isProcessingChoice = false;
    private bool isSetupTablePrompt = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (rootCanvasObject == null) rootCanvasObject = gameObject;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (promptButton != null)
        {
            promptButton.onClick.RemoveAllListeners();
            promptButton.onClick.AddListener(OnPromptButtonClicked);
        }
    }

    public void ShowInitialPrompt()
    {
        isSetupTablePrompt = false;
        if (rootCanvasObject != null) rootCanvasObject.SetActive(true);
        if (promptContainer != null) promptContainer.SetActive(true);
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(false);
        if (summaryContainer != null) summaryContainer.SetActive(false);

        if (promptButtonText != null)
        {
            promptButtonText.text = "Talk to the Mother";
        }
        activeScenario = initialExplanationScenario;
        currentDialogueNode = 0;
        isProcessingChoice = false;
    }

    public void ShowSetupTablePrompt()
    {
        isSetupTablePrompt = true;
        if (rootCanvasObject != null) rootCanvasObject.SetActive(true);
        if (promptContainer != null) promptContainer.SetActive(true);
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(false);
        if (summaryContainer != null) summaryContainer.SetActive(false);

        if (promptButtonText != null)
        {
            promptButtonText.text = "Step 7: Setup Table (Mayo Prep) - Tap to Proceed";
        }
        activeScenario = null;
        currentDialogueNode = 0;
        isProcessingChoice = false;
    }

    public void ShowCompletionPrompt()
    {
        isSetupTablePrompt = false;
        if (rootCanvasObject != null) rootCanvasObject.SetActive(true);
        if (promptContainer != null) promptContainer.SetActive(true);
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(false);
        if (summaryContainer != null) summaryContainer.SetActive(false);

        if (promptButtonText != null)
        {
            promptButtonText.text = "Inform Mother (Procedure Complete)";
        }
        activeScenario = postProcedureScenario;
        currentDialogueNode = 0;
        isProcessingChoice = false;
    }

    public void ShowSummaryEvaluation(int finalScore, List<string> mistakes)
    {
        if (rootCanvasObject != null) rootCanvasObject.SetActive(true);
        if (promptContainer != null) promptContainer.SetActive(false);
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(false);

        EnsureSummaryContainerExists();

        if (summaryContainer != null)
        {
            summaryContainer.SetActive(true);
        }

        if (summaryTitleText != null)
        {
            summaryTitleText.text = "CLINICAL EVALUATION SUMMARY";
        }

        if (summaryScoreText != null)
        {
            string grade = (finalScore >= 85)
                ? "<color=#34D399>PASS (EXCELLENT)</color>"
                : (finalScore >= 70 ? "<color=#FBBF24>PASS</color>" : "<color=#EF4444>NEEDS PRACTICE</color>");

            summaryScoreText.text = $"Final Score: <color=#38BDF8><b>{finalScore} / 100</b></color>  •  {grade}";
        }

        if (summaryMistakesListText != null)
        {
            if (mistakes == null || mistakes.Count == 0)
            {
                summaryMistakesListText.text = "<color=#34D399><b>✓ Flawless Procedure!</b>\n• Zero sterile violations recorded.\n• All instruments properly returned to sterile tray.\n• Accurate 8-step clinical sequencing maintained.</color>";
            }
            else
            {
                string listStr = $"<color=#EF4444><b>Clinical Mistakes & Deductions ({mistakes.Count}):</b></color>\n";
                foreach (var m in mistakes)
                {
                    listStr += $"<color=#FCA5A5>• {m}</color>\n";
                }
                summaryMistakesListText.text = listStr.TrimEnd();
            }
        }

        if (summaryRestartButton != null)
        {
            summaryRestartButton.onClick.RemoveAllListeners();
            summaryRestartButton.onClick.AddListener(OnRestartButtonClicked);
        }
    }

    private void EnsureSummaryContainerExists()
    {
        if (summaryContainer != null) return;

        // Check if existing child exists
        Transform existing = transform.Find("SummaryContainer");
        if (existing != null)
        {
            summaryContainer = existing.gameObject;
            summaryTitleText = summaryContainer.transform.Find("Title")?.GetComponent<TMP_Text>();
            summaryScoreText = summaryContainer.transform.Find("Score")?.GetComponent<TMP_Text>();
            summaryMistakesListText = summaryContainer.transform.Find("MistakesList")?.GetComponent<TMP_Text>();
            summaryRestartButton = summaryContainer.transform.Find("RestartButton")?.GetComponent<Button>();
            return;
        }

        // Dynamically create summary card container matching DialogueCanvas styling
        GameObject sumObj = new GameObject("SummaryContainer");
        sumObj.transform.SetParent(rootCanvasObject != null ? rootCanvasObject.transform : transform, false);

        RectTransform rt = sumObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        Image bg = sumObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

        Outline outline = sumObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.50f, 0.70f, 0.60f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(sumObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.05f, 0.82f);
        titleRt.anchorMax = new Vector2(0.95f, 0.96f);
        titleRt.sizeDelta = Vector2.zero;
        summaryTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        summaryTitleText.alignment = TextAlignmentOptions.Center;
        summaryTitleText.fontSize = 26;
        summaryTitleText.fontStyle = FontStyles.Bold;
        summaryTitleText.color = new Color(0.22f, 0.75f, 0.97f, 1f);

        // Score
        GameObject scoreObj = new GameObject("Score");
        scoreObj.transform.SetParent(sumObj.transform, false);
        RectTransform scoreRt = scoreObj.AddComponent<RectTransform>();
        scoreRt.anchorMin = new Vector2(0.05f, 0.68f);
        scoreRt.anchorMax = new Vector2(0.95f, 0.80f);
        scoreRt.sizeDelta = Vector2.zero;
        summaryScoreText = scoreObj.AddComponent<TextMeshProUGUI>();
        summaryScoreText.alignment = TextAlignmentOptions.Center;
        summaryScoreText.fontSize = 22;
        summaryScoreText.fontStyle = FontStyles.Bold;
        summaryScoreText.color = Color.white;

        // Mistakes List
        GameObject listObj = new GameObject("MistakesList");
        listObj.transform.SetParent(sumObj.transform, false);
        RectTransform listRt = listObj.AddComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0.08f, 0.22f);
        listRt.anchorMax = new Vector2(0.92f, 0.66f);
        listRt.sizeDelta = Vector2.zero;
        summaryMistakesListText = listObj.AddComponent<TextMeshProUGUI>();
        summaryMistakesListText.alignment = TextAlignmentOptions.TopLeft;
        summaryMistakesListText.fontSize = 17;
        summaryMistakesListText.textWrappingMode = TextWrappingModes.Normal;
        summaryMistakesListText.color = new Color(0.90f, 0.93f, 0.96f, 1f);

        // Restart Button
        GameObject btnObj = new GameObject("RestartButton");
        btnObj.transform.SetParent(sumObj.transform, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.25f, 0.05f);
        btnRt.anchorMax = new Vector2(0.75f, 0.18f);
        btnRt.sizeDelta = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.45f, 0.75f, 0.95f);
        summaryRestartButton = btnObj.AddComponent<Button>();

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform btnTxtRt = btnTxtObj.AddComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI btnTxt = btnTxtObj.AddComponent<TextMeshProUGUI>();
        btnTxt.text = "Restart Simulation";
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.fontSize = 20;
        btnTxt.fontStyle = FontStyles.Bold;
        btnTxt.color = Color.white;

        summaryContainer = sumObj;
    }

    public void OnRestartButtonClicked()
    {
        PlaySound();
        Debug.Log("[FloatingPokeDialogueUI] Restarting Simulation...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Hide()
    {
        if (rootCanvasObject != null) rootCanvasObject.SetActive(false);
    }

    public void OnPromptButtonClicked()
    {
        PlaySound();
        if (isSetupTablePrompt)
        {
            if (promptContainer != null) promptContainer.SetActive(false);
            if (rootCanvasObject != null) rootCanvasObject.SetActive(false);
            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnSetupTableCompleted();
            }
            return;
        }

        if (promptContainer != null) promptContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(false);
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(true);

        currentDialogueNode = 0;
        DisplayCurrentDialogue();
    }

    private void DisplayCurrentDialogue()
    {
        isProcessingChoice = false;
        if (responseContainer != null) responseContainer.SetActive(false);
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(true);

        if (activeScenario == null || currentDialogueNode >= activeScenario.dialogueSequence.Count)
        {
            EndCurrentDialogue();
            return;
        }

        DialogueNode node = activeScenario.dialogueSequence[currentDialogueNode];

        if (stepHeaderTitle != null)
        {
            stepHeaderTitle.text = activeScenario == initialExplanationScenario
                ? $"Step 1: Patient Communication ({currentDialogueNode + 1} of {activeScenario.dialogueSequence.Count})"
                : "Procedure Completion";
        }

        if (stepInstructionText != null)
        {
            stepInstructionText.text = !string.IsNullOrEmpty(node.nodeName) ? node.nodeName : "Select your statement to the patient:";
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            choiceButtons[i].onClick.RemoveAllListeners();

            if (i < node.choices.Count)
            {
                choiceButtons[i].gameObject.SetActive(true);
                DialogueChoice choice = node.choices[i];

                if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                {
                    choiceButtonTexts[i].text = choice.choiceText;
                }

                choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(choice));
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnChoiceSelected(DialogueChoice choice)
    {
        if (isProcessingChoice) return;
        isProcessingChoice = true;

        PlaySound();

        if (choice.penaltyPoints > 0)
        {
            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.RecordClinicalViolation($"Improper communication with patient (-{choice.penaltyPoints} pts)", choice.penaltyPoints);
            }
        }

        StartCoroutine(ShowMotherResponseRoutine(choice.penaltyPoints));
    }

    private IEnumerator ShowMotherResponseRoutine(int penalty)
    {
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(true);

        if (responseSpeakerText != null) responseSpeakerText.text = "Mother (Patient)";

        if (responseBodyText != null)
        {
            if (activeScenario == initialExplanationScenario)
            {
                if (penalty == 0)
                    responseBodyText.text = "\"Thank you for explaining, nurse. I am ready for the cleaning procedure.\"";
                else if (penalty == 5)
                    responseBodyText.text = "\"Okay nurse, please be gentle during the procedure.\"";
                else
                    responseBodyText.text = "\"...Okay, nurse. (Patient appears nervous due to abrupt tone).\"";
            }
            else
            {
                if (penalty == 0)
                    responseBodyText.text = "\"Thank you very much nurse. I feel much more comfortable now.\"";
                else
                    responseBodyText.text = "\"Thank you, nurse.\"";
            }
        }

        yield return new WaitForSeconds(2.0f);

        currentDialogueNode++;
        if (activeScenario != null && currentDialogueNode < activeScenario.dialogueSequence.Count)
        {
            DisplayCurrentDialogue();
        }
        else
        {
            EndCurrentDialogue();
        }
    }

    private void EndCurrentDialogue()
    {
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(false);
        if (rootCanvasObject != null) rootCanvasObject.SetActive(false);

        if (activeScenario == initialExplanationScenario)
        {
            Debug.Log("[FloatingPokeDialogueUI] Consent successfully completed!");
            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnMotherInformedConsentGiven();
            }
        }
        else
        {
            Debug.Log("[FloatingPokeDialogueUI] Post-procedure notification completed!");
            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnSetupTableCompleted();
            }
        }
    }

    private void PlaySound()
    {
        if (audioSource != null && pokeClickSound != null)
        {
            audioSource.PlayOneShot(pokeClickSound);
        }
    }
}
