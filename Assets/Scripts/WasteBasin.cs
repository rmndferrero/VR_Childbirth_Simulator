using UnityEngine;

/// <summary>
/// Bedside / Table waste basin for safely discarding used cotton balls.
/// Features:
/// - Detects discarded cotton balls and marks them safely discarded (0 drop penalty).
/// - Modular design: attaches to Kidney Basin or acts as an invisible drop zone.
/// - Does not generate raw primitive meshes at runtime.
/// </summary>
public class WasteBasin : MonoBehaviour
{
    [Header("Modular Visuals")]
    [Tooltip("Optional 3D Trashbin / Basin model reference (if null, acts as clean invisible trigger zone).")]
    public GameObject visualMeshSlot;

    [Header("Spawning & Audio")]
    [Tooltip("Optional non-interactable DirtyCotton_Prop prefab to stay inside the basin.")]
    public GameObject dirtyCottonPrefab;
    public AudioSource audioSource;
    public AudioClip discardSound;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        EnsureTriggerCollider();
    }

    public void EnsureTriggerCollider()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        if (col.size == Vector3.one || col.size == Vector3.zero)
        {
            col.size = new Vector3(0.30f, 0.25f, 0.30f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        CottonState cotton = other.GetComponent<CottonState>()
                          ?? other.GetComponentInParent<CottonState>()
                          ?? other.GetComponentInChildren<CottonState>();

        if (cotton != null || other.CompareTag("Cotton") || other.gameObject.name.ToLower().Contains("cotton"))
        {
            if (cotton != null)
            {
                cotton.isSafelyDiscarded = true;
            }

            if (audioSource != null && discardSound != null)
            {
                audioSource.PlayOneShot(discardSound);
            }

            // Spawn the dirty, non-interactable version if configured
            if (dirtyCottonPrefab != null)
            {
                Instantiate(dirtyCottonPrefab, other.transform.position, other.transform.rotation);
            }

            // Destroy the interactable cotton ball cleanly
            Destroy(other.gameObject);

            Debug.Log("[WasteBasin] Cotton safely discarded into waste basin (0 penalty).");
        }
    }
}