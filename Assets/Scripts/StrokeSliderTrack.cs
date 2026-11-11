using UnityEngine;

public enum SliderVisualState
{
    Active,
    Upcoming,
    Completed,
    Mistake,
    Dimmed
}

/// <summary>
/// Attached to each of the 9 3D on-skin stroke slider GameObjects in the scene.
/// Allows the user to select, move, and adjust the Start and End points (or StartNode/EndNode) directly in the Scene View.
/// Renders hollow concentric holographic reticles (100% transparent center so anatomy is visible),
/// delicate dashed blueprint lines, active wiping beam with sparkle particles, 3-mistake pulse hints,
/// and bidirectional support for Stroke 2 (Mons Pubis).
/// </summary>
[ExecuteInEditMode]
public class StrokeSliderTrack : MonoBehaviour
{
    [Header("Stroke Identification")]
    [Tooltip("Index from 0 to 8 corresponding to Strokes 1 through 9.")]
    public int strokeIndex = 0;
    public string strokeName = "Stroke 1";
    [Tooltip("If true, this landmark is a direct tap target (e.g. Stroke 9 Anus).")]
    public bool isTapOnly = false;
    [Tooltip("If true, the player can initiate this stroke from either StartPoint or EndPoint without penalty (used for Stroke 2: Mons Pubis).")]
    public bool isBidirectional = false;

    [Header("Endpoints (Move these in Scene View to adjust the path)")]
    public Transform startPointTransform;
    public Transform endPointTransform;

    [Header("Visual Components")]
    public LineRenderer trackLine;
    public LineRenderer fillLine;
    public GameObject startNode;
    public GameObject endNode;
    public GameObject directionArrow;
    public GameObject handleBead;

    [Header("Antiseptic Puddle Layer")]
    public WashableIodinePuddle puddleLayer;

    [Header("Materials")]
    public Material activeMaterial;
    public Material upcomingMaterial;
    public Material completedMaterial;
    public Material mistakeMaterial;

    [Header("Holographic Reticle & Blueprint Settings")]
    [Tooltip("Automatically scales reticle radius proportionally to stroke length so inner labia rings stay compact.")]
    public bool autoSizeReticlesByLength = true;
    public float outerRingRadius = 0.011f;
    public float innerRingRadius = 0.0065f;
    public float reticleLineWidth = 0.0014f;
    public float blueprintLineWidth = 0.0020f;
    public float activeBeamWidth = 0.0045f;
    public Color blueprintColor = new Color(0.28f, 0.88f, 1.0f, 0.28f);
    public Color activeGlowColor = new Color(0.22f, 0.94f, 1.0f, 0.95f);
    public Color hintGoldColor = new Color(1.0f, 0.82f, 0.16f, 1.0f);
    public Color hintAmberColor = new Color(1.0f, 0.58f, 0.08f, 0.92f);

    [Header("Editor Display Settings")]
    [Tooltip("When true in Edit Mode, displays the slider gizmos and holographic reticles in Scene View for easy positioning.")]
    public bool showInEditor = true;
    public Color gizmoColor = new Color(0.15f, 0.75f, 1f, 0.9f);

    // Cached holographic reticle rings & particle trail
    private LineRenderer startOuterRing;
    private LineRenderer startInnerRing;
    private LineRenderer endOuterRing;
    private LineRenderer endInnerRing;
    private LineRenderer handleRing;
    private ParticleSystem sparkleParticles;
    private bool isReverseFill = false;

    // Edit-mode bidirectional transform tracking so dragging EITHER StartPoint or StartNode in Scene View works seamlessly
    private Vector3 lastStartPointPos;
    private Vector3 lastStartNodePos;
    private Vector3 lastEndPointPos;
    private Vector3 lastEndNodePos;
    private bool transformTrackingInitialized = false;

    // Shared procedural holographic materials
    private static Material s_dashedBlueprintMat;
    private static Material s_solidGlowMat;
    private static Material s_sparkleMat;

    private const int RingSegments = 36;

    private void Awake()
    {
        EnsureBidirectionalDefault();
        EnsureHolographicVisuals();
        UpdateVisualPositions();
    }

    private void OnEnable()
    {
        EnsureBidirectionalDefault();
        EnsureHolographicVisuals();
        UpdateVisualPositions();
    }

    private void OnValidate()
    {
        EnsureBidirectionalDefault();
        EnsureHolographicVisuals();
        UpdateVisualPositions();
    }

    private void EnsureBidirectionalDefault()
    {
        // StrokeSlider_2_Mons_Pubis (index 1) is always bidirectional
        if (strokeIndex == 1 || (!string.IsNullOrEmpty(strokeName) && strokeName.ToLowerInvariant().Contains("mons")))
        {
            isBidirectional = true;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (showInEditor)
            {
                SyncEditorTransformsBidirectional();
                EnsureHolographicVisuals();
                UpdateVisualPositions();
            }
            return;
        }
#endif
        if (Application.isPlaying)
        {
            OrientReticlesToCamera();
        }
    }

#if UNITY_EDITOR
    private void SyncEditorTransformsBidirectional()
    {
        if (!transformTrackingInitialized)
        {
            if (startPointTransform != null) lastStartPointPos = startPointTransform.position;
            if (startNode != null) lastStartNodePos = startNode.transform.position;
            if (endPointTransform != null) lastEndPointPos = endPointTransform.position;
            if (endNode != null) lastEndNodePos = endNode.transform.position;
            transformTrackingInitialized = true;
            return;
        }

        if (startPointTransform != null && startNode != null)
        {
            bool ptMoved = (startPointTransform.position - lastStartPointPos).sqrMagnitude > 0.0000001f;
            bool nodeMoved = (startNode.transform.position - lastStartNodePos).sqrMagnitude > 0.0000001f;

            if (nodeMoved && !ptMoved)
            {
                startPointTransform.position = startNode.transform.position;
            }
            else if (ptMoved)
            {
                startNode.transform.position = startPointTransform.position;
            }

            lastStartPointPos = startPointTransform.position;
            lastStartNodePos = startNode.transform.position;
        }

        if (endPointTransform != null && endNode != null)
        {
            bool ptMoved = (endPointTransform.position - lastEndPointPos).sqrMagnitude > 0.0000001f;
            bool nodeMoved = (endNode.transform.position - lastEndNodePos).sqrMagnitude > 0.0000001f;

            if (nodeMoved && !ptMoved)
            {
                endPointTransform.position = endNode.transform.position;
            }
            else if (ptMoved)
            {
                endNode.transform.position = endPointTransform.position;
            }

            lastEndPointPos = endPointTransform.position;
            lastEndNodePos = endNode.transform.position;
        }
    }
#endif

    public Vector3 GetStartWorldPosition()
    {
        if (startPointTransform != null) return startPointTransform.position;
        return transform.position;
    }

    public Vector3 GetEndWorldPosition()
    {
        if (endPointTransform != null) return endPointTransform.position;
        return transform.position + transform.forward * 0.05f;
    }

    public void SetReverseFill(bool reverse)
    {
        isReverseFill = reverse;
    }

    private float GetEffectiveOuterRadius()
    {
        if (isTapOnly) return 0.013f;
        if (!autoSizeReticlesByLength) return outerRingRadius;

        float len = Vector3.Distance(GetStartWorldPosition(), GetEndWorldPosition());
        return Mathf.Clamp(len * 0.125f, 0.0065f, 0.0135f);
    }

    private float GetEffectiveInnerRadius()
    {
        return GetEffectiveOuterRadius() * 0.56f;
    }

    private static void EnsureSharedMaterials()
    {
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null) spriteShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (spriteShader == null) spriteShader = Shader.Find("Unlit/Transparent");

        if (s_dashedBlueprintMat == null && spriteShader != null)
        {
            s_dashedBlueprintMat = new Material(spriteShader);
            s_dashedBlueprintMat.name = "Holographic_Dashed_Blueprint_Mat";
            s_dashedBlueprintMat.hideFlags = HideFlags.HideAndDontSave;

            Texture2D dashTex = new Texture2D(64, 16, TextureFormat.RGBA32, false);
            dashTex.name = "DashedBlueprintTex";
            dashTex.wrapMode = TextureWrapMode.Repeat;
            dashTex.filterMode = FilterMode.Bilinear;
            dashTex.hideFlags = HideFlags.HideAndDontSave;

            for (int y = 0; y < 16; y++)
            {
                float vDist = Mathf.Abs((y - 7.5f) / 7.5f);
                float vFalloff = Mathf.Clamp01(1.0f - vDist * vDist);

                for (int x = 0; x < 64; x++)
                {
                    // Smooth dash (0..38) and gap (39..63)
                    float dashAlpha = 0f;
                    if (x >= 4 && x <= 36)
                    {
                        float edgeFade = 1f;
                        if (x < 9) edgeFade = (x - 4) / 5f;
                        else if (x > 31) edgeFade = (36 - x) / 5f;
                        dashAlpha = edgeFade * vFalloff;
                    }
                    dashTex.SetPixel(x, y, new Color(1f, 1f, 1f, dashAlpha));
                }
            }
            dashTex.Apply();
            s_dashedBlueprintMat.mainTexture = dashTex;
        }

        if (s_solidGlowMat == null && spriteShader != null)
        {
            s_solidGlowMat = new Material(spriteShader);
            s_solidGlowMat.name = "Holographic_Solid_Glow_Mat";
            s_solidGlowMat.hideFlags = HideFlags.HideAndDontSave;

            Texture2D glowTex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            glowTex.name = "SolidGlowTex";
            glowTex.wrapMode = TextureWrapMode.Clamp;
            glowTex.filterMode = FilterMode.Bilinear;
            glowTex.hideFlags = HideFlags.HideAndDontSave;

            for (int y = 0; y < 16; y++)
            {
                float vDist = Mathf.Abs((y - 7.5f) / 7.5f);
                float alpha = Mathf.Pow(Mathf.Clamp01(1.0f - vDist), 0.75f);
                for (int x = 0; x < 16; x++)
                {
                    glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            glowTex.Apply();
            s_solidGlowMat.mainTexture = glowTex;
        }

        if (s_sparkleMat == null && spriteShader != null)
        {
            s_sparkleMat = new Material(spriteShader);
            s_sparkleMat.name = "Holographic_Sparkle_Mat";
            s_sparkleMat.hideFlags = HideFlags.HideAndDontSave;

            Texture2D sparkTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            sparkTex.name = "SparkleDustTex";
            sparkTex.wrapMode = TextureWrapMode.Clamp;
            sparkTex.filterMode = FilterMode.Bilinear;
            sparkTex.hideFlags = HideFlags.HideAndDontSave;

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x - 15.5f) / 15.5f;
                    float dy = (y - 15.5f) / 15.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1.0f - r), 2.2f);
                    sparkTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            sparkTex.Apply();
            s_sparkleMat.mainTexture = sparkTex;
        }
    }

    private void EnsureHolographicVisuals()
    {
        EnsureSharedMaterials();

        float outerR = GetEffectiveOuterRadius();
        float innerR = GetEffectiveInnerRadius();

        // 1. Replace solid sphere on StartNode with hollow concentric reticle rings
        if (startNode != null)
        {
            DisableOpaqueSphereMesh(startNode);
            startOuterRing = GetOrCreateCircleRing(startNode.transform, "OuterReticleRing", outerR, reticleLineWidth);
            startInnerRing = GetOrCreateCircleRing(startNode.transform, "InnerReticleRing", innerR, reticleLineWidth * 0.78f);
        }

        // 2. Replace solid sphere on EndNode with hollow concentric reticle rings
        if (endNode != null)
        {
            DisableOpaqueSphereMesh(endNode);
            if (!isTapOnly)
            {
                endOuterRing = GetOrCreateCircleRing(endNode.transform, "OuterReticleRing", outerR, reticleLineWidth);
                endInnerRing = GetOrCreateCircleRing(endNode.transform, "InnerReticleRing", innerR, reticleLineWidth * 0.78f);
            }
        }

        // 3. Replace solid sphere on HandleBead with hollow contact ring + sparkle dust emitter
        if (handleBead != null)
        {
            DisableOpaqueSphereMesh(handleBead);
            handleRing = GetOrCreateCircleRing(handleBead.transform, "ContactReticleRing", innerR * 0.9f, reticleLineWidth * 1.1f);
            EnsureSparkleParticles(handleBead.transform);
        }

        // 4. Keep directionArrow hidden by default so direction is not spoiled
        if (directionArrow != null)
        {
            directionArrow.SetActive(false);
        }
    }

    private static void DisableOpaqueSphereMesh(GameObject nodeObj)
    {
        if (nodeObj == null) return;
        var mr = nodeObj.GetComponent<MeshRenderer>();
        if (mr != null && mr.enabled)
        {
            mr.enabled = false;
        }
    }

    private LineRenderer GetOrCreateCircleRing(Transform parent, string ringName, float worldRadius, float lineWidth)
    {
        Transform child = parent.Find(ringName);
        GameObject ringObj;
        if (child == null)
        {
            ringObj = new GameObject(ringName);
            ringObj.transform.SetParent(parent, false);
            ringObj.transform.localPosition = Vector3.zero;
            ringObj.transform.localRotation = Quaternion.identity;
            ringObj.transform.localScale = Vector3.one;
        }
        else
        {
            ringObj = child.gameObject;
        }

        LineRenderer lr = ringObj.GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = ringObj.AddComponent<LineRenderer>();
        }

        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = RingSegments;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        if (s_solidGlowMat != null) lr.sharedMaterial = s_solidGlowMat;

        // Compensate for parent scale so world radius is exact even if StartNode has scale (0.02, 0.02, 0.02)
        float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
        float localRadius = worldRadius / parentScale;

        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (float)i / RingSegments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * localRadius, Mathf.Sin(angle) * localRadius, 0f));
        }

        return lr;
    }

    private void EnsureSparkleParticles(Transform parent)
    {
        if (sparkleParticles == null)
        {
            Transform existing = parent.Find("SparkleTrail");
            if (existing != null)
            {
                sparkleParticles = existing.GetComponent<ParticleSystem>();
            }
        }

        if (sparkleParticles == null && Application.isPlaying)
        {
            GameObject pObj = new GameObject("SparkleTrail");
            pObj.transform.SetParent(parent, false);
            pObj.transform.localPosition = Vector3.zero;

            sparkleParticles = pObj.AddComponent<ParticleSystem>();
            var main = sparkleParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.004f, 0.015f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0022f, 0.0048f);
            main.startColor = new Color(0.45f, 0.96f, 1.0f, 0.85f);
            main.maxParticles = 80;

            var emission = sparkleParticles.emission;
            emission.rateOverTime = 24f;
            emission.rateOverDistance = 90f;

            var shape = sparkleParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.005f;

            var colOverLifetime = sparkleParticles.colorOverLifetime;
            colOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.55f, 0.98f, 1f), 0f), new GradientColorKey(new Color(0.2f, 0.85f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLifetime.color = grad;

            var psr = pObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null && s_sparkleMat != null)
            {
                psr.sharedMaterial = s_sparkleMat;
                psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                psr.receiveShadows = false;
            }
        }
    }

    private void OrientReticlesToCamera()
    {
        Camera cam = Camera.main;
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
        {
            cam = UnityEditor.SceneView.lastActiveSceneView.camera;
        }
#endif
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;

        if (startNode != null)
        {
            Vector3 toCam = (camPos - startNode.transform.position).normalized;
            if (toCam.sqrMagnitude > 0.001f)
            {
                startNode.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
            }
        }

        if (endNode != null && !isTapOnly)
        {
            Vector3 toCam = (camPos - endNode.transform.position).normalized;
            if (toCam.sqrMagnitude > 0.001f)
            {
                endNode.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
            }
        }

        if (handleBead != null && handleBead.activeSelf)
        {
            Vector3 toCam = (camPos - handleBead.transform.position).normalized;
            if (toCam.sqrMagnitude > 0.001f)
            {
                handleBead.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
            }
        }
    }

    /// <summary>
    /// Synchronizes the line renderers and hollow concentric reticles with the Start/End transforms.
    /// </summary>
    public void UpdateVisualPositions()
    {
        Vector3 startPos = GetStartWorldPosition();
        Vector3 endPos = GetEndWorldPosition();

        if (startNode != null) startNode.transform.position = startPos;
        if (endNode != null) endNode.transform.position = endPos;

        OrientReticlesToCamera();

        float outerR = GetEffectiveOuterRadius();
        float innerR = GetEffectiveInnerRadius();

        if (startOuterRing != null && startNode != null)
        {
            UpdateRingRadius(startOuterRing, startNode.transform, outerR);
        }
        if (startInnerRing != null && startNode != null)
        {
            UpdateRingRadius(startInnerRing, startNode.transform, innerR);
        }

        if (isTapOnly)
        {
            if (trackLine != null) trackLine.enabled = false;
            if (fillLine != null) fillLine.enabled = false;
            if (endNode != null) endNode.SetActive(false);
            if (directionArrow != null) directionArrow.SetActive(false);
            if (handleBead != null) handleBead.SetActive(false);
            return;
        }

        if (endOuterRing != null && endNode != null)
        {
            UpdateRingRadius(endOuterRing, endNode.transform, outerR);
        }
        if (endInnerRing != null && endNode != null)
        {
            UpdateRingRadius(endInnerRing, endNode.transform, innerR);
        }

        // Update Track Line connecting the outer perimeter of StartNode to the outer perimeter of EndNode
        // so the line never crosses through the hollow transparent centers of the reticles!
        if (trackLine != null)
        {
            Vector3 seg = endPos - startPos;
            float dist = seg.magnitude;
            Vector3 dir = dist > 0.0001f ? (seg / dist) : Vector3.forward;

            Vector3 lineStart = dist > (outerR * 2.2f) ? (startPos + dir * outerR) : startPos;
            Vector3 lineEnd = dist > (outerR * 2.2f) ? (endPos - dir * outerR) : endPos;

            trackLine.useWorldSpace = true;
            trackLine.positionCount = 2;
            trackLine.SetPosition(0, lineStart);
            trackLine.SetPosition(1, lineEnd);
            trackLine.textureMode = LineTextureMode.Tile;

            if (s_dashedBlueprintMat != null)
            {
                trackLine.sharedMaterial = s_dashedBlueprintMat;
            }
        }

        // Keep directionArrow off so student determines direction without clutter
        if (directionArrow != null)
        {
            directionArrow.SetActive(false);
        }
    }

    private void UpdateRingRadius(LineRenderer lr, Transform parent, float worldRadius)
    {
        if (lr == null || parent == null) return;
        if (lr.positionCount != RingSegments) lr.positionCount = RingSegments;

        float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
        float localRadius = worldRadius / parentScale;

        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (float)i / RingSegments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * localRadius, Mathf.Sin(angle) * localRadius, 0f));
        }
    }

    public SliderVisualState CurrentVisualState { get; private set; } = (SliderVisualState)(-1);
    private bool isShowingHintTracer = false;
    public bool IsShowingHintTracer => isShowingHintTracer;

    /// <summary>
    /// Sets the visual presentation according to the slider's state:
    /// - Upcoming: Subtle semi-transparent holographic blueprint (hollow cyan reticles + dashed line).
    /// - Active: Bright glowing cyan beam & reticles (when actively wiping or after 3 mistakes).
    /// - Dimmed: Low-opacity blueprint (~10%) while another zone displays the warm-gold remedial hint.
    /// - Completed: Hidden (replaced by Betadine puddle).
    /// </summary>
    public void SetVisualState(SliderVisualState state, bool force = false)
    {
        EnsureHolographicVisuals();
        if (!force && CurrentVisualState == state) return;
        CurrentVisualState = state;
        isShowingHintTracer = false;
        RestoreBaseRingAppearance();

        switch (state)
        {
            case SliderVisualState.Active:
                if (directionArrow != null) directionArrow.SetActive(false);
                if (trackLine != null)
                {
                    if (s_dashedBlueprintMat != null) trackLine.sharedMaterial = s_dashedBlueprintMat;
                    trackLine.startWidth = blueprintLineWidth * 1.25f;
                    trackLine.endWidth = blueprintLineWidth * 1.25f;
                    Color cLine = new Color(activeGlowColor.r, activeGlowColor.g, activeGlowColor.b, 0.72f);
                    trackLine.startColor = cLine;
                    trackLine.endColor = cLine;
                }
                if (fillLine != null)
                {
                    if (s_solidGlowMat != null) fillLine.sharedMaterial = s_solidGlowMat;
                    fillLine.startWidth = activeBeamWidth;
                    fillLine.endWidth = activeBeamWidth;
                    fillLine.startColor = activeGlowColor;
                    fillLine.endColor = activeGlowColor;
                }
                SetReticleColors(
                    new Color(activeGlowColor.r, activeGlowColor.g, activeGlowColor.b, 0.92f),
                    new Color(activeGlowColor.r, activeGlowColor.g, activeGlowColor.b, 0.68f)
                );
                break;

            case SliderVisualState.Upcoming:
                if (fillLine != null) fillLine.enabled = false;
                if (handleBead != null) handleBead.SetActive(false);
                if (directionArrow != null) directionArrow.SetActive(false);
                if (trackLine != null)
                {
                    if (s_dashedBlueprintMat != null) trackLine.sharedMaterial = s_dashedBlueprintMat;
                    trackLine.startWidth = blueprintLineWidth;
                    trackLine.endWidth = blueprintLineWidth;
                    trackLine.startColor = blueprintColor;
                    trackLine.endColor = blueprintColor;
                }
                SetReticleColors(
                    new Color(blueprintColor.r, blueprintColor.g, blueprintColor.b, 0.36f),
                    new Color(blueprintColor.r, blueprintColor.g, blueprintColor.b, 0.22f)
                );
                break;

            case SliderVisualState.Dimmed:
                if (fillLine != null) fillLine.enabled = false;
                if (handleBead != null) handleBead.SetActive(false);
                if (directionArrow != null) directionArrow.SetActive(false);
                if (trackLine != null)
                {
                    if (s_dashedBlueprintMat != null) trackLine.sharedMaterial = s_dashedBlueprintMat;
                    trackLine.startWidth = blueprintLineWidth * 0.75f;
                    trackLine.endWidth = blueprintLineWidth * 0.75f;
                    Color dimLine = new Color(blueprintColor.r, blueprintColor.g, blueprintColor.b, 0.08f);
                    trackLine.startColor = dimLine;
                    trackLine.endColor = dimLine;
                }
                SetReticleColors(
                    new Color(blueprintColor.r, blueprintColor.g, blueprintColor.b, 0.11f),
                    new Color(blueprintColor.r, blueprintColor.g, blueprintColor.b, 0.06f)
                );
                break;

            case SliderVisualState.Completed:
                SetVisualElementsVisible(false);
                break;

            case SliderVisualState.Mistake:
                if (fillLine != null) fillLine.enabled = false;
                if (handleBead != null) handleBead.SetActive(false);
                Color errCol = new Color(1.0f, 0.25f, 0.25f, 0.92f);
                if (trackLine != null)
                {
                    if (s_solidGlowMat != null) trackLine.sharedMaterial = s_solidGlowMat;
                    trackLine.startWidth = blueprintLineWidth * 1.55f;
                    trackLine.endWidth = blueprintLineWidth * 1.55f;
                    trackLine.startColor = errCol;
                    trackLine.endColor = errCol;
                }
                SetReticleColors(errCol, new Color(1f, 0.4f, 0.4f, 0.72f));
                break;
        }
    }

    /// <summary>
    /// Animates a high-visibility Warm Gold breathing beacon and directional bead tracer on this stroke
    /// after 3 mistakes so the target zone stands out clearly against dimmed non-target tracks.
    /// </summary>
    public void UpdatePulseHint(float time)
    {
        EnsureHolographicVisuals();
        isShowingHintTracer = true;

        float wave = 0.5f + 0.5f * Mathf.Sin(time * 5.8f);
        Color primaryGold = Color.Lerp(hintAmberColor, hintGoldColor, wave);
        Color innerGold = Color.Lerp(hintGoldColor, Color.white, 0.35f * wave);

        Color outerC = new Color(primaryGold.r, primaryGold.g, primaryGold.b, Mathf.Lerp(0.75f, 1.0f, wave));
        Color innerC = new Color(innerGold.r, innerGold.g, innerGold.b, Mathf.Lerp(0.60f, 0.95f, wave));
        SetReticleColors(outerC, innerC);

        float outerScale = isTapOnly ? Mathf.Lerp(1.02f, 1.42f, wave) : Mathf.Lerp(1.05f, 1.28f, wave);
        float startBiasScale = isBidirectional ? outerScale : outerScale * 1.08f;
        float ringWidth = reticleLineWidth * Mathf.Lerp(1.35f, 1.95f, wave);

        ApplyHintRingTransform(startOuterRing, startBiasScale, ringWidth);
        ApplyHintRingTransform(startInnerRing, Mathf.Lerp(1.0f, 1.20f, 1f - wave), ringWidth * 0.85f);
        ApplyHintRingTransform(endOuterRing, outerScale, ringWidth * (isBidirectional ? 1.0f : 0.90f));
        ApplyHintRingTransform(endInnerRing, Mathf.Lerp(1.0f, 1.15f, wave), ringWidth * 0.78f);

        if (isTapOnly)
        {
            if (handleBead != null) handleBead.SetActive(false);
            return;
        }

        if (trackLine != null)
        {
            if (s_solidGlowMat != null) trackLine.sharedMaterial = s_solidGlowMat;
            Color lc = new Color(primaryGold.r, primaryGold.g, primaryGold.b, Mathf.Lerp(0.68f, 1.0f, wave));
            trackLine.startColor = lc;
            trackLine.endColor = lc;
            float w = Mathf.Lerp(blueprintLineWidth * 1.85f, blueprintLineWidth * 2.85f, wave);
            trackLine.startWidth = w;
            trackLine.endWidth = w;
        }

        if (handleBead != null)
        {
            if (!handleBead.activeSelf) handleBead.SetActive(true);
            float tracerT = isBidirectional
                ? Mathf.PingPong(time * 0.85f, 1.0f)
                : Mathf.Repeat(time * 0.90f, 1.0f);
            Vector3 tracerPos = Vector3.Lerp(GetStartWorldPosition(), GetEndWorldPosition(), tracerT);
            handleBead.transform.position = tracerPos;

            if (handleRing != null)
            {
                handleRing.startColor = innerGold;
                handleRing.endColor = innerGold;
                handleRing.startWidth = reticleLineWidth * 1.55f;
                handleRing.endWidth = reticleLineWidth * 1.55f;
                handleRing.transform.localScale = Vector3.one * 1.22f;
            }
        }
    }

    private static void ApplyHintRingTransform(LineRenderer lr, float scale, float width)
    {
        if (lr == null) return;
        lr.transform.localScale = Vector3.one * scale;
        lr.startWidth = width;
        lr.endWidth = width;
    }

    private void RestoreBaseRingAppearance()
    {
        ApplyHintRingTransform(startOuterRing, 1f, reticleLineWidth);
        ApplyHintRingTransform(startInnerRing, 1f, reticleLineWidth * 0.78f);
        ApplyHintRingTransform(endOuterRing, 1f, reticleLineWidth);
        ApplyHintRingTransform(endInnerRing, 1f, reticleLineWidth * 0.78f);
        ApplyHintRingTransform(handleRing, 1f, reticleLineWidth * 1.1f);
    }

    private void SetReticleColors(Color outerColor, Color innerColor)
    {
        ApplyRingColor(startOuterRing, outerColor);
        ApplyRingColor(startInnerRing, innerColor);
        ApplyRingColor(endOuterRing, outerColor);
        ApplyRingColor(endInnerRing, innerColor);
        ApplyRingColor(handleRing, outerColor);
    }

    private static void ApplyRingColor(LineRenderer lr, Color c)
    {
        if (lr == null) return;
        lr.startColor = c;
        lr.endColor = c;
    }

    /// <summary>
    /// Enables or disables all 3D line renderers, hollow reticle nodes, and beads on this slider track.
    /// Does NOT touch puddleLayer so applied Betadine remains visible and washable!
    /// </summary>
    public void SetVisualElementsVisible(bool visible)
    {
        if (trackLine != null) trackLine.enabled = visible && !isTapOnly;
        if (!visible && fillLine != null) fillLine.enabled = false;
        if (startNode != null) startNode.SetActive(visible);
        if (endNode != null) endNode.SetActive(visible && !isTapOnly);
        if (directionArrow != null) directionArrow.SetActive(false);
        if (!visible && handleBead != null)
        {
            if (sparkleParticles != null && sparkleParticles.isPlaying) sparkleParticles.Stop();
            handleBead.SetActive(false);
        }
    }

    /// <summary>
    /// Updates real-time wipe progress (0% to 100%) as the cotton slides along the stroke.
    /// Supports both forward (StartPoint -> EndPoint) and reverse (EndPoint -> StartPoint for Mons Pubis) fill.
    /// </summary>
    public void UpdateProgress(float progress, Vector3 handlePos)
    {
        Vector3 originPos = isReverseFill ? GetEndWorldPosition() : GetStartWorldPosition();
        bool isWiping = progress > 0.01f;

        if (fillLine != null)
        {
            if (s_solidGlowMat != null) fillLine.sharedMaterial = s_solidGlowMat;
            fillLine.enabled = isWiping && !isTapOnly;
            fillLine.useWorldSpace = true;
            fillLine.positionCount = 2;
            fillLine.startWidth = activeBeamWidth;
            fillLine.endWidth = activeBeamWidth;
            fillLine.startColor = activeGlowColor;
            fillLine.endColor = activeGlowColor;
            fillLine.SetPosition(0, originPos);
            fillLine.SetPosition(1, handlePos);
        }

        if (handleBead != null)
        {
            bool showActiveBead = (isWiping && !isTapOnly) || isShowingHintTracer;
            handleBead.SetActive(showActiveBead);
            if (isWiping && !isTapOnly)
            {
                isShowingHintTracer = false;
                RestoreBaseRingAppearance();
                if (handleRing != null)
                {
                    handleRing.startColor = activeGlowColor;
                    handleRing.endColor = activeGlowColor;
                }
                handleBead.transform.position = handlePos;
            }

            if (sparkleParticles != null)
            {
                if (isWiping && !sparkleParticles.isPlaying) sparkleParticles.Play();
                else if (!isWiping && sparkleParticles.isPlaying) sparkleParticles.Stop();
            }
        }
    }

    public void ResetProgress()
    {
        isReverseFill = false;
        isShowingHintTracer = false;
        CurrentVisualState = (SliderVisualState)(-1);
        RestoreBaseRingAppearance();
        Vector3 startPos = GetStartWorldPosition();

        if (fillLine != null)
        {
            fillLine.enabled = false;
            fillLine.SetPosition(0, startPos);
            fillLine.SetPosition(1, startPos);
        }

        if (handleBead != null)
        {
            if (sparkleParticles != null && sparkleParticles.isPlaying) sparkleParticles.Stop();
            handleBead.transform.position = startPos;
            handleBead.SetActive(false);
        }
    }

    public void ApplyMaterial(Material mat)
    {
        // Preserved for external API compatibility; holographic shaders are managed via SetVisualState.
    }

    private void OnDrawGizmos()
    {
        if (!showInEditor) return;

        Vector3 startPos = GetStartWorldPosition();
        Vector3 endPos = GetEndWorldPosition();
        float outerR = GetEffectiveOuterRadius();

        Gizmos.color = new Color(0.25f, 0.88f, 1f, 0.65f);

        if (isTapOnly)
        {
            Gizmos.DrawWireSphere(startPos, outerR);
        }
        else
        {
            Gizmos.DrawLine(startPos, endPos);
            Gizmos.color = isBidirectional ? new Color(0.25f, 0.95f, 1f, 0.75f) : new Color(0.2f, 1f, 0.6f, 0.75f);
            Gizmos.DrawWireSphere(startPos, outerR);
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.75f);
            Gizmos.DrawWireSphere(endPos, outerR);
        }

#if UNITY_EDITOR
        string dirTag = isTapOnly ? "(Tap)" : (isBidirectional ? "(<-> Bidirectional)" : "");
        UnityEditor.Handles.Label(Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * 0.015f, $"[{strokeIndex + 1}] {strokeName} {dirTag}");
#endif
    }
}
