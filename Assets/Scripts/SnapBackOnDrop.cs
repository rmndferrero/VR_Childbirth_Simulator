using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class SnapBackOnDrop : MonoBehaviour
{
    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;

    [Header("Status")]
    [Tooltip("Is the item currently hovering over or sitting on a TableZone?")]
    public bool isOverValidTable = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        // Save initial position as starting fallback
        SaveCurrentLocation();
    }

    void OnEnable()
    {
        // Listen to XR Toolkit drop event
        grabInteractable.selectExited.AddListener(OnDrop);
    }

    void OnDisable()
    {
        grabInteractable.selectExited.RemoveListener(OnDrop);
    }

    /// <summary>
    /// Saves the item's current position and rotation as the new return point.
    /// </summary>
    public void SaveCurrentLocation()
    {
        lastValidPosition = transform.position;
        lastValidRotation = transform.rotation;
    }

    /// <summary>
    /// Triggered automatically when the user releases the grab button in VR.
    /// </summary>
    private void OnDrop(SelectExitEventArgs args)
    {
        // If dropped in mid-air / outside a TableZone
        if (!isOverValidTable)
        {
            ReturnToLastTable();
        }
        else
        {
            // If dropped inside a valid TableZone, lock in this new position
            SaveCurrentLocation();
        }
    }

    /// <summary>
    /// Teleports the item back to the last valid saved table position.
    /// </summary>
    public void ReturnToLastTable()
    {
        // Stop physics momentum so it doesn't keep flying/rolling
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.position = lastValidPosition;
        transform.rotation = lastValidRotation;

        // Reset state
        isOverValidTable = true;
    }

    // --- TRIGGER DETECTION ---

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("TableZone"))
        {
            isOverValidTable = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("TableZone"))
        {
            isOverValidTable = false;
        }
    }
}