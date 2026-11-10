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
            "• Under 'Button Highlight Anchors (Move to Adjust Glow)', drag/rotate/scale any anchor to position the cyan button glow.\n" +
            "• Under 'Target Ring Positions', drag any RingPos_* object (or TargetObjectHolographicRing) to set where that step's guide appears.\n" +
            "• All UI panels and texts in the Hierarchy are directly editable without runtime overwrites.",
            MessageType.Info
        );

        // ── 1. Select & Edit Button Highlight Glow Anchors ──
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Adjust Button Highlight Glow in Scene View:", EditorStyles.boldLabel);

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

        // ── 2. Scene View Visibility & Direct Selection for All Step UIs ──
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("All Step UIs in Hierarchy (Click to Select & Move in Scene):", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("👁 Show ALL Step UIs in Scene", GUILayout.Height(26)))
        {
            tm.SetAllEditableUIsVisibleInEditor(true);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("👁 Show Only Step 1 UI", GUILayout.Height(26)))
        {
            tm.SetAllEditableUIsVisibleInEditor(false);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1. Pitcher UI", GUILayout.Height(24)))
            SelectStepUI(tm.step1PitcherGuideUI);
        if (GUILayout.Button("2. Click Button UI", GUILayout.Height(24)))
            SelectStepUI(tm.step2SampleButtonGuideUI);
        if (GUILayout.Button("2B. Sample Panel", GUILayout.Height(24)))
            SelectStepUI(tm.sampleButtonPanel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("3A. Pickup Forceps UI", GUILayout.Height(24)))
            SelectStepUI(tm.step3APickupForcepsGuideUI);
        if (GUILayout.Button("3B. Iodine Jar UI", GUILayout.Height(24)))
            SelectStepUI(tm.step3BIodineJarGuideUI);
        if (GUILayout.Button("3C. Handling Forceps UI", GUILayout.Height(24)))
            SelectStepUI(tm.step3CHandlingForcepsGuideUI);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("3D. Transfer Cotton UI", GUILayout.Height(24)))
            SelectStepUI(tm.step3DTransferCottonGuideUI);
        if (GUILayout.Button("3E. Trash Basin UI", GUILayout.Height(24)))
            SelectStepUI(tm.step3ETrashBasinGuideUI);
        if (GUILayout.Button("4. Completion Card", GUILayout.Height(24)))
            SelectStepUI(tm.completionCard);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        DrawDefaultInspector();
    }

    private void SelectStepUI(GameObject uiObj)
    {
        if (uiObj == null) return;
        if (!uiObj.activeSelf) uiObj.SetActive(true);
        Selection.activeGameObject = uiObj;
        EditorGUIUtility.PingObject(uiObj);
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
