using UnityEngine;

/// <summary>
/// Attached to the Perineal_Wash_Zone GameObject in the scene hierarchy so its BoxCollider
/// is always clearly visible in Scene View and easy to move/resize in the Inspector.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class PerinealWashZoneGizmo : MonoBehaviour
{
    public Color idleFillColor = new Color(0.0f, 0.85f, 1.0f, 0.16f);
    public Color idleWireColor = new Color(0.0f, 0.90f, 1.0f, 0.90f);
    public Color selectedFillColor = new Color(0.15f, 1.0f, 0.45f, 0.28f);
    public Color selectedWireColor = new Color(0.20f, 1.0f, 0.50f, 1.0f);

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null && !box.isTrigger)
        {
            box.isTrigger = true;
        }
    }

    private void OnDrawGizmos()
    {
        DrawColliderGizmo(idleFillColor, idleWireColor);
    }

    private void OnDrawGizmosSelected()
    {
        DrawColliderGizmo(selectedFillColor, selectedWireColor);
    }

    private void DrawColliderGizmo(Color fill, Color wire)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null || !box.enabled) return;

        Matrix4x4 prevMatrix = Gizmos.matrix;
        Color prevColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = fill;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = wire;
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = prevMatrix;
        Gizmos.color = prevColor;
    }
}
