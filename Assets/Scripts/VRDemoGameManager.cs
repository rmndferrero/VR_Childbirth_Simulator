using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VRDemoGameManager : MonoBehaviour
{
    public static VRDemoGameManager Instance;

    [Header("Current Sequence")]
    public SimulationStep currentStep; // The ScriptableObject dictating the current phase

    [Header("UI")]
    public TMP_Text feedbackText; // Your floating warning UI

    void Awake()
    {
        Instance = this;
    }

    // Called instantly when a Tool is grabbed (Tier 1 Check)
    public void CheckTool(ToolItem tool)
    {
        if (currentStep == null) return;

        if (tool.toolID == currentStep.expectedID)
        {
            tool.MarkCorrect();
            feedbackText.text = "Correct tool selected. Please place it in the designated zone.";
        }
        else
        {
            tool.MarkWrong();
            ShowWarning(currentStep.outOfSequenceWarning);
        }
    }

    // Called by SocketValidator when dropped into the correct table slot (Tier 2 Check)
    public void ReportCorrectAction(SimulationStep step)
    {
        // Add score, update UI, advance to next checklist item
        feedbackText.text = "Correct Placement! " + step.stepName + " completed.";

        // TODO: Logic to load the next SimulationStep in the scenario goes here
    }

    public void ShowWarning(string warning)
    {
        feedbackText.text = warning;
        // Optionally add logic here to flash the text red or play an error sound
    }
}