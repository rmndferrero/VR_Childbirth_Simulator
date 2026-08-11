using UnityEngine;

public class CleaningZone : MonoBehaviour
{
    [Header("Cleaning Settings")]
    [Tooltip("How fast the blood fades (e.g., 0.5 means it takes 2 seconds of pouring to clean).")]
    public float fadeSpeed = 0.5f;

    private Material bloodMaterial;
    private Color currentColor;
    private bool isFullyCleaned = false;

    void Start()
    {
        // Grab the MeshRenderer from the trigger cube
        MeshRenderer renderer = GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            // Using .material (instead of .sharedMaterial) creates a unique instance.
            // This ensures each perineal zone fades independently when washed.
            bloodMaterial = renderer.material;
            currentColor = bloodMaterial.color;
        }
        else
        {
            Debug.LogError($"[CleaningZone] No MeshRenderer found on {gameObject.name}!");
        }
    }

    /// <summary>
    /// This is called by the PitcherPour raycast every frame it hits this cube.
    /// </summary>
    public void WashWithWater()
    {
        // Ignore if already clean or missing material
        if (isFullyCleaned || bloodMaterial == null) return;

        // Gradually reduce the alpha (opacity) channel over time
        currentColor.a -= fadeSpeed * Time.deltaTime;
        bloodMaterial.color = currentColor;

        // Check if the opacity has dropped to 0 or below
        if (currentColor.a <= 0f)
        {
            // Clamp it exactly to 0 for a clean visual state
            currentColor.a = 0f;
            bloodMaterial.color = currentColor;
            isFullyCleaned = true;

            Debug.Log($"[CleaningZone] {gameObject.name} cleaned. Disabling object.");

            // Disable the object entirely in the hierarchy
            gameObject.SetActive(false);
        }
    }
}