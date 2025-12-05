using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Editor script to set up Phase 1 of BlockBattle: XR Rig, floor, and table.
/// </summary>
public static class BlockBattleSceneSetup
{
    [MenuItem("BlockBattle/Setup Phase 1 Scene")]
    public static void SetupPhase1Scene()
    {
        // Create or load BlockBattleScene
        string scenePath = "Assets/Scenes/BlockBattleScene.unity";
        Scene scene;
        
        if (System.IO.File.Exists(scenePath))
        {
            scene = EditorSceneManager.OpenScene(scenePath);
            Debug.Log("Opened existing BlockBattleScene");
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Remove the default Main Camera since XR Origin has its own camera
            GameObject defaultCamera = GameObject.Find("Main Camera");
            if (defaultCamera != null)
            {
                Object.DestroyImmediate(defaultCamera);
            }
            
            // Ensure Directional Light is properly set up (Unity sometimes has reference issues)
            GameObject directionalLight = GameObject.Find("Directional Light");
            if (directionalLight != null)
            {
                Light lightComponent = directionalLight.GetComponent<Light>();
                if (lightComponent == null)
                {
                    lightComponent = directionalLight.AddComponent<Light>();
                    lightComponent.type = LightType.Directional;
                    lightComponent.color = new Color(1f, 0.95686275f, 0.8392157f);
                    lightComponent.intensity = 1f;
                    lightComponent.shadows = LightShadows.Soft;
                }
            }
            
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("Created new BlockBattleScene");
        }
        
        // Check if XR Origin already exists
        GameObject existingXROrigin = GameObject.Find("XR Origin (XR Rig)");
        if (existingXROrigin != null)
        {
            Debug.LogWarning("XR Origin (XR Rig) already exists in the scene. Skipping setup.");
            return;
        }
        
        // Ensure XR Interaction Manager exists
        XRInteractionManager interactionManager = Object.FindObjectOfType<XRInteractionManager>();
        if (interactionManager == null)
        {
            GameObject interactionManagerObj = new GameObject("XR Interaction Manager");
            interactionManager = interactionManagerObj.AddComponent<XRInteractionManager>();
            Debug.Log("Created XR Interaction Manager");
        }
        
        // Load XR Origin prefab
        string prefabPath = "Assets/Samples/XR Interaction Toolkit/3.2.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        GameObject xrOriginPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (xrOriginPrefab == null)
        {
            Debug.LogError($"Could not find XR Origin prefab at {prefabPath}");
            return;
        }
        
        // Instantiate XR Origin
        GameObject xrOrigin = PrefabUtility.InstantiatePrefab(xrOriginPrefab) as GameObject;
        xrOrigin.name = "XR Origin (XR Rig)";
        xrOrigin.transform.position = new Vector3(0, 0, 0);
        xrOrigin.transform.rotation = Quaternion.identity;
        
        // Create Floor
        GameObject floor = GameObject.Find("Floor");
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0, 0, 0);
            floor.transform.localScale = new Vector3(10, 1, 10);
            
            // Set material to a simple gray
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            Material floorMaterial = new Material(Shader.Find("Standard"));
            floorMaterial.color = new Color(0.5f, 0.5f, 0.5f);
            floorRenderer.material = floorMaterial;
        }
        
        // Create Table
        GameObject table = GameObject.Find("Table");
        if (table == null)
        {
            table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.position = new Vector3(0, 0.75f, 2);
            table.transform.localScale = new Vector3(2, 0.1f, 1.5f);
            
            // Set material to a wooden brown color
            Renderer tableRenderer = table.GetComponent<Renderer>();
            Material tableMaterial = new Material(Shader.Find("Standard"));
            tableMaterial.color = new Color(0.6f, 0.4f, 0.2f);
            tableRenderer.material = tableMaterial;
            
            // Add Rigidbody for physics (kinematic so it doesn't move)
            Rigidbody tableRigidbody = table.AddComponent<Rigidbody>();
            tableRigidbody.isKinematic = true;
        }
        
        // Mark scene as dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        // Add XR Setup script to ensure proper configuration
        GameObject xrSetupObj = GameObject.Find("BlockBattle XR Setup");
        if (xrSetupObj == null)
        {
            xrSetupObj = new GameObject("BlockBattle XR Setup");
            xrSetupObj.AddComponent<BlockBattle.BlockBattleXRSetup>();
            Debug.Log("Created BlockBattle XR Setup component");
        }

        // Mark scene as dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("Phase 1 setup complete: XR Origin, Floor, and Table have been added to BlockBattleScene.");
    }
}

