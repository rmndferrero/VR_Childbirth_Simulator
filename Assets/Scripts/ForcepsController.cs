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
            // Lock forceps rigidly into player's hand with zero wobble or lag
            forcepsGrabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
            forcepsGrabInteractable.attachEaseInTime = 0f;
            forcepsGrabInteractable.smoothPosition = false;
            forcepsGrabInteractable.smoothRotation = false;
        }

        if (cottonSocket != null)
        {
            cottonSocket.showInteractableHoverMeshes = false;
            cottonSocket.socketActive = HasCotton;
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
            forcepsGrabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
            forcepsGrabInteractable.attachEaseInTime = 0f;
            forcepsGrabInteractable.smoothPosition = false;
            forcepsGrabInteractable.smoothRotation = false;
            forcepsGrabInteractable.selectEntered.AddListener(OnForcepsGrabbed);
            forcepsGrabInteractable.selectExited.AddListener(OnForcepsReleased);
        }

        // Listen for cotton entering/leaving THIS forceps' socket, to track who holds it
        if (cottonSocket != null)
        {
            cottonSocket.socketActive = HasCotton;
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

            // Ignore collision between forceps colliders and the socketed cotton ball
            Collider cottonCol = args.interactableObject.transform.GetComponent<Collider>();
            if (cottonCol != null)
            {
                Collider[] myCols = GetComponentsInChildren<Collider>(true);
                foreach (var fc in myCols)
                {
                    if (fc != null && fc != cottonCol)
                    {
                        Physics.IgnoreCollision(fc, cottonCol, true);
                    }
                }
            }

            // Disable gravity on cotton ball while clamped to prevent fighting socket velocity tracking
            Rigidbody rb = args.interactableObject.transform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
            }
        }
    }

    private void OnCottonUnsocketed(SelectExitEventArgs args)
    {
        if (args.interactableObject != null && args.interactableObject.transform != null)
        {
            CottonState cotton = args.interactableObject.transform.GetComponent<CottonState>();
            if (cotton != null) cotton.SetHolder(null);

            // Restore gravity upon release
            Rigidbody rb = args.interactableObject.transform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = true;
            }
        }
    }

    private void OnForcepsGrabbed(SelectEnterEventArgs args)
    {
        Transform t = args.interactorObject.transform;
        string handName = FindHandName(t);

        if (handName.Contains("LEFT")) isHeldByLeftHand = true;
        else if (handName.Contains("RIGHT")) isHeldByRightHand = true;
        else
        {
            isHeldByRightHand = true;
            Debug.Log("[Forceps] Grabbed by " + t.name);
        }

        // Lock kinematic while held in hand to guarantee zero physics wobble/deflection
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
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

        if (role == ForcepsRole.Handling)
        {
            var tool = GetComponent<ToolItem>();
            if (tool != null)
            {
                if (tool.IsLockedOnTable2)
                {
                    tool.TeleportToTable2();
                }
                else
                {
                    tool.TeleportToHome();
                }
            }
        }
        else
        {
            // Restore physics when released in midair for other forceps
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
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
        if (!IsGrabbed && !isHeldByLeftHand && !isHeldByRightHand) return;

        bool isPressingButton = false;
        bool aPressed = rightPrimaryButton != null && rightPrimaryButton.action != null && rightPrimaryButton.action.IsPressed();
        bool xPressed = leftPrimaryButton != null && leftPrimaryButton.action != null && leftPrimaryButton.action.IsPressed();

        if (aPressed || xPressed)
        {
            isPressingButton = true;
        }

        // -------------------------------------------------------------
        // ROLE 1: PICKUP FORCEPS
        // -------------------------------------------------------------
        if (role == ForcepsRole.Pickup)
        {
            // Inside the jar zone: holding button extracts cotton
            if (isInJarZone && isPressingButton && !HasCotton && !isSpawning)
            {
                SpawnCotton();
            }

            // Securely retain cotton once picked up so player can carry it to Handling Forceps without falling
            if (HasCotton && cottonSocket != null)
            {
                cottonSocket.socketActive = true;
            }
        }
        // -------------------------------------------------------------
        // ROLE 2: HANDLING FORCEPS
        // -------------------------------------------------------------
        else if (role == ForcepsRole.Handling)
        {
            // Keep socket inactive whenever it doesn't hold cotton so it cannot passively attract cotton as a magnet!
            if (!HasCotton && cottonSocket != null && cottonSocket.socketActive)
            {
                cottonSocket.socketActive = false;
            }

            // TRANSFER LOGIC: Only happens when Handling Forceps has no cotton, Pickup Forceps HAS cotton,
            // tips are in range (<= transferDistance), AND player explicitly PRESSES Button A (WasPressedThisFrame)!
            // It will NEVER transfer automatically like a magnet just by being close!
            if (!HasCotton && otherForceps != null && otherForceps.HasCotton && cottonSocket != null && otherForceps.cottonSocket != null)
            {
                float distance = Vector3.Distance(cottonSocket.transform.position, otherForceps.cottonSocket.transform.position);
                if (distance <= transferDistance)
                {
                    bool transferPressed = false;
                    if (isHeldByRightHand && rightPrimaryButton != null && rightPrimaryButton.action != null)
                        transferPressed = rightPrimaryButton.action.WasPressedThisFrame();
                    else if (isHeldByLeftHand && leftPrimaryButton != null && leftPrimaryButton.action != null)
                        transferPressed = leftPrimaryButton.action.WasPressedThisFrame();
                    else
                    {
                        bool a = (rightPrimaryButton != null && rightPrimaryButton.action != null && rightPrimaryButton.action.WasPressedThisFrame());
                        bool x = (leftPrimaryButton != null && leftPrimaryButton.action != null && leftPrimaryButton.action.WasPressedThisFrame());
                        transferPressed = a || x;
                    }

                    if (transferPressed)
                    {
                        TryStealCotton();
                    }
                }
            }

            // Clamping while holding cotton:
            // Holding Button A keeps the cotton firmly clamped during wiping.
            // Letting go of Button A drops the cotton into the waste basin.
            if (HasCotton)
            {
                bool holdClamped = false;
                if (isHeldByRightHand && rightPrimaryButton != null && rightPrimaryButton.action != null)
                    holdClamped = rightPrimaryButton.action.IsPressed();
                else if (isHeldByLeftHand && leftPrimaryButton != null && leftPrimaryButton.action != null)
                    holdClamped = leftPrimaryButton.action.IsPressed();
                else
                {
                    bool a = (rightPrimaryButton != null && rightPrimaryButton.action != null && rightPrimaryButton.action.IsPressed());
                    bool x = (leftPrimaryButton != null && leftPrimaryButton.action != null && leftPrimaryButton.action.IsPressed());
                    holdClamped = a || x;
                }

                if (holdClamped)
                {
                    if (cottonSocket != null) cottonSocket.socketActive = true;
                }
                else
                {
                    DropCotton();
                    if (cottonSocket != null) cottonSocket.socketActive = false;
                }
            }
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
        
        // Ignore collision between forceps colliders and the new cotton ball
        Collider cottonCol = newCotton.GetComponent<Collider>();
        if (cottonCol != null)
        {
            Collider[] myCols = GetComponentsInChildren<Collider>(true);
            foreach (var fc in myCols)
            {
                if (fc != null && fc != cottonCol)
                {
                    Physics.IgnoreCollision(fc, cottonCol, true);
                }
            }
        }

        // Immediately attach to socket via XRInteractionManager so it never drops
        var grabInteractable = newCotton.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null && cottonSocket != null && cottonSocket.interactionManager != null)
        {
            cottonSocket.socketActive = true;
            cottonSocket.interactionManager.SelectEnter((IXRSelectInteractor)cottonSocket, (IXRSelectInteractable)grabInteractable);
        }

        CottonState cottonState = newCotton.GetComponent<CottonState>();
        if (cottonState != null && activeJarZone != null)
        {
            cottonState.SoakCotton(activeJarZone.soakedMaterial, activeJarZone.antisepticType);
            Debug.Log($"[Forceps] Spawned and clamped pre-soaked cotton with {activeJarZone.antisepticType} from jar.");
        }
        else
        {
            Debug.Log("[Forceps] New cotton spawned and clamped from the jar.");
        }

        // Wait half a second before allowing another spawn
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
            if (otherForceps.cottonSocket != null) otherForceps.cottonSocket.socketActive = false;

            // ...then pull it into this socket.
            cottonSocket.interactionManager.SelectEnter(
                (IXRSelectInteractor)cottonSocket, cottonInteractable);

            // Ignore collision with this forceps
            if (cottonInteractable.transform != null)
            {
                Collider cottonCol = cottonInteractable.transform.GetComponent<Collider>();
                if (cottonCol != null)
                {
                    Collider[] myCols = GetComponentsInChildren<Collider>(true);
                    foreach (var fc in myCols)
                    {
                        if (fc != null && fc != cottonCol)
                        {
                            Physics.IgnoreCollision(fc, cottonCol, true);
                        }
                    }
                }
            }

            Debug.Log($"[Forceps] Cotton transferred: {otherForceps.role} -> {role} via Button A press.");
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
                    if (cotton != null)
                    {
                        if (cotton.transform != null)
                        {
                            Rigidbody rb = cotton.transform.GetComponent<Rigidbody>();
                            if (rb != null) rb.useGravity = true;
                        }

                        if (cottonSocket.interactionManager != null)
                        {
                            cottonSocket.interactionManager.SelectCancel((IXRSelectInteractor)cottonSocket, cotton);
                        }
                    }
                }
            }

            cottonSocket.socketActive = false;
            Debug.Log($"[Forceps] DropCotton executed on {role} forceps.");
        }
    }
}
