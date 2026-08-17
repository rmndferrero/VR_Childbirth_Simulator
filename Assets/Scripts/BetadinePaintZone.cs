using UnityEngine;

public class BetadinePaintZone : MonoBehaviour
{
    [Header("Brush Settings")]
    [Tooltip("Drag your Betadine_Paint_Dab prefab here.")]
    public GameObject paintDabPrefab;

    [Tooltip("How far the cotton must move (in meters) to drop the next dot of paint.")]
    public float paintSpacing = 0.015f;

    private Vector3 lastPaintPosition;

    void Start()
    {
        // We want the shell to act as an invisible boundary, not a visible object.
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }
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

                // Check the distance. If it moved more than the spacing, drop paint!
                if (Vector3.Distance(currentPos, lastPaintPosition) > paintSpacing)
                {
                    DropPaintDab(currentPos);
                    lastPaintPosition = currentPos;
                }
            }
        }
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