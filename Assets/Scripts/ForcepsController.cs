using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ForcepsController : MonoBehaviour
{
    [Header("Role")]
    [Tooltip("Is this the Pickup Forceps (grabs cotton from the jar) or the Handling Forceps (performs the actual strokes)?")]
    public ForcepsRole role;

    [Tooltip("Drag the OTHER forceps' ForcepsController here, so cotton can be transferred between them.")]
    public ForcepsController otherForceps;

    [Tooltip("How close (in meters) the two forceps' sockets need to be for a transfer to happen.")]
    public float transferDistance = 0.05f;

    [Header("Component References")]
    [Tooltip("Drag the CottonSocket object here.")]
    public XRSocketInteractor cottonSocket;

    [Tooltip("Drag the XR Grab Interactable of the Forceps here.")]
    public XRGrabInteractable forcepsGrabInteractable;

    [Header("Input Setup")]
    [Tooltip("Select the Left Hand Primary Button (X Button).")]
    public InputActionReference leftPrimaryButton;

    [Tooltip("Select the Right Hand Primary Button (A Button).")]
    public InputActionReference rightPrimaryButton;

    [Header("Spawning System")]
    [Tooltip("Drag your Cotton Ball Prefab here from the Project window.")]
    public GameObject cottonPrefab;

    // Controlled by the CottonJarZone script
    [HideInInspector] public bool isInJarZone = false;
    private bool isSpawning = false;

    private bool isHeldByLeftHand = false;
    private bool isHeldByRightHand = false;

    private void OnEnable()
    {
        // Listen for when the forceps are picked up and dropped
        forcepsGrabInteractable.selectEntered.AddListener(OnForcepsGrabbed);
        forcepsGrabInteractable.selectExited.AddListener(OnForcepsReleased);

        // Listen for cotton entering/leaving THIS forceps' socket, to track who holds it
        cottonSocket.selectEntered.AddListener(OnCottonSocketed);
        cottonSocket.selectExited.AddListener(OnCottonUnsocketed);

        if (leftPrimaryButton != null) leftPrimaryButton.action.Enable();
        if (rightPrimaryButton != null) rightPrimaryButton.action.Enable();
    }

    private void OnDisable()
    {
        forcepsGrabInteractable.selectEntered.RemoveListener(OnForcepsGrabbed);
        forcepsGrabInteractable.selectExited.RemoveListener(OnForcepsReleased);

        cottonSocket.selectEntered.RemoveListener(OnCottonSocketed);
        cottonSocket.selectExited.RemoveListener(OnCottonUnsocketed);
    }

    private void OnCottonSocketed(SelectEnterEventArgs args)
    {
        CottonState cotton = args.interactableObject.transform.GetComponent<CottonState>();
        if (cotton != null) cotton.SetHolder(role);
    }

    private void OnCottonUnsocketed(SelectExitEventArgs args)
    {
        CottonState cotton = args.interactableObject.transform.GetComponent<CottonState>();
        if (cotton != null) cotton.SetHolder(null);
    }

    private void OnForcepsGrabbed(SelectEnterEventArgs args)
    {
        Transform t = args.interactorObject.transform;
        string handName = FindHandName(t);

        if (handName.Contains("LEFT")) isHeldByLeftHand = true;
        else if (handName.Contains("RIGHT")) isHeldByRightHand = true;
        else Debug.LogWarning("[Forceps] Could not determine hand from interactor: " + t.name);
    }

    private string FindHandName(Transform t)
    {
        Transform current = t;
        while (current != null)
        {
            if (current.name.ToUpper().Contains("LEFT HAND") || current.name.ToUpper().Contains("RIGHT HAND"))
                return current.name.ToUpper();
            current = current.parent;
        }
        return "";
    }

    private void OnForcepsReleased(SelectExitEventArgs args)
    {
        isHeldByLeftHand = false;
        isHeldByRightHand = false;

        // If the player drops the forceps, automatically drop the cotton too
        DropCotton();
        cottonSocket.socketActive = false;
    }

    private void Update()
    {
        // Only run logic if the forceps are actually being held
        if (!isHeldByLeftHand && !isHeldByRightHand) return;

        bool isPressingButton = false;

        // Check if the active hand is holding down its specific button
        if (isHeldByLeftHand && leftPrimaryButton.action.IsPressed())
        {
            isPressingButton = true;
        }
        else if (isHeldByRightHand && rightPrimaryButton.action.IsPressed())
        {
            isPressingButton = true;
        }

        // Logic: Socket is only active while the button is held
        if (isPressingButton)
        {
            cottonSocket.socketActive = true;

            // Jar spawning is Pickup Forceps only
            if (role == ForcepsRole.Pickup && isInJarZone && !cottonSocket.hasSelection && !isSpawning)
            {
                SpawnCotton();
            }

            // Transfer: if this socket is empty and the OTHER forceps' socket is holding
            // cotton, and the two tips are close enough, steal it into this socket.
            if (!cottonSocket.hasSelection && otherForceps != null && otherForceps.cottonSocket.hasSelection)
            {
                float distance = Vector3.Distance(cottonSocket.transform.position, otherForceps.cottonSocket.transform.position);
                if (distance <= transferDistance)
                {
                    TryStealCotton();
                }
            }
        }
        else
        {
            // The moment they let go of the button, drop the cotton and turn off the magnet
            if (cottonSocket.hasSelection)
            {
                DropCotton();
            }
            cottonSocket.socketActive = false;
        }
    }

    private void SpawnCotton()
    {
        if (cottonPrefab == null)
        {
            Debug.LogError("[Forceps] Cotton Prefab is missing! Drag it into the Inspector.");
            return;
        }

        isSpawning = true;

        // Spawn the cotton exactly inside the socket
        Instantiate(cottonPrefab, cottonSocket.transform.position, cottonSocket.transform.rotation);
        Debug.Log("[Forceps] New cotton spawned from the jar.");

        // Wait half a second before allowing another spawn, giving the XRI socket time to attach
        Invoke(nameof(ResetSpawnCooldown), 0.5f);
    }

    private void ResetSpawnCooldown()
    {
        isSpawning = false;
    }

    private void TryStealCotton()
    {
        IXRSelectInteractable cottonInteractable = otherForceps.cottonSocket.GetOldestInteractableSelected();
        if (cottonInteractable == null) return;

        // Release from the other forceps' socket first...
        otherForceps.cottonSocket.interactionManager.SelectCancel(
            (IXRSelectInteractor)otherForceps.cottonSocket, cottonInteractable);

        // ...then pull it into this socket. Both calls go through the Interaction Manager's
        // normal API, so the selectEntered/selectExited events fire as usual and
        // CottonState.SetHolder() gets called automatically in the right order.
        cottonSocket.interactionManager.SelectEnter(
            (IXRSelectInteractor)cottonSocket, cottonInteractable);

        Debug.Log($"[Forceps] Cotton transferred: {otherForceps.role} -> {role}");
    }

    private void DropCotton()
    {
        if (cottonSocket.hasSelection)
        {
            IXRSelectInteractable cotton = cottonSocket.GetOldestInteractableSelected();
            cottonSocket.interactionManager.SelectCancel((IXRSelectInteractor)cottonSocket, cotton);
            Debug.Log("[Forceps] Button released. Dropping cotton.");
        }
    }
}