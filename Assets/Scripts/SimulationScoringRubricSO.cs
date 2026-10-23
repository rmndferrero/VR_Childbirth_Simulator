using System;
using UnityEngine;

public enum ClinicalViolationType
{
    // Step 1: Communication
    Suboptimal_Dialogue,
    Inadequate_Dialogue,
    Tool_Handled_Before_Consent,

    // Steps 2-5: Perineal Care & 9-Ball Scrub
    Reverse_Wipe_Direction,
    Out_Of_Order_Stroke,
    Reused_Cotton_Ball,

    // Step 6: Sterile Draping
    One_Handed_Drape_Violation,
    Out_Of_Order_Drape,

    // Step 7: Mayo Table Prep
    Incorrect_Table_Slot,
    Unsterile_Mayo_Placement_Violation,

    // Universal Field Violations
    Floor_Drop_Instrument,
    Contaminated_Drop_Outside_Basin
}

/// <summary>
/// Centralized ScriptableObject managing all clinical scoring benchmarks,
/// passing thresholds, and itemized violation point deductions.
/// Instructors can create and swap rubrics (e.g. Strict OSCE vs. Practice Mode)
/// directly in the Unity Inspector without recompiling code.
/// </summary>
[CreateAssetMenu(fileName = "SimulationScoringRubric", menuName = "VR Simulator/Scoring Rubric")]
public class SimulationScoringRubricSO : ScriptableObject
{
    [Header("Overall Scoring Benchmarks")]
    [Tooltip("Maximum starting score (typically 100 points).")]
    public int maxTotalScore = 100;

    [Tooltip("Minimum passing score required for clinical competency.")]
    public int passThresholdScore = 70;

    [Tooltip("Score threshold required for Honors / Excellent rating.")]
    public int honorsThresholdScore = 85;

    [Header("Step 1: Patient Communication Penalties")]
    [Tooltip("Penalty deducted when student confirms a Suboptimal statement (Orange tier, 50-79%).")]
    public int suboptimalDialoguePenalty = 5;

    [Tooltip("Penalty deducted when student attempts an Inadequate statement (Red tier, <50%).")]
    public int inadequateDialoguePenalty = 10;

    [Tooltip("Penalty deducted when student attempts to handle tools or pitcher before obtaining consent.")]
    public int instrumentHandledBeforeConsentPenalty = 5;

    [Header("Steps 2–5: Perineal Care & 9-Ball Scrub Penalties")]
    [Tooltip("Penalty for wiping in an upward / reverse direction (must be anterior-to-posterior).")]
    public int reverseWipeDirectionPenalty = 5;

    [Tooltip("Penalty for performing an out-of-order anatomical stroke in the 9-ball sequence.")]
    public int outOfOrderStrokePenalty = 5;

    [Tooltip("Penalty for attempting to wipe with an already used cotton ball.")]
    public int reusedCottonBallPenalty = 5;

    [Header("Step 6: Sterile Draping Penalties")]
    [Tooltip("Penalty for attempting to place linen with only 1 hand (violating 2-handed unfold technique).")]
    public int oneHandedDrapeViolationPenalty = 5;

    [Tooltip("Penalty for placing a drape on the wrong anatomical zone sequence.")]
    public int outOfOrderDrapePenalty = 5;

    [Header("Step 7: Mayo Table Prep Penalties")]
    [Tooltip("Penalty for placing a surgical instrument into the wrong Table 2 slot.")]
    public int incorrectTableSlotPenalty = 5;

    [Tooltip("Penalty for attempting to place an instrument onto the Mayo Stand before it is draped with sterile linen.")]
    public int unsterileMayoPlacementPenalty = 10;

    [Header("Universal Sterile Field Violations")]
    [Tooltip("Penalty for dropping any sterile instrument or linen onto the floor.")]
    public int floorDropPenalty = 5;

    [Tooltip("Penalty for dropping contaminated used cotton outside the waste basin.")]
    public int contaminatedDropPenalty = 10;

    /// <summary>
    /// Returns the configured penalty points for any clinical violation type.
    /// </summary>
    public int GetPenaltyForViolation(ClinicalViolationType violationType)
    {
        switch (violationType)
        {
            case ClinicalViolationType.Suboptimal_Dialogue:
                return suboptimalDialoguePenalty;
            case ClinicalViolationType.Inadequate_Dialogue:
                return inadequateDialoguePenalty;
            case ClinicalViolationType.Tool_Handled_Before_Consent:
                return instrumentHandledBeforeConsentPenalty;
            case ClinicalViolationType.Reverse_Wipe_Direction:
                return reverseWipeDirectionPenalty;
            case ClinicalViolationType.Out_Of_Order_Stroke:
                return outOfOrderStrokePenalty;
            case ClinicalViolationType.Reused_Cotton_Ball:
                return reusedCottonBallPenalty;
            case ClinicalViolationType.One_Handed_Drape_Violation:
                return oneHandedDrapeViolationPenalty;
            case ClinicalViolationType.Out_Of_Order_Drape:
                return outOfOrderDrapePenalty;
            case ClinicalViolationType.Incorrect_Table_Slot:
                return incorrectTableSlotPenalty;
            case ClinicalViolationType.Unsterile_Mayo_Placement_Violation:
                return unsterileMayoPlacementPenalty;
            case ClinicalViolationType.Floor_Drop_Instrument:
                return floorDropPenalty;
            case ClinicalViolationType.Contaminated_Drop_Outside_Basin:
                return contaminatedDropPenalty;
            default:
                return 5;
        }
    }
}
