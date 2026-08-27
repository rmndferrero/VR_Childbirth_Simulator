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

    private Collider myCollider;
    private CottonState[] allCottons;

    private void Start()
    {
        myCollider = GetComponent<Collider>();
        paintSpacing = 0.012f; // Force a very small spacing so the stroke is completely solid and smooth
    }

    private void Update()
    {
        if (Time.frameCount % 30 == 0 || allCottons == null)
        {
            allCottons = FindObjectsOfType<CottonState>();
        }

        foreach (CottonState cotton in allCottons)
        {
            if (cotton == null) continue;

            if (cotton.isSoaked)
            {
                Collider[] hits = Physics.OverlapSphere(cotton.transform.position, 0.04f);
                bool isTouching = false;
                foreach (Collider hit in hits)
                {
                    if (hit == myCollider)
                    {
                        isTouching = true;
                        break;
                    }
                }

                if (isTouching)
                {
                    Vector3 center = transform.position; 
                    Vector3 dirToCenter = (center - cotton.transform.position).normalized;
                    
                    Ray ray = new Ray(cotton.transform.position - dirToCenter * 0.05f, dirToCenter);
                    
                    if (myCollider.Raycast(ray, out RaycastHit rayHit, 0.2f))
                    {
                        HandleTouch(cotton.gameObject, rayHit.point, rayHit.normal);
                    }
                    else
                    {
                        HandleTouch(cotton.gameObject, cotton.transform.position, dirToCenter * -1f);
                    }
                }
                else
                {
                    HandleTouchExit(cotton.gameObject);
                }
            }
        }
    }

    private void HandleTouch(GameObject obj, Vector3 pos, Vector3 normal)
    {
        CottonState cotton = obj.GetComponent<CottonState>();
        if (cotton != null && cotton.isSoaked)
        {
            bool isTooClose = false;
            foreach (Vector3 p in placedDabs)
            {
                if (Vector3.Distance(pos, p) < paintSpacing)
                {
                    isTooClose = true;
                    break;
                }
            }

            if (!isTooClose)
            {
                DropPaintDab(pos, normal);
                lastPaintPosition = pos;
                placedDabs.Add(pos);
            }
        }
    }

    private void HandleTouchExit(GameObject obj)
    {
        lastPaintPosition = Vector3.zero;
    }

    private void DropPaintDab(Vector3 position, Vector3 normal)
    {
        if (paintDabPrefab == null) return;

        // PULL BACK: Move the projector slightly AWAY from the skin. 
        // This prevents the decal from clipping or getting chopped off on curved surfaces.
        Vector3 spawnPos = position + normal * 0.1f;

        Quaternion dabRotation = Quaternion.LookRotation(-normal);
        GameObject newDecal = Instantiate(paintDabPrefab, spawnPos, dabRotation);
        newDecal.transform.SetParent(transform);

        // Increase the size slightly so it feels like a nice thick brush stroke
        UnityEngine.Rendering.Universal.DecalProjector proj = newDecal.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
        if (proj != null)
        {
            proj.size = new Vector3(0.06f, 0.06f, 0.2f);
        }

        if (newDecal.GetComponent<WashableDecal>() == null)
        {
            newDecal.AddComponent<WashableDecal>();
        }
    }
}