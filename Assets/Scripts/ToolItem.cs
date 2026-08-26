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

        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    private void Start()
    {
        homePos = transform.position;
        homeRot = transform.rotation;
        homeScale = transform.localScale;

        // Lock kinematic while resting on table so player physical collisions cannot push or knock over tools
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        IgnoreCollisionsWithOtherToolsAndPlayer();
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
        CharacterController[] playerControllers = FindObjectsOfType<CharacterController>();
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

        if (VRDemoGameManager.Instance != null)
            VRDemoGameManager.Instance.CheckHeldToolHazard(toolID);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (isBeingRejected) return;

        // Enable physics gravity so the tool falls naturally when dropped in mid-air
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
    /// Lets the tool drop/fall under gravity for 1.2s, then snaps it back to its current table if unheld.
    /// </summary>
    private IEnumerator DropTimerRoutine()
    {
        yield return new WaitForSeconds(1.2f);
        if (grab != null && !grab.isSelected && !isBeingRejected)
        {
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

        // Lock kinematic on Table 2 so it won't move when player gets near
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

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
            StopCoroutine(rejectCo);

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
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
}