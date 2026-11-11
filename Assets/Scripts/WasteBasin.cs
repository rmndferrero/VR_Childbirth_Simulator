using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interior disposal zone for the Trash Can / Waste Basin.
/// Strictly negates the cotton ball drop penalty when cotton is dropped inside the bin.
/// NEVER interferes with, cancels, or modifies ForcepsController or the player's grip.
/// </summary>
public class WasteBasin : MonoBehaviour
{
    public static event Action<WasteBasin, CottonState> OnCottonDisposed;
    [Header("Modular Visuals")]
    [Tooltip("Optional 3D Trashbin / Basin model reference (if null, acts as clean invisible trigger zone).")]
    public GameObject visualMeshSlot;

    [Header("Spawning & Audio")]
    [Tooltip("Optional non-interactable DirtyCotton_Prop prefab to stay inside the basin.")]
    public GameObject dirtyCottonPrefab;
    public AudioSource audioSource;
    public AudioClip discardSound;

    private readonly HashSet<int> disposedCottonIds = new HashSet<int>();

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
            col.size = new Vector3(0.24f, 0.28f, 0.24f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCottonInZone(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleCottonInZone(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        CottonState cotton = other.GetComponent<CottonState>() ?? other.GetComponentInParent<CottonState>();
        if (cotton != null && cotton.currentHolder != null && !disposedCottonIds.Contains(cotton.GetInstanceID()))
        {
            // Player pulled the held cotton back out of the bin without releasing it
            cotton.isSafelyDiscarded = false;
        }
    }

    private void HandleCottonInZone(Collider other)
    {
        if (other == null) return;

        // Strictly check for CottonState component only (never match 'CottonSocket' on the forceps!)
        CottonState cotton = other.GetComponent<CottonState>() ?? other.GetComponentInParent<CottonState>();
        if (cotton == null) return;

        int id = cotton.GetInstanceID();

        // Mark safely inside bin so CottonState's drop timer is negated while inside the interior trigger
        cotton.MarkSafelyDiscardedInBin();

        // Only finalize disposal once the player actually releases/drops the cotton ball (never force-cancel forceps!)
        if (cotton.currentHolder == null && !disposedCottonIds.Contains(id))
        {
            disposedCottonIds.Add(id);
            CottonState.activeCottons.Remove(cotton);

            if (audioSource != null && discardSound != null)
            {
                audioSource.PlayOneShot(discardSound);
            }

            if (dirtyCottonPrefab != null)
            {
                Instantiate(dirtyCottonPrefab, cotton.transform.position, cotton.transform.rotation);
            }

            Debug.Log("[WasteBasin] Cotton ball properly disposed inside trash bin (0 penalty, forceps untouched).");
            EvaluationLogger.Instance.LogCorrect("Cotton disposed in waste basin", MatrixCriterion.Sterility);
            OnCottonDisposed?.Invoke(this, cotton);

            if (Application.isPlaying)
            {
                Destroy(cotton.gameObject, 0.20f);
            }
            else
            {
                DestroyImmediate(cotton.gameObject);
            }
        }
    }
}
