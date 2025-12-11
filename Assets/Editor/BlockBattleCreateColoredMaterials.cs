using UnityEngine;
using UnityEditor;
using BlockBattle;

/// <summary>
/// Editor utility to create colored materials for blocks.
/// </summary>
public static class BlockBattleCreateColoredMaterials
{
    private const string MaterialsFolderPath = "Assets/BlockBattle/Materials";

    /// <summary>
    /// Creates all colored block materials.
    /// </summary>
    [MenuItem("BlockBattle/Create Colored Block Materials")]
    public static void CreateAllColoredMaterials()
    {
        EnsureFolderStructure();

        foreach (BlockColor color in System.Enum.GetValues(typeof(BlockColor)))
        {
            CreateColoredMaterial(color);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("BlockBattle: All colored block materials created successfully!");
    }

    /// <summary>
    /// Creates a colored material for a specific block color.
    /// </summary>
    /// <param name="blockColor">The block color to create a material for</param>
    /// <returns>The created material</returns>
    public static Material CreateColoredMaterial(BlockColor blockColor)
    {
        EnsureFolderStructure();

        string materialName = BlockColorUtility.GetMaterialName(blockColor);
        string materialPath = $"{MaterialsFolderPath}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material != null)
        {
            Debug.Log($"Material {materialName} already exists. Skipping creation.");
            return material;
        }

        // Use URP shader
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null)
        {
            urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }

        if (urpShader == null)
        {
            Debug.LogError("Could not find URP shader! Please ensure Universal Render Pipeline is installed.");
            return null;
        }

        material = new Material(urpShader);
        material.name = materialName;

        // Set color using URP property names
        Color color = BlockColorUtility.GetColor(blockColor);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.3f);
        material.SetFloat("_Metallic", 0f);

        // Ensure solid opaque rendering properties
        material.SetFloat("_Surface", 0f); // Opaque
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", 1f);
        material.SetFloat("_DstBlend", 0f);
        material.SetFloat("_SrcBlendAlpha", 1f);
        material.SetFloat("_DstBlendAlpha", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_Cull", 2f); // Back-face culling

        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"BlockBattle: Created material {materialName} at {materialPath}");
        return material;
    }

    /// <summary>
    /// Ensures the materials folder exists.
    /// </summary>
    private static void EnsureFolderStructure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/BlockBattle"))
        {
            AssetDatabase.CreateFolder("Assets", "BlockBattle");
        }

        if (!AssetDatabase.IsValidFolder(MaterialsFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle", "Materials");
        }
    }
}

