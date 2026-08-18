using UnityEngine;
using System.Collections.Generic;

public class BetadinePaintZone : MonoBehaviour
{
    [Header("Brush Settings")]
    [Tooltip("Drag your Betadine_Paint_Dab prefab here.")]
    public GameObject paintDabPrefab;

    [Tooltip("How far the cotton must move (in meters) to drop the next dot of paint.")]
    public float paintSpacing = 0.015f;

    // Tracks which grid cells have already been painted so strokes don't stack/darken
    // when the player passes back over an already-painted area.
    private HashSet<Vector3Int> paintedCells = new HashSet<Vector3Int>();

    void Start()
    {
        // We want the shell to act as an invisible boundary, not a visible object.
        // NOTE: this shell uses a Skinned Mesh Renderer, not a regular Mesh Renderer,
        // so we grab the base Renderer type to make sure it's actually found and disabled.
        Renderer meshRenderer = GetComponent<Renderer>();
    }

    private void OnTriggerStay(Collider other)
    {
        // Only react to the Cotton
        if (other.CompareTag("Cotton"))
        {
            CottonState cotton = other.GetComponent<CottonState>();

            // Only paint if the cotton is actually soaked in Betadine
            if (cotton != null && cotton.isSoaked)
            {
                Vector3 currentPos = other.transform.position;
                Vector3Int cell = WorldToCell(currentPos);

                // Only paint if this grid cell hasn't been painted before.
                // This alone both spaces out dabs AND stops overlapping/backtracking
                // strokes from stacking opacity - no separate distance check needed.
                if (!paintedCells.Contains(cell))
                {
                    DropPaintDab(currentPos);
                    paintedCells.Add(cell);
                }
            }
        }
    }

    // Quantizes world position into a grid cell sized to the brush spacing.
    // Using local space (relative to the shell) keeps cells stable even if the mother/shell moves.
    private Vector3Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float cellSize = paintSpacing;
        return new Vector3Int(
            Mathf.RoundToInt(local.x / cellSize),
            Mathf.RoundToInt(local.y / cellSize),
            Mathf.RoundToInt(local.z / cellSize)
        );
    }

    private void DropPaintDab(Vector3 position)
    {
        if (paintDabPrefab == null) return;

        // 1. Spawn the paint dab at the cotton's exact location
        GameObject newDecal = Instantiate(paintDabPrefab, position, Quaternion.identity);

        // 2. Aim the projector inward toward the center of the shell
        // This ensures the decal projects directly down onto the mother's skin
        newDecal.transform.LookAt(transform.position);

        // 3. Parent it to the shell so if the mother moves, the paint moves with her
        newDecal.transform.SetParent(transform);
    }
}