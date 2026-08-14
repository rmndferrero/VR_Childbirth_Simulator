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

    // Visual feedback
    private Renderer[] allRenderers;
    private Dictionary<Renderer, Color[]> savedColors = new Dictionary<Renderer, Color[]>();

    // Table 1 home position + scale
    private Vector3 homePos;
    private Quaternion homeRot;
    private Vector3 homeScale;

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

        allRenderers = GetComponentsInChildren<Renderer>();
        foreach (var r in allRenderers)
        {
            if (r == null || r.materials == null) continue;
            var cols = new Color[r.materials.Length];
            for (int i = 0; i < r.materials.Length; i++)
            {
                var m = r.materials[i];
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

        // Enable physics movement while held by player hand
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        if (VRDemoGameManager.Instance != null)
            VRDemoGameManager.Instance.CheckHeldToolHazard(toolID);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Immediately lock kinematic on release so tool cannot roll, drift, or fall
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (isBeingRejected || isLockedOnTable2) return;

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

    private IEnumerator DropTimerRoutine()
    {
        yield return new WaitForSeconds(1.2f);
        if (grab != null && !grab.isSelected && !isLockedOnTable2 && !isBeingRejected)
            WarpHome();
        dropCo = null;
    }

    // ── Correct Placement ──

    public void MarkCorrect(Vector3 slotPos, Quaternion slotRot)
    {
        CancelDropTimer();
        isLockedOnTable2 = true;
        isBeingRejected = false;

        // Lock kinematic on Table 2
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

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

        // 3. Disable grab interactable so socket CANNOT re-select after exit
        if (grab != null)
            grab.enabled = false;

        // 4. Force exit from all interactors (socket + any hand controllers)
        if (grab != null)
        {
            var mgr = (socket != null) ? socket.interactionManager : grab.interactionManager;
            if (mgr != null)
            {
                var holders = new List<IXRSelectInteractor>(grab.interactorsSelecting);
                foreach (var h in holders)
                    mgr.SelectExit(h, grab);
            }
        }

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
    public void SaveTable2Origin(Vector3 p, Quaternion r) { isLockedOnTable2 = true; }
    public void ResetOriginToTable1() { isLockedOnTable2 = false; }

    // ── Color helpers ──

    private void Tint(Color c)
    {
        if (allRenderers == null) return;
        foreach (var r in allRenderers)
        {
            if (r == null || r.materials == null) continue;
            foreach (var m in r.materials)
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
            if (r == null || r.materials == null) continue;
            if (!savedColors.TryGetValue(r, out var cols)) continue;
            for (int i = 0; i < r.materials.Length && i < cols.Length; i++)
            {
                var m = r.materials[i];
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cols[i]);
                if (m.HasProperty("_Color"))     m.color = cols[i];
            }
        }
    }
}