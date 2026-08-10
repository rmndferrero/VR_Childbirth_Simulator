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

    private void Awake()
    {
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
    }

    private void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }

    // ── XR Events ──

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // NEVER cancel a running rejection — only cancel the mid-air drop timer
        if (!isBeingRejected)
        {
            CancelDropTimer();
        }

        if (VRDemoGameManager.Instance != null)
            VRDemoGameManager.Instance.CheckHeldToolHazard(toolID);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Don't interfere with rejection or if already on Table 2
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

        // 8. Wait a frame then force scale again — XRI may override scale on re-enable
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
        }

        Physics.SyncTransforms();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
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