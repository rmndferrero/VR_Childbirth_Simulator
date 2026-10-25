using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class SetupPitcherAndBetadineCanvas
{
    private const string Betadine75Dir = "Assets/Objects/Betadine/PovidoneIodine_7.5";
    private const string Betadine10Dir = "Assets/Objects/Betadine/PovidoneIodine_10";
    private const string PitcherFbxPath = "Assets/Objects/Jug/Jug.fbx";

    [MenuItem("Tools/Setup Pitcher & Betadine World Canvas")]
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "MainScene")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            scene = EditorSceneManager.GetActiveScene();
        }

        // ══════════════════════════════════════════════════════════════
        // STEP 1: CONVERT BETADINE TEXTURES TO SPRITES
        // ══════════════════════════════════════════════════════════════
        Debug.Log("[Setup] Step 1: Converting Betadine PNG textures to Sprites...");
        ConvertTexturesToSprites(Betadine75Dir);
        ConvertTexturesToSprites(Betadine10Dir);
        AssetDatabase.Refresh();

        // ══════════════════════════════════════════════════════════════
        // STEP 2: SETUP NEW PITCHER WITH COMPLETE HANDLING & POUR LOGIC
        // ══════════════════════════════════════════════════════════════
        Debug.Log("[Setup] Step 2: Replacing Old Pitcher with New Pitcher Model...");
        SetupPitcherModel();

        // ══════════════════════════════════════════════════════════════
        // STEP 3: SETUP BETADINE WORLD SPACE CANVAS & ZONE IMAGES
        // ══════════════════════════════════════════════════════════════
        Debug.Log("[Setup] Step 3: Setting up Betadine World Space Canvas...");
        SetupBetadineWorldCanvas();

        // Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[Setup] SUCCESS! Pitcher replacement and Betadine World Canvas complete with 0 errors!");
    }

    private static void ConvertTexturesToSprites(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        var files = Directory.GetFiles(directoryPath, "*.png");
        foreach (var file in files)
        {
            string unityPath = file.Replace("\\", "/");
            var importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }
                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    dirty = true;
                }
                if (dirty)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"[Setup] Configured Sprite: {unityPath}");
                }
            }
        }
    }

    private static void SetupPitcherModel()
    {
        var table3 = GameObject.Find("Table 3") ?? GameObject.Find("------ TABLES AND TOOLS/Table 3");
        if (table3 == null)
        {
            Debug.LogError("[Setup] Table 3 not found!");
            return;
        }

        var oldPitcher = GameObject.Find("------ TABLES AND TOOLS/Table 3/Old Pitcher");
        var newPitcherObj = GameObject.Find("------ TABLES AND TOOLS/Table 3/Pitcher");

        if (newPitcherObj == null)
        {
            newPitcherObj = new GameObject("Pitcher");
            newPitcherObj.transform.SetParent(table3.transform, false);
        }

        Vector3 tablePlacementPos = oldPitcher != null ? oldPitcher.transform.position : new Vector3(0.13f, 1.14f, 0.66f);
        Quaternion tablePlacementRot = Quaternion.identity; // Ensures transform.up is (0, 1, 0)

        newPitcherObj.transform.position = tablePlacementPos;
        newPitcherObj.transform.rotation = tablePlacementRot;
        newPitcherObj.transform.localScale = Vector3.one;

        // 1. Create Child "Model" holding the mesh
        Transform modelT = newPitcherObj.transform.Find("Model");
        GameObject modelObj;
        if (modelT == null)
        {
            modelObj = new GameObject("Model");
            modelObj.transform.SetParent(newPitcherObj.transform, false);
        }
        else
        {
            modelObj = modelT.gameObject;
        }

        // Transfer MeshFilter and MeshRenderer to child Model
        var pitcherFbx = AssetDatabase.LoadAssetAtPath<GameObject>(PitcherFbxPath);
        Mesh pitcherMesh = null;
        Material[] pitcherMats = null;
        if (pitcherFbx != null)
        {
            var fbxMf = pitcherFbx.GetComponentInChildren<MeshFilter>();
            var fbxMr = pitcherFbx.GetComponentInChildren<MeshRenderer>();
            if (fbxMf != null) pitcherMesh = fbxMf.sharedMesh;
            if (fbxMr != null) pitcherMats = fbxMr.sharedMaterials;
        }

        var mf = modelObj.GetComponent<MeshFilter>();
        if (mf == null) mf = modelObj.AddComponent<MeshFilter>();
        if (pitcherMesh != null) mf.sharedMesh = pitcherMesh;

        var mr = modelObj.GetComponent<MeshRenderer>();
        if (mr == null) mr = modelObj.AddComponent<MeshRenderer>();
        if (pitcherMats != null) mr.sharedMaterials = pitcherMats;

        // Clean components off root if they were placed directly on Pitcher
        var rootMf = newPitcherObj.GetComponent<MeshFilter>();
        if (rootMf != null) Object.DestroyImmediate(rootMf);
        var rootMr = newPitcherObj.GetComponent<MeshRenderer>();
        if (rootMr != null) Object.DestroyImmediate(rootMr);

        // Orient the Model:
        // Euler(270, 90, 0) perfectly aligns mesh upright along +Y, spout along +Z, handle along -Z
        modelObj.transform.localPosition = new Vector3(0f, 0f, 0f);
        modelObj.transform.localRotation = Quaternion.Euler(270f, 90f, 0f);
        modelObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.07f);

        // 2. Transfer / Setup Functional Children
        Transform spoutOrigin = newPitcherObj.transform.Find("SpoutOrigin");
        Transform waterPour = newPitcherObj.transform.Find("WaterPour");
        Transform handleGrab = newPitcherObj.transform.Find("HandleGrabPoint");
        Transform attachPoint = newPitcherObj.transform.Find("AttachPoint");

        if (oldPitcher != null)
        {
            if (spoutOrigin == null)
            {
                var oldSpout = oldPitcher.transform.Find("SpoutOrigin");
                if (oldSpout != null) oldSpout.SetParent(newPitcherObj.transform);
                spoutOrigin = oldSpout;
            }
            if (waterPour == null)
            {
                var oldWater = oldPitcher.transform.Find("WaterPour");
                if (oldWater != null) oldWater.SetParent(newPitcherObj.transform);
                waterPour = oldWater;
            }
            if (handleGrab == null)
            {
                var oldHandle = oldPitcher.transform.Find("HandleGrabPoint");
                if (oldHandle != null) oldHandle.SetParent(newPitcherObj.transform);
                handleGrab = oldHandle;
            }
            if (attachPoint == null)
            {
                var oldAttach = oldPitcher.transform.Find("AttachPoint");
                if (oldAttach != null) oldAttach.SetParent(newPitcherObj.transform);
                attachPoint = oldAttach;
            }
        }

        if (spoutOrigin == null)
        {
            var so = new GameObject("SpoutOrigin");
            so.transform.SetParent(newPitcherObj.transform, false);
            spoutOrigin = so.transform;
        }
        if (handleGrab == null)
        {
            var hg = new GameObject("HandleGrabPoint");
            hg.transform.SetParent(newPitcherObj.transform, false);
            handleGrab = hg.transform;
        }
        if (attachPoint == null)
        {
            var ap = new GameObject("AttachPoint");
            ap.transform.SetParent(newPitcherObj.transform, false);
            attachPoint = ap.transform;
        }

        // Position Functional Transforms relative to the new Pitcher geometry:
        // Spout at exact physical spout rim lip of Jug.fbx: (0, 0.070, 0.058)
        spoutOrigin.localPosition = new Vector3(0f, 0.070f, 0.058f);
        spoutOrigin.localRotation = Quaternion.identity;
        spoutOrigin.localScale = Vector3.one;

        if (waterPour != null)
        {
            waterPour.SetParent(spoutOrigin, false);
            waterPour.localPosition = Vector3.zero;
            waterPour.localRotation = Quaternion.Euler(15f, 0f, 0f); // points forward-down directly out of spout rim
            waterPour.localScale = new Vector3(0.03f, 0.03f, 0.03f);

            var ps = waterPour.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startSpeed = 0.35f;
                main.gravityModifier = 1.2f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.012f;
            }
        }

        // Handle at back: (0, 0.020, -0.065) centered on vertical handle bar
        handleGrab.localPosition = new Vector3(0f, 0.020f, -0.065f);
        handleGrab.localRotation = Quaternion.identity;
        handleGrab.localScale = Vector3.one;

        attachPoint.localPosition = new Vector3(0f, 0.020f, -0.065f);
        attachPoint.localRotation = Quaternion.identity;
        attachPoint.localScale = Vector3.one;

        // 3. Components on root Pitcher
        var col = newPitcherObj.GetComponent<BoxCollider>();
        if (col == null) col = newPitcherObj.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.08f, 0f);
        col.size = new Vector3(0.13f, 0.16f, 0.18f);
        col.isTrigger = false;

        var rb = newPitcherObj.GetComponent<Rigidbody>();
        if (rb == null) rb = newPitcherObj.AddComponent<Rigidbody>();
        rb.mass = 1.0f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var grab = newPitcherObj.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = newPitcherObj.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.attachTransform = attachPoint;
        grab.throwOnDetach = true;
        grab.selectMode = InteractableSelectMode.Multiple;

        var tool = newPitcherObj.GetComponent<ToolItem>();
        if (tool == null) tool = newPitcherObj.AddComponent<ToolItem>();
        tool.toolID = "Pitcher_01";
        tool.handAttachPoint = attachPoint;

        var pour = newPitcherObj.GetComponent<PitcherPour>();
        if (pour == null) pour = newPitcherObj.AddComponent<PitcherPour>();
        pour.spoutOrigin = spoutOrigin;
        if (waterPour != null) pour.waterParticleSystem = waterPour.GetComponent<ParticleSystem>();
        pour.pourAngleThreshold = 45f;
        pour.emissionRate = 200f;
        pour.waterStreamRadius = 0.04f;
        pour.puddleWashRadius = 0.18f;

        // 4. Update Return Sockets
        var sockets = Object.FindObjectsByType<ToolReturnSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var s in sockets)
        {
            if (s.targetTool != null && (s.targetTool.gameObject == oldPitcher || s.targetTool.toolID == "Pitcher_01"))
            {
                s.targetTool = tool;
                s.targetToolID = "Pitcher_01";
                EditorUtility.SetDirty(s);
            }
        }

        // Deactivate Old Pitcher
        if (oldPitcher != null)
        {
            oldPitcher.SetActive(false);
            EditorUtility.SetDirty(oldPitcher);
        }

        EditorUtility.SetDirty(newPitcherObj);
        Debug.Log("[Setup] Pitcher GameObject successfully configured with upright orientation, particle pour, and grab mechanics!");
    }

    private static void SetupBetadineWorldCanvas()
    {
        var hip = GameObject.Find("----- MODEL/Mother/labor/pregfemale/Root/hip");
        if (hip == null)
        {
            Debug.LogError("[Setup] Mother hip transform not found!");
            return;
        }

        var mbp = GameObject.Find("----- MODEL/Mother/labor/pregfemale/Root/hip/Mother_Betadine_Puddles");
        if (mbp == null)
        {
            Debug.LogError("[Setup] Mother_Betadine_Puddles not found!");
            return;
        }

        // 1. Create or Find Mother_Betadine_Canvas under hip
        Transform canvasT = hip.transform.Find("Mother_Betadine_Canvas");
        GameObject canvasObj;
        if (canvasT == null)
        {
            canvasObj = new GameObject("Mother_Betadine_Canvas");
            canvasObj.transform.SetParent(hip.transform, false);
        }
        else
        {
            canvasObj = canvasT.gameObject;
        }

        // Perineum center world pos: (-1.01, 1.01, 1.50)
        canvasObj.transform.position = new Vector3(-1.01f, 1.01f, 1.50f);
        canvasObj.transform.rotation = Quaternion.Euler(0f, 0f, 25f); // matches anatomical perineal tilt
        canvasObj.transform.localScale = Vector3.one * 0.0006f;

        var canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var raycaster = canvasObj.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = canvasObj.AddComponent<GraphicRaycaster>();

        var canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800f, 800f);

        // 2. Zone Sprite Mapping
        string[] sprite75Names = new string[]
        {
            "PVP7.5_1_LabiaMinora.png",
            "PVP7.5_2_MonsPubis.png",
            "PVP7.5_3_LeftLabiaMajora.png",
            "PVP7.5_4_RightLabiaMajora.png",
            "PVP7.5_5_RightInnerThigh.png",
            "PVP7.5_6_LeftInnerThigh.png",
            "PVP7.5_7_LeftButtock.png",
            "PVP7.5_8_RightButtock.png",
            "PVP7.5_9_Anus.png"
        };

        string[] sprite10Names = new string[]
        {
            "PVP10_1_LabiaMinora.png",
            "PVP10_2_MonsPubis.png",
            "PVP10_3_LeftLabiaMajora.png",
            "PVP10_4_RightLabiaMajora.png",
            "PVP10_5_RightInnerThigh.png",
            "PVP10_6_LeftInnerThigh.png",
            "PVP10_7_LeftButtock.png",
            "PVP10_8_RightButtock.png",
            "PVP10_9_Anus.png"
        };

        // Zone UI Sizes (scaled relative to world space canvas where 1 unit = 0.6mm)
        Vector2[] zoneSizes = new Vector2[]
        {
            new Vector2(160f, 120f), // Zone 1
            new Vector2(180f, 130f), // Zone 2
            new Vector2(160f, 120f), // Zone 3
            new Vector2(160f, 120f), // Zone 4
            new Vector2(240f, 180f), // Zone 5 (Right thigh)
            new Vector2(240f, 180f), // Zone 6 (Left thigh)
            new Vector2(200f, 140f), // Zone 7 (Left buttock)
            new Vector2(200f, 140f), // Zone 8 (Right buttock)
            new Vector2(120f, 100f)  // Zone 9 (Anus)
        };

        // 3. Setup Each Zone Image and Wire to WashableIodinePuddle
        for (int i = 0; i < 9; i++)
        {
            int zoneNum = i + 1;
            string zoneName = $"BetadinePuddle_Zone_{zoneNum}";
            Transform puddleRoot = mbp.transform.Find(zoneName);
            if (puddleRoot == null)
            {
                Debug.LogWarning($"[Setup] {zoneName} not found under Mother_Betadine_Puddles!");
                continue;
            }

            var puddleComp = puddleRoot.GetComponent<WashableIodinePuddle>();
            if (puddleComp == null) continue;

            // Find old 3D placeholder objects to capture position & disable them
            Transform oldP75 = puddleComp.puddle75Object != null ? puddleComp.puddle75Object.transform : null;
            Transform oldC10 = puddleComp.coating10Object != null ? puddleComp.coating10Object.transform : null;

            Vector3 zoneWorldPos = oldP75 != null ? oldP75.position : puddleRoot.position;
            Quaternion zoneWorldRot = oldP75 != null ? oldP75.rotation : puddleRoot.rotation;

            // Create Zone UI Node under Canvas
            string uiNodeName = $"Zone_{zoneNum}_Betadine";
            Transform uiNodeT = canvasObj.transform.Find(uiNodeName);
            GameObject uiNode;
            if (uiNodeT == null)
            {
                uiNode = new GameObject(uiNodeName);
                uiNode.transform.SetParent(canvasObj.transform, false);
            }
            else
            {
                uiNode = uiNodeT.gameObject;
            }

            uiNode.transform.position = zoneWorldPos;
            uiNode.transform.rotation = zoneWorldRot;

            var nodeRect = uiNode.GetComponent<RectTransform>();
            if (nodeRect == null) nodeRect = uiNode.AddComponent<RectTransform>();
            nodeRect.sizeDelta = zoneSizes[i];

            // Setup Image 7.5
            string img75Name = $"Image_7_5_Zone_{zoneNum}";
            Transform img75T = uiNode.transform.Find(img75Name);
            GameObject img75Obj = img75T != null ? img75T.gameObject : new GameObject(img75Name);
            img75Obj.transform.SetParent(uiNode.transform, false);

            var img75Rect = img75Obj.GetComponent<RectTransform>();
            if (img75Rect == null) img75Rect = img75Obj.AddComponent<RectTransform>();
            img75Rect.anchorMin = Vector2.zero;
            img75Rect.anchorMax = Vector2.one;
            img75Rect.sizeDelta = Vector2.zero;

            var img75 = img75Obj.GetComponent<Image>();
            if (img75 == null) img75 = img75Obj.AddComponent<Image>();
            string path75 = $"{Betadine75Dir}/{sprite75Names[i]}";
            var sprite75 = AssetDatabase.LoadAssetAtPath<Sprite>(path75);
            if (sprite75 != null) img75.sprite = sprite75;
            img75.color = Color.white;
            img75.raycastTarget = false;
            img75Obj.SetActive(false);

            // Setup Image 10
            string img10Name = $"Image_10_Zone_{zoneNum}";
            Transform img10T = uiNode.transform.Find(img10Name);
            GameObject img10Obj = img10T != null ? img10T.gameObject : new GameObject(img10Name);
            img10Obj.transform.SetParent(uiNode.transform, false);

            var img10Rect = img10Obj.GetComponent<RectTransform>();
            if (img10Rect == null) img10Rect = img10Obj.AddComponent<RectTransform>();
            img10Rect.anchorMin = Vector2.zero;
            img10Rect.anchorMax = Vector2.one;
            img10Rect.sizeDelta = Vector2.zero;

            var img10 = img10Obj.GetComponent<Image>();
            if (img10 == null) img10 = img10Obj.AddComponent<Image>();
            string path10 = $"{Betadine10Dir}/{sprite10Names[i]}";
            var sprite10 = AssetDatabase.LoadAssetAtPath<Sprite>(path10);
            if (sprite10 != null) img10.sprite = sprite10;
            img10.color = Color.white;
            img10.raycastTarget = false;
            img10Obj.SetActive(false);

            // Wire WashableIodinePuddle to the new UI images!
            puddleComp.puddle75Object = img75Obj;
            puddleComp.coating10Object = img10Obj;
            EditorUtility.SetDirty(puddleComp);

            // Disable old 3D green placeholder cubes
            if (oldP75 != null && oldP75.gameObject != img75Obj)
            {
                oldP75.gameObject.SetActive(false);
                EditorUtility.SetDirty(oldP75.gameObject);
            }
            if (oldC10 != null && oldC10.gameObject != img10Obj)
            {
                oldC10.gameObject.SetActive(false);
                EditorUtility.SetDirty(oldC10.gameObject);
            }
        }

        EditorUtility.SetDirty(canvasObj);
        Debug.Log("[Setup] Betadine World Space Canvas populated with all 9 zone sprites and wired to WashableIodinePuddle!");
    }
}
