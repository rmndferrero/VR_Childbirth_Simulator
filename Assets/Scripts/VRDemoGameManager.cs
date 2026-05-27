using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VRDemoGameManager : MonoBehaviour
{
    public static VRDemoGameManager Instance;

    private int currentStepIndex = 0; // Tracks which tool we are currently on

    [Header("Current Sequence")]
    public SimulationStep currentStep;

    [Header("UI")]
    public TMP_Text feedbackText;

    [Header("Scoring Tracker")]
    public int currentScenarioScore = 100;

    [Header("Scenario Progression")]
    public int currentScenarioPhase = 1; // 1 = Mayo Prep, 2 = Patient Assessment
    public Dictionary<int, int> scenarioScores = new Dictionary<int, int>();

    [Header("Scenario 1: Mayo Preparation")]
    [Tooltip("Drag your Step 1 through 4 ScriptableObjects here in order.")]
    public List<SimulationStep> mayoPreparationSteps = new List<SimulationStep>();

    [Header("Scenario 2: References")]
    public GameObject patientAssessmentFloorHighlight;


    private HashSet<string> penalizedMistakes = new HashSet<string>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Initialize the first step when the game starts
        if (mayoPreparationSteps != null && mayoPreparationSteps.Count > 0)
        {
            currentStep = mayoPreparationSteps[0];
            Debug.Log($"[GameManager] Scenario started. Current Step: {currentStep.stepName}");
            // TODO: Update your static HUD UI here with currentStep.instructionPrompt
        }
        else
        {
            Debug.LogWarning("[GameManager] Mayo Preparation Steps list is empty! Please assign them in the inspector.");
        }
    }

    // Called ONLY by SocketValidator when dropped into the CORRECT table slot
    public void ReportCorrectAction(SimulationStep step)
    {
        feedbackText.text = "Correct Placement! " + step.stepName + " completed.";
    }

    // Called ONLY by SocketValidator when dropped into the WRONG table slot
    public void ShowWarning(string warning)
    {
        feedbackText.text = warning;
        // Optionally add logic to flash the text red or play an error sound
    }

    /// Processes a mistake, deducting points only if it hasn't been made before.
    public void RecordMistake(string wrongToolID)
    {
        if (currentStep == null) return;

        // NOTE: Make sure your SimulationStep.cs uses 'expectedID'. If it uses 'expectedToolID', update this line!
        string mistakeSignature = $"{currentStep.expectedID}_vs_{wrongToolID}";

        // If this exact mistake hasn't been recorded yet, apply the penalty
        if (!penalizedMistakes.Contains(mistakeSignature))
        {
            penalizedMistakes.Add(mistakeSignature);
            currentScenarioScore -= currentStep.penaltyPoints;

            // Clamp score so it doesn't go below 0
            currentScenarioScore = Mathf.Max(0, currentScenarioScore);

            Debug.Log($"[Scoring] Penalty Applied! Expected: {currentStep.expectedID}, Placed: {wrongToolID}. " +
                      $"Deducted {currentStep.penaltyPoints} points. Current Mayo Table Score: {currentScenarioScore}");
        }
        else
        {
            Debug.Log($"[Scoring] Repeated mistake ignored: {mistakeSignature}. Score remains: {currentScenarioScore}");
        }
    }

    public void AdvanceStep()
    {
        // FIX: Increment the micro-step, NOT the macro-phase!
        currentStepIndex++;

        // Check if there are more steps left in the Mayo phase
        if (currentStepIndex < mayoPreparationSteps.Count)
        {
            // Load the next step
            currentStep = mayoPreparationSteps[currentStepIndex];
            Debug.Log($"[GameManager] Step advanced! New Step: {currentStep.stepName}");

            // TODO: Update your static HUD UI here with the new currentStep.instructionPrompt
        }
        else
        {
            // No steps left! Trigger the transition to Scenario 2.
            CompleteMayoPreparation();
        }

        if (currentStepIndex >= mayoPreparationSteps.Count)
        {
            Debug.LogWarning("[GameManager] AdvanceStep called after completion. Ignored.");
            return;
        }
    }

    public void CompleteMayoPreparation()
    {
        scenarioScores[1] = currentScenarioScore;
        Debug.Log($"[GameManager] Mayo Prep Complete! Final Score: {currentScenarioScore}");

        currentScenarioPhase = 2; // NOW we advance the macro-phase
        ResetScenarioScore();

        // Activate the trigger zone for Scenario 2
        if (patientAssessmentFloorHighlight != null)
        {
            patientAssessmentFloorHighlight.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[GameManager] Patient Assessment Floor Highlight is not assigned in the Inspector!");
        }
    }

    public void CompletePatientAssessment()
    {
        scenarioScores[2] = currentScenarioScore;

        Debug.Log($"[GameManager] Patient Assessment Complete! Final Score: {currentScenarioScore}");

        currentScenarioPhase = 3;

        // Optional:
        // Trigger next scenario here
        // Show completion screen
        // Save overall results
    }
    /// Call this when transitioning to a brand new scenario
    public void ResetScenarioScore()
    {
        currentScenarioScore = 100;
        penalizedMistakes.Clear();
        Debug.Log("[Scoring] New Scenario started. Score reset to 100.");
    }
}