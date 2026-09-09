using UnityEngine;

/// <summary>
/// Attached to each of the 9 user-placed Stroke Zone trigger colliders in the scene.
/// Detects cotton touches and forwards them directly to StrokeTrackingManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class StrokeZoneTrigger : MonoBehaviour
{
    [Tooltip("The data asset describing this zone's name, order, and anatomical landmark.")]
    public StrokeZoneDefinition zoneDefinition;

    [Tooltip("Reference to the StrokeTrackingManager in the scene.")]
    public StrokeTrackingManager manager;

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<StrokeTrackingManager>();
        
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        if (manager == null) manager = FindFirstObjectByType<StrokeTrackingManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        CottonState cotton = FindCotton(other);
        if (cotton == null) return;

        if (manager == null) manager = FindFirstObjectByType<StrokeTrackingManager>();
        if (manager != null)
        {
            manager.OnCottonEnterZone(cotton, zoneDefinition, transform.position);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        CottonState cotton = FindCotton(other);
        if (cotton == null) return;

        if (manager == null) manager = FindFirstObjectByType<StrokeTrackingManager>();
        if (manager != null)
        {
            manager.OnCottonStayInZone(cotton, zoneDefinition, other.transform.position);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CottonState cotton = FindCotton(other);
        if (cotton == null) return;

        if (manager == null) manager = FindFirstObjectByType<StrokeTrackingManager>();
        if (manager != null)
        {
            manager.OnCottonExitZone(cotton, zoneDefinition);
        }
    }

    private CottonState FindCotton(Collider col)
    {
        if (col == null) return null;
        CottonState cotton = col.GetComponent<CottonState>();
        if (cotton == null) cotton = col.GetComponentInParent<CottonState>();
        if (cotton == null) cotton = col.GetComponentInChildren<CottonState>();
        return cotton;
    }
}