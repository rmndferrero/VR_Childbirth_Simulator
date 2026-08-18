using UnityEngine;

public class CottonJarZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the jar belongs to the forceps
        ForcepsController forceps = other.GetComponentInParent<ForcepsController>();
        if (forceps != null)
        {
            forceps.isInJarZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        ForcepsController forceps = other.GetComponentInParent<ForcepsController>();
        if (forceps != null)
        {
            forceps.isInJarZone = false;
        }
    }
}