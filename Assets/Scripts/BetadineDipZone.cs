using UnityEngine;

public class BetadineDipZone : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Drag your brown Betadine Material here.")]
    public Material betadineMaterial;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the bottle is tagged as Cotton
        if (other.CompareTag("Cotton"))
        {
            CottonState cotton = other.GetComponent<CottonState>();

            // If it has the script and isn't already soaked, soak it!
            if (cotton != null && !cotton.isSoaked)
            {
                cotton.SoakCotton(betadineMaterial);
            }
        }
    }
}