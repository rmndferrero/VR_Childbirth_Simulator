#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialManager))]
public class TutorialManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var tm = (TutorialManager)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "SCENE VIEW VISUAL CONTROLS:\n" +
            "• Under 'Button Highlight Anchors (Move to Adjust Glow)', drag/rotate/scale any anchor to position the cyan button glow exactly where it looks best on the controller.\n" +
            "• Under 'Target Ring Positions', drag any RingPos_* object to set exactly where that step's ring appears in VR.\n" +
            "• Use the 1-click buttons below to select & edit any glow or preview ring positions in Scene View instantly.",
            MessageType.Info
        );

        // ── 1. Select & Edit Button Highlight Glow Anchors ──
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Adjust Button Highlight Glow in Scene View:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click a button below to focus the Scene View on that glow anchor. Use Move (W), Rotate (E), and Scale (R) tools to adjust the highlight on the 3D controller surface in real time!",
            MessageType.None
        );

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎯 Select & Edit GRIP Glow", GUILayout.Height(28)))
        {
            SelectButtonGlow(tm, HolographicButtonType.Grip);
        }
        if (GUILayout.Button("🎯 Select & Edit TRIGGER Glow", GUILayout.Height(28)))
        {
            SelectButtonGlow(tm, HolographicButtonType.Trigger);
        }
        if (GUILayout.Button("🎯 Select & Edit BUTTON A Glow", GUILayout.Height(28)))
        {
            SelectButtonGlow(tm, HolographicButtonType.ButtonA);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── 2. Preview Target Ring Positions ──
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Preview Ring Position in Scene View:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1. Pitcher", GUILayout.Height(26)))
        {
            SnapRingTo(tm, tm.ringPosition_Pitcher, "GRAB PITCHER");
        }
        if (GUILayout.Button("2. UI Button", GUILayout.Height(26)))
        {
            SnapRingTo(tm, tm.ringPosition_UIButton, "CLICK SAMPLE BUTTON");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("3A. Pickup Forceps", GUILayout.Height(24)))
        {
            SnapRingTo(tm, tm.ringPosition_PickupForceps, "GRAB FORCEPS");
        }
        if (GUILayout.Button("3B. Iodine Jar", GUILayout.Height(24)))
        {
            SnapRingTo(tm, tm.ringPosition_IodineJar, "DIP & HOLD [A]");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("3C. Transfer Area", GUILayout.Height(24)))
        {
            SnapRingTo(tm, tm.ringPosition_TransferCotton, "TRANSFER COTTON");
        }
        if (GUILayout.Button("3D. Trash Basin", GUILayout.Height(24)))
        {
            SnapRingTo(tm, tm.ringPosition_TrashBasin, "DROP IN BIN");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        DrawDefaultInspector();
    }

    private void SnapRingTo(TutorialManager tm, Transform anchor, string label)
    {
        if (tm == null || tm.holographicGuide == null || tm.holographicGuide.targetRingRoot == null || anchor == null)
            return;

        Undo.RecordObject(tm.holographicGuide.targetRingRoot, "Preview Ring Position");
        tm.holographicGuide.targetRingRoot.gameObject.SetActive(true);
        tm.holographicGuide.targetRingRoot.position = anchor.position;
        if (tm.holographicGuide.targetRingLabel != null)
        {
            tm.holographicGuide.targetRingLabel.text = label;
        }
        SceneView.RepaintAll();
    }

    private void SelectButtonGlow(TutorialManager tm, HolographicButtonType btnType)
    {
        if (tm == null || tm.holographicGuide == null) return;
        var guide = tm.holographicGuide;

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
