using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class ToolSnapback : MonoBehaviour
{
    [Header("Snapback Settings")]
    public float snapbackDelay = 1.0f;

    private Vector3 originPosition;
    private Quaternion originRotation;
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Coroutine checkDropCoroutine;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        SaveOrigin(transform.position, transform.rotation);
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        CancelDropTimer();
    }

    public void SaveOrigin(Vector3 newPos, Quaternion newRot)
    {
        originPosition = newPos;
        originRotation = newRot;

        // Tool was successfully placed on Table 2, stop the snapback timer!
        CancelDropTimer();
    }

    private void CancelDropTimer()
    {
        if (checkDropCoroutine != null)
        {
            StopCoroutine(checkDropCoroutine);
            checkDropCoroutine = null;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        CancelDropTimer();
        checkDropCoroutine = StartCoroutine(CheckDropState());
    }

    private IEnumerator CheckDropState()
    {
        yield return new WaitForSeconds(snapbackDelay);

        if (!grabInteractable.isSelected)
        {
            SnapBack();
        }

        checkDropCoroutine = null;
    }

    public void SnapBack()
    {
        transform.position = originPosition;
        transform.rotation = originRotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}