using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueUIController : MonoBehaviour
{
    public static DialogueUIController Instance;

    [Header("UI References")]
    public GameObject dialogueCanvas;
    [Tooltip("Assign the 3 buttons used for dialogue choices.")]
    public Button[] choiceButtons;
    private TextMeshProUGUI[] buttonTexts;

    private DialogueScenario currentScenario;
    private int currentNodeIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Cache the TextMeshPro components of the buttons
        buttonTexts = new TextMeshProUGUI[choiceButtons.Length];
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            buttonTexts[i] = choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
        }

        dialogueCanvas.SetActive(false);
    }

    public void StartDialogue(DialogueScenario newScenario)
    {
        currentScenario = newScenario;
        currentNodeIndex = 0;
        dialogueCanvas.SetActive(true);
        DisplayCurrentNode();
    }

    private void DisplayCurrentNode()
    {
        // Check if we finished all dialogue selections
        if (currentNodeIndex >= currentScenario.dialogueSequence.Count)
        {
            EndDialogue();
            return;
        }

        DialogueNode node = currentScenario.dialogueSequence[currentNodeIndex];

        // Populate the buttons
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            choiceButtons[i].onClick.RemoveAllListeners(); // Prevent duplicate firing

            if (i < node.choices.Count)
            {
                choiceButtons[i].gameObject.SetActive(true);
                DialogueChoice choiceData = node.choices[i];
                buttonTexts[i].text = choiceData.choiceText;

                // Bind the penalty to the button click
                choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(choiceData.penaltyPoints));
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false); // Hide button if less than 3 choices are used
            }
        }
    }

    private void OnChoiceSelected(int penaltyPoints)
    {
        if (penaltyPoints > 0)
        {
            VRDemoGameManager.Instance.currentScenarioScore -= penaltyPoints;
            // Prevent score from dropping below 0
            VRDemoGameManager.Instance.currentScenarioScore = Mathf.Max(0, VRDemoGameManager.Instance.currentScenarioScore);

            Debug.Log($"[Scoring] Dialogue Penalty! Deducted {penaltyPoints} points. Current Scenario Score: {VRDemoGameManager.Instance.currentScenarioScore}");
        }
        else
        {
            Debug.Log($"[Scoring] Perfect Choice! No deduction. Current Scenario Score: {VRDemoGameManager.Instance.currentScenarioScore}");
        }

        // Move to the 2nd selection
        currentNodeIndex++;
        DisplayCurrentNode();
    }

    private void EndDialogue()
    {
        dialogueCanvas.SetActive(false);
        VRDemoGameManager.Instance.CompletePatientAssessment();
    }
}