using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Updates the floor material to a proper gray material asset.
/// </summary>
public static class BlockBattleUpdateFloor
{
    [MenuItem("BlockBattle/Update Floor Material")]
    public static void UpdateFloorMaterial()
    {
        // Ensure materials folder exists
        string materialsPath = "Assets/BlockBattle/Materials";
        if (!AssetDatabase.IsValidFolder(materialsPath))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle", "Materials");
        }
        
        // Create or load floor material
        string materialPath = $"{materialsPath}/FloorMaterial.mat";
        Material floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        
        if (floorMaterial == null)
        {
            floorMaterial = new Material(Shader.Find("Standard"));
            floorMaterial.name = "FloorMaterial";
            floorMaterial.color = new Color(0.6f, 0.6f, 0.6f); // Light gray
            floorMaterial.SetFloat("_Glossiness", 0.1f); // Slightly glossy
            floorMaterial.SetFloat("_Metallic", 0f); // Non-metallic
            
            AssetDatabase.CreateAsset(floorMaterial, materialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created floor material at {materialPath}");
        }
        else
        {
            // Update existing material
            floorMaterial.color = new Color(0.6f, 0.6f, 0.6f);
            floorMaterial.SetFloat("_Glossiness", 0.1f);
            floorMaterial.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(floorMaterial);
            Debug.Log($"Updated existing floor material");
        }
        
        // Load scene
        string scenePath = "Assets/Scenes/BlockBattleScene.unity";
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogError($"BlockBattleScene not found at {scenePath}");
            return;
        }
        
        Scene scene = EditorSceneManager.OpenScene(scenePath);
        
        // Find floor and update material
        GameObject floor = GameObject.Find("Floor");
        if (floor != null)
        {
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                floorRenderer.sharedMaterial = floorMaterial;
                EditorUtility.SetDirty(floor);
                Debug.Log("Updated floor material in scene");
            }
            else
            {
                Debug.LogWarning("Floor GameObject found but has no Renderer component");
            }
        }
        else
        {
            Debug.LogWarning("Floor GameObject not found in scene");
        }
        
        // Save scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("Floor material update complete!");
    }
}




