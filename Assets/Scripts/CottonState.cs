using UnityEngine;
using System.Collections.Generic;

public class CottonState : MonoBehaviour
{
    [Tooltip("Is this cotton ball soaked in Betadine?")]
    public bool isSoaked = false;

    [Tooltip("Has this cotton ball already touched the skin? Shared by both painting and stroke-zone tracking, so a cotton counts as 'spent' the moment either system marks it used.")]
    public bool isUsed = false;

    [Header("Stroke Tracking")]
    [Tooltip("World-space positions recorded while this cotton is inside its current zone. Cleared each time it enters a new zone.")]
    [HideInInspector] public List<Vector3> currentStrokePath = new List<Vector3>();

    [Tooltip("Which zone this cotton is currently recording a stroke for, if any.")]
    [HideInInspector] public StrokeZoneDefinition currentZone;

    [Header("Forceps Tracking")]
    [Tooltip("Which forceps (Pickup or Handling) currently holds this cotton, if any. Null means it's not held by either.")]
    [HideInInspector] public ForcepsRole? currentHolder = null;

    private MeshRenderer meshRenderer;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    // Central place to change/log who's holding this cotton, so every transfer
    // (pickup from jar, steal between forceps, drop) is tracked consistently.
    public void SetHolder(ForcepsRole? newHolder)
    {
        if (currentHolder == newHolder) return;

        string from = currentHolder.HasValue ? currentHolder.Value.ToString() : "None";
        string to = newHolder.HasValue ? newHolder.Value.ToString() : "None";
        Debug.Log($"[CottonState] Holder changed: {from} -> {to}");

        currentHolder = newHolder;
    }

    public void SoakCotton(Material soakedMaterial)
    {
        if (!isSoaked && meshRenderer != null && soakedMaterial != null)
        {
            meshRenderer.material = soakedMaterial;
            isSoaked = true;
            Debug.Log("[CottonState] Cotton is now soaked in Betadine!");
        }
    }
}