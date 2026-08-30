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
    private Dictionary<StrokeZoneDefinition, Collider> zoneColliderCache = new Dictionary<StrokeZoneDefinition, Collider>();

    public event System.Action<int, StrokeZoneDefinition> OnStrokeAdvanced;
    public event System.Action<string, bool> OnStrokeValidated;

    private void Awake()
    {
        CacheZoneColliders();
    }

    private void CacheZoneColliders()
    {
        zoneColliderCache.Clear();
        var triggers = FindObjectsOfType<StrokeZoneTrigger>(true);
        foreach (var trig in triggers)
        {
            if (trig.zoneDefinition != null)
            {
                var col = trig.GetComponent<Collider>();
                if (col != null)
                {
                    zoneColliderCache[trig.zoneDefinition] = col;
                }
            }
        }
    }

    public void ResetForPhase(AntisepticType phaseAntiseptic)
    {
        currentPhaseAntiseptic = phaseAntiseptic;
        nextExpectedOrderIndex = 0;
        completedZones.Clear();
        CacheZoneColliders();
        Debug.Log($"[StrokeTrackingManager] Reset for phase: {phaseAntiseptic}");

        if (allZonesInOrder != null && allZonesInOrder.Count > 0)
        {
            OnStrokeAdvanced?.Invoke(nextExpectedOrderIndex, allZonesInOrder[0]);
        }
    }

    public StrokeZoneDefinition GetCurrentActiveZone()
    {
        if (allZonesInOrder != null && nextExpectedOrderIndex >= 0 && nextExpectedOrderIndex < allZonesInOrder.Count)
        {
            return allZonesInOrder[nextExpectedOrderIndex];
        }
        return null;
    }

    public int GetCurrentStrokeIndex()
    {
        return nextExpectedOrderIndex;
    }

    /// <summary>
    /// Checks if a paint contact point is within the CURRENT active expected stroke zone.
    /// </summary>
    public bool ValidatePaintPoint(Vector3 point, out StrokeZoneDefinition currentTargetZone)
    {
        currentTargetZone = GetCurrentActiveZone();
        if (currentTargetZone == null) return true; // No active zone restrictions

        if (zoneColliderCache.Count == 0) CacheZoneColliders();

        if (zoneColliderCache.TryGetValue(currentTargetZone, out Collider targetCol))
        {
            if (targetCol != null)
            {
                // Check if point is inside or very close to the active zone's collider bounds
                if (targetCol.bounds.Contains(point) || Vector3.Distance(point, targetCol.bounds.ClosestPoint(point)) < 0.02f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float activeStrokeProgress = 0f;
    private Vector3 lastCottonPos = Vector3.zero;

    public void OnCottonEnterZone(CottonState cotton, StrokeZoneDefinition zone)
    {
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
        lastCottonPos = cotton.transform.position;
        activeStrokeProgress = 0f;

        Debug.Log($"[StrokeTracking] Cotton entered zone: {zone.zoneName}");
    }

    public void OnCottonStayInZone(CottonState cotton, StrokeZoneDefinition zone, Vector3 worldPos)
    {
        if (cotton.currentZone != zone) return;
        cotton.currentStrokePath.Add(worldPos);

        // Motion progression check: is cotton swiping downward in the active zone?
        StrokeZoneDefinition expectedZone = GetCurrentActiveZone();
        if (zone == expectedZone)
        {
            Vector3 movement = worldPos - lastCottonPos;
            lastCottonPos = worldPos;

            float downwardStep = Vector3.Dot(movement, zone.idealDirectionLocal);
            if (downwardStep > 0.0005f) // Moving in expected downward direction
            {
                activeStrokeProgress += downwardStep * 15f; // Accumulate swipe progress
                if (activeStrokeProgress >= 0.85f && !completedZones.Contains(zone))
                {
                    CompleteActiveStroke(cotton, zone);
                }
            }
        }
    }

    public void OnCottonExitZone(CottonState cotton, StrokeZoneDefinition zone)
    {
        if (cotton.currentZone != zone) return;

        if (!completedZones.Contains(zone))
        {
            if (activeStrokeProgress >= 0.45f)
            {
                CompleteActiveStroke(cotton, zone);
            }
            else
            {
                Debug.Log($"[StrokeTracking] {zone.zoneName}: Stroke incomplete ({activeStrokeProgress:P0}).");
            }
        }

        // One cotton = one zone = one stroke rule
        cotton.isUsed = true;
        cotton.currentZone = null;
        activeStrokeProgress = 0f;
    }

    private void CompleteActiveStroke(CottonState cotton, StrokeZoneDefinition zone)
    {
        completedZones.Add(zone);
        nextExpectedOrderIndex++;

        Debug.Log($"[StrokeTracking] {zone.zoneName}: Stroke SUCCESSFUL ({completedZones.Count}/{allZonesInOrder.Count}).");
        OnStrokeValidated?.Invoke($"✓ {zone.zoneName} Completed!", true);

        if (nextExpectedOrderIndex < allZonesInOrder.Count)
        {
            OnStrokeAdvanced?.Invoke(nextExpectedOrderIndex, allZonesInOrder[nextExpectedOrderIndex]);
        }

        // Check if all 9 zones completed
        if (completedZones.Count >= allZonesInOrder.Count && allZonesInOrder.Count > 0)
        {
            Debug.Log("[StrokeTracking] All 9 stroke zones successfully completed!");
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
        OnStrokeValidated?.Invoke($"Warning: {message}", false);
    }
}