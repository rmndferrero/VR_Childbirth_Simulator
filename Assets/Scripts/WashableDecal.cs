using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Attached to every spawned Betadine_Paint_Dab at runtime.
/// Adds a small trigger collider so the pitcher's RaycastAll can detect it,
/// and fades the decal projector's opacity when washed with water.
/// </summary>
public class WashableDecal : MonoBehaviour
{
    [Header("Wash Settings")]
    [Tooltip("How fast the decal fades when water hits it.")]
    public float fadeSpeed = 0.8f;

    private DecalProjector projector;
    private bool isFullyWashed = false;

    void Awake()
    {
        projector = GetComponent<DecalProjector>();

        // Add a small flat box collider so the pitcher raycast can detect this dab
        // We make it thin (z=0.005) so it doesn't stick out and cause premature washing
        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.04f, 0.04f, 0.005f); // Matches the 4cm decal size, but very thin
        col.center = new Vector3(0, 0, 0.0025f);
    }

    /// <summary>
    /// Called by PitcherPour's raycast every frame it hits this dab.
    /// </summary>
    public void WashWithWater()
    {
        if (isFullyWashed || projector == null) return;

        // Gradually reduce the decal opacity
        float newOpacity = projector.fadeFactor - fadeSpeed * Time.deltaTime;
        projector.fadeFactor = Mathf.Max(0f, newOpacity);

        if (projector.fadeFactor <= 0f)
        {
            isFullyWashed = true;
            Debug.Log("[WashableDecal] Paint dab fully washed away.");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Auto-fade for wrong-area penalty (call this to make it fade on its own).
    /// </summary>
    public void FadeAway()
    {
        StartCoroutine(AutoFadeRoutine());
    }

    private System.Collections.IEnumerator AutoFadeRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Brief pause so player sees the mistake

        while (projector != null && projector.fadeFactor > 0f)
        {
            projector.fadeFactor -= fadeSpeed * Time.deltaTime;
            yield return null;
        }

        if (gameObject != null) Destroy(gameObject);
    }
}
