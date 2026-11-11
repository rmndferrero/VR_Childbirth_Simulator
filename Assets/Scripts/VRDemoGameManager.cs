using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class ToolDropSummaryEntry
{
    public string toolName;
    public int dropCount;
}

[System.Serializable]
public class SimulationRecord
{
    public string playerName;
    public string dateCompleted;
    public int finalScore;
    public List<string> mistakesMade = new List<string>();

    // ── Tiered Mistake Summaries (Cotton Balls & Tool Drops) ──
    public int affectedCottonBallCount;
    public int cottonBallDeduction;
    public int totalToolDropCount;
    public int toolDropDeduction;
    public int totalDeductions;
    public List<ToolDropSummaryEntry> toolDropBreakdown = new List<ToolDropSummaryEntry>();

    // ── Decision Matrix (parallel evaluation; the fields above are unchanged) ──
    public string sessionId;
    public string sessionFolder;
    public float matrixScore;
    public int severeMistakeCount;
    public DecisionMatrixResult decisionMatrix;
    public List<EvaluationEvent> evaluationEvents = new List<EvaluationEvent>();
    public List<StepTiming> stepTimings = new List<StepTiming>();
    public List<string> capturePaths = new List<string>();
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

    public static SimulationRecord LastCompletedRecord { get; private set; }
    public static CommunicationDebriefData LastDebrief { get; private set; }
    public static SimulationScoringRubricSO LastRubric { get; private set; }

    [Header("Player Data")]
    public string currentPlayerName = "Guest";
    private SimulationRecord currentRecord;

    [Header("Central Phase Progression")]
    public SimulationPhase currentSimulationPhase = SimulationPhase.STEP_1_TALK_TO_MOTHER;
    public int currentScenarioPhase => (int)currentSimulationPhase;

    [Header("UI Feedback")]
    public TMP_Text feedbackText;

    [Header("Centralized Scoring Rubric")]
    [Tooltip("Master ScriptableObject rubric defining all passing thresholds and penalty deductions.")]
    public SimulationScoringRubricSO scoringRubric;

    [Header("Step 8 Summary Screen Controller")]
    public SummaryScreenController summaryScreenController;

    [Header("Decision Matrix (parallel score, does not affect currentScore)")]
    [Tooltip("Inspector-editable Decision Matrix. If empty, built-in placeholder values are used.")]
    public DecisionMatrixSO decisionMatrix;

    [Tooltip("If true, the Step 8 summary asks 'Save Evaluation / Don't Save' before writing the session folder (JSON, CSV, HTML, screenshots). If false, it is saved automatically. The small SimulationRecord_*.json is always saved.")]
    public bool askBeforeSaving = true;

    [Header("Scoring Engine")]
    public int currentScore = 100;
    public int currentScenarioScore
    {
        get => currentScore;
        set => currentScore = value;
    }
    public int dropPenaltyPoints => scoringRubric != null ? scoringRubric.floorDropPenalty : 5;

    [Header("Communication Debrief Cache (Step 1 -> Step 8)")]
    public CommunicationDebriefData communicationDebrief;

    [Header("Scene Component References")]
    public PerinealCareManager perinealCareManager;
    public MotherDrapingManager motherDrapingManager;

    private readonly HashSet<string> penalizedMistakes = new HashSet<string>();
    private readonly HashSet<string> heldToolPenalties = new HashSet<string>();
    private readonly HashSet<int> affectedCottonIds = new HashSet<int>();
    private readonly Dictionary<string, int> toolDropCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private int totalStandardDeductions = 0;
    private int totalToolDropCount = 0;
    public readonly List<string> mistakeLog = new List<string>();

    public int AffectedCottonBallCount => affectedCottonIds.Count;
    public int CottonBallDeduction => GetCottonBallTierPenalty(affectedCottonIds.Count);
    public int TotalToolDropCount => totalToolDropCount;
    public int ToolDropDeduction => GetToolDropTierPenalty(totalToolDropCount);
    public int TotalDeductions => totalStandardDeductions + CottonBallDeduction + ToolDropDeduction;
    public IReadOnlyDictionary<string, int> ToolDropCounts => toolDropCounts;

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

        EvaluationLogger.Instance.Bind(this);
        GameplayCaptureService.EnsureExists();
    }

    private void Start()
    {
        InitializeSimulation();
    }

    public void InitializeSimulation()
    {
        penalizedMistakes.Clear();
        heldToolPenalties.Clear();
        affectedCottonIds.Clear();
        toolDropCounts.Clear();
        totalStandardDeductions = 0;
        totalToolDropCount = 0;
        mistakeLog.Clear();
        RecalculateCurrentScore();
        InitializeNewRecord();
        EvaluationLogger.Instance.ResetSession();

        Debug.Log("[VRDemoGameManager] Initializing 8-Step Simulation Protocol at Step 1: Talk to Mother / Informed Consent.");
        SetPhase(SimulationPhase.STEP_1_TALK_TO_MOTHER);
    }

    private void RecalculateCurrentScore()
    {
        int maxScore = scoringRubric != null ? scoringRubric.maxTotalScore : 100;
        currentScore = Mathf.Clamp(maxScore - TotalDeductions, 0, maxScore);
    }

    public static int GetCottonBallTierPenalty(int affectedCount)
    {
        if (affectedCount <= 0) return 0;
        if (affectedCount <= 2) return 5;
        if (affectedCount <= 5) return 10;
        return 15;
    }

    // Option 9B (Clinical-Severity Tool Drop Tiering): 0 -> 0, 1 -> -5, 2-3 -> -10, >=4 -> -15 cap
    public static int GetToolDropTierPenalty(int droppedCount)
    {
        if (droppedCount <= 0) return 0;
        if (droppedCount == 1) return 5;
        if (droppedCount <= 3) return 10;
        return 15;
    }

    public static string NormalizeToolDisplayName(string rawItemId)
    {
        if (string.IsNullOrWhiteSpace(rawItemId)) return "Surgical Instrument";
        string t = rawItemId.Trim().ToLowerInvariant();
        if (t.Contains("pitcher") || t.Contains("flask")) return "Pitcher";
        if (t.Contains("mayo")) return "Mayo Scissors";
        if (t.Contains("bandage")) return "Bandage Scissors";
        if (t.Contains("umbi")) return "Umbilical Clamp";
        if (t.Contains("catheter")) return "Straight Catheter";
        if (t.Contains("basin")) return "Kidney Basin";
        if (t.Contains("gauze")) return "Gauze Pack";
        if (t.Contains("scalpel")) return "Scalpel";
        if (t.Contains("curette")) return "Curette";
        if (t.Contains("sponge") || t == "scissor" || t == "scissors") return "Sponge Forceps";
        if (t.Contains("thumb") || t.Contains("tongs")) return "Thumb Forceps";
        if (t.Contains("handling")) return "Handling Forceps";
        if (t.Contains("pickup")) return "Pickup Forceps";
        if (t.Contains("artery") || t.Contains("forcep")) return "Artery Forceps";
        if (t.Contains("linen") || t.Contains("drape")) return "Sterile Linen";
        return rawItemId.Replace("_01", "").Replace("_", " ").Trim();
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
        EvaluationLogger.Instance.LogCorrect("Informed consent obtained", MatrixCriterion.Communication);
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

        SaveRecordLocally();

        if (Application.CanStreamedLevelBeLoaded("SummaryScene"))
        {
            Debug.Log("[VRDemoGameManager] Transitioning smoothly to SummaryScene...");
            VRSceneTransitionFader.FadeToScene("SummaryScene");
            return;
        }

        if (summaryScreenController != null)
        {
            summaryScreenController.OpenSummaryScreen();
        }
        else
        {
            var summaryScreen = FindFirstObjectByType<SummaryScreenController>();
            if (summaryScreen != null)
            {
                summaryScreen.OpenSummaryScreen();
            }
            else
            {
                var dialogueUI = FindFirstObjectByType<FloatingPokeDialogueUI>();
                if (dialogueUI != null)
                {
                    dialogueUI.ShowSummaryEvaluation(finalScore, mistakeLog);
                }
            }
        }
    }

    // ── Scoring & Penalty Handling ──

    public void ShowWarning(string warningMessage)
    {
        if (feedbackText != null && !string.IsNullOrEmpty(warningMessage))
        {
            feedbackText.text = warningMessage;
        }
    }

    /// <summary>
    /// Records a rubric-defined clinical violation with centralized penalty calculation.
    /// </summary>
    public void RecordViolation(ClinicalViolationType violationType, string customDetails = "")
    {
        int penalty = scoringRubric != null ? scoringRubric.GetPenaltyForViolation(violationType) : 5;
        string violationName = violationType.ToString().Replace('_', ' ');
        string mistakeSignature = $"step{(int)currentSimulationPhase}_{violationType}_{customDetails}";
        EvaluationLogger.Instance.LogViolation(violationType, violationName, customDetails, penalty, !penalizedMistakes.Contains(mistakeSignature));

        if (!penalizedMistakes.Contains(mistakeSignature))
        {
            penalizedMistakes.Add(mistakeSignature);
            string readable = string.IsNullOrEmpty(customDetails) ? violationName : $"{violationName} ({customDetails})";
            mistakeLog.Add($"Step {(int)currentSimulationPhase}: {readable} (-{penalty} pts)");

            totalStandardDeductions += Mathf.Max(0, penalty);
            RecalculateCurrentScore();
            ShowWarning($"Violation: {readable} (-{penalty} pts)");
            Debug.Log($"[Scoring Engine] Violation '{violationType}' recorded. Deducted {penalty} pts. Remaining Score: {currentScore}");
        }
    }

    public void RecordMistake(string wrongToolOrAction, int penalty = 5)
    {
        string mistakeSignature = $"step{(int)currentSimulationPhase}_{wrongToolOrAction}";
        EvaluationLogger.Instance.LogFreeTextMistake(wrongToolOrAction, penalty, !penalizedMistakes.Contains(mistakeSignature));

        if (!penalizedMistakes.Contains(mistakeSignature))
        {
            penalizedMistakes.Add(mistakeSignature);
            string readable = wrongToolOrAction.Replace('_', ' ');
            mistakeLog.Add($"Step {(int)currentSimulationPhase}: {readable} (-{penalty} pts)");

            totalStandardDeductions += Mathf.Max(0, penalty);
            RecalculateCurrentScore();
            ShowWarning($"Mistake: {readable} (-{penalty} pts)");
            Debug.Log($"[Scoring Engine] Deducted {penalty} pts for '{wrongToolOrAction}'. Remaining Score: {currentScore}");
        }
    }

    /// <summary>
    /// Tiered Cotton Ball Penalty (Section 8): counts each unique CottonState instance ID once across reuse and outside-basin drops.
    /// Tiers: 0 -> 0, 1-2 -> -5, 3-5 -> -10, >5 -> -15 max.
    /// </summary>
    public void RecordCottonBallMistake(
        int cottonInstanceId,
        ClinicalViolationType violationType,
        string actionText,
        string customWarning = "",
        string detail = "")
    {
        int prevTier = CottonBallDeduction;
        bool isNewCotton = affectedCottonIds.Add(cottonInstanceId);
        int nextTier = CottonBallDeduction;
        int delta = isNewCotton ? Mathf.Max(0, nextTier - prevTier) : 0;

        EvaluationLogger.Instance.LogViolation(violationType, actionText, detail, delta, delta > 0);

        if (isNewCotton)
        {
            RecalculateCurrentScore();
            mistakeLog.Add(
                $"Step {(int)currentSimulationPhase}: {actionText} " +
                $"(Affected cottons: {affectedCottonIds.Count}, Cotton tier: -{nextTier} pts{(delta > 0 ? $", -{delta} pts" : "")})");
        }

        string warning = !string.IsNullOrEmpty(customWarning)
            ? customWarning
            : $"Cotton Handling Caution: {actionText} ({affectedCottonIds.Count} affected, -{nextTier} pts tier).";
        ShowWarning(warning);
        Debug.Log($"[Scoring Engine] Cotton Mistake ({violationType}) id={cottonInstanceId}, uniqueCount={affectedCottonIds.Count}, tier=-{nextTier} (delta=-{delta}). Score={currentScore}");
    }

    /// <summary>
    /// Deducts points when a tool or cotton ball is dropped on the floor or unsterile surface.
    /// </summary>
    public void RecordDropPenalty(string itemID, string customWarning = "")
    {
        RecordDropPenalty(itemID, customWarning, ClinicalViolationType.Floor_Drop_Instrument);
    }

    /// <summary>
    /// Tiered Tool Drop Penalty (Section 9 - Option 9B) with automatic cotton-item routing if a legacy caller passes cotton IDs.
    /// </summary>
    public void RecordDropPenalty(string itemID, string customWarning, ClinicalViolationType matrixViolation)
    {
        if (!string.IsNullOrEmpty(itemID) && itemID.ToLowerInvariant().Contains("cotton"))
        {
            RecordCottonBallMistake(
                Mathf.Abs((itemID + "_" + affectedCottonIds.Count).GetHashCode()),
                matrixViolation,
                "Cotton ball dropped outside waste basin",
                customWarning,
                itemID);
            return;
        }

        string displayName = NormalizeToolDisplayName(itemID);
        int prevTier = ToolDropDeduction;
        totalToolDropCount++;
        toolDropCounts.TryGetValue(displayName, out int perToolCount);
        toolDropCounts[displayName] = perToolCount + 1;

        int nextTier = ToolDropDeduction;
        int delta = Mathf.Max(0, nextTier - prevTier);

        EvaluationLogger.Instance.LogViolation(matrixViolation, $"Dropped {displayName}", displayName, delta, delta > 0);

        RecalculateCurrentScore();
        mistakeLog.Add(
            $"Step {(int)currentSimulationPhase}: Dropped {displayName} on floor/unsterile surface " +
            $"(Total tool drops: {totalToolDropCount}, Tool tier: -{nextTier} pts{(delta > 0 ? $", -{delta} pts" : "")})");

        string warning = !string.IsNullOrEmpty(customWarning)
            ? customWarning
            : $"Sterile Violation: '{displayName}' dropped! Return instruments to the sterile tray.";

        ShowWarning(warning);
        Debug.Log($"[Scoring Engine] Tool Drop ({displayName}): totalDrops={totalToolDropCount}, tier=-{nextTier} (delta=-{delta}). Remaining Score: {currentScore}");
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
        RecalculateCurrentScore();
        LastCompletedRecord = currentRecord;
        LastDebrief = communicationDebrief;
        LastRubric = scoringRubric;
        currentRecord.finalScore = Mathf.Clamp(currentScore, 0, 100);
        currentRecord.mistakesMade = new List<string>(mistakeLog);
        currentRecord.affectedCottonBallCount = AffectedCottonBallCount;
        currentRecord.cottonBallDeduction = CottonBallDeduction;
        currentRecord.totalToolDropCount = TotalToolDropCount;
        currentRecord.toolDropDeduction = ToolDropDeduction;
        currentRecord.totalDeductions = TotalDeductions;
        currentRecord.toolDropBreakdown = new List<ToolDropSummaryEntry>();
        foreach (var kvp in toolDropCounts)
        {
            if (kvp.Value > 0)
            {
                currentRecord.toolDropBreakdown.Add(new ToolDropSummaryEntry
                {
                    toolName = kvp.Key,
                    dropCount = kvp.Value
                });
            }
        }
        EvaluationReportExporter.Populate(currentRecord, scoringRubric, communicationDebrief);

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

        if (askBeforeSaving) EvaluationReportExporter.SetPending(currentRecord, scoringRubric);
        else EvaluationReportExporter.SaveSession(currentRecord, scoringRubric);
    }
}