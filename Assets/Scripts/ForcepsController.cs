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
    public float transferDistance = 0.08f;

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
    [HideInInspector] public CottonJarZone activeJarZone = null;
    private bool isSpawning = false;

    private bool isHeldByLeftHand = false;
    private bool isHeldByRightHand = false;

    // Scale preservation to prevent 3D stretching glitches
    private Vector3 initialLocalScale;

    public bool IsGrabbed => forcepsGrabInteractable != null && forcepsGrabInteractable.isSelected;

    /// <summary>
    /// Robust check: true only if the socket is actually holding a valid, non-destroyed cotton interactable.
    /// </summary>
    public bool HasCotton
    {
        get
        {
            if (cottonSocket == null || !cottonSocket.hasSelection) return false;
            var interactable = cottonSocket.GetOldestInteractableSelected();
            if (interactable == null) return false;
            if (interactable is Object uObj && uObj == null) return false;
            if (interactable.transform == null) return false;
            return true;
        }
    }

    private void Awake()
    {
        initialLocalScale = transform.localScale;

        var grabTransformers = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Transformers.XRGeneralGrabTransformer>(true);
        foreach (var gt in grabTransformers)
        {
            gt.allowOneHandedScaling = false;
            gt.allowTwoHandedScaling = false;
        }

        if (forcepsGrabInteractable != null)
        {
            forcepsGrabInteractable.trackScale = false;
        }

        if (cottonSocket != null)
        {
            cottonSocket.showInteractableHoverMeshes = false;
            if (cottonSocket.interactionManager == null)
            {
                cottonSocket.interactionManager = FindAnyObjectByType<XRInteractionManager>();
            }
        }
    }

    private void OnEnable()
    {
        if (initialLocalScale == Vector3.zero)
        {
            initialLocalScale = transform.localScale;
        }

        var grabTransformers = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Transformers.XRGeneralGrabTransformer>(true);
        foreach (var gt in grabTransformers)
        {
            gt.allowOneHandedScaling = false;
            gt.allowTwoHandedScaling = false;
        }

        // Listen for when the forceps are picked up and dropped
        if (forcepsGrabInteractable != null)
        {
            forcepsGrabInteractable.trackScale = false;
            forcepsGrabInteractable.selectEntered.AddListener(OnForcepsGrabbed);
            forcepsGrabInteractable.selectExited.AddListener(OnForcepsReleased);
        }

        // Listen for cotton entering/leaving THIS forceps' socket, to track who holds it
        if (cottonSocket != null)
        {
            cottonSocket.selectEntered.AddListener(OnCottonSocketed);
            cottonSocket.selectExited.AddListener(OnCottonUnsocketed);
        }

        if (leftPrimaryButton != null) leftPrimaryButton.action.Enable();
        if (rightPrimaryButton != null) rightPrimaryButton.action.Enable();
    }

    private void OnDisable()
    {
        if (forcepsGrabInteractable != null)
        {
            forcepsGrabInteractable.selectEntered.RemoveListener(OnForcepsGrabbed);
            forcepsGrabInteractable.selectExited.RemoveListener(OnForcepsReleased);
        }

        if (cottonSocket != null)
        {
            cottonSocket.selectEntered.RemoveListener(OnCottonSocketed);
            cottonSocket.selectExited.RemoveListener(OnCottonUnsocketed);
        }
    }

    private void OnCottonSocketed(SelectEnterEventArgs args)
    {
        if (args.interactableObject != null && args.interactableObject.transform != null)
        {
            CottonState cotton = args.interactableObject.transform.GetComponent<CottonState>();
            if (cotton != null) cotton.SetHolder(role);
        }
    }

    private void OnCottonUnsocketed(SelectExitEventArgs args)
    {
        if (args.interactableObject != null && args.interactableObject.transform != null)
        {
            CottonState cotton = args.interactableObject.transform.GetComponent<CottonState>();
            if (cotton != null) cotton.SetHolder(null);
        }
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
            string upper = current.name.ToUpper();
            if (upper.Contains("LEFT")) return "LEFT";
            if (upper.Contains("RIGHT")) return "RIGHT";
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
        if (cottonSocket != null) cottonSocket.socketActive = false;
    }

    private void Update()
    {
        // Clean up any destroyed or invalid interactable references from the socket safely without disabling the socket component
        if (cottonSocket != null && cottonSocket.hasSelection && !HasCotton)
        {
            if (cottonSocket.interactionManager != null)
            {
                var list = new System.Collections.Generic.List<IXRSelectInteractable>(cottonSocket.interactablesSelected);
                foreach (var item in list)
                {
                    if (item != null)
                        cottonSocket.interactionManager.SelectCancel((IXRSelectInteractor)cottonSocket, item);
                }
            }
        }

        // Only run logic if the forceps are actually being held
        if (!isHeldByLeftHand && !isHeldByRightHand) return;

        bool isPressingButton = false;

        // Check if the active hand is holding down its specific button
        if (isHeldByLeftHand && leftPrimaryButton != null && leftPrimaryButton.action.IsPressed())
        {
            isPressingButton = true;
        }
        else if (isHeldByRightHand && rightPrimaryButton != null && rightPrimaryButton.action.IsPressed())
        {
            isPressingButton = true;
        }

        // Logic: Socket is only active while the button is held
        if (isPressingButton)
        {
            if (cottonSocket != null) cottonSocket.socketActive = true;

            // Jar spawning is Pickup Forceps only
            if (role == ForcepsRole.Pickup && isInJarZone && !HasCotton && !isSpawning)
            {
                SpawnCotton();
            }

            // Transfer: if this socket is empty and the OTHER forceps' socket is holding
            // cotton, and the two tips are close enough, steal it into this socket.
            if (!HasCotton && otherForceps != null && otherForceps.HasCotton && cottonSocket != null && otherForceps.cottonSocket != null)
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
            if (HasCotton)
            {
                DropCotton();
            }
            if (cottonSocket != null) cottonSocket.socketActive = false;
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
        GameObject newCotton = Instantiate(cottonPrefab, cottonSocket.transform.position, cottonSocket.transform.rotation);
        CottonState cottonState = newCotton.GetComponent<CottonState>();
        if (cottonState != null && activeJarZone != null)
        {
            cottonState.SoakCotton(activeJarZone.soakedMaterial, activeJarZone.antisepticType);
            Debug.Log($"[Forceps] Spawned pre-soaked cotton with {activeJarZone.antisepticType} from jar.");
        }
        else
        {
            Debug.Log("[Forceps] New cotton spawned from the jar.");
        }

        // Wait half a second before allowing another spawn, giving the XRI socket time to attach
        Invoke(nameof(ResetSpawnCooldown), 0.5f);
    }

    private void ResetSpawnCooldown()
    {
        isSpawning = false;
    }

    private void TryStealCotton()
    {
        if (otherForceps == null || otherForceps.cottonSocket == null || cottonSocket == null) return;

        IXRSelectInteractable cottonInteractable = otherForceps.cottonSocket.GetOldestInteractableSelected();
        if (cottonInteractable == null || (cottonInteractable is Object o && o == null)) return;

        if (cottonSocket != null) cottonSocket.socketActive = true;

        // Release from the other forceps' socket first...
        if (otherForceps.cottonSocket.interactionManager != null)
        {
            otherForceps.cottonSocket.interactionManager.SelectCancel(
                (IXRSelectInteractor)otherForceps.cottonSocket, cottonInteractable);

            // Force socket reset on the donor forceps so it is completely clean
            otherForceps.DropCotton();

            // ...then pull it into this socket.
            cottonSocket.interactionManager.SelectEnter(
                (IXRSelectInteractor)cottonSocket, cottonInteractable);

            Debug.Log($"[Forceps] Cotton transferred: {otherForceps.role} -> {role}");
        }
    }

    public void DropCotton()
    {
        if (cottonSocket != null)
        {
            if (cottonSocket.hasSelection)
            {
                var list = new System.Collections.Generic.List<IXRSelectInteractable>(cottonSocket.interactablesSelected);
                foreach (var cotton in list)
                {
                    if (cotton != null && cottonSocket.interactionManager != null)
                    {
                        cottonSocket.interactionManager.SelectCancel((IXRSelectInteractor)cottonSocket, cotton);
                    }
                }
            }

            cottonSocket.socketActive = false;
            Debug.Log($"[Forceps] DropCotton executed on {role} forceps.");
        }
    }
}