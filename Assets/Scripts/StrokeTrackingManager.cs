using UnityEngine;
using System.Collections.Generic;

// One instance of this lives in the scene (e.g. on an empty "StrokeTrackingManager" GameObject).
// All StrokeZoneTrigger components reference it.
public class StrokeTrackingManager : MonoBehaviour
{
    [Tooltip("All zone definitions, listed in the correct cleanest-to-dirtiest order. With 3 placeholder zones for now.")]
    public List<StrokeZoneDefinition> allZonesInOrder;

    private int nextExpectedOrderIndex = 0;
    private HashSet<StrokeZoneDefinition> completedZones = new HashSet<StrokeZoneDefinition>();

    public void OnCottonEnterZone(CottonState cotton, StrokeZoneDefinition zone)
    {
        // Rule: only a soaked, unused cotton ball can start a valid stroke -
        // matches the same gate BetadinePaintZone uses for painting.
        if (cotton.isUsed)
        {
            ReportViolation(zone, "Reused/dirty cotton ball entered a zone - should have been discarded after its first stroke.");
            return;
        }

        if (!cotton.isSoaked)
        {
            ReportViolation(zone, "Dry cotton entered a zone - should be soaked in Betadine first.");
            return;
        }

        cotton.currentZone = zone;
        cotton.currentStrokePath.Clear();
        cotton.currentStrokePath.Add(cotton.transform.position);

        Debug.Log($"[StrokeTracking] Cotton entered zone: {zone.zoneName}");
    }

    public void OnCottonStayInZone(CottonState cotton, StrokeZoneDefinition zone, Vector3 worldPos)
    {
        // Ignore stray contact from a different zone's collider overlapping slightly
        if (cotton.currentZone != zone) return;

        cotton.currentStrokePath.Add(worldPos);
    }

    public void OnCottonExitZone(CottonState cotton, StrokeZoneDefinition zone)
    {
        if (cotton.currentZone != zone) return;

        EvaluateStroke(cotton, zone);

        // One cotton = one zone = one stroke. Mark it dirty the moment it leaves THIS zone,
        // not just when it leaves the outer shell - this is the same isUsed flag
        // BetadinePaintZone checks, so painting stops for this cotton too from here on.
        cotton.isUsed = true;
        cotton.currentZone = null;
    }

    private void EvaluateStroke(CottonState cotton, StrokeZoneDefinition zone)
    {
        List<Vector3> path = cotton.currentStrokePath;

        if (path.Count < 2)
        {
            Debug.Log($"[StrokeTracking] {zone.zoneName}: stroke too brief to evaluate, ignoring.");
            return;
        }

        Vector3 actualDirection = (path[path.Count - 1] - path[0]).normalized;
        float angle = Vector3.Angle(actualDirection, zone.idealDirectionLocal);

        bool directionOk = angle <= zone.directionToleranceDegrees;
        if (!directionOk)
        {
            ReportViolation(zone, $"Wrong stroke direction ({angle:F0}\u00b0 off expected).");
        }

        bool backtrackDetected = false;
        float maxProjected = float.MinValue;
        for (int i = 0; i < path.Count; i++)
        {
            float projected = Vector3.Dot(path[i] - path[0], zone.idealDirectionLocal);
            if (projected < maxProjected - zone.maxBacktrackDistance)
            {
                backtrackDetected = true;
                break;
            }
            maxProjected = Mathf.Max(maxProjected, projected);
        }
        if (backtrackDetected)
        {
            ReportViolation(zone, "Backtracking / scrubbing motion detected.");
        }

        bool orderOk = zone.expectedOrderIndex == nextExpectedOrderIndex;
        if (!orderOk)
        {
            ReportViolation(zone, $"Cleaned out of order (expected step {nextExpectedOrderIndex + 1}, this was step {zone.expectedOrderIndex + 1}).");
        }
        else
        {
            nextExpectedOrderIndex++;
        }

        completedZones.Add(zone);

        if (directionOk && !backtrackDetected && orderOk)
        {
            Debug.Log($"[StrokeTracking] {zone.zoneName}: stroke OK.");
        }
    }

    // Call this once the procedure is considered finished (e.g. player presses a "done" button)
    public List<string> GetMissedZones()
    {
        List<string> missed = new List<string>();
        foreach (StrokeZoneDefinition zone in allZonesInOrder)
        {
            if (!completedZones.Contains(zone))
                missed.Add(zone.zoneName);
        }
        return missed;
    }

    private void ReportViolation(StrokeZoneDefinition zone, string message)
    {
        Debug.LogWarning($"[StrokeTracking] {zone.zoneName}: {message}");
        // TODO once this is validated: route this into GlobalHazardMatrix instead of just logging,
        // matching how Mayo Table / Assessment phase penalties are recorded.
    }
}