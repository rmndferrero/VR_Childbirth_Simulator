using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Attached to each stroke zone to manage the left-behind antiseptic coating.
/// - In Phase 2 (7.5% Scrub): Leaves behind a translucent light-green puddle on completed zones.
/// - In Phase 3 (Water Rinse): Fades away smoothly as water from the pitcher is poured over it.
/// - In Phase 4 (10% Paint): Leaves behind a persistent dark iodine layer that STAYS permanently during draping.
/// </summary>
public class WashableIodinePuddle : MonoBehaviour
{
    [Header("Antiseptic Layers")]
    public GameObject puddle75Object;
    public GameObject coating10Object;

    [Header("Wash Properties")]
    public float washFadeSpeed = 1.8f;
    public bool isPuddleActive { get; private set; } = false;
    public bool isFullyWashed { get; private set; } = false;

    private float currentOpacity = 1.0f;
    private Renderer puddleRenderer;
    private DecalProjector puddleDecal;
    private Material instantiatedMat;

    private void Awake()
    {
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        if (puddle75Object != null)
        {
            puddleRenderer = puddle75Object.GetComponent<Renderer>();
            puddleDecal = puddle75Object.GetComponent<DecalProjector>();

            if (puddleRenderer != null && puddleRenderer.material != null)
            {
                instantiatedMat = puddleRenderer.material;
            }
        }
    }

    /// <summary>
    /// Activates the translucent light-green 7.5% iodine puddle upon completing this stroke.
    /// </summary>
    public void Apply7_5Puddle()
    {
        CacheRenderers();
        if (puddle75Object != null)
        {
            puddle75Object.SetActive(true);
            currentOpacity = 1.0f;
            isPuddleActive = true;
            isFullyWashed = false;
            UpdatePuddleOpacity(1.0f);
        }

        if (coating10Object != null)
        {
            coating10Object.SetActive(false);
        }
    }

    /// <summary>
    /// Activates the persistent 10% iodine surgical prep layer (stays for draping).
    /// </summary>
    public void Apply10Coating()
    {
        if (puddle75Object != null) puddle75Object.SetActive(false);

        if (coating10Object != null)
        {
            coating10Object.SetActive(true);
        }
    }

    /// <summary>
    /// Called when water from the pitcher is poured onto this zone.
    /// </summary>
    public void WashWithWater(float deltaTime)
    {
        if (!isPuddleActive || isFullyWashed) return;

        currentOpacity -= washFadeSpeed * deltaTime;
        currentOpacity = Mathf.Max(0f, currentOpacity);
        UpdatePuddleOpacity(currentOpacity);

        if (currentOpacity <= 0.04f)
        {
            isFullyWashed = true;
            isPuddleActive = false;
            currentOpacity = 0f;
            if (puddle75Object != null) puddle75Object.SetActive(false);
            Debug.Log($"[WashableIodinePuddle] {gameObject.name} light-green puddle fully washed away by water.");
        }
    }

    public float GetRemainingOpacity()
    {
        if (isFullyWashed || !isPuddleActive) return 0f;
        return currentOpacity;
    }

    private void UpdatePuddleOpacity(float alpha)
    {
        if (puddleDecal != null)
        {
            puddleDecal.fadeFactor = alpha;
        }

        if (instantiatedMat != null)
        {
            if (instantiatedMat.HasProperty("_BaseColor"))
            {
                Color c = instantiatedMat.GetColor("_BaseColor");
                c.a = alpha * 0.45f;
                instantiatedMat.SetColor("_BaseColor", c);
            }
            else if (instantiatedMat.HasProperty("_Color"))
            {
                Color c = instantiatedMat.color;
                c.a = alpha * 0.45f;
                instantiatedMat.color = c;
            }
        }
    }

    public void ResetPuddles()
    {
        isPuddleActive = false;
        isFullyWashed = false;
        currentOpacity = 1.0f;
        if (puddle75Object != null) puddle75Object.SetActive(false);
        if (coating10Object != null) coating10Object.SetActive(false);
    }
}