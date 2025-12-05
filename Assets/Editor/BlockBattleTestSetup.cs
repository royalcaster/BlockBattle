using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor script to set up a test environment for BlockBattle with XR Device Simulator and test blocks.
/// </summary>
public static class BlockBattleTestSetup
{
    /// <summary>
    /// Sets up the test environment with XR Device Simulator and spawns test blocks.
    /// </summary>
    [MenuItem("BlockBattle/Setup Test Environment")]
    public static void SetupTestEnvironment()
    {
        // Load BlockBattleScene
        string scenePath = "Assets/Scenes/BlockBattleScene.unity";
        Scene scene;
        
        if (System.IO.File.Exists(scenePath))
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }
        else
        {
            Debug.LogError($"BlockBattleScene not found at {scenePath}. Please run 'BlockBattle > Setup Phase 1 Scene' first.");
            return;
        }
        
        // Add XR Device Simulator if not present
        GameObject existingSimulator = GameObject.Find("XR Device Simulator");
        if (existingSimulator == null)
        {
            string simulatorPath = "Assets/Samples/XR Interaction Toolkit/3.2.0/XR Device Simulator/XR Device Simulator.prefab";
            GameObject simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(simulatorPath);
            
            if (simulatorPrefab != null)
            {
                GameObject simulator = PrefabUtility.InstantiatePrefab(simulatorPrefab) as GameObject;
                simulator.name = "XR Device Simulator";
                Debug.Log("Added XR Device Simulator to scene");
            }
            else
            {
                Debug.LogWarning($"Could not find XR Device Simulator prefab at {simulatorPath}");
            }
        }
        else
        {
            Debug.Log("XR Device Simulator already exists in scene");
        }
        
        // Spawn test blocks on the table
        SpawnTestBlocks();
        
        // Mark scene as dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("Test environment setup complete! Press Play and use the XR Device Simulator UI to interact with blocks.");
    }
    
    /// <summary>
    /// Spawns test blocks on the table for testing.
    /// </summary>
    private static void SpawnTestBlocks()
    {
        GameObject table = GameObject.Find("Table");
        if (table == null)
        {
            Debug.LogWarning("Table not found in scene. Blocks will be spawned at origin.");
        }
        
        Vector3 spawnPosition = table != null ? table.transform.position + Vector3.up * 0.2f : Vector3.up * 1f;
        
        // Spawn a few blocks of each type
        string[] blockTypes = { "Block_Cube", "Block_Cylinder", "Block_Triangle" };
        string blocksPath = "Assets/BlockBattle/Prefabs/Blocks";
        
        float spacing = 0.15f; // 15cm spacing between blocks
        int blockIndex = 0;
        
        foreach (string blockType in blockTypes)
        {
            string prefabPath = $"{blocksPath}/{blockType}.prefab";
            GameObject blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (blockPrefab != null)
            {
                // Spawn 2 of each type
                for (int i = 0; i < 2; i++)
                {
                    Vector3 position = spawnPosition + Vector3.right * (blockIndex * spacing);
                    GameObject block = PrefabUtility.InstantiatePrefab(blockPrefab) as GameObject;
                    block.transform.position = position;
                    block.name = $"{blockType}_Test_{i + 1}";
                    blockIndex++;
                }
                
                Debug.Log($"Spawned test blocks: {blockType}");
            }
            else
            {
                Debug.LogWarning($"Could not find block prefab at {prefabPath}. Please run 'BlockBattle > Create Block Prefabs' first.");
            }
        }
    }
}




