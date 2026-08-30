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
    public float fadeSpeed = 3.0f; // Fast, responsive washing

    private DecalProjector projector;
    private bool isFullyWashed = false;

    void Awake()
    {
        projector = GetComponent<DecalProjector>();

        // Add a generous sphere trigger collider so water stream easily detects and cleans this dab from any angle
        if (GetComponent<SphereCollider>() == null && GetComponent<Collider>() == null)
        {
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.045f;
        }
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
        StartCoroutine(AutoFadeRoutine(1.2f, fadeSpeed));
    }

    /// <summary>
    /// Split-second flash fade for mistake touches (vanishes in ~0.4s).
    /// </summary>
    public void FastFade(float delay = 0.15f, float speed = 4.5f)
    {
        StartCoroutine(AutoFadeRoutine(delay, speed));
    }

    private System.Collections.IEnumerator AutoFadeRoutine(float delay, float speed)
    {
        yield return new WaitForSeconds(delay);

        while (projector != null && projector.fadeFactor > 0f)
        {
            projector.fadeFactor -= speed * Time.deltaTime;
            yield return null;
        }

        if (gameObject != null) Destroy(gameObject);
    }
}
