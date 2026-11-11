using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-editable Rule-Based Knowledge Base for the 9-ball perineal cleansing procedure.
/// Pairs directly with PerinealRuleEngine (pure C# Inference Engine) and feeds structured violations
/// into DecisionMatrixEvaluator. Optional on StrokeTrackingManager: if left unassigned,
/// PerinealRuleConfig.CreateDefault() is used automatically.
/// </summary>
[CreateAssetMenu(fileName = "PerinealCareRuleSet", menuName = "VR Simulator/Perineal Care Rule Set")]
public class PerinealCareRuleSetSO : ScriptableObject
{
    [Header("Scaffolding & Hint Threshold")]
    [Min(1)]
    [Tooltip("Number of failed attempts on the current stage before the high-visibility warm-gold hint activates.")]
    public int mistakesRequiredForHint = 3;

    [Header("R2: Clean-to-Dirty Clinical Stage Rules")]
    [Tooltip("Equivalence stages in strict anatomical precedence. Within paired stages (2, 3, 4), either side may be done first.")]
    public List<PerinealStageRule> stageRules = new List<PerinealStageRule>();

    [Header("R3-R6: Per-Zone Kinematic & Directional Rules")]
    public List<PerinealZoneRuleConfig> zoneRules = new List<PerinealZoneRuleConfig>();

    private void Reset()
    {
        LoadClinicalDefaults();
    }

    private void OnValidate()
    {
        if (mistakesRequiredForHint < 1)
        {
            mistakesRequiredForHint = 1;
        }
    }

    [ContextMenu("Load Default 9-Stroke Clinical Rules")]
    public void LoadClinicalDefaults()
    {
        PerinealRuleConfig defaults = PerinealRuleConfig.CreateDefault();
        mistakesRequiredForHint = defaults.mistakesRequiredForHint;
        stageRules = defaults.stages != null
            ? new List<PerinealStageRule>(defaults.stages)
            : new List<PerinealStageRule>();
        zoneRules = defaults.zones != null
            ? new List<PerinealZoneRuleConfig>(defaults.zones)
            : new List<PerinealZoneRuleConfig>();
    }

    /// <summary>
    /// Converts this ScriptableObject into an immutable-intent runtime config for PerinealRuleEngine,
    /// falling back to PerinealRuleConfig.CreateDefault() for any unpopulated sections.
    /// </summary>
    public PerinealRuleConfig ToConfig()
    {
        PerinealRuleConfig fallback = PerinealRuleConfig.CreateDefault();
        return new PerinealRuleConfig
        {
            mistakesRequiredForHint = Mathf.Max(1, mistakesRequiredForHint),
            stages = stageRules != null && stageRules.Count > 0 ? stageRules.ToArray() : fallback.stages,
            zones = zoneRules != null && zoneRules.Count > 0 ? zoneRules.ToArray() : fallback.zones
        };
    }
}
