using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class ToolSnapback : MonoBehaviour
{
    [Header("Snapback Settings")]
    public float snapbackDelay = 1.0f;

    private Vector3 table1Position;
    private Quaternion table1Rotation;
    private Vector3 table2Position;
    private Quaternion table2Rotation;
    private bool isPlacedOnTable2 = false;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Coroutine checkDropCoroutine;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Capture initial position on Table 1
        table1Position = transform.position;
        table1Rotation = transform.rotation;
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        CancelDropTimer();
    }

    public void SaveOrigin(Vector3 newPos, Quaternion newRot)
    {
        if (isPlacedOnTable2)
        {
            table2Position = newPos;
            table2Rotation = newRot;
        }
        else
        {
            table1Position = newPos;
            table1Rotation = newRot;
        }
        CancelDropTimer();
    }

    public void SaveTable2Origin(Vector3 newPos, Quaternion newRot)
    {
        table2Position = newPos;
        table2Rotation = newRot;
        isPlacedOnTable2 = true;
        CancelDropTimer();
    }

    public void ResetToTable1()
    {
        isPlacedOnTable2 = false;
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

        if (grabInteractable != null && !grabInteractable.isSelected)
        {
            SnapBack();
        }

        checkDropCoroutine = null;
    }

    public void SnapBack()
    {
        Vector3 targetPos = isPlacedOnTable2 ? table2Position : table1Position;
        Quaternion targetRot = isPlacedOnTable2 ? table2Rotation : table1Rotation;

        transform.position = targetPos;
        transform.rotation = targetRot;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }
}