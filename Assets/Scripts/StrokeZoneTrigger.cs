using UnityEngine;

/// <summary>
/// Legacy trigger component retained for backwards compatibility with existing scene objects.
/// All stroke tracking and visual calculations are now handled directly by StrokeSliderTrack.
/// </summary>
public class StrokeZoneTrigger : MonoBehaviour
{
    public StrokeZoneDefinition zoneDefinition;
}