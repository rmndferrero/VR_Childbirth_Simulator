using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum HolographicButtonType
{
    None,
    Grip,
    Trigger,
    ButtonA
}

/// <summary>
/// Renders 3D Holographic VR Controller instruction rigs, orthogonal leader lines,
/// pulsing button highlights, target object contour outline, and holographic targeting rings.
/// Preserves all Hierarchy/Inspector UI styling and scene placements without hardcoded overwrites.
/// </summary>
[ExecuteAlways]
public class HolographicTutorialGuide : MonoBehaviour
{
    [Header("Grip Interaction Rig (Optional Separate Rig)")]
    public Transform gripRigRoot;
    public Transform gripModelRoot;
    public Transform gripHighlightAnchor;
    public Transform gripActiveHighlight;
    public LineRenderer gripLeaderLine;
    public CanvasGroup gripCalloutCanvasGroup;
    public TextMeshProUGUI gripUpperCalloutText;
    public TextMeshProUGUI gripLowerCalloutText;
    public TextMeshProUGUI gripButtonBadgeText;
    public Transform gripUpperCalloutAnchor;

    [Header("Trigger / Interact Rig (Optional Separate Rig)")]
    public Transform triggerRigRoot;
    public Transform triggerModelRoot;
    public Transform triggerHighlightAnchor;
    public Transform triggerActiveHighlight;
    public LineRenderer triggerLeaderLine;
    public LineRenderer triggerLaserPointerLine;
    public CanvasGroup triggerCalloutCanvasGroup;
    public TextMeshProUGUI triggerUpperCalloutText;
    public TextMeshProUGUI triggerLowerCalloutText;
    public TextMeshProUGUI triggerButtonBadgeText;
    public Transform triggerUpperCalloutAnchor;

    [Header("Button A Rig (Optional Separate Rig)")]
    public Transform buttonARigRoot;
    public Transform buttonAModelRoot;
    public Transform buttonAHighlightAnchor;
    public Transform buttonAActiveHighlight;
    public LineRenderer buttonALeaderLine;
    public CanvasGroup buttonACalloutCanvasGroup;
    public TextMeshProUGUI buttonAUpperCalloutText;
    public TextMeshProUGUI buttonALowerCalloutText;
    public TextMeshProUGUI buttonAButtonBadgeText;
    public Transform buttonAUpperCalloutAnchor;

    [Header("Main / Shared 3D Holographic Controller Rig")]
    public Transform controllerRigRoot;
    public Transform controllerModelRoot;
    public Transform gripButtonAnchor;
    public Transform triggerButtonAnchor;
    public Transform buttonAAnchor;
    public Transform laserOriginAnchor;
    public Transform activeButtonHighlight;
    public LineRenderer upperLeaderLine;
    public LineRenderer lowerLeaderLine;
    public LineRenderer laserPointerLine;
    public CanvasGroup controllerCalloutCanvasGroup;
    public TextMeshProUGUI upperCalloutText;
    public TextMeshProUGUI lowerCalloutText;
    public TextMeshProUGUI buttonBadgeText;
    public Transform upperCalloutAnchor;
    public Transform lowerCalloutAnchor;

    [Header("Manual Glow Fine-Tuning Offsets (Optional)")]
    public Vector3 gripGlowLocalOffset = Vector3.zero;
    public Vector3 triggerGlowLocalOffset = Vector3.zero;
    public Vector3 buttonAGlowLocalOffset = Vector3.zero;

    [Header("Target Object Holographic Ring & Contour Outline")]
    public Transform targetRingRoot;
    public CanvasGroup targetRingCanvasGroup;
    public TextMeshProUGUI targetRingLabel;
    public Image targetRingOuterImage;
    public Image targetRingInnerImage;
    public Image targetRingMiddleImage;
    public Image targetRingRippleImage1;
    public Image targetRingRippleImage2;
    public Material targetSilhouetteMaterial;

    [Header("Follow & Ergonomics Settings")]
    [Tooltip("When TRUE, the active 3D controller floats in player's view. When FALSE, keeps manual Scene View position.")]
    public bool lockControllerToHeadset = false;
    [Tooltip("When FALSE, the target ring always stays at your manual RingPos anchor in the scene.")]
    public bool autoSnapRingToTargetInPlayMode = false;
    public bool billboardTargetRingInPlayMode = true;
    public Vector3 controllerViewOffset = new Vector3(0.18f, -0.10f, 0.52f);
    public float smoothFollowSpeed = 6.0f;

    [Header("State (Runtime & Editor Preview)")]
    public HolographicButtonType currentButton = HolographicButtonType.Grip;
    [Range(0f, 1f)] public float masterAlpha = 1.0f;
    public bool isTargetRingVisible = true;
    public bool isRingCirclesVisible = true;

    private Camera mainCam;
    private Transform currentTargetObject;
    private Transform currentRingAnchor;
    private Vector3 currentTargetOffset = Vector3.zero;
    private Coroutine fadeRoutine;
    private Vector3 rigVelocity = Vector3.zero;
    private Vector3 lastEditorRingPosition = Vector3.zero;
    private readonly Dictionary<Transform, Vector3> baseLocalPositions = new Dictionary<Transform, Vector3>();

    private readonly List<Renderer> controllerRenderers = new List<Renderer>();
    private readonly List<Renderer> highlightRenderers = new List<Renderer>();
    private readonly List<GameObject> activeOutlineObjects = new List<GameObject>();
    private readonly List<Renderer> activeOutlineRenderers = new List<Renderer>();
    private MaterialPropertyBlock propBlock;

    private static readonly Color CyanPulseBright = new Color(0.25f, 0.95f, 1.0f, 0.95f);
    private static readonly Color TargetOutlineEdge = new Color(0.22f, 0.88f, 1.0f, 0.92f);

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        CacheBaseLocalPositions();
        AutoResolveAnchors();
        EnsureGlowAnchorsExist();
        CacheRenderers();
    }

    private void CacheBaseLocalPositions()
    {
        if (gripRigRoot != null) baseLocalPositions[gripRigRoot] = gripRigRoot.localPosition;
        if (triggerRigRoot != null) baseLocalPositions[triggerRigRoot] = triggerRigRoot.localPosition;
        if (buttonARigRoot != null) baseLocalPositions[buttonARigRoot] = buttonARigRoot.localPosition;
        if (controllerRigRoot != null) baseLocalPositions[controllerRigRoot] = controllerRigRoot.localPosition;
    }

    private void OnEnable()
    {
        propBlock = new MaterialPropertyBlock();
        CacheBaseLocalPositions();
        AutoResolveAnchors();
        EnsureGlowAnchorsExist();
        CacheRenderers();

        if (!Application.isPlaying)
        {
            if (currentButton == HolographicButtonType.None)
                currentButton = HolographicButtonType.Grip;
            SetAlphaImmediate(1f);
            PositionActiveButtonHighlight();
            UpdateAllLeaderLines();
        }
    }

    private void OnDisable()
    {
        RestoreTargetObjectSilhouette();
    }

    public Transform GetActiveRigForButton(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripRigRoot != null) return gripRigRoot;
                break;
            case HolographicButtonType.Trigger:
                if (triggerRigRoot != null) return triggerRigRoot;
                break;
            case HolographicButtonType.ButtonA:
                if (buttonARigRoot != null) return buttonARigRoot;
                break;
        }
        return controllerRigRoot;
    }

    public Transform GetActiveModelRoot(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripModelRoot != null) return gripModelRoot;
                break;
            case HolographicButtonType.Trigger:
                if (triggerModelRoot != null) return triggerModelRoot;
                break;
            case HolographicButtonType.ButtonA:
                if (buttonAModelRoot != null) return buttonAModelRoot;
                break;
        }
        return controllerModelRoot;
    }

    public Transform GetActiveHighlightAnchor(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripHighlightAnchor != null) return gripHighlightAnchor;
                if (gripButtonAnchor != null) return gripButtonAnchor;
                return gripModelRoot != null ? gripModelRoot : controllerModelRoot;

            case HolographicButtonType.Trigger:
                if (triggerHighlightAnchor != null) return triggerHighlightAnchor;
                if (triggerButtonAnchor != null) return triggerButtonAnchor;
                return triggerModelRoot != null ? triggerModelRoot : controllerModelRoot;

            case HolographicButtonType.ButtonA:
                if (buttonAHighlightAnchor != null) return buttonAHighlightAnchor;
                if (buttonAAnchor != null) return buttonAAnchor;
                return buttonAModelRoot != null ? buttonAModelRoot : controllerModelRoot;

            default:
                return controllerModelRoot;
        }
    }

    public Transform GetCurrentButtonAnchor()
    {
        return GetActiveHighlightAnchor(currentButton);
    }

    public Transform GetActiveHighlightObject(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripActiveHighlight != null) return gripActiveHighlight;
                break;
            case HolographicButtonType.Trigger:
                if (triggerActiveHighlight != null) return triggerActiveHighlight;
                break;
            case HolographicButtonType.ButtonA:
                if (buttonAActiveHighlight != null) return buttonAActiveHighlight;
                break;
        }
        return activeButtonHighlight;
    }

    public LineRenderer GetActiveLeaderLine(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripLeaderLine != null) return gripLeaderLine;
                break;
            case HolographicButtonType.Trigger:
                if (triggerLeaderLine != null) return triggerLeaderLine;
                break;
            case HolographicButtonType.ButtonA:
                if (buttonALeaderLine != null) return buttonALeaderLine;
                break;
        }
        return upperLeaderLine;
    }

    public CanvasGroup GetActiveCalloutCanvasGroup(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripCalloutCanvasGroup != null) return gripCalloutCanvasGroup;
                break;
            case HolographicButtonType.Trigger:
                if (triggerCalloutCanvasGroup != null) return triggerCalloutCanvasGroup;
                break;
            case HolographicButtonType.ButtonA:
                if (buttonACalloutCanvasGroup != null) return buttonACalloutCanvasGroup;
                break;
        }
        return controllerCalloutCanvasGroup;
    }

    public TextMeshProUGUI GetActiveUpperCalloutText(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripUpperCalloutText != null) return gripUpperCalloutText;
                break;
            case HolographicButtonType.Trigger:
                if (triggerUpperCalloutText != null) return triggerUpperCalloutText;
                break;
            case HolographicButtonType.ButtonA:
                if (buttonAUpperCalloutText != null) return buttonAUpperCalloutText;
                break;
        }
        return upperCalloutText;
    }

    public TextMeshProUGUI GetActiveButtonBadgeText(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripButtonBadgeText != null) return gripButtonBadgeText;
                break;
            case HolographicButtonType.Trigger:
                if (triggerButtonBadgeText != null) return triggerButtonBadgeText;
                break;
            case HolographicButtonType.ButtonA:
                if (buttonAButtonBadgeText != null) return buttonAButtonBadgeText;
                break;
        }
        return buttonBadgeText;
    }

    public Transform GetActiveUpperCalloutAnchor(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip:
                if (gripUpperCalloutAnchor != null) return gripUpperCalloutAnchor;
                if (gripUpperCalloutText != null) return gripUpperCalloutText.transform;
                if (gripCalloutCanvasGroup != null) return gripCalloutCanvasGroup.transform;
                break;
            case HolographicButtonType.Trigger:
                if (triggerUpperCalloutAnchor != null) return triggerUpperCalloutAnchor;
                if (triggerUpperCalloutText != null) return triggerUpperCalloutText.transform;
                if (triggerCalloutCanvasGroup != null) return triggerCalloutCanvasGroup.transform;
                break;
            case HolographicButtonType.ButtonA:
                if (buttonAUpperCalloutAnchor != null) return buttonAUpperCalloutAnchor;
                if (buttonAUpperCalloutText != null) return buttonAUpperCalloutText.transform;
                if (buttonACalloutCanvasGroup != null) return buttonACalloutCanvasGroup.transform;
                break;
        }
        if (upperCalloutAnchor != null) return upperCalloutAnchor;
        if (upperCalloutText != null) return upperCalloutText.transform;
        return controllerCalloutCanvasGroup != null ? controllerCalloutCanvasGroup.transform : null;
    }

    public void CacheRenderers()
    {
        controllerRenderers.Clear();
        highlightRenderers.Clear();

        CacheRigRenderers(gripModelRoot, gripActiveHighlight);
        CacheRigRenderers(triggerModelRoot, triggerActiveHighlight);
        CacheRigRenderers(buttonAModelRoot, buttonAActiveHighlight);
        CacheRigRenderers(controllerModelRoot, activeButtonHighlight);
    }

    private void CacheRigRenderers(Transform modelRoot, Transform highlight)
    {
        if (modelRoot != null)
        {
            foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (highlight != null && r.transform.IsChildOf(highlight)) continue;
                if (!controllerRenderers.Contains(r)) controllerRenderers.Add(r);
            }
        }
        if (highlight != null)
        {
            foreach (var r in highlight.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && !highlightRenderers.Contains(r)) highlightRenderers.Add(r);
            }
        }
    }

    public void AutoResolveAnchors()
    {
        ResolveAnchorsForRig(gripModelRoot, gripRigRoot, ref gripButtonAnchor, ref gripHighlightAnchor, "GripButton_GlowAnchor", "right_b_trigger_grip", "b_trigger_grip", "Bumper");
        ResolveAnchorsForRig(triggerModelRoot, triggerRigRoot, ref triggerButtonAnchor, ref triggerHighlightAnchor, "TriggerButton_GlowAnchor", "right_b_trigger_front", "b_trigger_front", "Trigger");
        ResolveAnchorsForRig(buttonAModelRoot, buttonARigRoot, ref buttonAAnchor, ref buttonAHighlightAnchor, "ButtonA_GlowAnchor", "b_button_a", "Button_A", null);

        if (controllerModelRoot != null)
        {
            if (gripButtonAnchor == null)
                gripButtonAnchor = FindChildRecursive(controllerModelRoot, "right_b_trigger_grip")
                                ?? FindChildRecursive(controllerModelRoot, "b_trigger_grip")
                                ?? FindChildRecursive(controllerModelRoot, "Bumper");

            if (triggerButtonAnchor == null)
                triggerButtonAnchor = FindChildRecursive(controllerModelRoot, "right_b_trigger_front")
                                   ?? FindChildRecursive(controllerModelRoot, "b_trigger_front")
                                   ?? FindChildRecursive(controllerModelRoot, "Trigger");

            if (buttonAAnchor == null)
                buttonAAnchor = FindChildRecursive(controllerModelRoot, "b_button_a")
                             ?? FindChildRecursive(controllerModelRoot, "Button_A");

            if (laserOriginAnchor == null)
                laserOriginAnchor = FindChildRecursive(controllerModelRoot, "right_laser_begin")
                                 ?? triggerButtonAnchor;
        }

        Transform rigRoot = controllerRigRoot != null ? controllerRigRoot : transform;
        if (gripHighlightAnchor == null) gripHighlightAnchor = FindChildRecursive(rigRoot, "GripButton_GlowAnchor");
        if (triggerHighlightAnchor == null) triggerHighlightAnchor = FindChildRecursive(rigRoot, "TriggerButton_GlowAnchor");
        if (buttonAHighlightAnchor == null) buttonAHighlightAnchor = FindChildRecursive(rigRoot, "ButtonA_GlowAnchor");
    }

    private void ResolveAnchorsForRig(
        Transform modelRoot,
        Transform rigRoot,
        ref Transform boneAnchor,
        ref Transform glowAnchor,
        string glowAnchorName,
        string boneName1,
        string boneName2,
        string boneName3)
    {
        if (modelRoot != null && boneAnchor == null)
        {
            if (!string.IsNullOrEmpty(boneName1)) boneAnchor = FindChildRecursive(modelRoot, boneName1);
            if (boneAnchor == null && !string.IsNullOrEmpty(boneName2)) boneAnchor = FindChildRecursive(modelRoot, boneName2);
            if (boneAnchor == null && !string.IsNullOrEmpty(boneName3)) boneAnchor = FindChildRecursive(modelRoot, boneName3);
        }

        if (glowAnchor == null && rigRoot != null)
        {
            glowAnchor = FindChildRecursive(rigRoot, glowAnchorName);
        }
    }

    public void EnsureGlowAnchorsExist()
    {
        EnsureAnchorOnRig(gripRigRoot ?? controllerRigRoot, ref gripHighlightAnchor, "GripButton_GlowAnchor",
            new Vector3(-0.018f, -0.028f, -0.012f), Quaternion.Euler(15f, 90f, 0f), new Vector3(0.026f, 0.018f, 0.028f));

        EnsureAnchorOnRig(triggerRigRoot ?? controllerRigRoot, ref triggerHighlightAnchor, "TriggerButton_GlowAnchor",
            new Vector3(0.002f, -0.038f, 0.034f), Quaternion.Euler(75f, 0f, 0f), new Vector3(0.022f, 0.016f, 0.026f));

        EnsureAnchorOnRig(buttonARigRoot ?? controllerRigRoot, ref buttonAHighlightAnchor, "ButtonA_GlowAnchor",
            new Vector3(0.012f, 0.012f, -0.018f), Quaternion.Euler(30f, 25f, 0f), new Vector3(0.018f, 0.014f, 0.018f));
    }

    private void EnsureAnchorOnRig(Transform rig, ref Transform anchor, string name, Vector3 localPos, Quaternion localRot, Vector3 localScale)
    {
        if (rig == null || anchor != null) return;

        anchor = FindChildRecursive(rig, name);
        if (anchor == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            anchor = go.transform;
        }
    }

    private static Transform FindChildRecursive(Transform parent, string exactOrPartialName)
    {
        if (parent == null) return null;
        foreach (var tr in parent.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(tr.name, exactOrPartialName, System.StringComparison.OrdinalIgnoreCase))
                return tr;
        }
        foreach (var tr in parent.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name.IndexOf(exactOrPartialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return tr;
        }
        return null;
    }

    /// <summary>
    /// Binds a dedicated per-step World-Space UI Canvas from the Hierarchy as the active target guide UI,
    /// preserving its Scene View position, scale, and Hierarchy-edited label text.
    /// </summary>
    public void BindActiveRingUI(Transform ringUI, bool overwriteLabel = false, string labelOverride = null)
    {
        if (ringUI == null) return;

        if (Application.isPlaying && targetRingRoot != null && targetRingRoot != ringUI)
        {
            targetRingRoot.gameObject.SetActive(false);
        }

        targetRingRoot = ringUI;
        targetRingCanvasGroup = ringUI.GetComponent<CanvasGroup>();

        var bannerLabel = FindChildRecursive(ringUI, "TargetLabelText");
        targetRingLabel = bannerLabel != null
            ? bannerLabel.GetComponent<TextMeshProUGUI>()
            : ringUI.GetComponentInChildren<TextMeshProUGUI>(true);

        if (overwriteLabel && !string.IsNullOrEmpty(labelOverride) && targetRingLabel != null)
        {
            targetRingLabel.text = labelOverride;
        }

        var outerTr = FindChildRecursive(ringUI, "OuterCyanRing");
        targetRingOuterImage = outerTr != null ? outerTr.GetComponent<Image>() : null;

        var innerTr = FindChildRecursive(ringUI, "InnerGlowDisc");
        targetRingInnerImage = innerTr != null ? innerTr.GetComponent<Image>() : null;

        var midTr = FindChildRecursive(ringUI, "MiddleReticleRing");
        targetRingMiddleImage = midTr != null ? midTr.GetComponent<Image>() : null;

        var rip1Tr = FindChildRecursive(ringUI, "PulseRippleRing");
        targetRingRippleImage1 = rip1Tr != null ? rip1Tr.GetComponent<Image>() : null;

        var rip2Tr = FindChildRecursive(ringUI, "PulseRippleRing2");
        targetRingRippleImage2 = rip2Tr != null ? rip2Tr.GetComponent<Image>() : null;
    }

    public void ShowInstruction(
        HolographicButtonType buttonType,
        string upperCallout,
        string lowerCallout,
        string badgeLabel,
        Transform targetObject,
        string targetLabel,
        Vector3 targetWorldOffset,
        bool showLaserRay = false,
        bool showTargetRing = true,
        Transform ringAnchor = null,
        bool showRingCircles = true,
        bool preserveRingHierarchyText = false)
    {
        AutoResolveAnchors();
        currentButton = buttonType;

        bool anchorIsDedicatedUI = ringAnchor != null && ringAnchor.GetComponent<Canvas>() != null;
        if (anchorIsDedicatedUI)
        {
            BindActiveRingUI(ringAnchor, overwriteLabel: !preserveRingHierarchyText, labelOverride: targetLabel);
            currentRingAnchor = null;
        }
        else
        {
            currentRingAnchor = ringAnchor;
        }

        isTargetRingVisible = showTargetRing && (targetObject != null || currentRingAnchor != null || anchorIsDedicatedUI);
        isRingCirclesVisible = showRingCircles;
        SetTargetObjectWithSilhouette(targetObject);
        currentTargetOffset = targetWorldOffset;

        if (!string.IsNullOrEmpty(upperCallout))
        {
            var upperTxt = GetActiveUpperCalloutText(buttonType);
            if (upperTxt != null) upperTxt.text = upperCallout;
            if (upperCalloutText != null && upperCalloutText != upperTxt) upperCalloutText.text = upperCallout;
        }

        if (!string.IsNullOrEmpty(badgeLabel))
        {
            var badgeTxt = GetActiveButtonBadgeText(buttonType);
            if (badgeTxt != null) badgeTxt.text = badgeLabel;
            if (buttonBadgeText != null && buttonBadgeText != badgeTxt) buttonBadgeText.text = badgeLabel;
        }

        if (!string.IsNullOrEmpty(lowerCallout) && lowerCalloutText != null)
            lowerCalloutText.text = lowerCallout;

        if (!preserveRingHierarchyText && !string.IsNullOrEmpty(targetLabel) && targetRingLabel != null)
            targetRingLabel.text = targetLabel;

        // Target Ring & Pulsing Circles
        UpdateRingCircleVisibility();
        if (targetRingRoot != null)
        {
            targetRingRoot.gameObject.SetActive(isTargetRingVisible);
            if (isTargetRingVisible)
            {
                if (currentRingAnchor != null)
                {
                    targetRingRoot.position = currentRingAnchor.position;
                    lastEditorRingPosition = targetRingRoot.position;
                }
                else if (!anchorIsDedicatedUI && autoSnapRingToTargetInPlayMode && targetObject != null)
                {
                    targetRingRoot.position = GetTargetVisualCenter(targetObject) + currentTargetOffset;
                }
            }
        }

        // Active rig activation: in Play Mode, show only the active segment rig
        Transform activeRig = GetActiveRigForButton(buttonType);
        if (Application.isPlaying)
        {
            SetRigActiveSafe(gripRigRoot, gripRigRoot == activeRig);
            SetRigActiveSafe(triggerRigRoot, triggerRigRoot == activeRig);
            SetRigActiveSafe(buttonARigRoot, buttonARigRoot == activeRig);
            if (controllerRigRoot != null && controllerRigRoot != activeRig)
            {
                controllerRigRoot.gameObject.SetActive(false);
            }
        }

        if (activeRig != null)
        {
            activeRig.gameObject.SetActive(true);
            if (lockControllerToHeadset && Application.isPlaying)
            {
                SnapRigToView(activeRig);
            }
        }

        // Laser ray
        LineRenderer activeLaser = (buttonType == HolographicButtonType.Trigger && triggerLaserPointerLine != null)
            ? triggerLaserPointerLine
            : laserPointerLine;
        if (activeLaser != null) activeLaser.gameObject.SetActive(showLaserRay);
        if (laserPointerLine != null && laserPointerLine != activeLaser) laserPointerLine.gameObject.SetActive(false);
        if (triggerLaserPointerLine != null && triggerLaserPointerLine != activeLaser) triggerLaserPointerLine.gameObject.SetActive(false);

        PositionActiveButtonHighlight();
        UpdateAllLeaderLines();

        if (Application.isPlaying)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(1f, 0.40f));
        }
        else
        {
            SetAlphaImmediate(1f);
        }
    }

    private void UpdateRingCircleVisibility()
    {
        if (targetRingOuterImage != null && targetRingOuterImage.gameObject.activeSelf != isRingCirclesVisible)
            targetRingOuterImage.gameObject.SetActive(isRingCirclesVisible);

        if (targetRingInnerImage != null && targetRingInnerImage.gameObject.activeSelf != isRingCirclesVisible)
            targetRingInnerImage.gameObject.SetActive(isRingCirclesVisible);

        if (targetRingMiddleImage != null && targetRingMiddleImage.gameObject.activeSelf != isRingCirclesVisible)
            targetRingMiddleImage.gameObject.SetActive(isRingCirclesVisible);

        if (targetRingRippleImage1 != null && targetRingRippleImage1.gameObject.activeSelf != isRingCirclesVisible)
            targetRingRippleImage1.gameObject.SetActive(isRingCirclesVisible);

        if (targetRingRippleImage2 != null && targetRingRippleImage2.gameObject.activeSelf != isRingCirclesVisible)
            targetRingRippleImage2.gameObject.SetActive(isRingCirclesVisible);
    }

    private static void SetRigActiveSafe(Transform rig, bool active)
    {
        if (rig != null && rig.gameObject.activeSelf != active)
        {
            rig.gameObject.SetActive(active);
        }
    }

    public Coroutine FadeOutInstruction(float duration = 0.40f)
    {
        RestoreTargetObjectSilhouette();
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(0f, duration));
        return fadeRoutine;
    }

    public void UpdateTargetAndCallout(
        HolographicButtonType buttonType,
        string upperCallout,
        string lowerCallout,
        string badgeLabel,
        Transform targetObject,
        string targetLabel,
        Vector3 targetWorldOffset,
        bool showLaserRay = false,
        bool showTargetRing = true,
        Transform ringAnchor = null,
        bool showRingCircles = true,
        bool preserveRingHierarchyText = false)
    {
        currentButton = buttonType;

        bool anchorIsDedicatedUI = ringAnchor != null && ringAnchor.GetComponent<Canvas>() != null;
        if (anchorIsDedicatedUI)
        {
            BindActiveRingUI(ringAnchor, overwriteLabel: !preserveRingHierarchyText, labelOverride: targetLabel);
            currentRingAnchor = null;
        }
        else if (ringAnchor != null)
        {
            currentRingAnchor = ringAnchor;
        }

        isTargetRingVisible = showTargetRing && (targetObject != null || currentRingAnchor != null || anchorIsDedicatedUI);
        isRingCirclesVisible = showRingCircles;
        SetTargetObjectWithSilhouette(targetObject);
        currentTargetOffset = targetWorldOffset;

        if (!string.IsNullOrEmpty(upperCallout))
        {
            var upperTxt = GetActiveUpperCalloutText(buttonType);
            if (upperTxt != null) upperTxt.text = upperCallout;
            if (upperCalloutText != null && upperCalloutText != upperTxt) upperCalloutText.text = upperCallout;
        }

        if (!string.IsNullOrEmpty(badgeLabel))
        {
            var badgeTxt = GetActiveButtonBadgeText(buttonType);
            if (badgeTxt != null) badgeTxt.text = badgeLabel;
            if (buttonBadgeText != null && buttonBadgeText != badgeTxt) buttonBadgeText.text = badgeLabel;
        }

        if (!string.IsNullOrEmpty(lowerCallout) && lowerCalloutText != null)
            lowerCalloutText.text = lowerCallout;

        if (!preserveRingHierarchyText && !string.IsNullOrEmpty(targetLabel) && targetRingLabel != null)
            targetRingLabel.text = targetLabel;

        UpdateRingCircleVisibility();
        if (targetRingRoot != null)
        {
            targetRingRoot.gameObject.SetActive(isTargetRingVisible);
            if (targetRingCanvasGroup != null) targetRingCanvasGroup.alpha = masterAlpha;
            if (isTargetRingVisible)
            {
                if (currentRingAnchor != null)
                {
                    targetRingRoot.position = currentRingAnchor.position;
                    lastEditorRingPosition = targetRingRoot.position;
                }
                else if (!anchorIsDedicatedUI && autoSnapRingToTargetInPlayMode && targetObject != null)
                {
                    targetRingRoot.position = GetTargetVisualCenter(targetObject) + currentTargetOffset;
                }
            }
        }

        LineRenderer activeLaser = (buttonType == HolographicButtonType.Trigger && triggerLaserPointerLine != null)
            ? triggerLaserPointerLine
            : laserPointerLine;
        if (activeLaser != null) activeLaser.gameObject.SetActive(showLaserRay);

        PositionActiveButtonHighlight();
        UpdateAllLeaderLines();
    }

    /// <summary>
    /// Creates a clean, non-destructive thin cyan contour outline around the target mesh without
    /// modifying or tinting the target object's own materials or MaterialPropertyBlock.
    /// </summary>
    public void SetTargetObjectWithSilhouette(Transform target)
    {
        if (currentTargetObject == target && activeOutlineObjects.Count > 0) return;

        RestoreTargetObjectSilhouette();
        currentTargetObject = target;

        if (!Application.isPlaying) return;
        if (currentTargetObject == null || targetSilhouetteMaterial == null) return;
        if (currentTargetObject.GetComponentInParent<Canvas>() != null) return;

        foreach (var r in currentTargetObject.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || r.GetComponentInParent<Canvas>() != null) continue;
            if (r.gameObject.name == "__HolographicContourOutline") continue;

            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var outlineGo = new GameObject("__HolographicContourOutline");
            outlineGo.transform.SetParent(r.transform, false);
            outlineGo.transform.localPosition = Vector3.zero;
            outlineGo.transform.localRotation = Quaternion.identity;
            outlineGo.transform.localScale = Vector3.one * 1.012f;

            var outlineMf = outlineGo.AddComponent<MeshFilter>();
            outlineMf.sharedMesh = mf.sharedMesh;

            var outlineMr = outlineGo.AddComponent<MeshRenderer>();
            outlineMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineMr.receiveShadows = false;
            outlineMr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            outlineMr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            outlineMr.sharedMaterial = targetSilhouetteMaterial;

            activeOutlineObjects.Add(outlineGo);
            activeOutlineRenderers.Add(outlineMr);
        }
    }

    public void RestoreTargetObjectSilhouette()
    {
        for (int i = activeOutlineObjects.Count - 1; i >= 0; i--)
        {
            if (activeOutlineObjects[i] != null)
            {
                if (Application.isPlaying) Destroy(activeOutlineObjects[i]);
                else DestroyImmediate(activeOutlineObjects[i]);
            }
        }
        activeOutlineObjects.Clear();
        activeOutlineRenderers.Clear();
    }

    private static Vector3 GetTargetVisualCenter(Transform target)
    {
        if (target == null) return Vector3.zero;
        var mr = target.GetComponentInChildren<MeshRenderer>();
        if (mr != null) return mr.bounds.center;
        return target.position;
    }

    public Vector3 GetButtonGlowOffset(HolographicButtonType buttonType)
    {
        switch (buttonType)
        {
            case HolographicButtonType.Grip: return gripGlowLocalOffset;
            case HolographicButtonType.Trigger: return triggerGlowLocalOffset;
            case HolographicButtonType.ButtonA: return buttonAGlowLocalOffset;
            default: return Vector3.zero;
        }
    }

    public void PositionActiveButtonHighlight()
    {
        if (currentButton == HolographicButtonType.None) return;

        Transform anchor = GetActiveHighlightAnchor(currentButton);
        Transform highlight = GetActiveHighlightObject(currentButton);

        PositionHighlight(highlight, anchor, GetButtonGlowOffset(currentButton));

        if (!Application.isPlaying)
        {
            if (gripActiveHighlight != null && gripHighlightAnchor != null)
                PositionHighlight(gripActiveHighlight, gripHighlightAnchor, gripGlowLocalOffset);
            if (triggerActiveHighlight != null && triggerHighlightAnchor != null)
                PositionHighlight(triggerActiveHighlight, triggerHighlightAnchor, triggerGlowLocalOffset);
            if (buttonAActiveHighlight != null && buttonAHighlightAnchor != null)
                PositionHighlight(buttonAActiveHighlight, buttonAHighlightAnchor, buttonAGlowLocalOffset);
        }
    }

    private static void PositionHighlight(Transform highlight, Transform anchor, Vector3 localOffset)
    {
        if (highlight == null || anchor == null) return;

        highlight.gameObject.SetActive(true);
        Vector3 offset = anchor.rotation * localOffset;
        highlight.position = anchor.position + offset;
        highlight.rotation = anchor.rotation;

        Vector3 targetScale = anchor.localScale;
        targetScale.x = Mathf.Clamp(targetScale.x, 0.005f, 0.08f);
        targetScale.y = Mathf.Clamp(targetScale.y, 0.005f, 0.08f);
        targetScale.z = Mathf.Clamp(targetScale.z, 0.005f, 0.08f);
        highlight.localScale = targetScale;
    }

    private void SnapRigToView(Transform rig)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || rig == null) return;

        Vector3 flatFwd = mainCam.transform.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
        flatFwd.Normalize();

        Quaternion viewYaw = Quaternion.LookRotation(flatFwd, Vector3.up);
        rig.position = mainCam.transform.position + viewYaw * controllerViewOffset;
        rig.rotation = viewYaw;
    }

    private void LateUpdate()
    {
        // ─── EDIT MODE ───
        if (!Application.isPlaying)
        {
            CacheBaseLocalPositions();
            AutoResolveAnchors();
            EnsureGlowAnchorsExist();
            PositionActiveButtonHighlight();
            UpdateAllLeaderLines();

            // Bidirectional sync in Scene View: if you drag TargetObjectHolographicRing directly,
            // update currentRingAnchor; if you drag currentRingAnchor, update TargetObjectHolographicRing!
            if (targetRingRoot != null && currentRingAnchor != null)
            {
                if ((targetRingRoot.position - lastEditorRingPosition).sqrMagnitude > 0.000001f)
                {
                    currentRingAnchor.position = targetRingRoot.position;
                    lastEditorRingPosition = targetRingRoot.position;
                }
                else if ((currentRingAnchor.position - targetRingRoot.position).sqrMagnitude > 0.000001f)
                {
                    targetRingRoot.position = currentRingAnchor.position;
                    lastEditorRingPosition = targetRingRoot.position;
                }
            }
            return;
        }

        // ─── PLAY MODE ───
        if (masterAlpha <= 0.001f) return;
        if (mainCam == null) mainCam = Camera.main;

        Transform activeRig = GetActiveRigForButton(currentButton);
        if (activeRig != null)
        {
            if (lockControllerToHeadset && mainCam != null)
            {
                Vector3 flatFwd = mainCam.transform.forward;
                flatFwd.y = 0f;
                if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
                flatFwd.Normalize();

                Quaternion viewYaw = Quaternion.LookRotation(flatFwd, Vector3.up);
                float hoverBob = Mathf.Sin(Time.time * 2.2f) * 0.004f;
                Vector3 desiredPos = mainCam.transform.position + viewYaw * (controllerViewOffset + new Vector3(0f, hoverBob, 0f));

                activeRig.position = Vector3.SmoothDamp(activeRig.position, desiredPos, ref rigVelocity, 1f / smoothFollowSpeed);
                activeRig.rotation = Quaternion.Slerp(activeRig.rotation, viewYaw, Time.deltaTime * smoothFollowSpeed);
            }
            else
            {
                if (!baseLocalPositions.TryGetValue(activeRig, out Vector3 basePos))
                {
                    basePos = activeRig.localPosition;
                    baseLocalPositions[activeRig] = basePos;
                }
                float hoverBob = Mathf.Sin(Time.time * 2.2f) * 0.003f;
                activeRig.localPosition = basePos + new Vector3(0f, hoverBob, 0f);
            }
        }

        // Pulse button highlight
        Transform btnAnchor = GetActiveHighlightAnchor(currentButton);
        Transform highlight = GetActiveHighlightObject(currentButton);
        if (highlight != null && btnAnchor != null && currentButton != HolographicButtonType.None)
        {
            highlight.gameObject.SetActive(true);
            Vector3 offset = btnAnchor.rotation * GetButtonGlowOffset(currentButton);
            highlight.position = btnAnchor.position + offset;
            highlight.rotation = btnAnchor.rotation;

            float pulseScale = 1.0f + Mathf.Sin(Time.time * 5.5f) * 0.14f;
            Vector3 baseScale = btnAnchor.localScale;
            baseScale.x = Mathf.Clamp(baseScale.x, 0.005f, 0.08f);
            baseScale.y = Mathf.Clamp(baseScale.y, 0.005f, 0.08f);
            baseScale.z = Mathf.Clamp(baseScale.z, 0.005f, 0.08f);
            highlight.localScale = baseScale * pulseScale;
        }

        UpdateAllLeaderLines();
        UpdateTargetObjectRing();
        ApplyVisualAlpha(masterAlpha);
    }

    public void UpdateLeaderLines(Transform btnAnchor = null)
    {
        UpdateAllLeaderLines();
    }

    public void UpdateAllLeaderLines()
    {
        if (Application.isPlaying)
        {
            Transform anchor = GetActiveHighlightAnchor(currentButton);
            Transform callout = GetActiveUpperCalloutAnchor(currentButton);
            LineRenderer line = GetActiveLeaderLine(currentButton);
            UpdateSingleLeaderLine(line, anchor, callout);

            LineRenderer laser = (currentButton == HolographicButtonType.Trigger && triggerLaserPointerLine != null)
                ? triggerLaserPointerLine
                : laserPointerLine;
            UpdateLaserRay(laser, anchor);
        }
        else
        {
            UpdateSingleLeaderLine(gripLeaderLine, gripHighlightAnchor, gripUpperCalloutAnchor ?? (gripUpperCalloutText != null ? gripUpperCalloutText.transform : null));
            UpdateSingleLeaderLine(triggerLeaderLine, triggerHighlightAnchor, triggerUpperCalloutAnchor ?? (triggerUpperCalloutText != null ? triggerUpperCalloutText.transform : null));
            UpdateSingleLeaderLine(buttonALeaderLine, buttonAHighlightAnchor, buttonAUpperCalloutAnchor ?? (buttonAUpperCalloutText != null ? buttonAUpperCalloutText.transform : null));
            UpdateSingleLeaderLine(upperLeaderLine, GetActiveHighlightAnchor(currentButton), upperCalloutAnchor ?? (upperCalloutText != null ? upperCalloutText.transform : null));
            UpdateLaserRay(triggerLaserPointerLine ?? laserPointerLine, triggerHighlightAnchor);
        }
    }

    private static void UpdateSingleLeaderLine(LineRenderer line, Transform btnAnchor, Transform calloutAnchor)
    {
        if (line == null || btnAnchor == null || calloutAnchor == null) return;

        line.positionCount = 3;
        Vector3 startPos = btnAnchor.position;
        Vector3 endPos = calloutAnchor.position;
        Vector3 elbow = new Vector3(endPos.x, startPos.y, endPos.z);
        line.SetPosition(0, endPos);
        line.SetPosition(1, elbow);
        line.SetPosition(2, startPos);
    }

    private void UpdateLaserRay(LineRenderer laser, Transform originAnchor)
    {
        if (laser == null || !laser.gameObject.activeSelf) return;

        Vector3 rayStart = laserOriginAnchor != null ? laserOriginAnchor.position : (originAnchor != null ? originAnchor.position : transform.position);
        Vector3 rayEnd = currentTargetObject != null
            ? GetTargetVisualCenter(currentTargetObject) + currentTargetOffset
            : rayStart + (triggerModelRoot != null ? triggerModelRoot.forward * 0.35f : Vector3.forward * 0.35f);

        laser.positionCount = 2;
        laser.SetPosition(0, rayStart);
        laser.SetPosition(1, rayEnd);
    }

    private void UpdateTargetObjectRing()
    {
        if (targetRingRoot == null || !isTargetRingVisible) return;

        UpdateRingCircleVisibility();

        if (currentRingAnchor != null && !autoSnapRingToTargetInPlayMode)
        {
            targetRingRoot.position = currentRingAnchor.position;
        }
        else if (autoSnapRingToTargetInPlayMode && currentTargetObject != null)
        {
            Vector3 targetPos = GetTargetVisualCenter(currentTargetObject) + currentTargetOffset;
            targetRingRoot.position = Vector3.Lerp(targetRingRoot.position, targetPos, Time.deltaTime * 15f);
        }
        else if (currentRingAnchor != null)
        {
            targetRingRoot.position = currentRingAnchor.position;
        }

        if (billboardTargetRingInPlayMode && mainCam != null)
        {
            Vector3 dirToCam = targetRingRoot.position - mainCam.transform.position;
            dirToCam.y = 0f;
            if (dirToCam.sqrMagnitude > 0.001f)
            {
                targetRingRoot.rotation = Quaternion.LookRotation(dirToCam.normalized, Vector3.up);
            }
        }

        // Pulsing circle visual effect for OuterCyanRing, MiddleReticleRing, Sonar Waves & InnerGlowDisc
        if (isRingCirclesVisible)
        {
            bool hasMultiLayerReticle = targetRingMiddleImage != null || targetRingRippleImage1 != null;

            if (targetRingOuterImage != null)
            {
                float ringPulse = hasMultiLayerReticle
                    ? (1.0f + Mathf.Sin(Time.time * 4.2f) * 0.08f)
                    : (1.0f + Mathf.Sin(Time.time * 3.8f) * 0.06f);
                targetRingOuterImage.rectTransform.localScale = Vector3.one * ringPulse;
                if (hasMultiLayerReticle)
                {
                    targetRingOuterImage.rectTransform.Rotate(0f, 0f, -14f * Time.deltaTime);
                    Color c = targetRingOuterImage.color;
                    c.a = 0.84f + Mathf.Sin(Time.time * 4.2f) * 0.16f;
                    targetRingOuterImage.color = c;
                }
            }

            if (targetRingMiddleImage != null)
            {
                float midPulse = 0.96f + Mathf.Sin(Time.time * 4.2f + Mathf.PI) * 0.09f;
                targetRingMiddleImage.rectTransform.localScale = Vector3.one * midPulse;
                targetRingMiddleImage.rectTransform.Rotate(0f, 0f, 28f * Time.deltaTime);
                Color c = targetRingMiddleImage.color;
                c.a = 0.72f + Mathf.Sin(Time.time * 4.2f + 1.2f) * 0.22f;
                targetRingMiddleImage.color = c;
            }

            if (targetRingRippleImage1 != null)
            {
                float t1 = Mathf.Repeat(Time.time * 0.72f, 1f);
                float scale1 = Mathf.Lerp(0.55f, 1.48f, t1);
                float alpha1 = Mathf.Sin(t1 * Mathf.PI) * (1f - t1 * 0.30f) * 0.85f;
                targetRingRippleImage1.rectTransform.localScale = Vector3.one * scale1;
                Color c1 = targetRingRippleImage1.color;
                c1.a = alpha1;
                targetRingRippleImage1.color = c1;
            }

            if (targetRingRippleImage2 != null)
            {
                float t2 = Mathf.Repeat(Time.time * 0.72f + 0.5f, 1f);
                float scale2 = Mathf.Lerp(0.55f, 1.48f, t2);
                float alpha2 = Mathf.Sin(t2 * Mathf.PI) * (1f - t2 * 0.30f) * 0.85f;
                targetRingRippleImage2.rectTransform.localScale = Vector3.one * scale2;
                Color c2 = targetRingRippleImage2.color;
                c2.a = alpha2;
                targetRingRippleImage2.color = c2;
            }

            if (targetRingInnerImage != null)
            {
                float innerPulse = hasMultiLayerReticle
                    ? (0.92f + Mathf.Sin(Time.time * 5.0f + 0.6f) * 0.12f)
                    : (0.94f + Mathf.Sin(Time.time * 3.8f + 0.8f) * 0.06f);
                targetRingInnerImage.rectTransform.localScale = Vector3.one * innerPulse;
                if (hasMultiLayerReticle)
                {
                    Color ci = targetRingInnerImage.color;
                    ci.a = 0.36f + Mathf.Sin(Time.time * 5.0f) * 0.18f;
                    targetRingInnerImage.color = ci;
                }
            }
        }
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = masterAlpha;
        float elapsed = 0f;

        Transform activeRig = GetActiveRigForButton(currentButton);
        if (targetAlpha > 0f)
        {
            if (activeRig != null) activeRig.gameObject.SetActive(true);
            if (targetRingRoot != null && isTargetRingVisible) targetRingRoot.gameObject.SetActive(true);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float smoothT = t * t * (3f - 2f * t);
            SetAlphaImmediate(Mathf.Lerp(startAlpha, targetAlpha, smoothT));
            yield return null;
        }

        SetAlphaImmediate(targetAlpha);

        if (targetAlpha <= 0.001f)
        {
            if (activeRig != null) activeRig.gameObject.SetActive(false);
            if (targetRingRoot != null) targetRingRoot.gameObject.SetActive(false);
        }
    }

    public void SetAlphaImmediate(float alpha)
    {
        masterAlpha = Mathf.Clamp01(alpha);
        ApplyVisualAlpha(masterAlpha);
    }

    private void ApplyVisualAlpha(float alpha)
    {
        CanvasGroup activeCallout = GetActiveCalloutCanvasGroup(currentButton);
        if (activeCallout != null) activeCallout.alpha = alpha;
        if (controllerCalloutCanvasGroup != null && controllerCalloutCanvasGroup != activeCallout)
            controllerCalloutCanvasGroup.alpha = alpha;

        if (targetRingCanvasGroup != null)
            targetRingCanvasGroup.alpha = alpha;

        float pulse = Application.isPlaying ? (0.82f + Mathf.Sin(Time.time * 4.5f) * 0.18f) : 1.0f;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        // Button highlight pulse
        Color btnCol = new Color(CyanPulseBright.r, CyanPulseBright.g, CyanPulseBright.b, alpha * pulse);
        foreach (var r in highlightRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", btnCol);
            propBlock.SetColor("_BaseColor", btnCol);
            propBlock.SetColor("_EdgeColor", Color.white * alpha);
            r.SetPropertyBlock(propBlock);
        }

        // Thin blue contour outline on the child outline mesh ONLY (never touches the target's base material!)
        Color outlineInterior = new Color(TargetOutlineEdge.r, TargetOutlineEdge.g, TargetOutlineEdge.b, 0f);
        Color outlineEdgeCol = new Color(TargetOutlineEdge.r, TargetOutlineEdge.g, TargetOutlineEdge.b, TargetOutlineEdge.a * alpha * pulse);
        foreach (var r in activeOutlineRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", outlineInterior);
            propBlock.SetColor("_BaseColor", outlineInterior);
            propBlock.SetColor("_EdgeColor", outlineEdgeCol);
            r.SetPropertyBlock(propBlock);
        }
    }

    private void OnDrawGizmos()
    {
        DrawAnchorGizmo(gripHighlightAnchor, "Grip Highlight Anchor", new Color(0.2f, 0.9f, 1f, 0.9f));
        DrawAnchorGizmo(triggerHighlightAnchor, "Trigger Highlight Anchor", new Color(0.2f, 0.9f, 1f, 0.9f));
        DrawAnchorGizmo(buttonAHighlightAnchor, "Button A Highlight Anchor", new Color(0.2f, 0.9f, 1f, 0.9f));
        if (currentRingAnchor != null)
        {
            DrawAnchorGizmo(currentRingAnchor, currentRingAnchor.name, new Color(0.25f, 0.95f, 0.6f, 0.9f));
        }
    }

    private void OnDrawGizmosSelected()
    {
        OnDrawGizmos();
    }

    private static void DrawAnchorGizmo(Transform anchor, string label, Color color)
    {
        if (anchor == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(anchor.position, 0.016f);
        Gizmos.DrawLine(anchor.position, anchor.position + anchor.forward * 0.035f);
#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(anchor.position + Vector3.up * 0.025f, label);
#endif
    }
}
