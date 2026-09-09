using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls 3D visual collider box guides across the 9 stroke landmarks.
/// - Active Target Zone: Automatically illuminates as a translucent Glowing Blue Collider Box ONLY when cotton is held in the Handling Forceps.
/// - No numbers or words: Pure translucent 3D visual collider boxes with gentle breathing pulse.
/// - Out-of-Order Mistake: Collider box instantly switches to Glowing Neon Red and flashes for 1.25s.
/// - Inactive / No Cotton in Handling Forceps: All collider boxes remain completely hidden.
/// </summary>
public class StrokeAreaVisualGuide : MonoBehaviour
{
    [Header("Visual Collider Materials")]
    public Material activeBoxMaterial;
    public Material mistakeBoxMaterial;

    [Header("Handling Forceps Reference")]
    [Tooltip("Reference to the Handling Forceps. Auto-cached if null.")]
    public ForcepsController handlingForceps;

    private StrokeTrackingManager trackingManager;
    private List<GameObject> zoneVisualBoxes = new List<GameObject>();
    private List<MeshRenderer> zoneBoxRenderers = new List<MeshRenderer>();
    private List<DecalProjector> zoneProjectors = new List<DecalProjector>();
    private int currentActiveIndex = 0;
    private int currentMistakeIndex = -1;
    private float mistakeFlashTimer = 0f;

    private void Awake()
    {
        trackingManager = FindFirstObjectByType<StrokeTrackingManager>();
        if (trackingManager != null)
        {
            trackingManager.OnStrokeAdvanced += HandleStrokeAdvanced;
            trackingManager.OnMistakeZoneTriggered += HandleMistakeZoneTriggered;
        }

        LoadMaterialsIfNull();
        CacheHandlingForceps();
        SetupZoneVisuals();
    }

    private void Start()
    {
        CacheHandlingForceps();
        SetupZoneVisuals();
        RefreshActiveZoneDisplay();
    }

    private void OnEnable()
    {
        CacheHandlingForceps();
        SetupZoneVisuals();
        RefreshActiveZoneDisplay();
    }

    private void OnDestroy()
    {
        if (trackingManager != null)
        {
            trackingManager.OnStrokeAdvanced -= HandleStrokeAdvanced;
            trackingManager.OnMistakeZoneTriggered -= HandleMistakeZoneTriggered;
        }
    }

    private void CacheHandlingForceps()
    {
        if (handlingForceps != null) return;
        var allForceps = FindObjectsByType<ForcepsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var f in allForceps)
        {
            if (f != null && f.role == ForcepsRole.Handling)
            {
                handlingForceps = f;
                break;
            }
        }
    }

    private void LoadMaterialsIfNull()
    {
        if (activeBoxMaterial == null)
        {
#if UNITY_EDITOR
            activeBoxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Active_Mat.mat");
            if (activeBoxMaterial == null)
                activeBoxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Mesh_Mat.mat");
#endif
        }

        if (mistakeBoxMaterial == null)
        {
#if UNITY_EDITOR
            mistakeBoxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Red_Mistake_Mat.mat");
#endif
        }
    }

    public void SetupZoneVisuals()
    {
        zoneVisualBoxes.Clear();
        zoneBoxRenderers.Clear();
        zoneProjectors.Clear();

        var triggers = FindObjectsByType<StrokeZoneTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var triggerList = new List<StrokeZoneTrigger>(triggers);

        // Strictly sort by expected order index (0 to 8: Stroke 1 to 9)
        triggerList.Sort((a, b) => {
            int orderA = (a != null && a.zoneDefinition != null) ? a.zoneDefinition.expectedOrderIndex : 99;
            int orderB = (b != null && b.zoneDefinition != null) ? b.zoneDefinition.expectedOrderIndex : 99;
            return orderA.CompareTo(orderB);
        });

        foreach (var trig in triggerList)
        {
            if (trig == null) continue;

            BoxCollider box = trig.GetComponent<BoxCollider>();
            Vector3 colliderSize = box != null ? box.size : new Vector3(0.04f, 0.08f, 0.04f);
            Vector3 colliderCenter = box != null ? box.center : Vector3.zero;

            // 1. Locate or create clean 3D Visual Collider Box (Cube Mesh without collider)
            string boxName = $"VisualColliderBox_{trig.gameObject.name}";
            Transform boxT = trig.transform.Find(boxName);
            GameObject boxGo = null;

            if (boxT != null)
            {
                boxGo = boxT.gameObject;
            }
            else
            {
                boxGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boxGo.name = boxName;
                boxGo.transform.SetParent(trig.transform, false);

                // Strip any default collider from the visual primitive
                var defaultCol = boxGo.GetComponent<Collider>();
                if (defaultCol != null) DestroyImmediate(defaultCol);
            }

            boxGo.transform.localPosition = colliderCenter;
            boxGo.transform.localRotation = Quaternion.identity;
            boxGo.transform.localScale = colliderSize;

            var mr = boxGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = activeBoxMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            boxGo.SetActive(false);
            zoneVisualBoxes.Add(boxGo);
            zoneBoxRenderers.Add(mr);

            // 2. Also align any decal projector if present
            string projName = $"DecalGuide_{trig.gameObject.name}";
            Transform projT = trig.transform.Find(projName);
            if (projT != null)
            {
                var proj = projT.GetComponent<DecalProjector>();
                if (proj != null)
                {
                    proj.enabled = false;
                    zoneProjectors.Add(proj);
                }
            }
        }

        Debug.Log($"[StrokeAreaVisualGuide] Initialized {zoneVisualBoxes.Count} pure 3D Blue Collider visual guides.");
    }

    private void HandleStrokeAdvanced(int strokeIndex, StrokeZoneDefinition activeZone)
    {
        currentActiveIndex = strokeIndex;
        mistakeFlashTimer = 0f;
        currentMistakeIndex = -1;
        RefreshActiveZoneDisplay();
    }

    private void HandleMistakeZoneTriggered(int mistakeIndex)
    {
        if (trackingManager != null && mistakeIndex < trackingManager.GetCurrentStrokeIndex())
        {
            return;
        }

        currentMistakeIndex = mistakeIndex;
        mistakeFlashTimer = 1.25f; // Glow red for 1.25s
    }

    private void RefreshActiveZoneDisplay()
    {
        if (trackingManager != null)
        {
            currentActiveIndex = trackingManager.GetCurrentStrokeIndex();
        }
    }

    /// <summary>
    /// Checks if a fresh, usable cotton ball is currently held in the Handling Forceps.
    /// </summary>
    public bool IsCottonInHandlingForceps()
    {
        if (handlingForceps == null) CacheHandlingForceps();

        if (handlingForceps != null && handlingForceps.HasCotton)
        {
            return true;
        }

        if (CottonState.activeCottons != null)
        {
            foreach (var cotton in CottonState.activeCottons)
            {
                if (cotton != null && cotton.currentHolder == ForcepsRole.Handling && !cotton.isUsed)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Update()
    {
        if (mistakeFlashTimer > 0f)
        {
            mistakeFlashTimer -= Time.deltaTime;
            if (mistakeFlashTimer <= 0f)
            {
                currentMistakeIndex = -1;
            }
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        bool isHoldingCotton = IsCottonInHandlingForceps();
        bool isMistakeActive = (mistakeFlashTimer > 0f && currentMistakeIndex >= 0);

        for (int i = 0; i < zoneVisualBoxes.Count; i++)
        {
            var boxGo = zoneVisualBoxes[i];
            var mr = zoneBoxRenderers[i];
            if (boxGo == null || mr == null) continue;

            // 1. MISTAKE FLASH: High-priority Glowing Red Collider Box
            if (i == currentMistakeIndex && isMistakeActive)
            {
                boxGo.SetActive(true);
                if (mistakeBoxMaterial != null && mr.sharedMaterial != mistakeBoxMaterial)
                {
                    mr.sharedMaterial = mistakeBoxMaterial;
                }

                // Red pulse during mistake
                float redPulse = 1.0f + Mathf.Sin(Time.time * 12f) * 0.08f;
                var trig = boxGo.transform.parent;
                var boxCol = trig != null ? trig.GetComponent<BoxCollider>() : null;
                Vector3 baseSize = boxCol != null ? boxCol.size : Vector3.one * 0.05f;
                boxGo.transform.localScale = baseSize * redPulse;
            }
            // 2. UNCOMPLETED ZONES: Visible in static solid blue ONLY when holding handling forceps with cotton
            else if (isHoldingCotton && i >= currentActiveIndex && currentActiveIndex < zoneVisualBoxes.Count)
            {
                boxGo.SetActive(true);
                if (activeBoxMaterial != null && mr.sharedMaterial != activeBoxMaterial)
                {
                    mr.sharedMaterial = activeBoxMaterial;
                }

                var trig = boxGo.transform.parent;
                var boxCol = trig != null ? trig.GetComponent<BoxCollider>() : null;
                Vector3 baseSize = boxCol != null ? boxCol.size : Vector3.one * 0.05f;
                boxGo.transform.localScale = baseSize; // Clean static size - NO BLUE PULSE
            }
            // 3. COMPLETED ZONES OR NO COTTON HELD: Completely Hidden
            else
            {
                boxGo.SetActive(false);
            }
        }
    }
}

