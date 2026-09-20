using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class SimulationRecord
{
    public string playerName;
    public string dateCompleted;
    public int finalScore;
    public List<string> mistakesMade = new List<string>();
}

public enum SimulationPhase
{
    STEP_1_TALK_TO_MOTHER = 1,
    STEP_2_PRELIMINARY_WATER_WASH = 2,
    STEP_3_IODINE_7_5_SCRUB = 3,
    STEP_4_ANTISEPTIC_RINSE = 4,
    STEP_5_IODINE_10_PAINT = 5,
    STEP_6_STERILE_DRAPING = 6,
    STEP_7_SETUP_TABLE = 7,
    STEP_8_FINAL_SUMMARY = 8
}

/// <summary>
/// Master Game Manager for the VR Childbirth Simulation.
/// Enforces the 8-Step Clinical Protocol:
/// Step 1: Talk to Mother / Informed Consent
/// Step 2: Preliminary Water Wash (0-100%)
/// Step 3: 7.5% Iodine 9-Ball Technique (Washable light green puddles)
/// Step 4: Antiseptic Rinse (Pitcher washes puddles 0-100%)
/// Step 5: 10% Iodine Surgical Paint (Persistent amber coating)
/// Step 6: Sterile Draping (Strict two-handed grab, 4 zones)
/// Step 7: Setup Table (Placeholder / Mayo prep)
/// Step 8: Done / Final Score Calculation & Evaluation Summary UI
/// </summary>
public class VRDemoGameManager : MonoBehaviour
{
    public static VRDemoGameManager Instance { get; private set; }

    [Header("Player Data")]
    public string currentPlayerName = "Guest";
    private SimulationRecord currentRecord;

    [Header("Central Phase Progression")]
    public SimulationPhase currentSimulationPhase = SimulationPhase.STEP_1_TALK_TO_MOTHER;
    public int currentScenarioPhase => (int)currentSimulationPhase;

    [Header("UI Feedback")]
    public TMP_Text feedbackText;

    [Header("Scoring Engine")]
    public int currentScore = 100;
    public int currentScenarioScore
    {
        get => currentScore;
        set => currentScore = value;
    }
    public int dropPenaltyPoints = 5;

    [Header("Scene Component References")]
    public PerinealCareManager perinealCareManager;
    public MotherDrapingManager motherDrapingManager;

    private readonly HashSet<string> penalizedMistakes = new HashSet<string>();
    private readonly HashSet<string> heldToolPenalties = new HashSet<string>();
    public readonly List<string> mistakeLog = new List<string>();

    public event Action<SimulationPhase> OnSimulationPhaseChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (perinealCareManager == null) perinealCareManager = FindFirstObjectByType<PerinealCareManager>();
        if (motherDrapingManager == null) motherDrapingManager = FindFirstObjectByType<MotherDrapingManager>();
    }

    private void Start()
    {
        InitializeSimulation();
    }

    public void InitializeSimulation()
    {
        currentScore = 100;
        penalizedMistakes.Clear();
        heldToolPenalties.Clear();
        mistakeLog.Clear();
        InitializeNewRecord();

        Debug.Log("[VRDemoGameManager] Initializing 8-Step Simulation Protocol at Step 1: Talk to Mother / Informed Consent.");
        SetPhase(SimulationPhase.STEP_1_TALK_TO_MOTHER);
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

    // ── Phase Transitions & Flow ──

    public void SetPhase(SimulationPhase nextPhase)
    {
        currentSimulationPhase = nextPhase;
        Debug.Log($"[VRDemoGameManager] Transitioned to Simulation Phase: {currentSimulationPhase} (Step {(int)currentSimulationPhase} of 8)");

        OnSimulationPhaseChanged?.Invoke(currentSimulationPhase);
    }

    public void AdvanceFromStep1_Consent()
    {
        SetPhase(SimulationPhase.STEP_2_PRELIMINARY_WATER_WASH);
        if (perinealCareManager != null)
        {
            perinealCareManager.SetState(PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH);
        }
    }

    public void AdvanceFromStep2_WaterWash()
    {
        SetPhase(SimulationPhase.STEP_3_IODINE_7_5_SCRUB);
        if (perinealCareManager != null)
        {
            perinealCareManager.SetState(PerinealCareState.STATE_3_IODINE_7_5_SCRUB);
        }
    }

    public void AdvanceFromStep3_7_5Scrub()
    {
        SetPhase(SimulationPhase.STEP_4_ANTISEPTIC_RINSE);
        if (perinealCareManager != null)
        {
            perinealCareManager.SetState(PerinealCareState.STATE_4_ANTISEPTIC_RINSE);
        }
    }

    public void AdvanceFromStep4_Rinse()
    {
        SetPhase(SimulationPhase.STEP_5_IODINE_10_PAINT);
        if (perinealCareManager != null)
        {
            perinealCareManager.SetState(PerinealCareState.STATE_5_IODINE_10_PAINT);
        }
    }

    public void AdvanceFromStep5_10Paint()
    {
        SetPhase(SimulationPhase.STEP_6_STERILE_DRAPING);
        if (perinealCareManager != null)
        {
            perinealCareManager.SetState(PerinealCareState.STATE_6_STERILE_DRAPING);
        }
    }

    public void AdvanceFromStep6_Draping()
    {
        SetPhase(SimulationPhase.STEP_7_SETUP_TABLE);
        if (perinealCareManager != null)
        {
            perinealCareManager.SetState(PerinealCareState.STATE_7_SETUP_TABLE);
        }
    }

    public void AdvanceFromStep7_SetupTable()
    {
        SetPhase(SimulationPhase.STEP_8_FINAL_SUMMARY);
        ShowFinalSummary();
    }

    public void ShowFinalSummary()
    {
        currentSimulationPhase = SimulationPhase.STEP_8_FINAL_SUMMARY;
        int finalScore = Mathf.Clamp(currentScore, 0, 100);

        Debug.Log($"[VRDemoGameManager] Step 8 Reached: Final Score = {finalScore}/100. Total mistakes logged: {mistakeLog.Count}");

        var dialogueUI = FindFirstObjectByType<FloatingPokeDialogueUI>();
        if (dialogueUI != null)
        {
            dialogueUI.ShowSummaryEvaluation(finalScore, mistakeLog);
        }

        SaveRecordLocally();
    }

    // ── Scoring & Penalty Handling ──

    public void ShowWarning(string warningMessage)
    {
        if (feedbackText != null && !string.IsNullOrEmpty(warningMessage))
        {
            feedbackText.text = warningMessage;
        }
    }

    public void RecordMistake(string wrongToolOrAction, int penalty = 5)
    {
        string mistakeSignature = $"step{(int)currentSimulationPhase}_{wrongToolOrAction}";

        if (!penalizedMistakes.Contains(mistakeSignature))
        {
            penalizedMistakes.Add(mistakeSignature);
            string readable = wrongToolOrAction.Replace('_', ' ');
            mistakeLog.Add($"Step {(int)currentSimulationPhase}: {readable} (-{penalty} pts)");

            currentScore = Mathf.Max(0, currentScore - penalty);
            ShowWarning($"Mistake: {readable} (-{penalty} pts)");
            Debug.Log($"[Scoring Engine] Deducted {penalty} pts for '{wrongToolOrAction}'. Remaining Score: {currentScore}");
        }
    }

    /// <summary>
    /// Deducts points when a tool or cotton ball is dropped on the floor or unsterile surface.
    /// </summary>
    public void RecordDropPenalty(string itemID, string customWarning = "")
    {
        string cleanName = itemID.Replace('_', ' ');
        mistakeLog.Add($"Step {(int)currentSimulationPhase}: Dropped {cleanName} on floor/unsterile surface (-{dropPenaltyPoints} pts)");

        currentScore = Mathf.Max(0, currentScore - dropPenaltyPoints);

        string warning = !string.IsNullOrEmpty(customWarning)
            ? customWarning
            : $"Sterile Violation: '{cleanName}' dropped! Return instruments to the sterile tray.";

        ShowWarning(warning);
        Debug.Log($"[Scoring Engine] Drop Penalty! Deducted {dropPenaltyPoints} pts for dropping {cleanName}. Remaining Score: {currentScore}");
    }

    public void CheckHeldToolHazard(string heldToolID)
    {
        // Reserved for phase hazard checks
    }

    public void AdvanceStep()
    {
        // Reserved for step advance
    }

    private void SaveRecordLocally()
    {
        if (currentRecord == null) InitializeNewRecord();
        currentRecord.finalScore = Mathf.Clamp(currentScore, 0, 100);
        currentRecord.mistakesMade = new List<string>(mistakeLog);

        string jsonData = JsonUtility.ToJson(currentRecord, true);
        string filePath = Path.Combine(Application.persistentDataPath, $"SimulationRecord_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        try
        {
            File.WriteAllText(filePath, jsonData);
            Debug.Log($"[Storage] Simulation session record saved to: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Storage] Failed to save record: {ex.Message}");
        }
    }
}