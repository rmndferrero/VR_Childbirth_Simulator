using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System;

[System.Serializable]
public class SimulationRecord
{
    public string playerName;
    public string dateCompleted;
    public int finalScore;
    public List<string> mistakesMade = new List<string>();
}

public class VRDemoGameManager : MonoBehaviour
{
    public static VRDemoGameManager Instance;

    [Header("Player Data")]
    public string currentPlayerName = "Guest";
    private SimulationRecord currentRecord;

    private int currentStepIndex = 0;

    [Header("Current Sequence")]
    public SimulationStep currentStep;

    [Header("UI")]
    public TMP_Text feedbackText;

    [Header("Scoring Tracker")]
    public int currentScenarioScore = 100;

    [Header("Scenario Progression")]
    public int currentScenarioPhase = 1;
    public Dictionary<int, int> scenarioScores = new Dictionary<int, int>();

    [Header("Scenario 1: Mayo Preparation")]
    // Updated to use your Master Container
    public ScenarioChecklist mayoPreparationScenario;

    [Header("Scenario 2: Patient Assessment / Dialogue")]
    // Added reference to your Dialogue Scenario
    public DialogueScenario patientAssessmentScenario;

    [Header("Scenario 2: References")]
    public GameObject patientAssessmentFloorHighlight;

    [Header("Decision Matrix")]
    [Tooltip("Global, phase-aware hazard/penalty matrix. Replaces per-step penaltyMatrix.")]
    public GlobalHazardMatrix globalHazardMatrix;

    private HashSet<string> penalizedMistakes = new HashSet<string>();
    private HashSet<string> heldToolPenalties = new HashSet<string>();
    private List<string> mistakeLog = new List<string>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        InitializeNewRecord();

        // Load the first step from the Master Container
        if (mayoPreparationScenario != null && mayoPreparationScenario.steps.Count > 0)
        {
            currentStep = mayoPreparationScenario.steps[0];
            Debug.Log($"[GameManager] Scenario started. Current Step: {currentStep.stepName}");
        }
    }

    public void SetPlayerName(string name)
    {
        currentPlayerName = name;
        if (currentRecord != null) currentRecord.playerName = name;
    }

    private void InitializeNewRecord()
    {
        currentRecord = new SimulationRecord
        {
            playerName = currentPlayerName,
            dateCompleted = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            mistakesMade = mistakeLog
        };
    }

    public void ReportCorrectAction(SimulationStep step)
    {
        if (feedbackText != null)
            feedbackText.text = "Correct Placement! " + step.stepName + " completed.";

        // Add this line to force the console to record the success!
        Debug.Log($"[GameManager] Correct Placement! {step.stepName} completed.");
    }

    public void ShowWarning(string wrongToolID)
    {
        if (currentStep == null || feedbackText == null) return;

        string warningText = currentStep.outOfSequenceWarning;

        // Global Decision Matrix: phase-aware lookup first
        if (globalHazardMatrix != null &&
            globalHazardMatrix.EvaluateHazard(wrongToolID, currentScenarioPhase, out int _, out string specificWarning))
        {
            warningText = specificWarning;
        }

        feedbackText.text = warningText;
    }

    public void RecordMistake(string wrongToolID)
    {
        if (currentStep == null) return;

        string mistakeSignature = $"{currentStep.expectedID}_vs_{wrongToolID}";

        if (!penalizedMistakes.Contains(mistakeSignature))
        {
            penalizedMistakes.Add(mistakeSignature);

            mistakeLog.Add($"Failed to place {currentStep.expectedID}, used {wrongToolID} instead.");

            // Global Decision Matrix: phase-aware penalty lookup
            int calculatedPenalty = currentStep.basePenaltyPoints;

            if (globalHazardMatrix != null &&
                globalHazardMatrix.EvaluateHazard(wrongToolID, currentScenarioPhase, out int matrixPenalty, out string _))
            {
                calculatedPenalty = matrixPenalty;
            }

            currentScenarioScore -= calculatedPenalty;
            currentScenarioScore = Mathf.Max(0, currentScenarioScore);

            Debug.Log($"[Scoring Matrix] Deducted {calculatedPenalty} points. Score: {currentScenarioScore}");
        }
    }

    /// <summary>
    /// Called when a tool is grabbed (held), regardless of scenario/socket state.
    /// Checks the Global Hazard Matrix for the current phase - e.g. holding a scalpel
    /// or curette during the Dialogue phase. Independent of currentStep, so this works
    /// even in phases (like Phase 2) where currentStep is intentionally null.
    /// </summary>
    public void CheckHeldToolHazard(string heldToolID)
    {
        if (globalHazardMatrix == null) return;

        string signature = $"phase{currentScenarioPhase}_{heldToolID}";

        if (heldToolPenalties.Contains(signature)) return;

        if (globalHazardMatrix.EvaluateHazard(heldToolID, currentScenarioPhase, out int penaltyPoints, out string specificWarning))
        {
            heldToolPenalties.Add(signature);

            mistakeLog.Add($"Held hazardous tool '{heldToolID}' during Phase {currentScenarioPhase}.");

            currentScenarioScore -= penaltyPoints;
            currentScenarioScore = Mathf.Max(0, currentScenarioScore);

            if (feedbackText != null)
                feedbackText.text = specificWarning;

            Debug.Log($"[Scoring Matrix] Held-tool hazard! Deducted {penaltyPoints} points. Score: {currentScenarioScore}");
        }
    }

    public void AdvanceStep()
    {
        currentStepIndex++;

        if (mayoPreparationScenario != null && currentStepIndex < mayoPreparationScenario.steps.Count)
        {
            currentStep = mayoPreparationScenario.steps[currentStepIndex];
        }
        else
        {
            CompleteMayoPreparation();
        }
    }

    public void CompleteMayoPreparation()
    {
        scenarioScores[1] = currentScenarioScore;
        currentScenarioPhase = 2;
        currentStepIndex = 0;
        currentStep = null; // clear stale Scenario 1 step so it can't leak into ShowWarning/RecordMistake
        ResetScenarioScore();

        Debug.Log("[GameManager] Mayo Table Preparation Complete! Transitioning to Phase 2.");

        if (patientAssessmentFloorHighlight != null)
            patientAssessmentFloorHighlight.SetActive(true);

        // Wake up the UI Controller and feed it Phase 2
        if (DialogueUIController.Instance != null && patientAssessmentScenario != null)
        {
            DialogueUIController.Instance.StartDialogue(patientAssessmentScenario);
        }
        else
        {
            Debug.LogWarning("[GameManager] Missing DialogueUIController or Patient Assessment Scenario!");
        }
    }

    public void CompletePatientAssessment()
    {
        scenarioScores[2] = currentScenarioScore;
        currentScenarioPhase = 3;

        Debug.Log("[GameManager] Patient Assessment Complete! Saving Record.");
        SaveRecordLocally();
    }

    public void ResetScenarioScore()
    {
        currentScenarioScore = 100;
        penalizedMistakes.Clear();
        heldToolPenalties.Clear();
    }

    private void SaveRecordLocally()
    {
        // Add robust check in case a scenario was skipped
        int score1 = scenarioScores.ContainsKey(1) ? scenarioScores[1] : 0;
        int score2 = scenarioScores.ContainsKey(2) ? scenarioScores[2] : 0;

        currentRecord.finalScore = score1 + score2;

        string jsonData = JsonUtility.ToJson(currentRecord, true);
        string filePath = Path.Combine(Application.persistentDataPath, $"SimulationRecord_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        File.WriteAllText(filePath, jsonData);
        Debug.Log($"[Storage] VR Session Data successfully saved to: {filePath}");
    }
}