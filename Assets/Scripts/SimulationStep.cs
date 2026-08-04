using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSimStep", menuName = "VR Nursing Sim/Simulation Step")]
public class SimulationStep : ScriptableObject
{
    [Header("Step Information")]
    public string stepName;
    [TextArea]
    public string instructionPrompt;
    public int sequenceOrder;

    [Header("Validation Rules (Rule-Based)")]
    public string expectedID;
    public string expectedSocketID;

    [Header("Decision Matrix Settings (Fallback)")]
    [Tooltip("Used only when GlobalHazardMatrix has no phase-specific entry for the offending tool.")]
    public string outOfSequenceWarning = "Incorrect timing. Please follow the clinical sequence.";
    public int basePenaltyPoints = 5;
}