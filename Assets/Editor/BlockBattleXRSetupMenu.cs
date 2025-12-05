using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using BlockBattle;

/// <summary>
/// Editor menu item to set up XR configuration for BlockBattle.
/// </summary>
public static class BlockBattleXRSetupMenu
{
    /// <summary>
    /// Sets up the XR configuration by creating or finding the XRSetup GameObject and adding the BlockBattleXRSetup component.
    /// </summary>
    [MenuItem("BlockBattle/Setup XR Configuration")]
    public static void SetupXRConfiguration()
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
        
        // Find or create XRSetup GameObject
        GameObject xrSetupObj = GameObject.Find("XRSetup");
        if (xrSetupObj == null)
        {
            xrSetupObj = new GameObject("XRSetup");
            Debug.Log("Created XRSetup GameObject");
        }
        else
        {
            Debug.Log("Found existing XRSetup GameObject");
        }
        
        // Check if BlockBattleXRSetup component already exists
        BlockBattleXRSetup xrSetup = xrSetupObj.GetComponent<BlockBattleXRSetup>();
        if (xrSetup == null)
        {
            xrSetup = xrSetupObj.AddComponent<BlockBattleXRSetup>();
            Debug.Log("Added BlockBattleXRSetup component");
        }
        else
        {
            Debug.Log("BlockBattleXRSetup component already exists");
        }
        
        // Find XR Origin and assign it
        XROrigin xrOrigin = Object.FindFirstObjectByType<XROrigin>();
        if (xrOrigin != null)
        {
            // Use reflection to set the private field, or we can make it public
            // For now, let's use SerializedObject to set it
            SerializedObject serializedObject = new SerializedObject(xrSetup);
            SerializedProperty xrOriginProperty = serializedObject.FindProperty("m_XROrigin");
            if (xrOriginProperty != null)
            {
                xrOriginProperty.objectReferenceValue = xrOrigin;
                serializedObject.ApplyModifiedProperties();
                Debug.Log($"Assigned XR Origin '{xrOrigin.name}' to BlockBattleXRSetup");
            }
        }
        else
        {
            Debug.LogWarning("XR Origin not found in scene. The component will find it automatically at runtime.");
        }
        
        // Mark scene as dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        // Select the GameObject in the hierarchy
        Selection.activeGameObject = xrSetupObj;
        
        Debug.Log("XR Configuration setup complete! The BlockBattleXRSetup component is now configured.");
        Debug.Log("You can adjust the Camera Y Offset and Player Scale in the Inspector if needed.");
    }
}



