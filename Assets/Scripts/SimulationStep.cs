using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct MistakeWeight
{
    public string wrongToolID;
    [Tooltip("Multiplier for the base penalty. E.g., 3.0 for sharp hazards.")]
    public float severityMultiplier;
    public string specificWarning;
}

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

    [Header("Decision Matrix Settings")]
    public string outOfSequenceWarning = "Incorrect timing. Please follow the clinical sequence.";
    public int basePenaltyPoints = 5;

    [Tooltip("Define specific consequences for specific wrong tools.")]
    public List<MistakeWeight> penaltyMatrix = new List<MistakeWeight>();
}