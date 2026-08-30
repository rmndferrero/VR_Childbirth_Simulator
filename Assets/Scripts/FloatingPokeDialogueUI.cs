using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FloatingPokeDialogueUI : MonoBehaviour
{
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

    [Header("Audio & Feedback")]
    public AudioSource audioSource;
    public AudioClip pokeClickSound;

    [Header("Dialogue Scenarios")]
    public DialogueScenario initialExplanationScenario;
    public DialogueScenario postProcedureScenario;

    private DialogueScenario activeScenario;
    private int currentDialogueNode = 0;
    private bool isProcessingChoice = false;

    private void Awake()
    {
        if (promptButton != null)
        {
            promptButton.onClick.RemoveAllListeners();
            promptButton.onClick.AddListener(OnPromptButtonClicked);
        }
    }

    public void ShowInitialPrompt()
    {
        if (rootCanvasObject != null) rootCanvasObject.SetActive(true);
        if (promptContainer != null) promptContainer.SetActive(true);
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(false);

        if (promptButtonText != null)
        {
            promptButtonText.text = "Talk to the Mother";
        }
        activeScenario = initialExplanationScenario;
        currentDialogueNode = 0;
        isProcessingChoice = false;
    }

    public void ShowCompletionPrompt()
    {
        if (rootCanvasObject != null) rootCanvasObject.SetActive(true);
        if (promptContainer != null) promptContainer.SetActive(true);
        if (dialogueOptionsContainer != null) dialogueOptionsContainer.SetActive(false);
        if (responseContainer != null) responseContainer.SetActive(false);

        if (promptButtonText != null)
        {
            promptButtonText.text = "Inform Mother (Procedure Complete)";
        }
        activeScenario = postProcedureScenario;
        currentDialogueNode = 0;
        isProcessingChoice = false;
    }

    public void Hide()
    {
        if (rootCanvasObject != null) rootCanvasObject.SetActive(false);
    }

    public void OnPromptButtonClicked()
    {
        PlaySound();
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
                ? $"Patient Communication ({currentDialogueNode + 1} of {activeScenario.dialogueSequence.Count})"
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
                PerinealCareManager.Instance.OnProcedureFinishedAndMotherNotified();
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
