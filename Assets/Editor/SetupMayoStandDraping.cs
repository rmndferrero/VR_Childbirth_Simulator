using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupMayoStandDraping
{
    private const string GhostMatPath = "Assets/Materials/Zone_Guide_Blue_Active_Mat.mat";
    private const string GhostMatBackupPath = "Assets/Materials/Drape_Ghost_Mat.mat";
    private const string MistakeMatPath = "Assets/Materials/Zone_Guide_Red_Mistake_Mat.mat";
    private const string ChimeAudioPath = "Assets/VRTemplateAssets/Audio/Button_22_click.wav";
    private const string MistakeAudioPath = "Assets/VRTemplateAssets/Audio/Button_14_hover.wav";

    [MenuItem("Tools/Setup Mayo Stand Draping")]
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "MainScene")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            scene = EditorSceneManager.GetActiveScene();
        }

        var t2 = GameObject.Find("Table 2") ?? GameObject.Find("------ TABLES AND TOOLS/Table 2");
        if (t2 == null)
        {
            Debug.LogError("[SetupMayoStandDraping] 'Table 2' not found!");
            return;
        }

        var mayoTable = t2.transform.Find("MayoTable");
        if (mayoTable == null)
        {
            Debug.LogError("[SetupMayoStandDraping] 'MayoTable' not found under Table 2!");
            return;
        }

        var actualDrape = mayoTable.Find("MayoStand_Drape");
        if (actualDrape == null)
        {
            Debug.LogError("[SetupMayoStandDraping] 'MayoStand_Drape' not found under MayoTable!");
            return;
        }

        // 1. Ensure MayoStand_Drape is inactive by default
        actualDrape.gameObject.SetActive(false);
        EditorUtility.SetDirty(actualDrape.gameObject);

        // 2. Load Materials & Audio
        var ghostMat = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
        if (ghostMat == null) ghostMat = AssetDatabase.LoadAssetAtPath<Material>(GhostMatBackupPath);
        var mistakeMat = AssetDatabase.LoadAssetAtPath<Material>(MistakeMatPath);
        var chimeAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(ChimeAudioPath);
        var mistakeAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(MistakeAudioPath);

        // 3. Create or Update Holographic Ghost Preview
        Transform ghostT = mayoTable.Find("MayoStand_Drape_Ghost");
        GameObject ghostObj;
        if (ghostT == null)
        {
            ghostObj = new GameObject("MayoStand_Drape_Ghost");
            ghostObj.transform.SetParent(mayoTable, false);
            ghostObj.transform.localPosition = actualDrape.localPosition;
            ghostObj.transform.localRotation = actualDrape.localRotation;
            ghostObj.transform.localScale = actualDrape.localScale;
        }
        else
        {
            ghostObj = ghostT.gameObject;
        }

        var actualMf = actualDrape.GetComponent<MeshFilter>();
        var ghostMf = ghostObj.GetComponent<MeshFilter>();
        if (ghostMf == null) ghostMf = ghostObj.AddComponent<MeshFilter>();
        if (actualMf != null) ghostMf.sharedMesh = actualMf.sharedMesh;

        var ghostMr = ghostObj.GetComponent<MeshRenderer>();
        if (ghostMr == null) ghostMr = ghostObj.AddComponent<MeshRenderer>();
        if (ghostMat != null) ghostMr.sharedMaterial = ghostMat;

        ghostObj.SetActive(false);
        EditorUtility.SetDirty(ghostObj);

        // 4. Ensure BoxCollider Trigger on MayoTable
        var boxCol = mayoTable.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = mayoTable.gameObject.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        // Center: positioned slightly above the drape tray surface in MayoTable local coordinates
        boxCol.center = new Vector3(0.04f, 0.95f, 0.00f);
        boxCol.size = new Vector3(0.75f, 0.35f, 0.55f);
        EditorUtility.SetDirty(boxCol);

        // 5. Ensure MayoStandDrapeSocket Component
        var socket = mayoTable.GetComponent<MayoStandDrapeSocket>();
        if (socket == null) socket = mayoTable.gameObject.AddComponent<MayoStandDrapeSocket>();

        socket.actualDrapeObject = actualDrape.gameObject;
        socket.ghostDrapeObject = ghostObj;
        socket.ghostMaterial = ghostMat;
        socket.mistakeRedMaterial = mistakeMat;
        socket.drapePlaceAudio = chimeAudio;
        socket.drapeMistakeAudio = mistakeAudio;
        socket.isDraped = false;
        socket.isSocketActive = false;

        var audioSrc = mayoTable.GetComponent<AudioSource>();
        if (audioSrc == null) audioSrc = mayoTable.gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 1f; // 3D audio
        socket.audioSource = audioSrc;

        EditorUtility.SetDirty(socket);

        // 6. Ensure TablePrepManager starts with isMayoDraped = false
        var prepMgr = Object.FindFirstObjectByType<TablePrepManager>();
        if (prepMgr != null)
        {
            prepMgr.isMayoDraped = false;
            EditorUtility.SetDirty(prepMgr);
        }

        // 7. Ensure VRHeadsetVisualFeedback has buzzer audio configured
        var vrfb = Object.FindFirstObjectByType<VRHeadsetVisualFeedback>();
        if (vrfb != null && vrfb.warningBuzzerClip == null)
        {
            vrfb.warningBuzzerClip = mistakeAudio;
            EditorUtility.SetDirty(vrfb);
        }

        // 8. Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[SetupMayoStandDraping] Successfully configured MayoStandDrapeSocket, Ghost object, and Trigger volume on MayoTable!");
    }
}
