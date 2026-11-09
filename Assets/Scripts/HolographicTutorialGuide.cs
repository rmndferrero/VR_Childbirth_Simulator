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
/// Renders the 3D Holographic VR Controller instruction rig, orthogonal leader lines,
/// pulsing button highlights, target object silhouette rim-glow, and holographic targeting ring.
/// Supports 100% manual Scene View editing ([ExecuteAlways]) so every element can be freely
/// moved, rotated, and scaled in the Unity Editor while keeping leader lines connected.
/// </summary>
[ExecuteAlways]
public class HolographicTutorialGuide : MonoBehaviour
{
    [Header("3D Holographic Controller Rig")]
    [Tooltip("Root transform of the floating 3D holographic controller assembly.")]
    public Transform controllerRigRoot;

    [Tooltip("The 3D controller mesh root (OculusTouchForQuest2_Right or UniversalController).")]
    public Transform controllerModelRoot;

    [Tooltip("Bone/Transform for the Grip button (b_trigger_grip or Bumper).")]
    public Transform gripButtonAnchor;

    [Tooltip("Bone/Transform for the Index Trigger button (b_trigger_front or Trigger).")]
    public Transform triggerButtonAnchor;

    [Tooltip("Bone/Transform for Primary Button A (b_button_a or Button_A).")]
    public Transform buttonAAnchor;

    [Header("Dedicated Button Glow Anchors (Move & Scale in Scene View!)")]
    [Tooltip("Drag in Scene View to set the exact position, rotation, and size of the Grip button highlight glow.")]
    public Transform gripHighlightAnchor;

    [Tooltip("Drag in Scene View to set the exact position, rotation, and size of the Trigger button highlight glow.")]
    public Transform triggerHighlightAnchor;

    [Tooltip("Drag in Scene View to set the exact position, rotation, and size of the Button A highlight glow.")]
    public Transform buttonAHighlightAnchor;

    [Header("Manual Glow Fine-Tuning Offsets (Optional)")]
    public Vector3 gripGlowLocalOffset = Vector3.zero;
    public Vector3 triggerGlowLocalOffset = Vector3.zero;
    public Vector3 buttonAGlowLocalOffset = Vector3.zero;

    [Tooltip("Bone/Transform for the laser pointer origin on the controller.")]
    public Transform laserOriginAnchor;

    [Header("Button Highlight & Leader Lines")]
    [Tooltip("Glowing cyan pill/ring highlight placed on the currently active controller button.")]
    public Transform activeButtonHighlight;

    [Tooltip("Upper orthogonal L-shaped leader line connecting the button to the top callout text.")]
    public LineRenderer upperLeaderLine;

    [Tooltip("Lower leader line connecting the button to the bottom callout text.")]
    public LineRenderer lowerLeaderLine;

    [Tooltip("Holographic laser ray shown during Step 2 (UI Button Trigger interaction).")]
    public LineRenderer laserPointerLine;

    [Header("Controller Callout UI")]
    public CanvasGroup controllerCalloutCanvasGroup;
    public TextMeshProUGUI upperCalloutText;
    public TextMeshProUGUI lowerCalloutText;
    public TextMeshProUGUI buttonBadgeText;
    public Transform upperCalloutAnchor;
    public Transform lowerCalloutAnchor;

    [Header("Target Object Holographic Ring & Silhouette")]
    [Tooltip("Floating holographic ring + label positioned over the target object on the table.")]
    public Transform targetRingRoot;
    public CanvasGroup targetRingCanvasGroup;
    public TextMeshProUGUI targetRingLabel;
    public Image targetRingOuterImage;
    public Image targetRingInnerImage;

    [Tooltip("Holographic Fresnel outline material overlaid onto the active 3D target object on the table.")]
    public Material targetSilhouetteMaterial;

    [Header("Follow & Ergonomics Settings")]
    [Tooltip("When TRUE, the 3D controller floats smoothly in the player's view in VR. When FALSE, keeps manual Scene View position.")]
    public bool lockControllerToHeadset = true;

    [Tooltip("When TRUE in Play Mode, the target ring smoothly tracks the active step's target object on the table.")]
    public bool autoSnapRingToTargetInPlayMode = true;

    [Tooltip("When TRUE in Play Mode, the target ring gently rotates around Y to face the player camera.")]
    public bool billboardTargetRingInPlayMode = true;

    [Tooltip("Peripheral right-side offset relative to the player's view (used only if lockControllerToHeadset is true).")]
    public Vector3 controllerViewOffset = new Vector3(0.28f, -0.07f, 0.54f);
    public Vector3 controllerLocalEuler = new Vector3(18f, -145f, 12f);
    public float smoothFollowSpeed = 7.0f;

    [Header("Editor Preview Button Selection")]
    public HolographicButtonType currentButton = HolographicButtonType.Grip;

    [SerializeField]
    private Vector3 baseControllerLocalPos;
    private Transform currentRingAnchor;
    private Transform currentTargetObject;
    private Vector3 currentTargetOffset = Vector3.zero;
    private bool isTargetRingVisible = true;
    private float masterAlpha = 1f;
    private Coroutine fadeRoutine;
    private Vector3 rigVelocity;
    private Camera mainCam;

    private readonly List<Renderer> controllerRenderers = new List<Renderer>();
    private readonly List<Renderer> highlightRenderers = new List<Renderer>();
    private readonly Dictionary<Renderer, Material[]> originalTargetMaterials = new Dictionary<Renderer, Material[]>();
    private MaterialPropertyBlock propBlock;

    // Smokey translucent cool white-blue glass body + crisp cyan Fresnel rim (matches inspiration image)
    private static readonly Color CyanHologramBody = new Color(0.75f, 0.88f, 0.96f, 0.18f);
    private static readonly Color CyanHologramEdge = new Color(0.42f, 0.90f, 1.0f, 0.88f);
    private static readonly Color CyanPulseBright  = new Color(0.25f, 0.95f, 1.0f, 0.95f);

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        CacheRenderers();
        AutoResolveAnchors();
        EnsureGlowAnchorsExist();

        if (Application.isPlaying)
        {
            SetAlphaImmediate(0f);
        }
        else
        {
            SetAlphaImmediate(1f);
        }
    }

    private void OnEnable()
    {
        propBlock = new MaterialPropertyBlock();
        CacheRenderers();
        AutoResolveAnchors();
        EnsureGlowAnchorsExist();

        if (!Application.isPlaying)
        {
            if (currentButton == HolographicButtonType.None)
                currentButton = HolographicButtonType.Grip;
            SetAlphaImmediate(1f);
            PositionActiveButtonHighlight();
            UpdateLeaderLines(GetCurrentButtonAnchor());
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            RestoreTargetObjectSilhouette();
        }
    }

    public void CacheRenderers()
    {
        controllerRenderers.Clear();
        highlightRenderers.Clear();

        if (controllerModelRoot != null)
        {
            foreach (var r in controllerModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (activeButtonHighlight != null && r.transform.IsChildOf(activeButtonHighlight))
                    continue;
                controllerRenderers.Add(r);
            }
        }

        if (activeButtonHighlight != null)
        {
            foreach (var r in activeButtonHighlight.GetComponentsInChildren<Renderer>(true))
            {
                highlightRenderers.Add(r);
            }
        }
    }

    public void AutoResolveAnchors()
    {
        if (controllerModelRoot != null)
        {
            if (gripButtonAnchor == null)
                gripButtonAnchor = FindChildRecursive(controllerModelRoot, "b_trigger_grip")
                                ?? FindChildRecursive(controllerModelRoot, "Bumper");

            if (triggerButtonAnchor == null)
                triggerButtonAnchor = FindChildRecursive(controllerModelRoot, "b_trigger_front")
                                   ?? FindChildRecursive(controllerModelRoot, "Trigger");

            if (buttonAAnchor == null)
                buttonAAnchor = FindChildRecursive(controllerModelRoot, "b_button_a")
                             ?? FindChildRecursive(controllerModelRoot, "Button_A");

            if (laserOriginAnchor == null)
                laserOriginAnchor = FindChildRecursive(controllerModelRoot, "right_laser_begin")
                                 ?? triggerButtonAnchor;
        }

        Transform rigRoot = controllerRigRoot != null ? controllerRigRoot : transform;
        if (gripHighlightAnchor == null)
            gripHighlightAnchor = FindChildRecursive(rigRoot, "GripButton_GlowAnchor");
        if (triggerHighlightAnchor == null)
            triggerHighlightAnchor = FindChildRecursive(rigRoot, "TriggerButton_GlowAnchor");
        if (buttonAHighlightAnchor == null)
            buttonAHighlightAnchor = FindChildRecursive(rigRoot, "ButtonA_GlowAnchor");
    }

    /// <summary>
    /// Ensures that dedicated, movable glow anchor GameObjects exist under HolographicControllerRig.
    /// This allows the developer to drag, rotate, and scale each button's highlight directly in Scene View.
    /// </summary>
    public void EnsureGlowAnchorsExist()
    {
        Transform rigRoot = controllerRigRoot != null ? controllerRigRoot : transform;
        Transform anchorsGroup = FindChildRecursive(rigRoot, "Button Highlight Anchors (Move to Adjust Glow)");
        if (anchorsGroup == null)
        {
            var go = new GameObject("Button Highlight Anchors (Move to Adjust Glow)");
            go.transform.SetParent(rigRoot, false);
            anchorsGroup = go.transform;
        }

        if (gripHighlightAnchor == null)
        {
            gripHighlightAnchor = FindChildRecursive(anchorsGroup, "GripButton_GlowAnchor");
            if (gripHighlightAnchor == null)
            {
                var go = new GameObject("GripButton_GlowAnchor");
                go.transform.SetParent(anchorsGroup, false);
                go.transform.localPosition = new Vector3(-0.018f, -0.028f, -0.012f);
                go.transform.localRotation = Quaternion.Euler(15f, 90f, 0f);
                go.transform.localScale = new Vector3(0.026f, 0.018f, 0.028f);
                gripHighlightAnchor = go.transform;
            }
        }

        if (triggerHighlightAnchor == null)
        {
            triggerHighlightAnchor = FindChildRecursive(anchorsGroup, "TriggerButton_GlowAnchor");
            if (triggerHighlightAnchor == null)
            {
                var go = new GameObject("TriggerButton_GlowAnchor");
                go.transform.SetParent(anchorsGroup, false);
                go.transform.localPosition = new Vector3(0.002f, -0.038f, 0.034f);
                go.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
                go.transform.localScale = new Vector3(0.022f, 0.016f, 0.026f);
                triggerHighlightAnchor = go.transform;
            }
        }

        if (buttonAHighlightAnchor == null)
        {
            buttonAHighlightAnchor = FindChildRecursive(anchorsGroup, "ButtonA_GlowAnchor");
            if (buttonAHighlightAnchor == null)
            {
                var go = new GameObject("ButtonA_GlowAnchor");
                go.transform.SetParent(anchorsGroup, false);
                go.transform.localPosition = new Vector3(0.012f, 0.012f, -0.018f);
                go.transform.localRotation = Quaternion.Euler(30f, 25f, 0f);
                go.transform.localScale = new Vector3(0.018f, 0.014f, 0.018f);
                buttonAHighlightAnchor = go.transform;
            }
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
    /// Displays and smoothly fades in a holographic instruction step, highlighting both the
    /// controller button and the 3D target object on the table.
    /// </summary>
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
        Transform ringAnchor = null)
    {
        AutoResolveAnchors();
        currentButton = buttonType;
        currentRingAnchor = ringAnchor;
        isTargetRingVisible = showTargetRing && (targetObject != null || currentRingAnchor != null);
        SetTargetObjectWithSilhouette(targetObject);
        currentTargetOffset = targetWorldOffset;

        if (upperCalloutText != null) upperCalloutText.text = upperCallout;
        if (lowerCalloutText != null) lowerCalloutText.text = lowerCallout;
        if (buttonBadgeText != null) buttonBadgeText.text = badgeLabel;
        if (targetRingLabel != null) targetRingLabel.text = targetLabel;

        if (targetRingRoot != null)
        {
            targetRingRoot.gameObject.SetActive(isTargetRingVisible);
            if (isTargetRingVisible)
            {
                if (autoSnapRingToTargetInPlayMode && targetObject != null)
                {
                    targetRingRoot.position = GetTargetVisualCenter(targetObject) + currentTargetOffset;
                }
                else if (currentRingAnchor != null)
                {
                    targetRingRoot.position = currentRingAnchor.position;
                }
            }
        }

        if (laserPointerLine != null)
        {
            laserPointerLine.gameObject.SetActive(showLaserRay);
        }

        if (controllerRigRoot != null)
        {
            controllerRigRoot.gameObject.SetActive(true);
            if (lockControllerToHeadset)
            {
                SnapControllerRigToView();
            }
        }

        PositionActiveButtonHighlight();

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

    /// <summary>
    /// Smoothly fades out the current holographic instruction when the user completes the action.
    /// </summary>
    public Coroutine FadeOutInstruction(float duration = 0.40f)
    {
        RestoreTargetObjectSilhouette();
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(0f, duration));
        return fadeRoutine;
    }

    /// <summary>
    /// Updates the target object and callout text dynamically without hiding the hologram.
    /// </summary>
    public void UpdateTargetAndCallout(
        HolographicButtonType buttonType,
        string upperCallout,
        string lowerCallout,
        string badgeLabel,
        Transform targetObject,
        string targetLabel,
        Vector3 targetWorldOffset,
        bool showTargetRing = true,
        Transform ringAnchor = null)
    {
        currentButton = buttonType;
        if (ringAnchor != null) currentRingAnchor = ringAnchor;
        isTargetRingVisible = showTargetRing && (targetObject != null || currentRingAnchor != null);
        SetTargetObjectWithSilhouette(targetObject);
        currentTargetOffset = targetWorldOffset;

        if (upperCalloutText != null) upperCalloutText.text = upperCallout;
        if (lowerCalloutText != null) lowerCalloutText.text = lowerCallout;
        if (buttonBadgeText != null) buttonBadgeText.text = badgeLabel;
        if (targetRingLabel != null) targetRingLabel.text = targetLabel;

        if (targetRingRoot != null)
        {
            targetRingRoot.gameObject.SetActive(isTargetRingVisible);
            if (isTargetRingVisible)
            {
                if (autoSnapRingToTargetInPlayMode && targetObject != null)
                {
                    targetRingRoot.position = GetTargetVisualCenter(targetObject) + currentTargetOffset;
                }
                else if (currentRingAnchor != null)
                {
                    targetRingRoot.position = currentRingAnchor.position;
                }
            }
        }

        PositionActiveButtonHighlight();
    }

    private void SetTargetObjectWithSilhouette(Transform newTarget)
    {
        RestoreTargetObjectSilhouette();
        currentTargetObject = newTarget;

        if (!Application.isPlaying) return;
        if (currentTargetObject == null || targetSilhouetteMaterial == null) return;

        // Do not apply 3D mesh material overlay onto UI Canvas elements
        if (currentTargetObject.GetComponentInParent<Canvas>() != null) return;

        foreach (var r in currentTargetObject.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || r.GetComponentInParent<Canvas>() != null) continue;

            var origMats = r.sharedMaterials;
            originalTargetMaterials[r] = origMats;

            var newMats = new Material[origMats.Length + 1];
            for (int i = 0; i < origMats.Length; i++) newMats[i] = origMats[i];
            newMats[origMats.Length] = targetSilhouetteMaterial;
            r.sharedMaterials = newMats;
        }
    }

    private void RestoreTargetObjectSilhouette()
    {
        foreach (var kvp in originalTargetMaterials)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.sharedMaterials = kvp.Value;
            }
        }
        originalTargetMaterials.Clear();
    }

    private static Vector3 GetTargetVisualCenter(Transform target)
    {
        if (target == null) return Vector3.zero;
        var mr = target.GetComponentInChildren<MeshRenderer>();
        if (mr != null) return mr.bounds.center;
        return target.position;
    }

    public Transform GetCurrentButtonAnchor()
    {
        switch (currentButton)
        {
            case HolographicButtonType.Grip:
                if (gripHighlightAnchor != null) return gripHighlightAnchor;
                return gripButtonAnchor != null ? gripButtonAnchor : controllerModelRoot;

            case HolographicButtonType.Trigger:
                if (triggerHighlightAnchor != null) return triggerHighlightAnchor;
                return triggerButtonAnchor != null ? triggerButtonAnchor : controllerModelRoot;

            case HolographicButtonType.ButtonA:
                if (buttonAHighlightAnchor != null) return buttonAHighlightAnchor;
                return buttonAAnchor != null ? buttonAAnchor : controllerModelRoot;

            default:
                return controllerModelRoot;
        }
    }

    public Vector3 GetButtonGlowOffset()
    {
        switch (currentButton)
        {
            case HolographicButtonType.Grip:
                return gripGlowLocalOffset;
            case HolographicButtonType.Trigger:
                return triggerGlowLocalOffset;
            case HolographicButtonType.ButtonA:
                return buttonAGlowLocalOffset;
            default:
                return Vector3.zero;
        }
    }

    public void PositionActiveButtonHighlight()
    {
        if (activeButtonHighlight == null) return;

        Transform anchor = GetCurrentButtonAnchor();
        if (anchor == null || currentButton == HolographicButtonType.None)
        {
            activeButtonHighlight.gameObject.SetActive(false);
            return;
        }

        activeButtonHighlight.gameObject.SetActive(true);
        Vector3 offset = anchor.rotation * GetButtonGlowOffset();
        activeButtonHighlight.position = anchor.position + offset;
        activeButtonHighlight.rotation = anchor.rotation;

        Vector3 targetScale = anchor.localScale;
        targetScale.x = Mathf.Clamp(targetScale.x, 0.005f, 0.08f);
        targetScale.y = Mathf.Clamp(targetScale.y, 0.005f, 0.08f);
        targetScale.z = Mathf.Clamp(targetScale.z, 0.005f, 0.08f);
        activeButtonHighlight.localScale = targetScale;
    }

    private void SnapControllerRigToView()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || controllerRigRoot == null) return;

        Vector3 flatFwd = mainCam.transform.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
        flatFwd.Normalize();

        Quaternion viewYaw = Quaternion.LookRotation(flatFwd, Vector3.up);
        controllerRigRoot.position = mainCam.transform.position + viewYaw * controllerViewOffset;
        controllerRigRoot.rotation = viewYaw;
    }

    private void LateUpdate()
    {
        // In Edit Mode, keep everything visible and keep leader lines connected while the user drags objects in Scene View
        if (!Application.isPlaying)
        {
            if (controllerRigRoot != null)
            {
                baseControllerLocalPos = controllerRigRoot.localPosition;
            }
            AutoResolveAnchors();
            EnsureGlowAnchorsExist();
            Transform editorAnchor = GetCurrentButtonAnchor();
            if (activeButtonHighlight != null && editorAnchor != null && currentButton != HolographicButtonType.None)
            {
                activeButtonHighlight.gameObject.SetActive(true);
                Vector3 offset = editorAnchor.rotation * GetButtonGlowOffset();
                activeButtonHighlight.position = editorAnchor.position + offset;
                activeButtonHighlight.rotation = editorAnchor.rotation;

                Vector3 targetScale = editorAnchor.localScale;
                targetScale.x = Mathf.Clamp(targetScale.x, 0.005f, 0.08f);
                targetScale.y = Mathf.Clamp(targetScale.y, 0.005f, 0.08f);
                targetScale.z = Mathf.Clamp(targetScale.z, 0.005f, 0.08f);
                activeButtonHighlight.localScale = targetScale;
            }
            UpdateLeaderLines(editorAnchor);
            return;
        }

        if (masterAlpha <= 0.001f) return;

        if (mainCam == null) mainCam = Camera.main;

        // 1. Only move the controller rig if lockControllerToHeadset is explicitly enabled;
        //    otherwise preserve the exact manual Scene View position, rotation, and scale!
        if (lockControllerToHeadset && controllerRigRoot != null && mainCam != null)
        {
            Vector3 flatFwd = mainCam.transform.forward;
            flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();

            Quaternion viewYaw = Quaternion.LookRotation(flatFwd, Vector3.up);
            float hoverBob = Mathf.Sin(Time.time * 2.2f) * 0.004f;
            Vector3 desiredPos = mainCam.transform.position + viewYaw * (controllerViewOffset + new Vector3(0f, hoverBob, 0f));

            controllerRigRoot.position = Vector3.SmoothDamp(controllerRigRoot.position, desiredPos, ref rigVelocity, 1f / smoothFollowSpeed);
            controllerRigRoot.rotation = Quaternion.Slerp(controllerRigRoot.rotation, viewYaw, Time.deltaTime * smoothFollowSpeed);

            if (controllerModelRoot != null)
            {
                controllerModelRoot.localRotation = Quaternion.Euler(controllerLocalEuler);
            }
        }
        else if (controllerRigRoot != null)
        {
            if (baseControllerLocalPos == Vector3.zero)
            {
                baseControllerLocalPos = controllerRigRoot.localPosition;
            }
            float hoverBob = Mathf.Sin(Time.time * 2.2f) * 0.004f;
            controllerRigRoot.localPosition = baseControllerLocalPos + new Vector3(0f, hoverBob, 0f);
        }

        // 2. Keep active button highlight locked onto the target button anchor and pulse its scale
        Transform btnAnchor = GetCurrentButtonAnchor();
        if (activeButtonHighlight != null && btnAnchor != null && currentButton != HolographicButtonType.None)
        {
            activeButtonHighlight.gameObject.SetActive(true);
            Vector3 offset = btnAnchor.rotation * GetButtonGlowOffset();
            activeButtonHighlight.position = btnAnchor.position + offset;
            activeButtonHighlight.rotation = btnAnchor.rotation;

            float pulseScale = 1.0f + Mathf.Sin(Time.time * 5.5f) * 0.14f;
            Vector3 baseScale = btnAnchor.localScale;
            baseScale.x = Mathf.Clamp(baseScale.x, 0.005f, 0.08f);
            baseScale.y = Mathf.Clamp(baseScale.y, 0.005f, 0.08f);
            baseScale.z = Mathf.Clamp(baseScale.z, 0.005f, 0.08f);
            activeButtonHighlight.localScale = baseScale * pulseScale;
        }

        // 3. Update orthogonal L-shaped holographic leader lines from button bone to callouts
        UpdateLeaderLines(btnAnchor);

        // 4. Update Target Object Holographic Ring over the table
        UpdateTargetObjectRing();

        // 5. Apply pulsing holographic transparency
        ApplyVisualAlpha(masterAlpha);
    }

    public void UpdateLeaderLines(Transform btnAnchor)
    {
        if (btnAnchor == null) return;

        Vector3 startPos = btnAnchor.position;

        if (upperLeaderLine != null && upperCalloutAnchor != null)
        {
            upperLeaderLine.positionCount = 3;
            Vector3 endPos = upperCalloutAnchor.position;
            // Clean L-shaped orthogonal bend matching the reference photo (vertical down from callout, then horizontal to button)
            Vector3 elbow = new Vector3(endPos.x, startPos.y, endPos.z);
            upperLeaderLine.SetPosition(0, endPos);
            upperLeaderLine.SetPosition(1, elbow);
            upperLeaderLine.SetPosition(2, startPos);
        }

        if (lowerLeaderLine != null && lowerCalloutAnchor != null)
        {
            lowerLeaderLine.positionCount = 3;
            Vector3 endPos = lowerCalloutAnchor.position;
            Vector3 elbow = new Vector3(endPos.x, startPos.y, endPos.z);
            lowerLeaderLine.SetPosition(0, endPos);
            lowerLeaderLine.SetPosition(1, elbow);
            lowerLeaderLine.SetPosition(2, startPos);
        }

        if (laserPointerLine != null && laserPointerLine.gameObject.activeSelf)
        {
            Vector3 rayStart = laserOriginAnchor != null ? laserOriginAnchor.position : startPos;
            Vector3 rayEnd = currentTargetObject != null
                ? GetTargetVisualCenter(currentTargetObject) + currentTargetOffset
                : rayStart + (controllerModelRoot != null ? controllerModelRoot.forward * 0.35f : Vector3.forward * 0.35f);

            laserPointerLine.positionCount = 2;
            laserPointerLine.SetPosition(0, rayStart);
            laserPointerLine.SetPosition(1, rayEnd);
        }
    }

    private void UpdateTargetObjectRing()
    {
        if (targetRingRoot == null || !isTargetRingVisible) return;

        if (autoSnapRingToTargetInPlayMode && currentTargetObject != null)
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

        if (targetRingOuterImage != null)
        {
            float ringPulse = 1.0f + Mathf.Sin(Time.time * 3.5f) * 0.04f;
            targetRingOuterImage.rectTransform.localScale = Vector3.one * ringPulse;
        }
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = masterAlpha;
        float elapsed = 0f;

        if (targetAlpha > 0f)
        {
            if (controllerRigRoot != null) controllerRigRoot.gameObject.SetActive(true);
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
            if (controllerRigRoot != null) controllerRigRoot.gameObject.SetActive(false);
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
        if (controllerCalloutCanvasGroup != null)
            controllerCalloutCanvasGroup.alpha = alpha;

        if (targetRingCanvasGroup != null)
            targetRingCanvasGroup.alpha = alpha;

        float pulse = Application.isPlaying ? (0.82f + Mathf.Sin(Time.time * 4.5f) * 0.18f) : 1.0f;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        Color bodyCol = new Color(CyanHologramBody.r, CyanHologramBody.g, CyanHologramBody.b, CyanHologramBody.a * alpha);
        Color edgeCol = new Color(CyanHologramEdge.r, CyanHologramEdge.g, CyanHologramEdge.b, CyanHologramEdge.a * alpha);

        foreach (var r in controllerRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", bodyCol);
            propBlock.SetColor("_BaseColor", bodyCol);
            propBlock.SetColor("_EdgeColor", edgeCol);
            r.SetPropertyBlock(propBlock);
        }

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

        Color lineCol = new Color(0.68f, 0.94f, 1.0f, 0.88f * alpha);
        if (upperLeaderLine != null)
        {
            upperLeaderLine.startColor = lineCol;
            upperLeaderLine.endColor = lineCol;
        }
        if (lowerLeaderLine != null)
        {
            lowerLeaderLine.startColor = lineCol;
            lowerLeaderLine.endColor = lineCol;
        }
        if (laserPointerLine != null)
        {
            Color rayCol = new Color(0.35f, 0.94f, 1.0f, 0.80f * alpha * pulse);
            laserPointerLine.startColor = rayCol;
            laserPointerLine.endColor = new Color(0.35f, 0.94f, 1.0f, 0.20f * alpha);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.95f, 1.0f, 0.85f);
        if (gripHighlightAnchor != null)
        {
            Gizmos.DrawWireSphere(gripHighlightAnchor.position, 0.014f);
        }
        if (triggerHighlightAnchor != null)
        {
            Gizmos.DrawWireSphere(triggerHighlightAnchor.position, 0.014f);
        }
        if (buttonAHighlightAnchor != null)
        {
            Gizmos.DrawWireSphere(buttonAHighlightAnchor.position, 0.014f);
        }
    }
}
