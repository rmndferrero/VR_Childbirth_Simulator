using UnityEngine;

public class BetadinePaintZone : MonoBehaviour
{
    [Header("Brush Settings")]
    [Tooltip("Drag your Betadine_Paint_Dab prefab here.")]
    public GameObject paintDabPrefab;

    [Tooltip("How far the cotton must move (in meters) to drop the next dot of paint.")]
    public float paintSpacing = 0.04f;

    private Vector3 lastPaintPosition;
    private System.Collections.Generic.List<Vector3> placedDabs = new System.Collections.Generic.List<Vector3>();

    void Start()
    {
        // Hide the shell in gameplay so it blends with the mother model
        Renderer meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Cotton"))
        {
            CottonState cotton = other.GetComponent<CottonState>();

            // ONLY paint if the cotton is soaked AND has not been used yet
            if (cotton != null && cotton.isSoaked && !cotton.isUsed)
            {
                Vector3 currentPos = other.transform.position;

                bool isTooClose = false;
                foreach (Vector3 pos in placedDabs)
                {
                    if (Vector3.Distance(currentPos, pos) < paintSpacing)
                    {
                        isTooClose = true;
                        break;
                    }
                }

                if (!isTooClose)
                {
                    DropPaintDab(currentPos);
                    lastPaintPosition = currentPos;
                    placedDabs.Add(currentPos);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Cotton"))
        {
            CottonState cotton = other.GetComponent<CottonState>();

            // The moment the soaked cotton is lifted off the skin, it becomes dirty/used.
            // It can no longer paint, enforcing the "One Swipe Per Cotton" rule.
            if (cotton != null && cotton.isSoaked && lastPaintPosition != Vector3.zero)
            {
                cotton.isUsed = true;
                lastPaintPosition = Vector3.zero; // Reset for the NEXT fresh cotton ball
                Debug.Log("[BetadinePaintZone] Stroke finished. Cotton is now dirty and cannot be used again.");
            }
        }
    }

    private void DropPaintDab(Vector3 position)
    {
        if (paintDabPrefab == null) return;

        GameObject newDecal = Instantiate(paintDabPrefab, position, Quaternion.identity);
        newDecal.transform.LookAt(transform.position);
        newDecal.transform.SetParent(transform);
    }
}