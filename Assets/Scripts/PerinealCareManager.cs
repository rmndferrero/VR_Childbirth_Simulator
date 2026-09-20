using System;
using System.Collections.Generic;
using UnityEngine;

public enum PerinealCareState
{
    STATE_1_TALK_TO_MOTHER,
    STATE_2_PRELIMINARY_WATER_WASH,
    STATE_3_IODINE_7_5_SCRUB,
    STATE_4_ANTISEPTIC_RINSE,
    STATE_5_IODINE_10_PAINT,
    STATE_6_STERILE_DRAPING,
    STATE_7_SETUP_TABLE,
    STATE_8_FINAL_SUMMARY
}

/// <summary>
/// Controls the step-by-step clinical state progression:
/// Step 1: Talk to Mother / Informed Consent
/// Step 2: Preliminary Water Wash (0-100%)
/// Step 3: 7.5% Iodine 9-Ball Technique (Washable light green puddles)
/// Step 4: Antiseptic Rinse (Pitcher washes puddles 0-100%)
/// Step 5: 10% Iodine Surgical Paint (Persistent amber coating)
/// Step 6: Sterile Draping (Strict two-handed grab, 4 zones)
/// Step 7: Setup Table (Placeholder / Mayo prep)
/// Step 8: Done / Final Score Calculation & Evaluation Summary UI
/// </summary>
public class PerinealCareManager : MonoBehaviour
{
    public static PerinealCareManager Instance { get; private set; }

    [Header("Current State")]
    public PerinealCareState currentState = PerinealCareState.STATE_1_TALK_TO_MOTHER;

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
        else
        {
            Destroy(gameObject);
            return;
        }

        if (floatingDialogueUI == null) floatingDialogueUI = FindFirstObjectByType<FloatingPokeDialogueUI>(FindObjectsInactive.Include);
        if (cleaningProgressUI == null) cleaningProgressUI = FindFirstObjectByType<CleaningProgressUI>(FindObjectsInactive.Include);
        if (strokeGuideUI == null) strokeGuideUI = FindFirstObjectByType<StrokeGuideUI>(FindObjectsInactive.Include);
        if (strokeTrackingManager == null) strokeTrackingManager = FindFirstObjectByType<StrokeTrackingManager>();
        if (pitcherPour == null) pitcherPour = FindFirstObjectByType<PitcherPour>();
        if (motherDrapingManager == null) motherDrapingManager = FindFirstObjectByType<MotherDrapingManager>();

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
        SetState(PerinealCareState.STATE_1_TALK_TO_MOTHER);
    }

    public void SetState(PerinealCareState newState)
    {
        currentState = newState;
        Debug.Log($"[PerinealCareManager] Transitioned to state: {currentState} (Step {(int)currentState + 1} of 8)");

        switch (currentState)
        {
            case PerinealCareState.STATE_1_TALK_TO_MOTHER:
                if (floatingDialogueUI != null) floatingDialogueUI.ShowInitialPrompt();
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                break;

            case PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH:
                if (floatingDialogueUI != null) floatingDialogueUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.ResetProgress("Step 2: Preliminary Water Wash");
                    cleaningProgressUI.Show();
                }
                break;

            case PerinealCareState.STATE_3_IODINE_7_5_SCRUB:
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
                            strokeGuideUI.phaseTitleText.text = "Step 3: 7.5% Povidone-Iodine Scrub (9-Ball Technique)";
                        strokeGuideUI.Show();
                    }
                    else
                    {
                        strokeGuideUI.Hide();
                    }
                }
                Debug.Log("[PerinealCareManager] Step 3: 7.5% Iodine Scrub Started. 3D guides appear when holding handling forceps + cotton.");
                break;

            case PerinealCareState.STATE_4_ANTISEPTIC_RINSE:
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (floatingDialogueUI != null) floatingDialogueUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (cleaningProgressUI != null)
                {
                    cleaningProgressUI.ResetForRinse("Step 4: Antiseptic Rinse");
                    cleaningProgressUI.Show();
                }
                Debug.Log("[PerinealCareManager] Step 4: Antiseptic Rinse Started. Wash away all light green puddles with water pitcher.");
                break;

            case PerinealCareState.STATE_5_IODINE_10_PAINT:
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
                            strokeGuideUI.phaseTitleText.text = "Step 5: 10% Povidone-Iodine Antiseptic Paint (Surgical Prep)";
                        strokeGuideUI.Show();
                    }
                    else
                    {
                        strokeGuideUI.Hide();
                    }
                }
                Debug.Log("[PerinealCareManager] Step 5: 10% Iodine Paint Started. Persistent amber surgical coating.");
                break;

            case PerinealCareState.STATE_6_STERILE_DRAPING:
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (floatingDialogueUI != null) floatingDialogueUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();

                // Automatically equip player hands with sterile surgical blue gloves
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
                Debug.Log("[PerinealCareManager] Step 6: Sterile Draping Started. Player equipped blue gloves. Place dry linen on Under Buttocks -> Abdomen -> Left Thigh -> Right Thigh.");
                break;

            case PerinealCareState.STATE_7_SETUP_TABLE:
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (floatingDialogueUI != null) floatingDialogueUI.ShowSetupTablePrompt();
                Debug.Log("[PerinealCareManager] Step 7: Setup Table (Mayo Prep Placeholder).");
                break;

            case PerinealCareState.STATE_8_FINAL_SUMMARY:
                if (cleaningProgressUI != null) cleaningProgressUI.Hide();
                if (strokeGuideUI != null) strokeGuideUI.Hide();
                if (drapingGuideUI != null) drapingGuideUI.Hide();
                if (VRDemoGameManager.Instance != null)
                {
                    VRDemoGameManager.Instance.ShowFinalSummary();
                }
                else if (floatingDialogueUI != null)
                {
                    floatingDialogueUI.ShowSummaryEvaluation(100 - (totalMistakes * 5), procedureLog);
                }
                Debug.Log("[PerinealCareManager] Step 8: Procedure Complete! Showing Final Score & Mistake Breakdown Summary UI.");
                break;
        }

        OnStateChanged?.Invoke(currentState);
    }

    public void OnMotherInformedConsentGiven()
    {
        if (currentState == PerinealCareState.STATE_1_TALK_TO_MOTHER)
        {
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.AdvanceFromStep1_Consent();
            }
            else
            {
                SetState(PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH);
            }
        }
    }

    public void OnWaterWashCompleted()
    {
        if (currentState == PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH)
        {
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.AdvanceFromStep2_WaterWash();
            }
            else
            {
                SetState(PerinealCareState.STATE_3_IODINE_7_5_SCRUB);
            }
        }
        else if (currentState == PerinealCareState.STATE_4_ANTISEPTIC_RINSE)
        {
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.AdvanceFromStep4_Rinse();
            }
            else
            {
                SetState(PerinealCareState.STATE_5_IODINE_10_PAINT);
            }
        }
    }

    public void On7_5ScrubCompleted()
    {
        if (currentState == PerinealCareState.STATE_3_IODINE_7_5_SCRUB)
        {
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.AdvanceFromStep3_7_5Scrub();
            }
            else
            {
                SetState(PerinealCareState.STATE_4_ANTISEPTIC_RINSE);
            }
        }
    }

    public void On10PaintCompleted()
    {
        if (currentState == PerinealCareState.STATE_5_IODINE_10_PAINT)
        {
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.AdvanceFromStep5_10Paint();
            }
            else
            {
                SetState(PerinealCareState.STATE_6_STERILE_DRAPING);
            }
        }
    }

    public void OnDrapingCompleted()
    {
        if (currentState == PerinealCareState.STATE_6_STERILE_DRAPING)
        {
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.AdvanceFromStep6_Draping();
            }
            else
            {
                SetState(PerinealCareState.STATE_7_SETUP_TABLE);
            }
        }
    }

    public void OnSetupTableCompleted()
    {
        if (currentState == PerinealCareState.STATE_7_SETUP_TABLE)
        {
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.AdvanceFromStep7_SetupTable();
            }
            else
            {
                SetState(PerinealCareState.STATE_8_FINAL_SUMMARY);
            }
        }
    }

    public void RecordClinicalViolation(string message, int penaltyPoints = 5)
    {
        totalMistakes++;
        procedureLog.Add(message);
        Debug.LogWarning($"[Clinical Violation] {message} (-{penaltyPoints} pts)");

        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.RecordMistake(message, penaltyPoints);
        }
    }
}
