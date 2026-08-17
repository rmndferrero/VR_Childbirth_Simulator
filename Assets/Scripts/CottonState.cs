using UnityEngine;

public class CottonState : MonoBehaviour
{
    [Tooltip("Is this cotton ball soaked in Betadine?")]
    public bool isSoaked = false;

    private MeshRenderer meshRenderer;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Changes the cotton's material and marks it as soaked.
    /// </summary>
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