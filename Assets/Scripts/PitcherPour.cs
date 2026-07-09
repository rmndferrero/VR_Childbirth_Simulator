using UnityEngine;

public class PitcherPour : MonoBehaviour
{
    [Header("Pour Settings")]
    public ParticleSystem waterParticleSystem;
    public float pourAngleThreshold = 45f; // Starts pouring when tilted 45 degrees
    public float emissionRate = 50f;       // How much water pours out

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

        // Add this line: Force the system to play if it was stopped
        if (!waterParticleSystem.isPlaying)
        {
            waterParticleSystem.Play();
        }
    }

    private void StopPouring()
    {
        isPouring = false;
        emissionModule.rateOverTime = 0f;
        // Optional: Stop sound effect here
    }
}