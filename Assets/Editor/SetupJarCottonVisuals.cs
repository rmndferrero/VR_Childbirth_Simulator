using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupJarCottonVisuals
{
    private const string CottonPrefabPath = "Assets/Objects/CottonBall.prefab";
    private const string Mat75Path = "Assets/Materials/Cotton_Soaked_7_5_LightGreen_Mat.mat";
    private const string Mat10Path = "Assets/Materials/Cotton_Soaked_10_DarkGreen_Mat.mat";

    [MenuItem("Tools/Setup Jar Cotton Visuals & Spawner")]
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "MainScene")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            scene = EditorSceneManager.GetActiveScene();
        }

        var cottonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CottonPrefabPath);
        var mat75 = AssetDatabase.LoadAssetAtPath<Material>(Mat75Path);
        var mat10 = AssetDatabase.LoadAssetAtPath<Material>(Mat10Path);

        // 1. Ensure Forceps Controllers reference the new CottonBall prefab
        var forceps = Object.FindObjectsByType<ForcepsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var f in forceps)
        {
            f.cottonPrefab = cottonPrefab;
            EditorUtility.SetDirty(f);
        }
        Debug.Log($"[JarCotton] ForcepsControllers verified with cottonPrefab: {cottonPrefab.name}");

        // 2. Populate Visual Cotton Inside 7.5% Iodine Jar
        var jar75 = GameObject.Find("7.5_iodine");
        if (jar75 != null)
        {
            var existingVisual = jar75.transform.Find("Visual_Cotton_Cluster");
            if (existingVisual != null) Object.DestroyImmediate(existingVisual.gameObject);

            var cluster75 = new GameObject("Visual_Cotton_Cluster");
            cluster75.transform.SetParent(jar75.transform, false);
            // Center inside jar cavity (local jar scale is ~6.5)
            cluster75.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            cluster75.transform.localRotation = Quaternion.identity;
            cluster75.transform.localScale = Vector3.one;

            // Create 3 layered visual cotton balls inside the jar
            Vector3[] localOffsets = new Vector3[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.006f, 0.005f, -0.004f),
                new Vector3(-0.005f, -0.004f, 0.003f)
            };

            for (int i = 0; i < localOffsets.Length; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"VisualCotton_{i + 1}";
                sphere.transform.SetParent(cluster75.transform, false);
                sphere.transform.localPosition = localOffsets[i];
                sphere.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f); // Scale relative to jar
                var sphereCol = sphere.GetComponent<Collider>();
                if (sphereCol != null) Object.DestroyImmediate(sphereCol); // No physics collision to prevent blocking forceps
                var mr = sphere.GetComponent<MeshRenderer>();
                if (mr != null && mat75 != null) mr.sharedMaterial = mat75;
            }

            Debug.Log("[JarCotton] Added realistic 7.5% soaked visual cotton balls inside 7.5_iodine jar.");
        }

        // 3. Populate Visual Cotton Inside 10% Iodine Jar
        var jar10 = GameObject.Find("10_iodine");
        if (jar10 != null)
        {
            var existingVisual = jar10.transform.Find("Visual_Cotton_Cluster");
            if (existingVisual != null) Object.DestroyImmediate(existingVisual.gameObject);

            var cluster10 = new GameObject("Visual_Cotton_Cluster");
            cluster10.transform.SetParent(jar10.transform, false);
            cluster10.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            cluster10.transform.localRotation = Quaternion.identity;
            cluster10.transform.localScale = Vector3.one;

            Vector3[] localOffsets = new Vector3[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.006f, 0.005f, -0.004f),
                new Vector3(-0.005f, -0.004f, 0.003f)
            };

            for (int i = 0; i < localOffsets.Length; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"VisualCotton_{i + 1}";
                sphere.transform.SetParent(cluster10.transform, false);
                sphere.transform.localPosition = localOffsets[i];
                sphere.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
                var sphereCol = sphere.GetComponent<Collider>();
                if (sphereCol != null) Object.DestroyImmediate(sphereCol);
                var mr = sphere.GetComponent<MeshRenderer>();
                if (mr != null && mat10 != null) mr.sharedMaterial = mat10;
            }

            Debug.Log("[JarCotton] Added realistic 10% soaked visual cotton balls inside 10_iodine jar.");
        }

        // 4. Verify CottonJarZone materials on SpawnTriggers
        var trigger75 = jar75?.transform.Find("SpawnTrigger")?.GetComponent<CottonJarZone>();
        if (trigger75 != null)
        {
            trigger75.antisepticType = AntisepticType.Iodine_7_5_Scrub;
            trigger75.soakedMaterial = mat75;
            EditorUtility.SetDirty(trigger75);
        }

        var trigger10 = jar10?.transform.Find("SpawnTrigger")?.GetComponent<CottonJarZone>();
        if (trigger10 != null)
        {
            trigger10.antisepticType = AntisepticType.Iodine_10_Paint;
            trigger10.soakedMaterial = mat10;
            EditorUtility.SetDirty(trigger10);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[JarCotton] Successfully linked CottonBall template & jar visual clusters for 7.5% and 10% iodine!");
    }
}
