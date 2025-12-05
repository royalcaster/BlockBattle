using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// Editor script to fix material culling mode to ensure proper face rendering.
/// </summary>
public static class BlockBattleFixMaterialCulling
{
    [MenuItem("BlockBattle/Fix Material Culling")]
    public static void FixMaterialCulling()
    {
        string materialPath = "Assets/BlockBattle/Materials/BlockMaterial.mat";
        Material blockMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        
        if (blockMaterial == null)
        {
            Debug.LogError($"BlockMaterial not found at {materialPath}.");
            return;
        }
        
        // In URP, we need to use RenderFace enum values
        // RenderFace.Back = 2 (cull back faces - correct for normal rendering)
        // RenderFace.Front = 1 (cull front faces - would cause the issue the user described)
        // RenderFace.Both = 0 (double-sided - no culling)
        
        // Check current cull mode
        float currentCull = blockMaterial.GetFloat("_Cull");
        Debug.Log($"Current _Cull value: {currentCull} (0=Off/DoubleSided, 1=Front, 2=Back)");
        
        // Set to Back face culling (2) - this culls faces facing AWAY from camera
        // If it's set to Front (1), that would cause faces facing TOWARD camera to be culled
        blockMaterial.SetFloat("_Cull", 2); // Back = 2
        
        // Force the material to update by toggling a property
        // This ensures Unity recognizes the change
        Color baseColor = blockMaterial.GetColor("_BaseColor");
        blockMaterial.SetColor("_BaseColor", baseColor);
        
        EditorUtility.SetDirty(blockMaterial);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        float newCull = blockMaterial.GetFloat("_Cull");
        Debug.Log($"Fixed BlockMaterial culling mode. _Cull is now set to: {newCull}");
        
        if (newCull == 2)
        {
            Debug.Log("✓ Material is set to Back-face culling (correct). Faces facing the camera will be visible.");
        }
        else if (newCull == 1)
        {
            Debug.LogWarning("⚠ Material is set to Front-face culling (wrong!). This would cause the issue you're seeing.");
        }
        else if (newCull == 0)
        {
            Debug.Log("Material is set to Double-sided (no culling). This works but is less efficient.");
        }
        
        Debug.Log("\nIf blocks still look wrong after this fix, the meshes might have flipped normals.");
        Debug.Log("In that case, we may need to recalculate mesh normals or flip triangle winding order.");
    }
}

