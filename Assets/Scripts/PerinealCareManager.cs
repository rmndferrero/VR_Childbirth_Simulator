using System;
using System.Collections.Generic;
using UnityEngine;

public enum PerinealCareState
{
    STATE_0_PATIENT_TALK,
    STATE_1_WATER_WASH,
    STATE_2_IODINE_7_5,
    STATE_3_WATER_RINSE,
    STATE_4_IODINE_10,
    STATE_5_COMPLETION
}

public class PerinealCareManager : MonoBehaviour
{
    public static PerinealCareManager Instance { get; private set; }

    [Header("Current State")]
    public PerinealCareState currentState = PerinealCareState.STATE_0_PATIENT_TALK;

    [Header("UI References")]
    public FloatingPokeDialogueUI floatingDialogueUI;
    public CleaningProgressUI cleaningProgressUI;
    public StrokeGuideUI strokeGuideUI;

    [Header("Scene Component References")]
    public BetadinePaintZone paintZone;
    public StrokeTrackingManager strokeTrackingManager;
    public PitcherPour pitcherPour;

    [Header("Evaluation & Scoring")]
    public int totalMistakes = 0;
    public List<string> procedureLog = new List<string>();

    public event Action<PerinealCareState> OnStateChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        SetState(PerinealCareState.STATE_0_PATIENT_TALK);
    }

    public void SetState(PerinealCareState newState)
    {
        currentState = newState;
        Debug.Log($"[PerinealCareManager] Transitioned to state: {currentState}");

        switch (currentState)
        {
            case PerinealCareState.STATE_0_PATIENT_TALK:
                if (floatingDialogueUI != null)
                {
                    floatingDialogueUI.ShowInitialPrompt();
                }
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.Hide();
                }
                if (strokeGuideUI != null)
                {
                    strokeGuideUI.Hide();
                }
                break;

            case PerinealCareState.STATE_1_WATER_WASH:
                if (floatingDialogueUI != null)
                {
                    floatingDialogueUI.Hide();
                }
                if (strokeGuideUI != null)
                {
                    strokeGuideUI.Hide();
                }
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.ResetProgress("Step 1: Preliminary Water Wash");
                    cleaningProgressUI.Show();
                }
                break;

            case PerinealCareState.STATE_2_IODINE_7_5:
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.Hide();
                }
                if (floatingDialogueUI != null)
                {
                    floatingDialogueUI.Hide();
                }
                if (strokeTrackingManager != null)
                {
                    strokeTrackingManager.ResetForPhase(AntisepticType.Iodine_7_5_Scrub);
                }
                if (strokeGuideUI != null)
                {
                    if (strokeGuideUI.phaseTitleText != null)
                        strokeGuideUI.phaseTitleText.text = "Step 2: 7.5% Povidone-Iodine Scrub (9-Ball Technique)";
                    strokeGuideUI.Show();
                }
                Debug.Log("[PerinealCareManager] 7.5% Iodine Scrub Phase Started. Use Pickup Forceps -> 7.5% Jar -> Handling Forceps.");
                break;

            case PerinealCareState.STATE_3_WATER_RINSE:
                if (strokeGuideUI != null)
                {
                    strokeGuideUI.Hide();
                }
                if (floatingDialogueUI != null)
                {
                    floatingDialogueUI.Hide();
                }
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.ResetProgress("Step 3: Intermediate Water Rinse");
                    cleaningProgressUI.Show();
                }
                Debug.Log("[PerinealCareManager] Intermediate Water Rinse Started. Wash away the Light Green paint.");
                break;

            case PerinealCareState.STATE_4_IODINE_10:
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.Hide();
                }
                if (floatingDialogueUI != null)
                {
                    floatingDialogueUI.Hide();
                }
                if (strokeTrackingManager != null)
                {
                    strokeTrackingManager.ResetForPhase(AntisepticType.Iodine_10_Paint);
                }
                if (strokeGuideUI != null)
                {
                    if (strokeGuideUI.phaseTitleText != null)
                        strokeGuideUI.phaseTitleText.text = "Step 4: 10% Povidone-Iodine Antiseptic Paint (Surgical Prep)";
                    strokeGuideUI.Show();
                }
                Debug.Log("[PerinealCareManager] 10% Iodine Paint Phase Started. Use Pickup Forceps -> 10% Jar -> Handling Forceps.");
                break;

            case PerinealCareState.STATE_5_COMPLETION:
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.Hide();
                }
                if (strokeGuideUI != null)
                {
                    strokeGuideUI.Hide();
                }
                if (floatingDialogueUI != null)
                {
                    floatingDialogueUI.ShowCompletionPrompt();
                }
                Debug.Log("[PerinealCareManager] Perineal Preparation Finished! Notify the mother.");
                break;
        }

        OnStateChanged?.Invoke(currentState);
    }

    public void OnMotherInformedConsentGiven()
    {
        if (currentState == PerinealCareState.STATE_0_PATIENT_TALK)
        {
            SetState(PerinealCareState.STATE_1_WATER_WASH);
        }
    }

    public void OnWaterWashCompleted()
    {
        if (currentState == PerinealCareState.STATE_1_WATER_WASH)
        {
            SetState(PerinealCareState.STATE_2_IODINE_7_5);
        }
        else if (currentState == PerinealCareState.STATE_3_WATER_RINSE)
        {
            SetState(PerinealCareState.STATE_4_IODINE_10);
        }
    }

    public void On7_5ScrubCompleted()
    {
        if (currentState == PerinealCareState.STATE_2_IODINE_7_5)
        {
            SetState(PerinealCareState.STATE_3_WATER_RINSE);
        }
    }

    public void On10PaintCompleted()
    {
        if (currentState == PerinealCareState.STATE_4_IODINE_10)
        {
            SetState(PerinealCareState.STATE_5_COMPLETION);
        }
    }

    public void OnProcedureFinishedAndMotherNotified()
    {
        if (currentState == PerinealCareState.STATE_5_COMPLETION)
        {
            EvaluateProcedure();
        }
    }

    public void RecordClinicalViolation(string message, int penaltyPoints = 5)
    {
        totalMistakes++;
        procedureLog.Add(message);
        Debug.LogWarning($"[Clinical Violation] {message} (-{penaltyPoints} pts)");

        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.currentScenarioScore = Mathf.Max(0, VRDemoGameManager.Instance.currentScenarioScore - penaltyPoints);
        }
    }

    private void EvaluateProcedure()
    {
        Debug.Log($"[PerinealCareManager] Evaluation complete! Total Violations: {totalMistakes}");
        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.CompletePatientAssessment();
        }
    }
}
