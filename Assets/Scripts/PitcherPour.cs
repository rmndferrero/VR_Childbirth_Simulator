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
            
            // If we are pouring, fire the cleaning raycast every frame
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
        // Draw an invisible line straight down from the spout to mimic gravity
        Ray ray = new Ray(spoutOrigin.position, Vector3.down);
        // Cast the ray and get all hits (so the invisible Betadine Shell doesn't block it!)
        RaycastHit[] hits = Physics.RaycastAll(ray, pourDistance);
        
        foreach (RaycastHit hit in hits)
        {
            // Check if the object we hit has the CleaningZone script attached
            CleaningZone zone = hit.collider.GetComponent<CleaningZone>();
            
            if (zone != null)
            {
                // Wash away the blood!
                zone.WashWithWater();
                break; // Optional: stop after hitting the first valid zone
            }
        }
    }
}