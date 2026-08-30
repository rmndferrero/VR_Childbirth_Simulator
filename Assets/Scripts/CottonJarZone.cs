using UnityEngine;

public class CottonJarZone : MonoBehaviour
{
    [Header("Antiseptic Configuration")]
    [Tooltip("Which antiseptic this jar dispenses.")]
    public AntisepticType antisepticType = AntisepticType.Iodine_7_5_Scrub;

    [Tooltip("Material applied to the soaked cotton ball.")]
    public Material soakedMaterial;

    private void OnTriggerEnter(Collider other)
    {
        ForcepsController forceps = other.GetComponentInParent<ForcepsController>();
        if (forceps != null)
        {
            if (forceps.role == ForcepsRole.Pickup)
            {
                forceps.activeJarZone = this;
                forceps.isInJarZone = true;
            }
            else
            {
                Debug.LogWarning("[CottonJarZone] Only Pickup Forceps may extract cotton from antiseptic jars!");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        ForcepsController forceps = other.GetComponentInParent<ForcepsController>();
        if (forceps != null)
        {
            if (forceps.activeJarZone == this)
            {
                forceps.activeJarZone = null;
                forceps.isInJarZone = false;
            }
        }
    }
}