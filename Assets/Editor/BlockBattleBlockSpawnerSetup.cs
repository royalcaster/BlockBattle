using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor script to set up the BlockSpawner component in the scene.
/// </summary>
public static class BlockBattleBlockSpawnerSetup
{
    [MenuItem("BlockBattle/Setup Block Spawner")]
    public static void SetupBlockSpawner()
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

        // Find or create BlockSpawner GameObject
        GameObject spawnerObj = GameObject.Find("BlockSpawner");
        if (spawnerObj == null)
        {
            spawnerObj = new GameObject("BlockSpawner");
            Debug.Log("Created BlockSpawner GameObject");
        }
        else
        {
            Debug.Log("Found existing BlockSpawner GameObject");
        }

        // Add BlockSpawner component if it doesn't exist
        BlockBattle.BlockSpawner blockSpawner = spawnerObj.GetComponent<BlockBattle.BlockSpawner>();
        if (blockSpawner == null)
        {
            blockSpawner = spawnerObj.AddComponent<BlockBattle.BlockSpawner>();
            Debug.Log("Added BlockSpawner component");
        }
        else
        {
            Debug.Log("BlockSpawner component already exists");
        }

        // Load block prefabs and assign them
        string blocksPath = "Assets/BlockBattle/Prefabs/Blocks";
        
        GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{blocksPath}/Block_Cube.prefab");
        GameObject cylinderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{blocksPath}/Block_Cylinder.prefab");
        GameObject trianglePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{blocksPath}/Block_Triangle.prefab");

        // Use SerializedObject to set all properties at once
        SerializedObject serializedObject = new SerializedObject(blockSpawner);

        if (cubePrefab != null)
        {
            SerializedProperty cubeProperty = serializedObject.FindProperty("m_CubeBlockPrefab");
            if (cubeProperty != null)
            {
                cubeProperty.objectReferenceValue = cubePrefab;
            }
            Debug.Log("Assigned Cube block prefab");
        }
        else
        {
            Debug.LogWarning("Could not find Block_Cube.prefab. Please run 'BlockBattle > Create Block Prefabs' first.");
        }

        if (cylinderPrefab != null)
        {
            SerializedProperty cylinderProperty = serializedObject.FindProperty("m_CylinderBlockPrefab");
            if (cylinderProperty != null)
            {
                cylinderProperty.objectReferenceValue = cylinderPrefab;
            }
            Debug.Log("Assigned Cylinder block prefab");
        }
        else
        {
            Debug.LogWarning("Could not find Block_Cylinder.prefab. Please run 'BlockBattle > Create Block Prefabs' first.");
        }

        if (trianglePrefab != null)
        {
            SerializedProperty triangleProperty = serializedObject.FindProperty("m_TriangleBlockPrefab");
            if (triangleProperty != null)
            {
                triangleProperty.objectReferenceValue = trianglePrefab;
            }
            Debug.Log("Assigned Triangle block prefab");
        }
        else
        {
            Debug.LogWarning("Could not find Block_Triangle.prefab. Please run 'BlockBattle > Create Block Prefabs' first.");
        }

        // Find and assign table
        GameObject table = GameObject.Find("Table");
        if (table != null)
        {
            SerializedProperty tableProperty = serializedObject.FindProperty("m_Table");
            if (tableProperty != null)
            {
                tableProperty.objectReferenceValue = table;
            }
            Debug.Log("Assigned Table reference");
        }

        // Apply all serialized changes
        serializedObject.ApplyModifiedProperties();

        // Mark scene as dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Select the GameObject in the hierarchy
        UnityEditor.Selection.activeGameObject = spawnerObj;

        Debug.Log("Block Spawner setup complete! Blocks will spawn automatically when the game starts.");
    }
}

