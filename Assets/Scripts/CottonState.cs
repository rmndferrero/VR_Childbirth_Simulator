using UnityEngine;

public class CottonState : MonoBehaviour
{
    [Tooltip("Is this cotton ball soaked in Betadine?")]
    public bool isSoaked = false;

    [Tooltip("Has this cotton ball already touched the skin?")]
    public bool isUsed = false; // NEW FLAG

    private MeshRenderer meshRenderer;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public void SoakCotton(Material soakedMaterial)
    {
        if (!isSoaked && meshRenderer != null && soakedMaterial != null)
        {
            meshRenderer.material = soakedMaterial;
            isSoaked = true;
            Debug.Log("[CottonState] Cotton is now soaked in Betadine!");
        }
    }
}