using UnityEngine;
using System.Collections.Generic;

public class StrokeTrackingManager : MonoBehaviour
{
    [Header("Sequence Configuration")]
    [Tooltip("All 9 zone definitions listed in the clinical order.")]
    public List<StrokeZoneDefinition> allZonesInOrder;

    private int nextExpectedOrderIndex = 0;
    private HashSet<StrokeZoneDefinition> completedZones = new HashSet<StrokeZoneDefinition>();
    private AntisepticType currentPhaseAntiseptic = AntisepticType.Iodine_7_5_Scrub;

    public void ResetForPhase(AntisepticType phaseAntiseptic)
    {
        currentPhaseAntiseptic = phaseAntiseptic;
        nextExpectedOrderIndex = 0;
        completedZones.Clear();
        Debug.Log($"[StrokeTrackingManager] Reset for phase: {phaseAntiseptic}");
    }

    public void OnCottonEnterZone(CottonState cotton, StrokeZoneDefinition zone)
    {
        // Rule: Only a soaked, unused cotton ball held by Handling Forceps can start a stroke
        if (cotton.isUsed)
        {
            ReportViolation(zone, "Reused/dirty cotton ball entered a zone - must discard cotton after each stroke.");
            return;
        }

        if (!cotton.isSoaked)
        {
            ReportViolation(zone, "Dry cotton entered a zone - cotton must be soaked in antiseptic first.");
            return;
        }

        if (cotton.antisepticType != currentPhaseAntiseptic)
        {
            ReportViolation(zone, $"Wrong antiseptic solution for this phase (Expected {currentPhaseAntiseptic}, used {cotton.antisepticType}).");
            return;
        }

        if (cotton.currentHolder != ForcepsRole.Handling)
        {
            ReportViolation(zone, "Only Handling Forceps may perform strokes on the patient!");
            return;
        }

        cotton.currentZone = zone;
        cotton.currentStrokePath.Clear();
        cotton.currentStrokePath.Add(cotton.transform.position);

        Debug.Log($"[StrokeTracking] Cotton entered zone: {zone.zoneName}");
    }

    public void OnCottonStayInZone(CottonState cotton, StrokeZoneDefinition zone, Vector3 worldPos)
    {
        if (cotton.currentZone != zone) return;
        cotton.currentStrokePath.Add(worldPos);
    }

    public void OnCottonExitZone(CottonState cotton, StrokeZoneDefinition zone)
    {
        if (cotton.currentZone != zone) return;

        EvaluateStroke(cotton, zone);

        // One cotton = one zone = one stroke rule
        cotton.isUsed = true;
        cotton.currentZone = null;
    }

    private void EvaluateStroke(CottonState cotton, StrokeZoneDefinition zone)
    {
        List<Vector3> path = cotton.currentStrokePath;

        if (path.Count < 2)
        {
            Debug.Log($"[StrokeTracking] {zone.zoneName}: Stroke too brief, ignoring.");
            return;
        }

        // 1. Direction Check (Ideal direction is downward / local direction)
        Vector3 actualDirection = (path[path.Count - 1] - path[0]).normalized;
        float angle = Vector3.Angle(actualDirection, zone.idealDirectionLocal);

        bool directionOk = angle <= zone.directionToleranceDegrees;
        if (!directionOk)
        {
            ReportViolation(zone, $"Wrong stroke direction on {zone.zoneName} ({angle:F0}\u00b0 off expected downward direction).");
        }

        // 2. Backtrack / Scrubbing Check
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
            ReportViolation(zone, $"Backtracking / scrubbing motion detected on {zone.zoneName}. Always stroke in one continuous direction.");
        }

        // 3. Order Check (Cleanest to Dirtiest sequence)
        bool orderOk = zone.expectedOrderIndex == nextExpectedOrderIndex;
        if (!orderOk)
        {
            ReportViolation(zone, $"Cleaned out of sequence (expected Step {nextExpectedOrderIndex + 1}: {GetZoneNameByIndex(nextExpectedOrderIndex)}, cleaned {zone.zoneName}).");
        }
        else
        {
            nextExpectedOrderIndex++;
        }

        completedZones.Add(zone);

        if (directionOk && !backtrackDetected && orderOk)
        {
            Debug.Log($"[StrokeTracking] {zone.zoneName}: Stroke SUCCESSFUL ({completedZones.Count}/{allZonesInOrder.Count}).");
        }

        // Check if all 9 zones completed
        if (completedZones.Count >= allZonesInOrder.Count && allZonesInOrder.Count > 0)
        {
            Debug.Log("[StrokeTracking] All stroke zones successfully completed for this phase!");
            if (PerinealCareManager.Instance != null)
            {
                if (currentPhaseAntiseptic == AntisepticType.Iodine_7_5_Scrub)
                {
                    PerinealCareManager.Instance.On7_5ScrubCompleted();
                }
                else if (currentPhaseAntiseptic == AntisepticType.Iodine_10_Paint)
                {
                    PerinealCareManager.Instance.On10PaintCompleted();
                }
            }
        }
    }

    private string GetZoneNameByIndex(int index)
    {
        if (allZonesInOrder != null && index >= 0 && index < allZonesInOrder.Count && allZonesInOrder[index] != null)
        {
            return allZonesInOrder[index].zoneName;
        }
        return $"Zone {index + 1}";
    }

    public List<string> GetMissedZones()
    {
        List<string> missed = new List<string>();
        foreach (StrokeZoneDefinition zone in allZonesInOrder)
        {
            if (zone != null && !completedZones.Contains(zone))
                missed.Add(zone.zoneName);
        }
        return missed;
    }

    private void ReportViolation(StrokeZoneDefinition zone, string message)
    {
        Debug.LogWarning($"[StrokeTracking Violation] {zone?.zoneName}: {message}");
        if (PerinealCareManager.Instance != null)
        {
            PerinealCareManager.Instance.RecordClinicalViolation(message, 5);
        }
    }
}