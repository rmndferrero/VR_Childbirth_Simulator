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

        LoadGhostMaterialIfNull();

        GameObject ghost = new GameObject($"{targetTool.name}_ReturnGhost");
        ghost.transform.SetParent(snapTransform, false);
        ghost.transform.localPosition = Vector3.zero;
        ghost.transform.localRotation = Quaternion.identity;
        ghost.transform.localScale = Vector3.one;

        // Clone mesh filters and renderers from the target tool
        var sourceRenderers = targetTool.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var sr in sourceRenderers)
        {
            if (sr == null) continue;
            var mf = sr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            GameObject ghostPart = new GameObject(sr.gameObject.name + "_GhostPart");
            ghostPart.transform.SetParent(ghost.transform, false);

            // Compute relative transform
            Vector3 relPos = targetTool.transform.InverseTransformPoint(sr.transform.position);
            Quaternion relRot = Quaternion.Inverse(targetTool.transform.rotation) * sr.transform.rotation;
            Vector3 relScale = sr.transform.localScale;

            ghostPart.transform.localPosition = relPos;
            ghostPart.transform.localRotation = relRot;
            ghostPart.transform.localScale = relScale;

            var newMf = ghostPart.AddComponent<MeshFilter>();
            newMf.sharedMesh = mf.sharedMesh;

            var newMr = ghostPart.AddComponent<MeshRenderer>();
            newMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            newMr.receiveShadows = false;

            if (ghostMaterial != null)
            {
                Material[] ghostMats = new Material[sr.sharedMaterials.Length];
                for (int m = 0; m < ghostMats.Length; m++) ghostMats[m] = ghostMaterial;
                newMr.materials = ghostMats;
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
            float dist = Vector3.Distance(targetTool.transform.position, snapTransform.position);
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
    /// Snaps and locks the tool into this return socket.
    /// </summary>
    public void SnapTool(ToolItem tool)
    {
        if (tool == null) return;

        HideGhost();
        isToolInsideProximity = false;

        var rb = tool.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        tool.transform.position = snapTransform.position;
        tool.transform.rotation = snapTransform.rotation;

        if (rb != null)
        {
            rb.position = snapTransform.position;
            rb.rotation = snapTransform.rotation;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, proximityRadius);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, snapRadius);
    }
}
