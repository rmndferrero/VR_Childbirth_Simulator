using UnityEngine;

/// <summary>
/// Represents one of the tool placement slots on Table 2 (Mayo stand).
/// Displays a uniform small blue translucent circular plate ghost (NO tool-specific hints).
/// Snaps and locks correct tools; rejects wrong tools.
/// Includes full manual position & rotation setup (Scene gizmos, Euler offsets, and 1-click Transform capture).
/// </summary>
public class TablePrepSlot : MonoBehaviour
{
    [Header("Slot Identification")]
    [Tooltip("Slot index (1 to 6).")]
    public int slotNumber = 1;
    public string slotLabel = "Slot 1";

    [Header("Assigned Tool (Configure in Inspector)")]
    [Tooltip("Drag the ToolItem that belongs in this slot here.")]
    public ToolItem assignedTool;
    [Tooltip("Alternatively, specify the toolID string (e.g., 'Mayo_Scissors', 'Kelly_Clamp_1').")]
    public string assignedToolID;

    [Header("Snap Configuration")]
    public Transform snapTransform;
    [Tooltip("Optional dedicated child transform that can be freely moved and rotated in Scene View.")]
    public Transform customSnapPoint;
    public float snapRadius = 0.18f;
    public float proximityRadius = 0.25f;

    [Header("Manual Snap Orientation & Position Setup")]
    [Tooltip("Position offset relative to snapTransform.")]
    public Vector3 snapPositionOffset = Vector3.zero;
    [Tooltip("Euler rotation in degrees applied to the tool so it lies flat on the tray (e.g., 90, 0, 0 or 0, 90, 0).")]
    public Vector3 snapEulerAngles = Vector3.zero;
    [Tooltip("If true, uses snapEulerAngles as absolute world rotation; if false, combines with snapTransform rotation.")]
    public bool useAbsoluteSnapRotation = false;

    [Header("Visual Circle Plate Ghost (No Hint)")]
    public GameObject ghostPlateObject;
    public Material ghostMaterial;
    [Tooltip("Scale of the circular holographic plate (X/Z diameter, Y thickness).")]
    public Vector3 ghostPlateSize = new Vector3(0.09f, 0.002f, 0.09f);

    [Header("State")]
    public bool isFilled = false;
    public ToolItem currentPlacedTool = null;

    private void Awake()
    {
        if (snapTransform == null) snapTransform = transform;
        CreateGhostPlateIfNull();
    }

    private void Start()
    {
        if (assignedTool != null && string.IsNullOrEmpty(assignedToolID))
        {
            assignedToolID = assignedTool.toolID;
        }

        CreateGhostPlateIfNull();
        HideGhost();
    }

    public Vector3 GetTargetSnapPosition()
    {
        if (customSnapPoint != null) return customSnapPoint.position;
        Transform root = snapTransform != null ? snapTransform : transform;
        return root.position + root.TransformDirection(snapPositionOffset);
    }

    public Quaternion GetTargetSnapRotation()
    {
        if (customSnapPoint != null) return customSnapPoint.rotation;
        if (useAbsoluteSnapRotation) return Quaternion.Euler(snapEulerAngles);
        Transform root = snapTransform != null ? snapTransform : transform;
        return root.rotation * Quaternion.Euler(snapEulerAngles);
    }

    public void CreateGhostPlateIfNull()
    {
        if (ghostPlateObject != null) return;

        Transform root = snapTransform != null ? snapTransform : transform;
        Transform existing = root.Find($"Slot_{slotNumber}_GhostPlate");
        if (existing != null)
        {
            ghostPlateObject = existing.gameObject;
            return;
        }

        LoadGhostMaterialIfNull();

        // Check if there was an old box ghost and clean it up
        Transform oldBox = root.Find($"Slot_{slotNumber}_GhostBox");
        if (oldBox != null)
        {
            if (Application.isPlaying) Destroy(oldBox.gameObject);
            else DestroyImmediate(oldBox.gameObject);
        }

        // Create a flat circular disc / plate (Cylinder primitive with flat thickness)
        ghostPlateObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ghostPlateObject.name = $"Slot_{slotNumber}_GhostPlate";
        ghostPlateObject.transform.SetParent(root, false);
        ghostPlateObject.transform.localPosition = Vector3.zero;
        ghostPlateObject.transform.localRotation = Quaternion.identity;
        ghostPlateObject.transform.localScale = ghostPlateSize;

        // Remove collider so it does not block VR physics / raycasts
        var col = ghostPlateObject.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        var mr = ghostPlateObject.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (ghostMaterial != null) mr.material = ghostMaterial;
        }

        ghostPlateObject.SetActive(false);
    }

    private void LoadGhostMaterialIfNull()
    {
        if (ghostMaterial == null)
        {
#if UNITY_EDITOR
            ghostMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Active_Mat.mat")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Inactive_Mat.mat")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Drape_Ghost_Mat.mat");
#endif
        }
    }

    public bool CanAccept(ToolItem tool)
    {
        if (tool == null) return false;
        if (assignedTool != null && tool == assignedTool) return true;
        if (!string.IsNullOrEmpty(assignedToolID) && assignedToolID.Equals(tool.toolID, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public void ShowGhost()
    {
        if (!isFilled && ghostPlateObject != null)
        {
            ghostPlateObject.SetActive(true);
        }
    }

    public void HideGhost()
    {
        if (ghostPlateObject != null)
        {
            ghostPlateObject.SetActive(false);
        }
    }

    public void SnapCorrectTool(ToolItem tool)
    {
        if (tool == null) return;

        isFilled = true;
        currentPlacedTool = tool;
        HideGhost();

        Vector3 snapPos = GetTargetSnapPosition();
        Quaternion snapRot = GetTargetSnapRotation();

        tool.transform.position = snapPos;
        tool.transform.rotation = snapRot;
        tool.transform.localScale = tool.HomeScale;

        tool.MarkCorrect(snapPos, snapRot);

        Debug.Log($"[TablePrepSlot] Slot {slotNumber} ({slotLabel}) successfully locked correct tool: {tool.toolID}");
    }

    public void RejectWrongTool(ToolItem tool)
    {
        if (tool == null) return;

        Debug.LogWarning($"[TablePrepSlot] Slot {slotNumber} rejected incorrect tool: {tool.toolID}. Expected: {assignedToolID}");
        tool.HandleWrongPlacement(null, 1.0f);
    }

    public void ClearSlot()
    {
        isFilled = false;
        currentPlacedTool = null;
        ShowGhost();
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Assigned Tool Transform As Snap Settings")]
    public void CaptureAssignedToolTransform()
    {
        if (assignedTool == null)
        {
            Debug.LogWarning($"[TablePrepSlot] Slot {slotNumber}: Please assign a ToolItem to capture its transform.");
            return;
        }

        Transform root = snapTransform != null ? snapTransform : transform;
        snapPositionOffset = root.InverseTransformPoint(assignedTool.transform.position);
        snapEulerAngles = (Quaternion.Inverse(root.rotation) * assignedTool.transform.rotation).eulerAngles;
        useAbsoluteSnapRotation = false;

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[TablePrepSlot] Slot {slotNumber}: Successfully captured {assignedTool.name}'s transform as snap settings! (Offset: {snapPositionOffset}, Angles: {snapEulerAngles})");
    }

    [ContextMenu("Preview Snap Position (Snap Tool in Editor)")]
    public void PreviewSnapToolInEditor()
    {
        if (assignedTool == null) return;

        UnityEditor.Undo.RecordObject(assignedTool.transform, $"Preview Snap for Slot {slotNumber}");
        assignedTool.transform.position = GetTargetSnapPosition();
        assignedTool.transform.rotation = GetTargetSnapRotation();
        assignedTool.transform.localScale = assignedTool.HomeScale;
        UnityEditor.EditorUtility.SetDirty(assignedTool.gameObject);
        Debug.Log($"[TablePrepSlot] Slot {slotNumber}: Placed {assignedTool.name} at snap position for visual preview.");
    }

    [ContextMenu("Create SnapPoint Child")]
    public void CreateSnapPointChild()
    {
        if (customSnapPoint != null) return;

        GameObject sp = new GameObject($"Slot_{slotNumber}_SnapPoint");
        sp.transform.SetParent(transform, false);
        sp.transform.localPosition = snapPositionOffset;
        sp.transform.localRotation = Quaternion.Euler(snapEulerAngles);
        customSnapPoint = sp.transform;

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[TablePrepSlot] Created SnapPoint child for Slot {slotNumber}. You can now move and rotate it freely with Scene gizmos.");
    }
#endif

    private void OnDrawGizmos()
    {
        Vector3 pos = snapTransform != null ? snapTransform.position : transform.position;
        Gizmos.color = isFilled ? new Color(0.2f, 1f, 0.4f, 0.6f) : new Color(0.15f, 0.85f, 1f, 0.7f);
        Gizmos.DrawWireSphere(pos, ghostPlateSize.x * 0.5f);

        Vector3 snapPos = GetTargetSnapPosition();
        Quaternion snapRot = GetTargetSnapRotation();

        // Draw coordinate axes showing tool orientation
        Gizmos.color = Color.red;
        Gizmos.DrawRay(snapPos, snapRot * Vector3.right * 0.05f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(snapPos, snapRot * Vector3.up * 0.05f);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(snapPos, snapRot * Vector3.forward * 0.08f);

#if UNITY_EDITOR
        string label = $"[Slot {slotNumber}] {(assignedTool != null ? assignedTool.name : (string.IsNullOrEmpty(assignedToolID) ? "Unassigned" : assignedToolID))}";
        UnityEditor.Handles.Label(pos + Vector3.up * 0.04f, label);
#endif
    }
}
