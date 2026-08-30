using UnityEngine;

public class PerinealWashHitbox : MonoBehaviour
{
    [Tooltip("Which wash zone this hitbox represents.")]
    public WashZone zone = WashZone.Center;

    public void OnWaterPoured(CleaningProgressUI progressUI, float deltaTime)
    {
        if (progressUI != null)
        {
            progressUI.ReportWaterPour(zone, deltaTime);
        }
    }
}
