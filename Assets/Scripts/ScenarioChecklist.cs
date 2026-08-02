using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewScenarioChecklist", menuName = "VR Simulator/Scenario Checklist")]
public class ScenarioChecklist : ScriptableObject
{
    public string scenarioName;
    public List<SimulationStep> steps = new List<SimulationStep>();
}