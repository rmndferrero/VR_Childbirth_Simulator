using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VRDemoGameManager : MonoBehaviour
{
    public static VRDemoGameManager Instance;

    [Header("Current Sequence")]
    public SimulationStep currentStep; // Optional: Used if you still want to track macro-scenario progress

    [Header("UI")]
    public TMP_Text feedbackText; // Your floating warning UI

    void Awake()
    {
        Instance = this;
    }

    // Called ONLY by SocketValidator when dropped into the CORRECT table slot
    public void ReportCorrectAction(SimulationStep step)
    {
        feedbackText.text = "Correct Placement! " + step.stepName + " completed.";

        // Logic to track how many tools are placed on the Mayo table goes here
    }

    // Called ONLY by SocketValidator when dropped into the WRONG table slot
    public void ShowWarning(string warning)
    {
        feedbackText.text = warning;
        // Optionally add logic to flash the text red or play an error sound
    }
}