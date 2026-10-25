using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Manages return socket locations for tools on Table 1 and Table 2.
/// Features:
/// - Proximity detection: shows a blue holographic ghost mesh when the tool is held nearby.
/// - Snap & Lock: releasing the tool near the socket snaps it cleanly into place (0 penalty).
/// - Allows the player to freely pick up and return tools at any time.
/// </summary>
public class ToolReturnSocket : MonoBehaviour
{
    [Header("Target Tool Binding")]
    public ToolItem targetTool;
    public string targetToolID;

    [Header("Snap Configuration")]
    public Transform snapTransform;
    public float proximityRadius = 0.22f; // ~22cm proximity detection
    public float snapRadius = 0.18f;      // ~18cm release snap radius

    [Header("Manual Snap Orientation & Position Setup")]
    [Tooltip("Position offset relative to snapTransform.")]
    public Vector3 snapPositionOffset = Vector3.zero;
    [Tooltip("Euler rotation in degrees applied to the tool on snap.")]
    public Vector3 snapEulerAngles = Vector3.zero;
    [Tooltip("If true, uses snapEulerAngles as absolute world rotation; if false, combines with snapTransform rotation.")]
    public bool useAbsoluteSnapRotation = false;

    [Header("Visual Holographic Guide")]
    public GameObject blueGhostObject;
    public Material ghostMaterial;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip snapSound;

    private bool isToolInsideProximity = false;

    private void Awake()
    {
        if (snapTransform == null) snapTransform = transform;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        LoadGhostMaterialIfNull();
    }

    private void Start()
    {
        if (targetTool != null && string.IsNullOrEmpty(targetToolID))
        {
            targetToolID = targetTool.toolID;
        }

        CreateBlueGhostMeshIfNull();
        HideGhost();
    }

    public Vector3 GetTargetSnapPosition()
    {
        Transform root = snapTransform != null ? snapTransform : transform;
        return root.position + root.TransformDirection(snapPositionOffset);
    }

    public Quaternion GetTargetSnapRotation()
    {
        if (useAbsoluteSnapRotation) return Quaternion.Euler(snapEulerAngles);
        Transform root = snapTransform != null ? snapTransform : transform;
        return root.rotation * Quaternion.Euler(snapEulerAngles);
    }

    private void LoadGhostMaterialIfNull()
    {
        if (ghostMaterial == null)
        {
#if UNITY_EDITOR
            ghostMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Inactive_Mat.mat")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Active_Mat.mat")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Drape_Ghost_Mat.mat");
#endif
        }
    }

    /// <summary>
    /// Creates a lightweight, collider-free blue holographic duplicate of the target tool.
    /// </summary>
    public void CreateBlueGhostMeshIfNull()
    {
        if (blueGhostObject != null || targetTool == null) return;

        Transform root = snapTransform != null ? snapTransform : transform;
        Transform existing = root.Find($"{targetTool.name}_ReturnGhost");
        if (existing != null)
        {
            blueGhostObject = existing.gameObject;
            return;
        }

        LoadGhostMaterialIfNull();

        // Exact clone of the target tool to maintain identical multi-child hierarchies and scale factors
        GameObject ghost = Instantiate(targetTool.gameObject, root);
        ghost.name = $"{targetTool.name}_ReturnGhost";
        ghost.transform.localPosition = snapPositionOffset;
        ghost.transform.localRotation = Quaternion.Euler(snapEulerAngles);
        ghost.transform.localScale = targetTool.transform.localScale;

        // Strip non-visual components
        var scripts = ghost.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var s in scripts) DestroyImmediate(s);
        var colliders = ghost.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) DestroyImmediate(c);
        var rigidbodies = ghost.GetComponentsInChildren<Rigidbody>(true);
        foreach (var r in rigidbodies) DestroyImmediate(r);
        var audioSources = ghost.GetComponentsInChildren<AudioSource>(true);
        foreach (var a in audioSources) DestroyImmediate(a);

        // Apply ghost holographic material to all renderers
        var sourceRenderers = ghost.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in sourceRenderers)
        {
            if (mr == null) continue;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            if (ghostMaterial != null)
            {
                Material[] ghostMats = new Material[mr.sharedMaterials.Length];
                for (int m = 0; m < ghostMats.Length; m++) ghostMats[m] = ghostMaterial;
                mr.materials = ghostMats;
            }
        }

        blueGhostObject = ghost;
        blueGhostObject.SetActive(false);
    }
    private void Update()
    {
        if (targetTool == null) return;

        // Check if target tool is currently being held by the player
        if (targetTool.IsBeingHeld)
        {
            Vector3 snapPos = GetTargetSnapPosition();
            float dist = Vector3.Distance(targetTool.transform.position, snapPos);
            if (dist <= proximityRadius)
            {
                if (!isToolInsideProximity)
                {
                    isToolInsideProximity = true;
                    ShowGhost();
                }

                // Register this socket as a nearby candidate on the tool
                targetTool.RegisterNearbyReturnSocket(this);
            }
            else
            {
                if (isToolInsideProximity)
                {
                    isToolInsideProximity = false;
                    HideGhost();
                }
                targetTool.UnregisterNearbyReturnSocket(this);
            }
        }
        else
        {
            if (isToolInsideProximity)
            {
                isToolInsideProximity = false;
                HideGhost();
            }
        }
    }

    public bool CanAcceptTool(ToolItem tool)
    {
        if (tool == null) return false;
        if (targetTool != null && tool == targetTool) return true;
        if (!string.IsNullOrEmpty(targetToolID) && targetToolID.Equals(tool.toolID, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Snaps and locks the tool into this return socket with guaranteed scale preservation.
    /// </summary>
    public void SnapTool(ToolItem tool)
    {
        if (tool == null) return;

        HideGhost();
        isToolInsideProximity = false;

        if (tool.InitialParent != null && tool.transform.parent != tool.InitialParent)
        {
            tool.transform.SetParent(tool.InitialParent, true);
        }

        Vector3 snapPos = GetTargetSnapPosition();
        Quaternion snapRot = GetTargetSnapRotation();

        tool.transform.position = snapPos;
        tool.transform.rotation = snapRot;
        tool.transform.localScale = tool.HomeScale;

        var rb = tool.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = snapPos;
            rb.rotation = snapRot;
        }

        Physics.SyncTransforms();

        if (audioSource != null && snapSound != null)
        {
            audioSource.PlayOneShot(snapSound);
        }

        tool.OnToolSafelyReturned();
        Debug.Log($"[ToolReturnSocket] Tool '{tool.toolID}' safely returned to table socket.");
    }

    public void ShowGhost()
    {
        if (blueGhostObject != null) blueGhostObject.SetActive(true);
    }

    public void HideGhost()
    {
        if (blueGhostObject != null) blueGhostObject.SetActive(false);
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Target Tool Transform As Snap Settings")]
    public void CaptureTargetToolTransform()
    {
        if (targetTool == null)
        {
            Debug.LogWarning("[ToolReturnSocket] Please assign a target ToolItem to capture its transform.");
            return;
        }

        Transform root = snapTransform != null ? snapTransform : transform;
        snapPositionOffset = root.InverseTransformPoint(targetTool.transform.position);
        snapEulerAngles = (Quaternion.Inverse(root.rotation) * targetTool.transform.rotation).eulerAngles;
        useAbsoluteSnapRotation = false;

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[ToolReturnSocket] Captured {targetTool.name}'s transform as snap settings! (Offset: {snapPositionOffset}, Angles: {snapEulerAngles})");
    }

    [ContextMenu("Preview Snap Position (Snap Tool in Editor)")]
    public void PreviewSnapToolInEditor()
    {
        if (targetTool == null) return;

        UnityEditor.Undo.RecordObject(targetTool.transform, $"Preview Snap for {name}");
        targetTool.transform.position = GetTargetSnapPosition();
        targetTool.transform.rotation = GetTargetSnapRotation();
        targetTool.transform.localScale = targetTool.HomeScale;
        UnityEditor.EditorUtility.SetDirty(targetTool.gameObject);
        Debug.Log($"[ToolReturnSocket] Placed {targetTool.name} at snap position for visual preview.");
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Vector3 snapPos = GetTargetSnapPosition();
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.4f);
        Gizmos.DrawWireSphere(snapPos, proximityRadius);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(snapPos, snapRadius);

        Quaternion snapRot = GetTargetSnapRotation();
        Gizmos.color = Color.red;
        Gizmos.DrawRay(snapPos, snapRot * Vector3.right * 0.05f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(snapPos, snapRot * Vector3.up * 0.05f);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(snapPos, snapRot * Vector3.forward * 0.08f);
    }
}
