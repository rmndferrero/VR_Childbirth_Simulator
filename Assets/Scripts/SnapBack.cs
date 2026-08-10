using UnityEngine;

/// <summary>
/// Thin wrapper that delegates to ToolItem. Kept for scene compatibility
/// (existing GameObjects already have this component attached).
/// </summary>
public class ToolSnapback : MonoBehaviour
{
    private ToolItem tool;

    private void Awake()
    {
        tool = GetComponent<ToolItem>()
            ?? GetComponentInParent<ToolItem>()
            ?? GetComponentInChildren<ToolItem>();
    }

    public void SaveOrigin(Vector3 p, Quaternion r) { }
    public void SaveTable2Origin(Vector3 p, Quaternion r) { if (tool) tool.SaveTable2Origin(p, r); }
    public void ResetToTable1() { if (tool) tool.ResetOriginToTable1(); }
    public void SnapBack() { if (tool) tool.TeleportToTable1(); }
}