using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-editable Decision Matrix definition (weights, severity tiers, step applicability and
/// violation -> criterion/severity mapping). A nursing instructor can tune every value here without code.
/// Runs in parallel to the legacy 100-minus-penalties score; it never changes that score.
/// </summary>
[CreateAssetMenu(fileName = "DecisionMatrix", menuName = "VR Simulator/Decision Matrix")]
public class DecisionMatrixSO : ScriptableObject
{
    [Serializable]
    public class StepRow
    {
        public string stepName;
        [Tooltip("Uncheck to mark the cell N/A. A cell that receives a violation is always scored.")]
        public bool sterility = true;
        public bool sequence = true;
        public bool technique = true;
        public bool communication;
    }

    [Serializable]
    public class ViolationRule
    {
        public ClinicalViolationType violationType;
        public MatrixCriterion criterion;
        public MistakeSeverity severity = MistakeSeverity.Moderate;
        [TextArea(1, 3)]
        [Tooltip("Shown in the Mistake Gallery as the correct technique.")]
        public string correctTechnique;
    }

    [Header("Criterion Weights (WSM, renormalised automatically)")]
    [Range(0f, 1f)] public float sterilityWeight = 0.35f;
    [Range(0f, 1f)] public float sequenceWeight = 0.25f;
    [Range(0f, 1f)] public float techniqueWeight = 0.25f;
    [Range(0f, 1f)] public float communicationWeight = 0.15f;

    [Header("Severity Tiers (cell points deducted, once per violation type per step)")]
    public float moderateDeduction = 25f;
    public float severeDeduction = 50f;

    [Header("Rows: Steps 1-7")]
    public List<StepRow> steps = CreatePlaceholderRows();

    [Tooltip("Step whose Communication cell uses the phrase-board composite score.")]
    public int communicationStep = 1;

    [Header("Violation Mapping")]
    public List<ViolationRule> violationRules = CreatePlaceholderRules();

    public bool TryGetRule(ClinicalViolationType type, out ViolationRule rule)
    {
        rule = violationRules != null ? violationRules.Find(r => r.violationType == type) : null;
        return rule != null;
    }

    /// <summary>Builds the plain config used by DecisionMatrixEvaluator. Thresholds come from the scoring rubric.</summary>
    public DecisionMatrixConfig ToConfig(SimulationScoringRubricSO rubric = null)
    {
        var config = new DecisionMatrixConfig
        {
            criterionWeights = new[] { sterilityWeight, sequenceWeight, techniqueWeight, communicationWeight },
            moderateDeduction = moderateDeduction,
            severeDeduction = severeDeduction,
            communicationStep = communicationStep,
            stepNames = steps.ConvertAll(s => s.stepName).ToArray(),
            applicable = new bool[steps.Count, DecisionMatrixConfig.CriterionCount]
        };
        for (int i = 0; i < steps.Count; i++)
        {
            config.applicable[i, 0] = steps[i].sterility;
            config.applicable[i, 1] = steps[i].sequence;
            config.applicable[i, 2] = steps[i].technique;
            config.applicable[i, 3] = steps[i].communication;
        }
        if (rubric != null)
        {
            config.passThreshold = rubric.passThresholdScore;
            config.honorsThreshold = rubric.honorsThresholdScore;
        }
        return config;
    }

    [ContextMenu("Reset To Placeholder Values")]
    private void ResetToPlaceholder()
    {
        sterilityWeight = 0.35f; sequenceWeight = 0.25f; techniqueWeight = 0.25f; communicationWeight = 0.15f;
        moderateDeduction = 25f; severeDeduction = 50f;
        communicationStep = 1;
        steps = CreatePlaceholderRows();
        violationRules = CreatePlaceholderRules();
    }

    private static List<StepRow> CreatePlaceholderRows()
    {
        var rows = new List<StepRow>();
        var a = DecisionMatrixConfig.PlaceholderApplicability;
        for (int i = 0; i < a.GetLength(0); i++)
        {
            rows.Add(new StepRow
            {
                stepName = DecisionMatrixConfig.PlaceholderStepNames[i],
                sterility = a[i, 0], sequence = a[i, 1], technique = a[i, 2], communication = a[i, 3]
            });
        }
        return rows;
    }

    private static List<ViolationRule> CreatePlaceholderRules()
    {
        ViolationRule R(ClinicalViolationType t, MatrixCriterion c, MistakeSeverity s, string fix) =>
            new ViolationRule { violationType = t, criterion = c, severity = s, correctTechnique = fix };

        const MatrixCriterion St = MatrixCriterion.Sterility, Sq = MatrixCriterion.Sequence,
                              Te = MatrixCriterion.Technique, Co = MatrixCriterion.Communication;
        const MistakeSeverity Mod = MistakeSeverity.Moderate, Sev = MistakeSeverity.Severe;

        return new List<ViolationRule>
        {
            R(ClinicalViolationType.Suboptimal_Dialogue, Co, Mod, "Introduce yourself as a student nurse, verify the patient's identity and explain each sensation before starting."),
            R(ClinicalViolationType.Inadequate_Dialogue, Co, Sev, "Obtain valid informed consent with a professional, empathetic and clear explanation of the procedure."),
            R(ClinicalViolationType.Tool_Handled_Before_Consent, Sq, Mod, "Talk to the mother and obtain informed consent before handling any instrument."),
            R(ClinicalViolationType.Reverse_Wipe_Direction, Te, Mod, "Wipe from top to bottom (anterior to posterior) only."),
            R(ClinicalViolationType.Out_Of_Order_Stroke, Sq, Mod, "Follow the 9-stroke anatomical sequence, one stroke at a time."),
            R(ClinicalViolationType.Reused_Cotton_Ball, St, Mod, "Use one fresh cotton ball per stroke and discard it in the waste basin."),
            R(ClinicalViolationType.One_Handed_Drape_Violation, Te, Mod, "Grab and unfold the sterile linen with both hands before placing it."),
            R(ClinicalViolationType.Out_Of_Order_Drape, Sq, Mod, "Drape in order: under the buttocks first, then the remaining zones."),
            R(ClinicalViolationType.Incorrect_Table_Slot, Te, Mod, "Place each instrument in its designated Mayo table slot."),
            R(ClinicalViolationType.Unsterile_Mayo_Placement_Violation, St, Sev, "Drape the Mayo stand with sterile linen before placing instruments on it."),
            R(ClinicalViolationType.Floor_Drop_Instrument, St, Mod, "Keep instruments and linen over the sterile field; return them to the tray."),
            R(ClinicalViolationType.Contaminated_Drop_Outside_Basin, St, Sev, "Discard used cotton only into the waste basin."),
            R(ClinicalViolationType.Forceps_Transfer_Error, Te, Mod, "Take cotton from the jar with the Pickup forceps only, transfer it to the Handling forceps, and wipe with the Handling forceps."),
        };
    }
}
