using UnityEngine;

[CreateAssetMenu(fileName = "NewSimStep", menuName = "VR Nursing Sim/Simulation Step")]
public class SimulationStep : ScriptableObject
{
    [Header("Step Information")]
    public string stepName;
    [TextArea]
    public string instructionPrompt;

    [Header("Validation Rules (Rule-Based)")]
    public string expectedID;
    public string expectedSocketID;

    [Header("Decision Matrix Settings")]
    public string outOfSequenceWarning = "Incorrect timing. Please follow the clinical sequence.";
    public int penaltyPoints = 5; // Default deduction
}