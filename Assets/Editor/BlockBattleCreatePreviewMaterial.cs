using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to create a preview material for hologram-style structure previews.
/// </summary>
public static class BlockBattleCreatePreviewMaterial
{
    private const string MaterialsFolderPath = "Assets/BlockBattle/Materials";

    /// <summary>
    /// Creates a preview material with a hologram-like appearance (semi-transparent, glowing).
    /// </summary>
    [MenuItem("BlockBattle/Create Preview Material")]
    public static void CreatePreviewMaterial()
    {
        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder("Assets/BlockBattle"))
        {
            AssetDatabase.CreateFolder("Assets", "BlockBattle");
        }

        if (!AssetDatabase.IsValidFolder(MaterialsFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle", "Materials");
        }

        string materialPath = $"{MaterialsFolderPath}/PreviewMaterial.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material != null)
        {
            Debug.LogWarning($"PreviewMaterial already exists at {materialPath}. Skipping creation.");
            return;
        }

        // Use URP shader for preview material
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null)
        {
            urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }

        if (urpShader == null)
        {
            Debug.LogError("Could not find URP shader! Please ensure Universal Render Pipeline is installed.");
            return;
        }

        material = new Material(urpShader);
        material.name = "PreviewMaterial";

        // Set to transparent surface type
        material.SetFloat("_Surface", 1f); // 1 = Transparent
        material.SetFloat("_Blend", 0f); // Alpha blend
        material.SetFloat("_SrcBlend", 5f); // SrcAlpha
        material.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
        material.SetFloat("_ZWrite", 0f); // Disable Z-write for transparency
        material.SetFloat("_AlphaClip", 0f); // Disable alpha clipping

        // Set color to cyan/blue with transparency (hologram-like)
        material.SetColor("_BaseColor", new Color(0.2f, 0.8f, 1.0f, 0.3f)); // Light blue, semi-transparent
        material.SetFloat("_Smoothness", 0.8f); // Glossy
        material.SetFloat("_Metallic", 0f); // Non-metallic

        // Enable emission for glow effect
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 0.5f, 1f)); // Subtle glow

        // Save material
        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"BlockBattle: Created preview material at {materialPath}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = material;
    }
}

