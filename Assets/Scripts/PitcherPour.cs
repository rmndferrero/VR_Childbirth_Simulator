using UnityEngine;

public class PitcherPour : MonoBehaviour
{
    [Header("Visual Settings")]
    public ParticleSystem waterParticleSystem;
    public float pourAngleThreshold = 45f; // Starts pouring when tilted 45 degrees
    public float emissionRate = 50f;       // How much water pours out

    [Header("Mechanical Settings")]
    [Tooltip("Where the invisible cleaning raycast shoots from (e.g., the spout).")]
    public Transform spoutOrigin;
    [Tooltip("How far down the water reaches to clean.")]
    public float pourDistance = 1.5f;
    [Tooltip("Radius of the water stream (matches visual particle stream).")]
    public float waterStreamRadius = 0.08f;

    private ParticleSystem.EmissionModule emissionModule;
    private bool isPouring = false;

    void Start()
    {
        // Cache the emission module so we can toggle it
        emissionModule = waterParticleSystem.emission;
        emissionModule.rateOverTime = 0f; // Start with water off
    }

    void Update()
    {
        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        if (tiltAngle > pourAngleThreshold)
        {
            if (!isPouring) StartPouring();
            
            // If we are pouring, fire the cleaning spherecast every frame
            CastWaterRay();
        }
        else
        {
            if (isPouring) StopPouring();
        }
    }

    private void StartPouring()
    {
        isPouring = true;
        emissionModule.rateOverTime = emissionRate;

        // Force the system to play if it was stopped
        if (!waterParticleSystem.isPlaying)
        {
            waterParticleSystem.Play();
        }
    }

    private void StopPouring()
    {
        isPouring = false;
        emissionModule.rateOverTime = 0f;
    }

    private void CastWaterRay()
    {
        if (spoutOrigin == null) return;

        // Draw a cylinder/spherecast straight down from the spout to mimic water stream width
        Ray ray = new Ray(spoutOrigin.position, Vector3.down);
        RaycastHit[] hits = Physics.SphereCastAll(ray, waterStreamRadius, pourDistance, ~0, QueryTriggerInteraction.Collide);
        
        System.Collections.Generic.HashSet<GameObject> processedObjects = new System.Collections.Generic.HashSet<GameObject>();

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            GameObject go = hit.collider.gameObject;
            if (processedObjects.Contains(go)) continue;
            processedObjects.Add(go);

            // 1. Clean Blood (CleaningZone)
            CleaningZone zone = hit.collider.GetComponent<CleaningZone>();
            if (zone != null)
            {
                zone.WashWithWater();
            }

            // 2. Clean Betadine Paint (WashableDecal)
            WashableDecal decal = hit.collider.GetComponent<WashableDecal>();
            if (decal != null)
            {
                decal.WashWithWater();
            }

            // 3. Wash Hitboxes (CleaningProgressUI)
            PerinealWashHitbox washHitbox = hit.collider.GetComponent<PerinealWashHitbox>();
            if (washHitbox != null && PerinealCareManager.Instance != null && PerinealCareManager.Instance.cleaningProgressUI != null)
            {
                washHitbox.OnWaterPoured(PerinealCareManager.Instance.cleaningProgressUI, Time.deltaTime);
            }
        }
    }
}