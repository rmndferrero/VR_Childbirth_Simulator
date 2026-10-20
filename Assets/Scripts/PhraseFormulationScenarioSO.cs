using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining a complete clinical phrase formulation scenario.
/// Holds the available token pool, MCDM stage weights, evaluation threshold cutoffs,
/// and natural patient dialogue responses.
/// </summary>
[CreateAssetMenu(fileName = "NewPhraseScenario", menuName = "VR Simulator/Phrase Formulation Scenario")]
public class PhraseFormulationScenarioSO : ScriptableObject
{
    [Header("Scenario Information")]
    public string scenarioTitle = "Patient Informed Consent & Briefing";

    [Header("Decision Matrix Stage Weights (Must sum to 1.0)")]
    [Range(0f, 1f)] public float weightIntroduction = 0.25f;
    [Range(0f, 1f)] public float weightIdentification = 0.35f;
    [Range(0f, 1f)] public float weightExplanation = 0.40f;

    [Header("Evaluation Threshold Cutoffs (%)")]
    [Tooltip("Minimum normalized score percentage required for Optimal / Green tier (unlocks Step 2 with 0 penalty).")]
    public float greenThreshold = 80f;

    [Tooltip("Minimum normalized score percentage required for Suboptimal / Orange tier (unlocks Step 2 with minor penalty).")]
    public float orangeThreshold = 50f;

    [Header("Available Phrase Tokens Pool")]
    public List<DialoguePhraseTokenSO> availableTokens = new List<DialoguePhraseTokenSO>();

    [Header("Natural Patient Dialogue Responses")]
    [TextArea(1, 2)]
    public string optimalPatientResponse = "\"Thank you for explaining, nurse. I understand and am ready for the cleaning procedure.\"";

    [TextArea(1, 2)]
    public string suboptimalPatientResponse = "\"Okay nurse... please be gentle during the procedure.\"";

    [TextArea(1, 2)]
    public string inadequatePatientResponse = "\"Wait, who are you? What are you going to do to me? Please explain first!\"";
}
