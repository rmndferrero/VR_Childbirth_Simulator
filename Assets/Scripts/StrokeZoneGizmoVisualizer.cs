using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Scene View Gizmo Visualizer and Layout Helper for the 9 Stroke Zones.
/// Draws numbered badges (1 to 9), anatomical labels, and clean translucent bounds in the Scene View.
/// Provides a 1-click Reset Layout button in the Inspector to snap all 9 zones to perfect positions.
/// </summary>
[ExecuteInEditMode]
public class StrokeZoneGizmoVisualizer : MonoBehaviour
{
    [Header("Gizmo Display Settings")]
    public bool showGizmos = true;
    public bool showLabels = true;

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        var triggers = GetComponentsInChildren<StrokeZoneTrigger>(true);
        if (triggers == null || triggers.Length == 0) return;

        for (int i = 0; i < triggers.Length; i++)
        {
            var trig = triggers[i];
            if (trig == null) continue;

            int order = (trig.zoneDefinition != null) ? trig.zoneDefinition.expectedOrderIndex + 1 : (i + 1);
            string zoneName = (trig.zoneDefinition != null) ? trig.zoneDefinition.zoneName : trig.gameObject.name;

            BoxCollider box = trig.GetComponent<BoxCollider>();
            Gizmos.matrix = trig.transform.localToWorldMatrix;

            // Draw clean translucent box
            Gizmos.color = new Color(0.15f, 0.75f, 1.0f, 0.25f);
            Vector3 size = box != null ? box.size : new Vector3(0.04f, 0.08f, 0.04f);
            Vector3 center = box != null ? box.center : Vector3.zero;
            Gizmos.DrawCube(center, size);

            // Draw crisp wireframe border
            Gizmos.color = new Color(0.2f, 0.85f, 1.0f, 0.8f);
            Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
            if (showLabels)
            {
                Handles.matrix = Matrix4x4.identity;
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = Color.white;
                labelStyle.fontSize = 11;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.alignment = TextAnchor.MiddleCenter;

                Vector3 worldCenter = trig.transform.TransformPoint(center);
                Handles.Label(worldCenter + Vector3.up * 0.02f, $"[{order}] {zoneName}", labelStyle);
            }
#endif
        }
    }

    /// <summary>
    /// Snaps all 9 stroke zone GameObjects to anatomically calibrated positions flush with the mother's lithotomy pose.
    /// </summary>
    [ContextMenu("Reset All 9 Zones to Default Anatomical Layout")]
    public void ResetAllZonesToDefaultLayout()
    {
        var triggers = GetComponentsInChildren<StrokeZoneTrigger>(true);
        if (triggers == null) return;

        foreach (var trig in triggers)
        {
            if (trig == null || trig.zoneDefinition == null) continue;

            int index = trig.zoneDefinition.expectedOrderIndex;
            var t = trig.transform;
            var box = trig.GetComponent<BoxCollider>();
            if (box == null) box = trig.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;

            t.localScale = Vector3.one;

            switch (index)
            {
                case 0: // 1. Right Labia Majora
                    t.localPosition = new Vector3(-0.0260f, -0.0450f, -0.1940f);
                    t.localEulerAngles = new Vector3(0.08f, 270.00f, 344.71f);
                    box.size = new Vector3(0.035f, 0.080f, 0.035f);
                    break;

                case 1: // 2. Mons Pubis (Horizontal band above genitals)
                    t.localPosition = new Vector3(0.0000f, 0.0289f, -0.2193f);
                    t.localEulerAngles = new Vector3(0.40f, 270.10f, 13.95f);
                    box.size = new Vector3(0.035f, 0.070f, 0.110f);
                    break;

                case 2: // 3. Left Labia Majora (Equal to Right Labia Majora)
                    t.localPosition = new Vector3(0.0260f, -0.0450f, -0.1940f);
                    t.localEulerAngles = new Vector3(356.27f, 271.53f, 343.98f);
                    box.size = new Vector3(0.035f, 0.080f, 0.035f);
                    break;

                case 3: // 4. Right Labia Minora (Inner fold)
                    t.localPosition = new Vector3(-0.0120f, -0.0480f, -0.1920f);
                    t.localEulerAngles = new Vector3(8.16f, 282.62f, 352.39f);
                    box.size = new Vector3(0.030f, 0.070f, 0.030f);
                    break;

                case 4: // 5. Right Inner Thigh (Slanted along thigh)
                    t.localPosition = new Vector3(-0.1450f, 0.0700f, -0.1250f);
                    t.localEulerAngles = new Vector3(47.62f, 301.51f, 354.50f);
                    box.size = new Vector3(0.065f, 0.170f, 0.075f);
                    break;

                case 5: // 6. Left Inner Thigh (Equal to Right Inner Thigh)
                    t.localPosition = new Vector3(0.1450f, 0.0700f, -0.1250f);
                    t.localEulerAngles = new Vector3(346.17f, 317.00f, 314.87f);
                    box.size = new Vector3(0.065f, 0.170f, 0.075f);
                    break;

                case 6: // 7. Left Buttock (Gluteal fold)
                    t.localPosition = new Vector3(0.0800f, -0.1180f, -0.2010f);
                    t.localEulerAngles = new Vector3(30.33f, 246.80f, 326.43f);
                    box.size = new Vector3(0.045f, 0.065f, 0.110f);
                    break;

                case 7: // 8. Right Buttock (Equal to Left Buttock)
                    t.localPosition = new Vector3(-0.0800f, -0.1180f, -0.2010f);
                    t.localEulerAngles = new Vector3(328.58f, 296.88f, 322.46f);
                    box.size = new Vector3(0.045f, 0.065f, 0.110f);
                    break;

                case 8: // 9. Anus (Direct dab target)
                    t.localPosition = new Vector3(0.0000f, -0.1046f, -0.2126f);
                    t.localEulerAngles = new Vector3(20.11f, 261.87f, 326.69f);
                    box.size = new Vector3(0.035f, 0.035f, 0.035f);
                    break;
            }
        }

        Debug.Log("[StrokeZoneGizmoVisualizer] All 9 Stroke Zones snapped to equal symmetrical layout!");
    }
}
