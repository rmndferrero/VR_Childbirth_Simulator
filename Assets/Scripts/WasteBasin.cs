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
            GameObject targetGo = cotton != null ? cotton.gameObject : other.gameObject;

            if (cotton != null)
            {
                cotton.isSafelyDiscarded = true;
                cotton.SetHolder(null);
                CottonState.activeCottons.Remove(cotton);
            }

            // 1. Force XRI SelectCancel on all interactors currently holding this interactable
            var grab = targetGo.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>()
                    ?? targetGo.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>()
                    ?? targetGo.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            if (grab != null && grab.interactionManager != null)
            {
                var selectingList = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting);
                foreach (var interactor in selectingList)
                {
                    if (interactor != null)
                    {
                        grab.interactionManager.SelectCancel(interactor, grab);
                    }
                }
            }

            // 2. Ensure all forceps sockets in the scene drop and clear any reference to this cotton
            var allForceps = FindObjectsByType<ForcepsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var f in allForceps)
            {
                if (f != null)
                {
                    f.DropCotton();
                }
            }

            if (audioSource != null && discardSound != null)
            {
                audioSource.PlayOneShot(discardSound);
            }

            // Spawn the dirty, non-interactable version if configured
            if (dirtyCottonPrefab != null)
            {
                Instantiate(dirtyCottonPrefab, targetGo.transform.position, targetGo.transform.rotation);
            }

            // Destroy the interactable cotton ball cleanly
            Destroy(targetGo);

            Debug.Log("[WasteBasin] Cotton safely released from forceps and discarded into waste basin (0 penalty).");
            EvaluationLogger.Instance.LogCorrect("Cotton disposed in waste basin", MatrixCriterion.Sterility);
        }
    }
}