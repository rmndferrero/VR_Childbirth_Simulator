#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HolographicTutorialGuide))]
public class HolographicTutorialGuideEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var guide = (HolographicTutorialGuide)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "EDIT BUTTON HIGHLIGHT GLOW IN SCENE VIEW:\n" +
            "• Under 'Button Highlight Anchors (Move to Adjust Glow)' in the Hierarchy, each button has its own movable 3D anchor.\n" +
            "• Click any button below to focus the Scene View directly on that anchor.\n" +
            "• Use the Move (W), Rotate (E), and Scale (R) tools in the Scene View to position the glow exactly on the controller button surface in real time!",
            MessageType.Info
        );

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Quick Glow Focus & Adjustment:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎯 Select & Edit GRIP Glow", GUILayout.Height(28)))
        {
            SelectAnchor(guide, HolographicButtonType.Grip);
        }
        if (GUILayout.Button("🎯 Select & Edit TRIGGER Glow", GUILayout.Height(28)))
        {
            SelectAnchor(guide, HolographicButtonType.Trigger);
        }
        if (GUILayout.Button("🎯 Select & Edit BUTTON A Glow", GUILayout.Height(28)))
        {
            SelectAnchor(guide, HolographicButtonType.ButtonA);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        DrawDefaultInspector();
    }

    private void SelectAnchor(HolographicTutorialGuide guide, HolographicButtonType btnType)
    {
        if (guide == null) return;

        Undo.RecordObject(guide, "Select Button Glow");
        guide.currentButton = btnType;
        guide.EnsureGlowAnchorsExist();
        guide.PositionActiveButtonHighlight();

        Transform targetAnchor = guide.GetCurrentButtonAnchor();
        if (targetAnchor != null)
        {
            Selection.activeTransform = targetAnchor;
            EditorGUIUtility.PingObject(targetAnchor);
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        SceneView.RepaintAll();
    }
}
#endif
