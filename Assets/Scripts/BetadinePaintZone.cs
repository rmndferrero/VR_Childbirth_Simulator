using UnityEngine;

public class BetadinePaintZone : MonoBehaviour
{
    [Header("Antiseptic Paint Prefabs")]
    [Tooltip("Prefab for 7.5% Povidone-Iodine Scrub (Light Green, Washable).")]
    public GameObject prefab_7_5_LightGreen;

    [Tooltip("Prefab for 10% Povidone-Iodine Paint (Dark Green, Persistent).")]
    public GameObject prefab_10_DarkGreen;

    [Tooltip("Prefab for Mistake Stroke (Red, Auto-Fading).")]
    public GameObject prefab_Mistake_Red;

    [Header("Brush Settings")]
    [Tooltip("How far the cotton must move (in meters) to drop the next dot of paint.")]
    public float paintSpacing = 0.007f;

    private Vector3 lastPaintPosition;
    private System.Collections.Generic.List<Vector3> placedDabs = new System.Collections.Generic.List<Vector3>();

    private Collider myCollider;
    private CottonState[] allCottons;
    private StrokeTrackingManager strokeTrackingManager;

    private void Start()
    {
        myCollider = GetComponent<Collider>();
        paintSpacing = 0.007f;
        strokeTrackingManager = FindObjectOfType<StrokeTrackingManager>();
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
                        HandleTouch(cotton, rayHit.point, rayHit.normal);
                    }
                    else
                    {
                        HandleTouch(cotton, cotton.transform.position, dirToCenter * -1f);
                    }
                }
                else
                {
                    HandleTouchExit(cotton);
                }
            }
        }
    }

    private void HandleTouch(CottonState cotton, Vector3 pos, Vector3 normal)
    {
        if (cotton == null || !cotton.isSoaked) return;

        // Role Enforcement: Only Handling Forceps may touch patient!
        if (cotton.currentHolder != ForcepsRole.Handling)
        {
            if (Time.frameCount % 60 == 0 && PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.RecordClinicalViolation("Clinical protocol violation: Attempted to touch patient with Pickup Forceps! Always transfer to Handling Forceps.", 5);
            }
            return;
        }

        // Phase Alignment Check
        bool isMistake = false;

        if (PerinealCareManager.Instance != null)
        {
            var phase = PerinealCareManager.Instance.currentState;

            if (phase == PerinealCareState.STATE_2_IODINE_7_5)
            {
                if (cotton.antisepticType != AntisepticType.Iodine_7_5_Scrub)
                {
                    if (Time.frameCount % 60 == 0)
                    {
                        PerinealCareManager.Instance.RecordClinicalViolation("Wrong solution: Use 7.5% Iodine Scrub during Phase 2.", 5);
                    }
                    isMistake = true;
                }
                else if (strokeTrackingManager != null)
                {
                    // Check if contact is in the active stroke zone
                    bool isCorrectZone = strokeTrackingManager.ValidatePaintPoint(pos, out StrokeZoneDefinition expectedZone);
                    if (!isCorrectZone)
                    {
                        isMistake = true;
                    }
                }
            }
            else if (phase == PerinealCareState.STATE_4_IODINE_10)
            {
                if (cotton.antisepticType != AntisepticType.Iodine_10_Paint)
                {
                    if (Time.frameCount % 60 == 0)
                    {
                        PerinealCareManager.Instance.RecordClinicalViolation("Wrong solution: Use 10% Iodine Paint during Phase 4.", 5);
                    }
                    isMistake = true;
                }
            }
        }

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
            DropPaintDab(pos, normal, cotton.antisepticType, isMistake);
            lastPaintPosition = pos;
            if (!isMistake) placedDabs.Add(pos);
        }
    }

    private void HandleTouchExit(CottonState cotton)
    {
        lastPaintPosition = Vector3.zero;
    }

    private void DropPaintDab(Vector3 position, Vector3 normal, AntisepticType type, bool isMistake)
    {
        GameObject prefabToSpawn;

        if (isMistake)
        {
            prefabToSpawn = prefab_Mistake_Red != null ? prefab_Mistake_Red : prefab_7_5_LightGreen;
        }
        else if (type == AntisepticType.Iodine_10_Paint)
        {
            prefabToSpawn = prefab_10_DarkGreen;
        }
        else
        {
            prefabToSpawn = prefab_7_5_LightGreen;
        }

        if (prefabToSpawn == null) return;

        // PULL BACK: Move the projector slightly AWAY from the skin.
        Vector3 spawnPos = position + normal * 0.06f;
        Quaternion dabRotation = Quaternion.LookRotation(-normal);

        GameObject newDecal = Instantiate(prefabToSpawn, spawnPos, dabRotation);
        newDecal.transform.SetParent(transform);

        UnityEngine.Rendering.Universal.DecalProjector proj = newDecal.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
        if (proj != null)
        {
            proj.size = new Vector3(0.022f, 0.022f, 0.15f);
        }

        // If mistake, auto-fade and dissolve after split second (0.35s)!
        if (isMistake)
        {
            WashableDecal wd = newDecal.GetComponent<WashableDecal>();
            if (wd == null) wd = newDecal.AddComponent<WashableDecal>();
            wd.FastFade(0.12f, 5.0f);
        }
    }

    public void ClearPlacedDabsList()
    {
        placedDabs.Clear();
    }
}