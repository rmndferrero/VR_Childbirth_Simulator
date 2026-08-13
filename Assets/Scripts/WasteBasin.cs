using UnityEngine;

public class WasteBasin : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("Drag your non-interactable DirtyCotton_Prop prefab here.")]
    public GameObject dirtyCottonPrefab;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object falling in is the interactable Cotton
        if (other.CompareTag("Cotton"))
        {
            // Spawn the dirty, non-interactable version at the exact position and rotation of the dropped cotton
            if (dirtyCottonPrefab != null)
            {
                Instantiate(dirtyCottonPrefab, other.transform.position, other.transform.rotation);
            }

            // Destroy the original interactable cotton ball to free up processing power
            Destroy(other.gameObject);

            Debug.Log("[WasteBasin] Cotton safely discarded and swapped for static prop.");
        }
    }
}