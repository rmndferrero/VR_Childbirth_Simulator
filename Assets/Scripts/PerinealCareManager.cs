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
    STATE_5_DRAPING,
    STATE_6_COMPLETION
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
    public DrapingGuideUI drapingGuideUI;
    [Tooltip("If true, shows floating top progress tracker HUD during strokes.")]
    public bool showFloatingTextGuide = true;

    [Header("Scene Component References")]
    public StrokeTrackingManager strokeTrackingManager;
    public PitcherPour pitcherPour;
    public MotherDrapingManager motherDrapingManager;

    [Header("Evaluation & Scoring")]
    public int totalMistakes = 0;
    public List<string> procedureLog = new List<string>();

    public event Action<PerinealCareState> OnStateChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (motherDrapingManager == null)
            motherDrapingManager = FindFirstObjectByType<MotherDrapingManager>();

        if (drapingGuideUI == null)
            drapingGuideUI = FindFirstObjectByType<DrapingGuideUI>(FindObjectsInactive.Include);

        if (drapingGuideUI == null)
        {
            var go = new GameObject("DrapingGuideUI");
            drapingGuideUI = go.AddComponent<DrapingGuideUI>();
        }

        if (motherDrapingManager != null)
        {
            motherDrapingManager.OnAllDrapesCompleted += OnDrapingCompleted;
        }
    }

    private void OnDestroy()
    {
        if (motherDrapingManager != null)
        {
            motherDrapingManager.OnAllDrapesCompleted -= OnDrapingCompleted;
        }
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
                if (floatingDialogueUI != null) floatingDialogueUI.ShowInitialPrompt();
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                break;

            case PerinealCareState.STATE_1_WATER_WASH:
                if (floatingDialogueUI != null) floatingDialogueUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.ResetProgress("Step 1: Preliminary Water Wash");
                    cleaningProgressUI.Show();
                }
                break;

            case PerinealCareState.STATE_2_IODINE_7_5:
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (floatingDialogueUI != null) floatingDialogueUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (strokeTrackingManager != null)
                {
                    strokeTrackingManager.ResetForPhase(AntisepticType.Iodine_7_5_Scrub);
                }
                if (strokeGuideUI != null)
                {
                    if (showFloatingTextGuide)
                    {
                        if (strokeGuideUI.phaseTitleText != null)
                            strokeGuideUI.phaseTitleText.text = "Step 2: 7.5% Povidone-Iodine Scrub (9-Ball Technique)";
                        strokeGuideUI.Show();
                    }
                    else
                    {
                        strokeGuideUI.Hide();
                    }
                }
                Debug.Log("[PerinealCareManager] 7.5% Iodine Scrub Phase Started. Use Pickup Forceps -> 7.5% Jar -> Handling Forceps.");
                break;

            case PerinealCareState.STATE_3_WATER_RINSE:
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (floatingDialogueUI != null) floatingDialogueUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.ResetForRinse("Step 3: Antiseptic Rinse");
                    cleaningProgressUI.Show();
                }
                Debug.Log("[PerinealCareManager] Antiseptic Water Rinse Started. Wash away the light green puddles.");
                break;

            case PerinealCareState.STATE_4_IODINE_10:
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (floatingDialogueUI != null) floatingDialogueUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (strokeTrackingManager != null)
                {
                    strokeTrackingManager.ResetForPhase(AntisepticType.Iodine_10_Paint);
                }
                if (strokeGuideUI != null)
                {
                    if (showFloatingTextGuide)
                    {
                        if (strokeGuideUI.phaseTitleText != null)
                            strokeGuideUI.phaseTitleText.text = "Step 4: 10% Povidone-Iodine Antiseptic Paint (Surgical Prep)";
                        strokeGuideUI.Show();
                    }
                    else
                    {
                        strokeGuideUI.Hide();
                    }
                }
                Debug.Log("[PerinealCareManager] 10% Iodine Paint Phase Started. Use Pickup Forceps -> 10% Jar -> Handling Forceps.");
                break;

            case PerinealCareState.STATE_5_DRAPING:
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (floatingDialogueUI != null) floatingDialogueUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                
                // Automatically transition player hands to sterile surgical blue gloves
                if (PlayerHandMaterialManager.Instance != null)
                {
                    PlayerHandMaterialManager.Instance.EquipGloves();
                }

                if (drapingGuideUI != null)
                {
                    if (showFloatingTextGuide)
                    {
                        drapingGuideUI.Show();
                    }
                    else
                    {
                        drapingGuideUI.Hide();
                    }
                }

                if (motherDrapingManager != null)
                {
                    motherDrapingManager.BeginDrapingPhase();
                }
                Debug.Log("[PerinealCareManager] Sterile Draping Phase Started. Player equipped blue gloves. Place dry linen on Under Buttocks -> Abdominal -> Left Leg -> Right Leg.");
                break;

            case PerinealCareState.STATE_6_COMPLETION:
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (floatingDialogueUI != null) floatingDialogueUI.ShowCompletionPrompt();
                Debug.Log("[PerinealCareManager] Perineal Preparation and Sterile Draping Finished! Notify the mother.");
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
            SetState(PerinealCareState.STATE_5_DRAPING);
        }
    }

    public void OnDrapingCompleted()
    {
        if (currentState == PerinealCareState.STATE_5_DRAPING)
        {
            SetState(PerinealCareState.STATE_6_COMPLETION);
        }
    }

    public void OnProcedureFinishedAndMotherNotified()
    {
        if (currentState == PerinealCareState.STATE_6_COMPLETION)
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
