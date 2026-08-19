using UnityEngine;

// Attach this to each zone's trigger collider GameObject (e.g. Zone_A, Zone_B, Zone_C).
// Requires a Collider component set to "Is Trigger" on the same object.
public class StrokeZoneTrigger : MonoBehaviour
{
    [Tooltip("The data asset describing this zone's name, order, and ideal wipe direction.")]
    public StrokeZoneDefinition zoneDefinition;

    [Tooltip("Drag the scene's StrokeTrackingManager here.")]
    public StrokeTrackingManager manager;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Cotton")) return;

        CottonState cotton = other.GetComponent<CottonState>();
        if (cotton != null) manager.OnCottonEnterZone(cotton, zoneDefinition);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Cotton")) return;

        CottonState cotton = other.GetComponent<CottonState>();
        if (cotton != null) manager.OnCottonStayInZone(cotton, zoneDefinition, other.transform.position);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Cotton")) return;

        CottonState cotton = other.GetComponent<CottonState>();
        if (cotton != null) manager.OnCottonExitZone(cotton, zoneDefinition);
    }
}