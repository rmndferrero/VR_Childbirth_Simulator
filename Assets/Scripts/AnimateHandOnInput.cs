using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Animates the VR hand model's fingers based on controller Trigger and Grip inputs,
/// as well as active object grabbing states.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimateHandOnInput : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("Input action for finger trigger/pinch (0.0 to 1.0). Usually 'Activate Value'.")]
    public InputActionProperty pinchAnimationAction;

    [Tooltip("Input action for hand grip/fist (0.0 to 1.0). Usually 'Select Value'.")]
    public InputActionProperty gripAnimationAction;

    [Header("Hand Settings")]
    [Tooltip("Handedness for fallback input device querying.")]
    public UnityEngine.XR.XRNode handNode = UnityEngine.XR.XRNode.RightHand;

    [Tooltip("How smoothly the fingers transition between poses.")]
    public float animationSpeed = 18.0f;

    [Header("Animator Parameters")]
    public string gripParamName = "Grip";
    public string triggerParamName = "Trigger";

    private Animator animator;
    private float currentGrip = 0f;
    private float currentTrigger = 0f;
    private NearFarInteractor parentNearFarInteractor;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        parentNearFarInteractor = GetComponentInParent<NearFarInteractor>();
    }

    private void Update()
    {
        float targetTrigger = ReadTriggerInput();
        float targetGrip = ReadGripInput();

        // If currently grabbing an object with this hand, ensure grip is at least 0.95
        if (parentNearFarInteractor != null && parentNearFarInteractor.hasSelection)
        {
            targetGrip = Mathf.Max(targetGrip, 0.95f);
        }

        // Smoothly interpolate finger movements for natural fluid VR motion
        currentTrigger = Mathf.MoveTowards(currentTrigger, targetTrigger, Time.deltaTime * animationSpeed);
        currentGrip = Mathf.MoveTowards(currentGrip, targetGrip, Time.deltaTime * animationSpeed);

        animator.SetFloat(triggerParamName, currentTrigger);
        animator.SetFloat(gripParamName, currentGrip);
    }

    private float ReadTriggerInput()
    {
        if (pinchAnimationAction.action != null)
        {
            float val = pinchAnimationAction.action.ReadValue<float>();
            if (val > 0.001f) return Mathf.Clamp01(val);
        }

        // Fallback to XR InputDevice
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(handNode);
        if (device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerVal))
        {
            return Mathf.Clamp01(triggerVal);
        }

        return 0f;
    }

    private float ReadGripInput()
    {
        if (gripAnimationAction.action != null)
        {
            float val = gripAnimationAction.action.ReadValue<float>();
            if (val > 0.001f) return Mathf.Clamp01(val);
        }

        // Fallback to XR InputDevice
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(handNode);
        if (device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float gripVal))
        {
            return Mathf.Clamp01(gripVal);
        }

        return 0f;
    }
}
