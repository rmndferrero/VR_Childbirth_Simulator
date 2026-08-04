using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct PhasePenalty
{
    [Tooltip("The phase this applies to. 1 = Mayo Prep, 2 = Dialogue, 3 = Perineal Cleaning")]
    public int phaseIndex;

    [Tooltip("Points deducted if the tool is grabbed/placed during this specific phase.")]
    public int penaltyPoints;

    [TextArea(2, 3)]
    public string specificWarning;
}

[System.Serializable]
public struct HazardMatrixEntry
{
    public string wrongToolID;

    [Tooltip("List the varying penalties for this tool across different phases.")]
    public List<PhasePenalty> phasePenalties;
}

[CreateAssetMenu(fileName = "NewGlobalHazardMatrix", menuName = "VR Simulator/Global Hazard Matrix")]
public class GlobalHazardMatrix : ScriptableObject
{
    [Header("Phase-Aware Decision Matrix")]
    public List<HazardMatrixEntry> globalHazards = new List<HazardMatrixEntry>();

    /// <summary>
    /// The Decision Engine: Compares an incoming tool ID and the current phase against the matrix.
    /// Returns true if a match is found for that specific phase, outputting the exact penalty and warning.
    /// </summary>
    public bool EvaluateHazard(string incomingToolID, int currentPhase, out int penaltyPoints, out string specificWarning)
    {
        string formattedID = incomingToolID.Trim().ToLower();

        // Default out values
        penaltyPoints = 0;
        specificWarning = "";

        foreach (var hazard in globalHazards)
        {
            if (hazard.wrongToolID.Trim().ToLower() == formattedID)
            {
                // The tool is a known hazard. Now, search for the penalty linked to the current phase.
                foreach (var phaseData in hazard.phasePenalties)
                {
                    if (phaseData.phaseIndex == currentPhase)
                    {
                        // Match found for this specific phase!
                        penaltyPoints = phaseData.penaltyPoints;
                        specificWarning = phaseData.specificWarning;
                        return true;
                    }
                }

                // If the tool is grabbed in a phase not listed, it's not penalized globally.
                return false;
            }
        }

        // Tool not found in the global hazard list
        return false;
    }
}