using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ToolItem : MonoBehaviour
{
    [Header("Tool Configuration")]
    public string toolID;

    [Header("Hand Grip Alignment (Surgical & Realistic)")]
    [Tooltip("Dedicated transform where the player hand holds this tool.")]
    public Transform handAttachPoint;
    [Tooltip("Custom local offset for the hand grip position.")]
    public Vector3 attachPositionOffset = Vector3.zero;
    [Tooltip("Custom local Euler rotation for the hand grip (pointing forward from hand).")]
    public Vector3 attachEulerAngles = Vector3.zero;
    [Tooltip("If true, automatically computes the handle grip position for surgical scissors, forceps, clamps, and basins.")]
    public bool autoAlignHandleGrip = true;

    private XRGrabInteractable grab;
    private Rigidbody rb;

    // Visual feedback — cached material instances (created once, reused everywhere)
    private Renderer[] allRenderers;
    private Dictionary<Renderer, Material[]> cachedMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Color[]> savedColors = new Dictionary<Renderer, Color[]>();

    // Table 1 home position + scale
    private Vector3 homePos;
    private Quaternion homeRot;
    private Vector3 homeScale;
    private Transform initialParent;

    // Table 2 locked position
    private Vector3 table2Pos;
    private Quaternion table2Rot;

    // State
    private bool isLockedOnTable2 = false;
    private bool isBeingRejected = false;
    private Coroutine rejectCo;
    private Coroutine dropCo;

    // Static registry to prevent tools from colliding with / bumping each other or player
    private static HashSet<ToolItem> allToolsInScene = new HashSet<ToolItem>();
    private Collider[] myColliders;

    private void Awake()
    {
        allToolsInScene.Add(this);

        grab = GetComponent<XRGrabInteractable>()
            ?? GetComponentInParent<XRGrabInteractable>()
            ?? GetComponentInChildren<XRGrabInteractable>();

        rb = GetComponent<Rigidbody>()
            ?? GetComponentInParent<Rigidbody>()
            ?? GetComponentInChildren<Rigidbody>();

        // Cache renderers and create material instances ONCE
        allRenderers = GetComponentsInChildren<Renderer>();
        foreach (var r in allRenderers)
        {
            if (r == null) continue;

            // Access .materials ONCE to create instances, then cache them
            Material[] mats = r.materials;
            cachedMaterials[r] = mats;

            // Save original colors from the freshly-created instances
            var cols = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.HasProperty("_BaseColor"))      cols[i] = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color"))      cols[i] = m.color;
                else                                   cols[i] = Color.white;
            }
            savedColors[r] = cols;
        }

        homeScale = transform.localScale;
        initialParent = transform.parent;

        var grabTransformers = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Transformers.XRGeneralGrabTransformer>(true);
        foreach (var gt in grabTransformers)
        {
            gt.allowOneHandedScaling = false;
            gt.allowTwoHandedScaling = false;
        }

        EnsureInteractableAndAttachPoint();

        if (grab != null)
        {
            grab.trackScale = false;
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    public void EnsureInteractableAndAttachPoint()
    {
        if (grab == null)
        {
            grab = GetComponent<XRGrabInteractable>()
                ?? GetComponentInParent<XRGrabInteractable>()
                ?? GetComponentInChildren<XRGrabInteractable>();
        }

        if (grab != null)
        {
            // Ensure Player hand interaction layer (layer 2048) is enabled
            grab.interactionLayers = grab.interactionLayers.value | 2048;

            if (grab.interactionManager == null)
            {
                grab.interactionManager = FindAnyObjectByType<XRInteractionManager>();
            }

            // Auto-populate colliders if empty
            if (grab.colliders == null || grab.colliders.Count == 0)
            {
                var cols = GetComponentsInChildren<Collider>(true);
                foreach (var c in cols)
                {
                    if (c != null && !c.isTrigger) grab.colliders.Add(c);
                }
            }

            grab.useDynamicAttach = false;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.attachEaseInTime = 0.12f;

            SetupHandAttachPoint();
        }
    }

    public void SetupHandAttachPoint()
    {
        if (handAttachPoint == null)
        {
            // Check for existing child attach points
            Transform existing = transform.Find("AttachPoint") 
                              ?? transform.Find("GrabAttachPoint") 
                              ?? transform.Find("HandleGrabPoint");

            if (existing != null)
            {
                handAttachPoint = existing;
            }
            else
            {
                GameObject apObj = new GameObject("AttachPoint");
                apObj.transform.SetParent(transform, false);
                handAttachPoint = apObj.transform;

                if (autoAlignHandleGrip)
                {
                    AutoConfigureGripForToolType();
                }
                else
                {
                    handAttachPoint.localPosition = attachPositionOffset;
                    handAttachPoint.localRotation = Quaternion.Euler(attachEulerAngles);
                }
            }
        }

        if (grab != null && handAttachPoint != null)
        {
            grab.attachTransform = handAttachPoint;
        }
    }

    private void AutoConfigureGripForToolType()
    {
        if (handAttachPoint == null) return;

        string nameLower = (gameObject.name + "_" + toolID).ToLower();

        // Check if there is already a HandleGrabPoint on Pitcher or similar
        Transform handlePoint = transform.Find("HandleGrabPoint");
        if (handlePoint != null)
        {
            handAttachPoint.localPosition = handlePoint.localPosition;
            handAttachPoint.localRotation = handlePoint.localRotation;
            return;
        }

        // Calculate approximate local bounds
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            localBounds = mf.sharedMesh.bounds;
        }

        if (nameLower.Contains("pitcher") || nameLower.Contains("flask"))
        {
            // Pitcher: grip at side handle wrapped naturally across the 4 fingers
            attachPositionOffset = new Vector3(-0.0255f, 0.0195f, 0.0094f);
            attachEulerAngles = new Vector3(0.0f, 90.0f, 0.0f);
        }
        else if (nameLower.Contains("basin"))
        {
            // Kidney basin: grip at side rim
            attachPositionOffset = new Vector3(0.0f, localBounds.max.y * 0.8f, localBounds.max.z * 0.6f);
            attachEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);
        }
        else if (nameLower.Contains("scissor") || nameLower.Contains("curette") || nameLower.Contains("clamp") || nameLower.Contains("needle") || nameLower.Contains("tongs"))
        {
            // Scissors, Clamps, Needle Holders: grip at the finger rings / back handle
            Vector3 ext = localBounds.extents;
            // Identify longest axis
            if (ext.z >= ext.x && ext.z >= ext.y)
            {
                attachPositionOffset = new Vector3(0.0f, 0.0f, localBounds.min.z + ext.z * 0.35f);
                attachEulerAngles = Vector3.zero;
            }
            else if (ext.y >= ext.x && ext.y >= ext.z)
            {
                attachPositionOffset = new Vector3(0.0f, localBounds.min.y + ext.y * 0.35f, 0.0f);
                attachEulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
            }
            else
            {
                attachPositionOffset = new Vector3(localBounds.min.x + ext.x * 0.35f, 0.0f, 0.0f);
                attachEulerAngles = new Vector3(0.0f, 90.0f, 0.0f);
            }
        }
        else if (nameLower.Contains("forcep"))
        {
            // Forceps (Pickup / Handling / Thumb): grip at middle-to-back knurled shaft
            Vector3 ext = localBounds.extents;
            if (ext.y >= ext.x && ext.y >= ext.z)
            {
                attachPositionOffset = new Vector3(0.0f, localBounds.min.y + ext.y * 0.5f, 0.0f);
                attachEulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
            }
            else if (ext.z >= ext.x && ext.z >= ext.y)
            {
                attachPositionOffset = new Vector3(0.0f, 0.0f, localBounds.min.z + ext.z * 0.5f);
                attachEulerAngles = Vector3.zero;
            }
            else
            {
                attachPositionOffset = new Vector3(localBounds.min.x + ext.x * 0.5f, 0.0f, 0.0f);
                attachEulerAngles = new Vector3(0.0f, 90.0f, 0.0f);
            }
        }
        else if (nameLower.Contains("scalpel"))
        {
            // Scalpel: pencil grip along handle
            Vector3 ext = localBounds.extents;
            attachPositionOffset = new Vector3(0.0f, 0.0f, localBounds.min.z + ext.z * 0.45f);
            attachEulerAngles = Vector3.zero;
        }

        handAttachPoint.localPosition = attachPositionOffset;
        handAttachPoint.localRotation = Quaternion.Euler(attachEulerAngles);
    }

    public Vector3 HomeScale => homeScale;
    public Vector3 HomePos => homePos;
    public Quaternion HomeRot => homeRot;
    public Transform InitialParent => initialParent;

    // Return sockets tracking
    private readonly List<ToolReturnSocket> nearbySockets = new List<ToolReturnSocket>();
    private ToolReturnSocket table1Socket;
    private ToolReturnSocket table2Socket;

    public bool IsBeingHeld => grab != null && grab.isSelected;

    private void Start()
    {
        homePos = transform.position;
        homeRot = transform.rotation;
        homeScale = transform.localScale;
        initialParent = transform.parent;

        EnsureInteractableAndAttachPoint();

        // Lock kinematic while resting on table so player physical collisions cannot push or knock over tools
        if (rb != null)
        {
            SafeResetVelocity(rb);
            rb.isKinematic = true;
        }

        EnsureTable1ReturnSocket();
        IgnoreCollisionsWithOtherToolsAndPlayer();
    }

    private void EnsureTable1ReturnSocket()
    {
        if (table1Socket == null)
        {
            GameObject sockObj = new GameObject($"{gameObject.name}_Table1Socket");
            sockObj.transform.position = homePos;
            sockObj.transform.rotation = homeRot;
            if (transform.parent != null) sockObj.transform.SetParent(transform.parent, true);

            table1Socket = sockObj.AddComponent<ToolReturnSocket>();
            table1Socket.targetTool = this;
            table1Socket.targetToolID = toolID;
        }
    }

    public void RegisterNearbyReturnSocket(ToolReturnSocket socket)
    {
        if (socket != null && !nearbySockets.Contains(socket))
        {
            nearbySockets.Add(socket);
        }
    }

    public void UnregisterNearbyReturnSocket(ToolReturnSocket socket)
    {
        if (socket != null)
        {
            nearbySockets.Remove(socket);
        }
    }

    public void OnToolSafelyReturned()
    {
        CancelDropTimer();
        if (rb != null)
        {
            SafeResetVelocity(rb);
            rb.isKinematic = true;
        }
    }

    private void SafeResetVelocity(Rigidbody body)
    {
        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Disables physics collisions between tools and between tools and player body colliders.
    /// </summary>
    private void IgnoreCollisionsWithOtherToolsAndPlayer()
    {
        myColliders = GetComponentsInChildren<Collider>();
        if (myColliders == null || myColliders.Length == 0) return;

        // Ignore collisions with other tools
        foreach (var otherTool in allToolsInScene)
        {
            if (otherTool == null || otherTool == this) continue;

            Collider[] otherColliders = otherTool.myColliders ?? otherTool.GetComponentsInChildren<Collider>();
            if (otherColliders == null) continue;

            foreach (var myCol in myColliders)
            {
                if (myCol == null) continue;
                foreach (var otherCol in otherColliders)
                {
                    if (otherCol == null) continue;
                    Physics.IgnoreCollision(myCol, otherCol, true);
                }
            }
        }

        // Ignore collisions with player body / CharacterController colliders
        CharacterController[] playerControllers = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        foreach (var cc in playerControllers)
        {
            if (cc == null) continue;
            foreach (var myCol in myColliders)
            {
                if (myCol == null) continue;
                Physics.IgnoreCollision(myCol, cc, true);
            }
        }
    }

    private void OnDestroy()
    {
        allToolsInScene.Remove(this);

        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }

    // ── XR Events ──

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (!isBeingRejected)
        {
            CancelDropTimer();
        }

        // Enable dynamic physics while held by player hand
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        // Check if player tries to use/grab any tool before talking to mother in Step 1
        if (VRDemoGameManager.Instance != null && VRDemoGameManager.Instance.currentSimulationPhase == SimulationPhase.STEP_1_TALK_TO_MOTHER)
        {
            if (VRHeadsetVisualFeedback.Instance != null)
            {
                VRHeadsetVisualFeedback.Instance.TriggerStep1ToolViolation(toolID);
            }
        }
        else if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.CheckHeldToolHazard(toolID);
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (isBeingRejected) return;

        // 1. Check if released near any Table 2 slot during Step 7 (Mayo Prep)
        if (TablePrepManager.Instance != null && TablePrepManager.Instance.isTablePrepActive)
        {
            if (TablePrepManager.Instance.TryHandleToolRelease(this))
            {
                CancelDropTimer();
                return;
            }
        }

        // 2. Check if released within snap range of any nearby return socket (Option A)
        ToolReturnSocket bestSocket = null;
        float bestDist = float.MaxValue;

        for (int i = nearbySockets.Count - 1; i >= 0; i--)
        {
            var sock = nearbySockets[i];
            if (sock != null && sock.CanAcceptTool(this))
            {
                float d = Vector3.Distance(transform.position, sock.snapTransform.position);
                if (d <= sock.snapRadius && d < bestDist)
                {
                    bestDist = d;
                    bestSocket = sock;
                }
            }
        }

        if (bestSocket != null)
        {
            // Snapped cleanly into socket -> Safe return with 0 penalty!
            CancelDropTimer();
            bestSocket.SnapTool(this);
            return;
        }

        // Otherwise, enable physics gravity so the tool falls naturally when dropped in mid-air
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        CancelDropTimer();
        dropCo = StartCoroutine(DropTimerRoutine());
    }

    private void CancelDropTimer()
    {
        if (dropCo != null)
        {
            StopCoroutine(dropCo);
            dropCo = null;
        }
    }

    /// <summary>
    /// Lets the tool drop/fall under gravity for 1.2s, then applies drop penalty and snaps it back to its table slot.
    /// </summary>
    private IEnumerator DropTimerRoutine()
    {
        yield return new WaitForSeconds(1.2f);
        if (grab != null && !grab.isSelected && !isBeingRejected)
        {
            // Dropped outside valid table socket -> Record contamination penalty
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordDropPenalty(toolID, $"Sterile Violation: {toolID} dropped! Return instruments to the sterile tray.");
            }

            if (isLockedOnTable2)
            {
                WarpToTable2();
            }
            else
            {
                WarpHome();
            }
        }
        dropCo = null;
    }

    // ── Correct Placement ──

    public void MarkCorrect(Vector3 slotPos, Quaternion slotRot)
    {
        CancelDropTimer();

        // Stop any running reject coroutine so it can't re-tint or warp after this
        if (rejectCo != null)
        {
            StopCoroutine(rejectCo);
            rejectCo = null;
        }

        isLockedOnTable2 = true;
        isBeingRejected = false;
        table2Pos = slotPos;
        table2Rot = slotRot;

        // Ensure Table 2 has a return socket for this tool
        if (table2Socket == null)
        {
            GameObject sockObj = new GameObject($"{gameObject.name}_Table2Socket");
            sockObj.transform.position = table2Pos;
            sockObj.transform.rotation = table2Rot;
            table2Socket = sockObj.AddComponent<ToolReturnSocket>();
            table2Socket.targetTool = this;
            table2Socket.targetToolID = toolID;
        }

        // Lock kinematic on Table 2 so it won't move when player gets near
        if (rb != null)
        {
            SafeResetVelocity(rb);
            rb.isKinematic = true;
        }

        transform.localScale = homeScale;

        // Re-enable grab in case a reject coroutine disabled it
        if (grab != null)
            grab.enabled = true;

        Tint(new Color(0.3f, 1f, 0.3f));
    }

    // ── Wrong Placement ──

    public void HandleWrongPlacement(XRSocketInteractor socket, float redDuration)
    {
        CancelDropTimer();
        isBeingRejected = true;

        if (rejectCo != null)
        {
            StopCoroutine(rejectCo);
            rejectCo = null;
        }

        rejectCo = StartCoroutine(RejectRoutine(socket, redDuration));
    }

    private IEnumerator RejectRoutine(XRSocketInteractor socket, float redDuration)
    {
        // 1. Flash red while sitting in socket
        Tint(new Color(1f, 0.2f, 0.2f));

        // 2. Wait so player sees red feedback
        yield return new WaitForSeconds(redDuration);

        // 3. Force exit from all interactors FIRST (so hover meshes clean up properly)
        if (grab != null)
        {
            var mgr = (socket != null) ? socket.interactionManager : grab.interactionManager;
            if (mgr != null)
            {
                var holders = new List<IXRSelectInteractor>(grab.interactorsSelecting);
                foreach (var h in holders)
                    mgr.SelectExit(h, grab);
                    
                var hoverers = new List<IXRHoverInteractor>(grab.interactorsHovering);
                foreach (var h in hoverers)
                    mgr.HoverExit(h, grab);
            }
        }

        // 4. Disable grab interactable so socket CANNOT re-select after exit
        if (grab != null)
            grab.enabled = false;

        // 5. Wait for XRI cleanup
        yield return null;
        yield return new WaitForFixedUpdate();

        // 6. Teleport to Table 1
        WarpHome();

        // 7. Restore colors and re-enable grab
        yield return new WaitForSeconds(0.3f);
        RestoreColors();

        if (grab != null)
            grab.enabled = true;

        yield return null;
        transform.localScale = homeScale;

        isBeingRejected = false;
        rejectCo = null;
    }

    // ── Warp ──

    private void WarpToTable2()
    {
        if (rb != null)
        {
            SafeResetVelocity(rb);
            rb.isKinematic = true;
        }

        transform.position = table2Pos;
        transform.rotation = table2Rot;
        transform.localScale = homeScale; // Assuming original scale is desired

        if (rb != null)
        {
            rb.position = table2Pos;
            rb.rotation = table2Rot;
            rb.isKinematic = true; // Lock kinematic resting on Table 2
        }

        Physics.SyncTransforms();
    }

    private void WarpHome()
    {
        isLockedOnTable2 = false;

        if (rb != null)
        {
            SafeResetVelocity(rb);
            rb.isKinematic = true;
        }

        if (initialParent != null && transform.parent != initialParent)
        {
            transform.SetParent(initialParent, true);
        }

        transform.position = homePos;
        transform.rotation = homeRot;
        transform.localScale = homeScale;

        if (rb != null)
        {
            rb.position = homePos;
            rb.rotation = homeRot;
            rb.isKinematic = true; // Lock kinematic resting on Table 1
        }

        Physics.SyncTransforms();
    }

    // Compatibility
    public void TeleportToTable1() => WarpHome();
    public void SaveTable2Origin(Vector3 p, Quaternion r) { isLockedOnTable2 = true; table2Pos = p; table2Rot = r; }
    public void ResetOriginToTable1() { isLockedOnTable2 = false; }

    // ── Color helpers ──

    private void Tint(Color c)
    {
        if (allRenderers == null) return;
        foreach (var r in allRenderers)
        {
            if (r == null) continue;
            if (!cachedMaterials.TryGetValue(r, out var mats)) continue;
            foreach (var m in mats)
            {
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color"))     m.color = c;
            }
        }
    }

    public void RestoreColors()
    {
        if (allRenderers == null) return;
        foreach (var r in allRenderers)
        {
            if (r == null) continue;
            if (!cachedMaterials.TryGetValue(r, out var mats)) continue;
            if (!savedColors.TryGetValue(r, out var cols)) continue;

            for (int i = 0; i < mats.Length && i < cols.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cols[i]);
                if (m.HasProperty("_Color"))     m.color = cols[i];
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            }
        }
        Debug.Log($"[ToolItem] RestoreColors completed for {gameObject.name}");
    }

    public static void RestoreAllToolColors()
    {
        Debug.Log($"[ToolItem] RestoreAllToolColors called. Tools in scene: {allToolsInScene.Count}");
        foreach (var tool in allToolsInScene)
        {
            if (tool != null)
            {
                // Stop any lingering reject coroutines that could re-tint after we restore
                if (tool.rejectCo != null)
                {
                    tool.StopCoroutine(tool.rejectCo);
                    tool.rejectCo = null;
                    tool.isBeingRejected = false;
                }
                tool.RestoreColors();
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild / Align Hand Attach Point")]
    public void RebuildHandAttachPoint()
    {
        EnsureInteractableAndAttachPoint();
        AutoConfigureGripForToolType();
        UnityEditor.EditorUtility.SetDirty(this);
        if (handAttachPoint != null) UnityEditor.EditorUtility.SetDirty(handAttachPoint.gameObject);
        Debug.Log($"[ToolItem] Rebuilt hand attach point for {gameObject.name}: Offset = {attachPositionOffset}, Angles = {attachEulerAngles}");
    }

    [ContextMenu("Preset: Scissors / Clamps Grip")]
    public void SetPresetScissors()
    {
        EnsureInteractableAndAttachPoint();
        string nameLower = (gameObject.name + "_" + toolID).ToLower();
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) localBounds = mf.sharedMesh.bounds;
        Vector3 ext = localBounds.extents;

        if (ext.z >= ext.x && ext.z >= ext.y)
        {
            attachPositionOffset = new Vector3(0.0f, 0.0f, localBounds.min.z + ext.z * 0.35f);
            attachEulerAngles = Vector3.zero;
        }
        else if (ext.y >= ext.x && ext.y >= ext.z)
        {
            attachPositionOffset = new Vector3(0.0f, localBounds.min.y + ext.y * 0.35f, 0.0f);
            attachEulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
        }
        else
        {
            attachPositionOffset = new Vector3(localBounds.min.x + ext.x * 0.35f, 0.0f, 0.0f);
            attachEulerAngles = new Vector3(0.0f, 90.0f, 0.0f);
        }

        if (handAttachPoint != null)
        {
            handAttachPoint.localPosition = attachPositionOffset;
            handAttachPoint.localRotation = Quaternion.Euler(attachEulerAngles);
            UnityEditor.EditorUtility.SetDirty(handAttachPoint.gameObject);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Preset: Forceps Grip")]
    public void SetPresetForceps()
    {
        EnsureInteractableAndAttachPoint();
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) localBounds = mf.sharedMesh.bounds;
        Vector3 ext = localBounds.extents;

        if (ext.y >= ext.x && ext.y >= ext.z)
        {
            attachPositionOffset = new Vector3(0.0f, localBounds.min.y + ext.y * 0.5f, 0.0f);
            attachEulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
        }
        else if (ext.z >= ext.x && ext.z >= ext.y)
        {
            attachPositionOffset = new Vector3(0.0f, 0.0f, localBounds.min.z + ext.z * 0.5f);
            attachEulerAngles = Vector3.zero;
        }
        else
        {
            attachPositionOffset = new Vector3(localBounds.min.x + ext.x * 0.5f, 0.0f, 0.0f);
            attachEulerAngles = new Vector3(0.0f, 90.0f, 0.0f);
        }

        if (handAttachPoint != null)
        {
            handAttachPoint.localPosition = attachPositionOffset;
            handAttachPoint.localRotation = Quaternion.Euler(attachEulerAngles);
            UnityEditor.EditorUtility.SetDirty(handAttachPoint.gameObject);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Preset: Basin Grip")]
    public void SetPresetBasin()
    {
        EnsureInteractableAndAttachPoint();
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) localBounds = mf.sharedMesh.bounds;

        attachPositionOffset = new Vector3(0.0f, localBounds.max.y * 0.8f, localBounds.max.z * 0.6f);
        attachEulerAngles = Vector3.zero;

        if (handAttachPoint != null)
        {
            handAttachPoint.localPosition = attachPositionOffset;
            handAttachPoint.localRotation = Quaternion.Euler(attachEulerAngles);
            UnityEditor.EditorUtility.SetDirty(handAttachPoint.gameObject);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Preset: Pitcher Handle Grip")]
    public void SetPresetPitcher()
    {
        EnsureInteractableAndAttachPoint();
        attachPositionOffset = new Vector3(-0.0255f, 0.0195f, 0.0094f);
        attachEulerAngles = new Vector3(0.0f, 90.0f, 0.0f);

        if (handAttachPoint != null)
        {
            handAttachPoint.localPosition = attachPositionOffset;
            handAttachPoint.localRotation = Quaternion.Euler(attachEulerAngles);
            UnityEditor.EditorUtility.SetDirty(handAttachPoint.gameObject);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnDrawGizmosSelected()
    {
        if (handAttachPoint != null)
        {
            Gizmos.color = new Color(0.1f, 1f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(handAttachPoint.position, 0.02f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(handAttachPoint.position, handAttachPoint.right * 0.04f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(handAttachPoint.position, handAttachPoint.up * 0.04f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(handAttachPoint.position, handAttachPoint.forward * 0.06f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(handAttachPoint.position + Vector3.up * 0.03f, $"[Hand Grip: {gameObject.name}]");
#endif
        }
    }
}