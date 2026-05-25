using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueChoice
{
    [TextArea(2, 4)]
    public string choiceText;

    [Tooltip("Points to deduct. 0 for Best, 5 for Acceptable/Good, 10 for Wrong.")]
    public int penaltyPoints;
}

[System.Serializable]
public class DialogueNode
{
    [Tooltip("For internal organization only (e.g., '1st selection: Introduction'). Not shown in UI.")]
    public string nodeName;
    public List<DialogueChoice> choices = new List<DialogueChoice>();
}

[CreateAssetMenu(fileName = "New Dialogue Scenario", menuName = "VR Simulator/Dialogue Scenario")]
public class DialogueScenario : ScriptableObject
{
    public string scenarioTitle;
    public List<DialogueNode> dialogueSequence;
}