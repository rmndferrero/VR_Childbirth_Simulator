using UnityEngine;

// A data asset describing one wipe zone. Create instances via
// Assets > Create > PeriCare > Stroke Zone Definition.
// Values here are placeholders until the real clinical sequence is confirmed.
[CreateAssetMenu(menuName = "PeriCare/Stroke Zone Definition")]
public class StrokeZoneDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Human-readable name shown in logs, e.g. 'Zone A - Placeholder'.")]
    public string zoneName;

    [Tooltip("Position in the wipe sequence, starting at 0. Used to check cleanest-to-dirtiest order.")]
    public int expectedOrderIndex;

    [Header("Stroke Shape")]
    [Tooltip("Expected wipe direction in the shell's LOCAL space, e.g. (0, -1, 0) for top-to-bottom. Doesn't need to be exact yet - this is what we'll tune once the real technique is confirmed.")]
    public Vector3 idealDirectionLocal = Vector3.down;

    [Tooltip("How many degrees off the ideal direction is still considered acceptable.")]
    public float directionToleranceDegrees = 35f;

    [Tooltip("How much backward movement (in meters) along the ideal direction is tolerated before it's flagged as backtracking/scrubbing. Keep small - this exists mainly to absorb natural hand jitter.")]
    public float maxBacktrackDistance = 0.004f;
}