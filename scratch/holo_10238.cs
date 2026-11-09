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
/// pulsing button highlights, and target object holographic ring (matching the reference design).
/// Smoothly fades out when the player performs the instructed action.
/// </summary>
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

    [Header("Target Object Holographic Ring (Over Table)")]
    [Tooltip("Floating holographic ring + label positioned over the target object on the table.")]
    public Transform targetRingRoot;
    public CanvasGroup targetRingCanvasGroup;
    public TextMeshProUGUI targetRingLabel;
    public Image targetRingOuterImage;
    public Image targetRingInnerImage;

    [Header("Follow & Ergonomics Settings")]
    [Tooltip("Offset relative to the player's camera view for the floating 3D holographic controller.")]
    public Vector3 controllerViewOffset = new Vector3(0.24f, -0.10f, 0.52f);
    public Vector3 controllerLocalEuler = new Vector3(18f, -145f, 12f);
    public float smoothFollowSpeed = 7.0f;

    private HolographicButtonType currentButton = HolographicButtonType.None;
    private Transform currentTargetObject;
    private Vector3 currentTargetOffset = Vector3.zero;
    private float masterAlpha = 0f;
    private Coroutine fadeRoutine;
    private Vector3 rigVelocity;
    private Camera mainCam;

    private List<Renderer> controllerRenderers = new List<Renderer>();
    private List<Renderer> highlightRenderers = new List<Renderer>();
    private MaterialPropertyBlock propBlock;

    private static readonly Color CyanHologramEdge = new Color(0.36f, 0.88f, 0.96f, 1.0f);
    private static readonly Color CyanHologramBody = new Color(0.55f, 0.85f, 0.98f, 0.34f);
    private static readonly Color CyanPulseBright  = new Color(0.25f, 0.95f, 1.0f, 1.0f);

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        CacheRenderers();
        AutoResolveAnchors();
        SetAlphaImmediate(0f);
    }

    private void CacheRenderers()
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
        if (controllerModelRoot == null) return;

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
    /// Displays and smoothly fades in a holographic instruction step.
    /// </summary>
    public void ShowInstruction(
        HolographicButtonType buttonType,
        string upperCallout,
        string lowerCallout,
        string badgeLabel,
        Transform targetObject,
        string targetLabel,
        Vector3 targetWorldOffset,
        bool showLaserRay = false)
    {
        AutoResolveAnchors();
        currentButton = buttonType;
        currentTargetObject = targetObject;
        currentTargetOffset = targetWorldOffset;

        if (upperCalloutText != null) upperCalloutText.text = upperCallout;
        if (lowerCalloutText != null) lowerCalloutText.text = lowerCallout;
        if (buttonBadgeText != null) buttonBadgeText.text = badgeLabel;
        if (targetRingLabel != null) targetRingLabel.text = targetLabel;

        if (targetRingRoot != null)
        {
            targetRingRoot.gameObject.SetActive(targetObject != null);
        }

        if (laserPointerLine != null)
        {
            laserPointerLine.gameObject.SetActive(showLaserRay);
        }

        if (controllerRigRoot != null)
        {
            controllerRigRoot.gameObject.SetActive(true);
            SnapControllerRigToView();
        }

        PositionActiveButtonHighlight();

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(1f, 0.40f));
    }

    /// <summary>
    /// Smoothly fades out the current holographic instruction when the user completes the action.
    /// </summary>
    public Coroutine FadeOutInstruction(float duration = 0.40f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(0f, duration));
        return fadeRoutine;
    }

    /// <summary>
    /// Updates only the target object and label (e.g. during sub-steps of cotton transfer) without hiding the hologram.
    /// </summary>
    public void UpdateTargetAndCallout(
        HolographicButtonType buttonType,
        string upperCallout,
        string lowerCallout,
        string badgeLabel,
        Transform targetObject,
        string targetLabel,
        Vector3 targetWorldOffset)
    {
        currentButton = buttonType;
        currentTargetObject = targetObject;
        currentTargetOffset = targetWorldOffset;

        if (upperCalloutText != null) upperCalloutText.text = upperCallout;
        if (lowerCalloutText != null) lowerCalloutText.text = lowerCallout;
        if (buttonBadgeText != null) buttonBadgeText.text = badgeLabel;
        if (targetRingLabel != null) targetRingLabel.text = targetLabel;

        if (targetRingRoot != null)
        {
            targetRingRoot.gameObject.SetActive(targetObject != null);
        }

        PositionActiveButtonHighlight();
    }

    private Transform GetCurrentButtonAnchor()
    {
        switch (currentButton)
        {
            case HolographicButtonType.Grip:
                return gripButtonAnchor != null ? gripButtonAnchor : controllerModelRoot;
            case HolographicButtonType.Trigger:
                return triggerButtonAnchor != null ? triggerButtonAnchor : controllerModelRoot;
            case HolographicButtonType.ButtonA:
                return buttonAAnchor != null ? buttonAAnchor : controllerModelRoot;
            default:
                return controllerModelRoot;
        }
    }

    private void PositionActiveButtonHighlight()
    {
        if (activeButtonHighlight == null) return;

        Transform anchor = GetCurrentButtonAnchor();
        if (anchor == null || currentButton == HolographicButtonType.None)
        {
            activeButtonHighlight.gameObject.SetActive(false);
            return;
        }

        activeButtonHighlight.gameObject.SetActive(true);
        activeButtonHighlight.position = anchor.position;
        activeButtonHighlight.rotation = anchor.rotation;
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
        if (masterAlpha <= 0.001f) return;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        // 1. Smoothly position the holographic controller assembly in front-right of player view
        if (controllerRigRoot != null)
        {
            Vector3 flatFwd = mainCam.transform.forward;
            flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();

            Quaternion viewYaw = Quaternion.LookRotation(flatFwd, Vector3.up);
            float hoverBob = Mathf.Sin(Time.time * 2.2f) * 0.005f;
            Vector3 desiredPos = mainCam.transform.position + viewYaw * (controllerViewOffset + new Vector3(0f, hoverBob, 0f));

            controllerRigRoot.position = Vector3.SmoothDamp(controllerRigRoot.position, desiredPos, ref rigVelocity, 1f / smoothFollowSpeed);
            controllerRigRoot.rotation = Quaternion.Slerp(controllerRigRoot.rotation, viewYaw, Time.deltaTime * smoothFollowSpeed);

            if (controllerModelRoot != null)
            {
                controllerModelRoot.localRotation = Quaternion.Euler(controllerLocalEuler);
            }
        }

        // 2. Keep active button highlight locked onto the target button bone and pulse its scale
        Transform btnAnchor = GetCurrentButtonAnchor();
        if (activeButtonHighlight != null && btnAnchor != null && currentButton != HolographicButtonType.None)
        {
            activeButtonHighlight.position = btnAnchor.position;
            activeButtonHighlight.rotation = btnAnchor.rotation;
            float pulseScale = 1.0f + Mathf.Sin(Time.time * 5.0f) * 0.14f;
            activeButtonHighlight.localScale = new Vector3(0.024f, 0.016f, 0.028f) * pulseScale;
        }

        // 3. Update orthogonal L-shaped holographic leader lines from button bone to callouts
        UpdateLeaderLines(btnAnchor);

        // 4. Update Target Object Holographic Ring over the table
        UpdateTargetObjectRing();

        // 5. Pulse highlight brightness
        ApplyVisualAlpha(masterAlpha);
    }

    private void UpdateLeaderLines(Transform btnAnchor)
    {
        if (btnAnchor == null) return;

        Vector3 startPos = btnAnchor.position;

        if (upperLeaderLine != null && upperCalloutAnchor != null)
        {
            upperLeaderLine.positionCount = 3;
            Vector3 endPos = upperCalloutAnchor.position;
            // Orthogonal L-elbow matching the reference photo (out horizontally then straight up)
            Vector3 elbow = new Vector3(endPos.x, startPos.y, Mathf.Lerp(startPos.z, endPos.z, 0.5f));
            upperLeaderLine.SetPosition(0, startPos);
            upperLeaderLine.SetPosition(1, elbow);
            upperLeaderLine.SetPosition(2, endPos);
        }

        if (lowerLeaderLine != null && lowerCalloutAnchor != null)
        {
            lowerLeaderLine.positionCount = 3;
            Vector3 endPos = lowerCalloutAnchor.position;
            Vector3 elbow = new Vector3(endPos.x, startPos.y, Mathf.Lerp(startPos.z, endPos.z, 0.5f));
            lowerLeaderLine.SetPosition(0, startPos);
            lowerLeaderLine.SetPosition(1, elbow);
            lowerLeaderLine.SetPosition(2, endPos);
        }

        if (laserPointerLine != null && laserPointerLine.gameObject.activeSelf)
        {
            Vector3 rayStart = laserOriginAnchor != null ? laserOriginAnchor.position : startPos;
            Vector3 rayEnd = currentTargetObject != null
                ? currentTargetObject.position + currentTargetOffset
                : rayStart + (controllerModelRoot != null ? controllerModelRoot.forward * 0.35f : Vector3.forward * 0.35f);

            laserPointerLine.positionCount = 2;
            laserPointerLine.SetPosition(0, rayStart);
            laserPointerLine.SetPosition(1, rayEnd);
        }
    }

    private void UpdateTargetObjectRing()
    {
        if (targetRingRoot == null || currentTargetObject == null) return;

        Vector3 targetPos = currentTargetObject.position + currentTargetOffset;
        targetRingRoot.position = Vector3.Lerp(targetRingRoot.position, targetPos, Time.deltaTime * 14f);

        if (mainCam != null)
        {
            Vector3 dirToCam = targetRingRoot.position - mainCam.transform.position;
            dirToCam.y = 0f;
            if (dirToCam.sqrMagnitude > 0.001f)
            {
                targetRingRoot.rotation = Quaternion.LookRotation(dirToCam.normalized, Vector3.up);
            }
        }

        // Gentle breathing scale pulse on the circular holographic ring
        if (targetRingOuterImage != null)
        {
            float ringPulse = 1.0f + Mathf.Sin(Time.time * 3.8f) * 0.06f;
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
            if (targetRingRoot != null && currentTargetObject != null) targetRingRoot.gameObject.SetActive(true);
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

        float pulse = 0.78f + Mathf.Sin(Time.time * 5.0f) * 0.22f;

        // Update 3D Holographic Controller meshes
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

        // Update Active Button Highlight mesh
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

        // Update Leader Lines
        Color lineCol = new Color(0.55f, 0.94f, 1.0f, 0.90f * alpha);
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
            Color rayCol = new Color(0.25f, 0.92f, 1.0f, 0.85f * alpha * pulse);
            laserPointerLine.startColor = rayCol;
            laserPointerLine.endColor = new Color(0.25f, 0.92f, 1.0f, 0.20f * alpha);
        }
    }
}
