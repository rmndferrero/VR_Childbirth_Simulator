using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewScenarioChecklist", menuName = "VR Nursing Sim/Scenario Checklist")]
public class ScenarioChecklist : ScriptableObject
{
    public string scenarioName;
    public List<SimulationStep> steps = new List<SimulationStep>();
}