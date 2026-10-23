using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class SetupCottonAndColliders
{
    private const string MaterialsDir = "Assets/Materials";
    private const string ObjectsDir = "Assets/Objects";
    private const string DryMatPath = MaterialsDir + "/Cotton_Dry_White_Mat.mat";
    private const string PrefabPath = ObjectsDir + "/CottonBall.prefab";

    [MenuItem("Tools/Setup Non-Phasing CottonBall & Skin Colliders")]
    public static void ExecuteSetup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "MainScene")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            scene = EditorSceneManager.GetActiveScene();
        }

        // 1. Ensure Materials directory & Dry Cotton Material exist
        if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);
        if (!Directory.Exists(ObjectsDir)) Directory.CreateDirectory(ObjectsDir);

        var dryMat = AssetDatabase.LoadAssetAtPath<Material>(DryMatPath);
        if (dryMat == null)
        {
            dryMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            dryMat.name = "Cotton_Dry_White_Mat";
            dryMat.color = new Color(0.96f, 0.95f, 0.92f, 1f); // Soft medical cotton white
            if (dryMat.HasProperty("_Smoothness")) dryMat.SetFloat("_Smoothness", 0.05f); // Matte cloth finish
            AssetDatabase.CreateAsset(dryMat, DryMatPath);
            Debug.Log($"[SetupCotton] Created dry cotton material at: {DryMatPath}");
        }

        // 2. Build or Update CottonBall Prefab
        var tempCotton = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tempCotton.name = "CottonBall";
        tempCotton.tag = "Cotton";
        tempCotton.transform.localScale = new Vector3(0.028f, 0.028f, 0.028f); // Realistic ~2.8cm surgical cotton ball

        // Mesh Renderer with default material (User can drag ANY material here!)
        var mr = tempCotton.GetComponent<MeshRenderer>();
        mr.sharedMaterial = dryMat;

        // Solid Sphere Collider (Stops at skin/tables)
        var sc = tempCotton.GetComponent<SphereCollider>();
        sc.center = Vector3.zero;
        sc.radius = 0.5f;
        sc.isTrigger = false;

        // Rigidbody with ContinuousDynamic anti-tunneling
        var rb = tempCotton.AddComponent<Rigidbody>();
        rb.mass = 0.05f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // XR Grab Interactable
        var grab = tempCotton.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.interactionLayers = 512; // Layer 512 matches forceps cotton socket
        grab.trackScale = false;
        grab.velocityDamping = 1.0f;
        grab.angularVelocityDamping = 1.0f;

        // CottonState
        tempCotton.AddComponent<CottonState>();

        // Save as Prefab
        var prefab = PrefabUtility.SaveAsPrefabAsset(tempCotton, PrefabPath);
        Object.DestroyImmediate(tempCotton);
        Debug.Log($"[SetupCotton] Saved Non-Phasing CottonBall Prefab at: {PrefabPath}");

        // 3. Connect to Forceps
        var forcepsList = Object.FindObjectsByType<ForcepsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var f in forcepsList)
        {
            f.cottonPrefab = prefab;
            EditorUtility.SetDirty(f);
        }
        Debug.Log($"[SetupCotton] Updated cottonPrefab reference on {forcepsList.Length} forceps controllers.");

        // 4. Add Solid Perineal Skin Barrier to Mother so Cotton & Forceps cannot sink in
        var perinealSliders = GameObject.Find("Perineal_Stroke_Sliders");
        if (perinealSliders != null)
        {
            var barrier = perinealSliders.transform.Find("Perineal_Skin_Barrier");
            if (barrier == null)
            {
                var barrierObj = new GameObject("Perineal_Skin_Barrier");
                barrierObj.transform.SetParent(perinealSliders.transform, false);
                barrierObj.transform.localPosition = new Vector3(0f, -0.012f, 0f); // Positioned flush right under skin sliders
                barrierObj.transform.localRotation = Quaternion.identity;

                var box = barrierObj.AddComponent<BoxCollider>();
                box.size = new Vector3(0.24f, 0.04f, 0.28f); // Covers entire perineal/vulva/anus zone
                box.isTrigger = false;
                Debug.Log("[SetupCotton] Created solid Perineal_Skin_Barrier BoxCollider under Perineal_Stroke_Sliders.");
            }
        }

        // 5. Add Thigh & Pelvic Surface Colliders to Mother if missing
        var laborObj = GameObject.Find("labor");
        if (laborObj != null)
        {
            var bodyObj = laborObj.transform.Find("bodyfinal");
            if (bodyObj != null && bodyObj.GetComponent<Collider>() == null)
            {
                var pelvicCollider = bodyObj.gameObject.AddComponent<BoxCollider>();
                pelvicCollider.center = new Vector3(0f, 0.95f, 0.05f);
                pelvicCollider.size = new Vector3(0.40f, 0.35f, 0.30f); // Solid pelvis barrier
                pelvicCollider.isTrigger = false;
                Debug.Log("[SetupCotton] Added solid Pelvis BoxCollider to bodyfinal.");
            }
        }

        // 6. Spawn or Refresh a CottonBall Template GameObject on Table 3 for Easy Material Customization
        var tablesObj = GameObject.Find("------ TABLES AND TOOLS");
        var table3 = GameObject.Find("Table 3");
        var existingTemplate = GameObject.Find("CottonBall_Model_Template");
        if (existingTemplate != null) Object.DestroyImmediate(existingTemplate);

        var templateInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        templateInstance.name = "CottonBall_Model_Template";
        if (tablesObj != null)
        {
            templateInstance.transform.SetParent(tablesObj.transform, true);
        }
        else if (table3 != null)
        {
            templateInstance.transform.SetParent(table3.transform, true);
        }
        templateInstance.transform.position = new Vector3(-0.06f, 1.23f, 0.42f); // Resting right on Table 3 next to jars
        templateInstance.transform.localScale = new Vector3(0.028f, 0.028f, 0.028f);
        var tempRb = templateInstance.GetComponent<Rigidbody>();
        if (tempRb != null) tempRb.isKinematic = true; // Resting stably until grabbed
        Debug.Log("[SetupCotton] Spawned editable 'CottonBall_Model_Template' on Table 3.");

        // 7. Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[SetupCotton] Successfully configured Non-Phasing CottonBall and solid Mother Skin Colliders!");
    }
}
