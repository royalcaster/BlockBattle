using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to create a holographic material for reference structures.
/// </summary>
public static class BlockBattleCreateHolographicMaterial
{
    private const string MaterialsFolderPath = "Assets/BlockBattle/Materials";
    private const string ShadersFolderPath = "Assets/BlockBattle/Shaders";

    /// <summary>
    /// Creates the holographic material for reference structures.
    /// </summary>
    [MenuItem("BlockBattle/Create Holographic Material")]
    public static void CreateHolographicMaterial()
    {
        EnsureFolderStructure();

        string materialPath = $"{MaterialsFolderPath}/HolographicMaterial.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material != null)
        {
            Debug.Log($"HolographicMaterial already exists. Updating properties.");
        }
        else
        {
            // Load the holographic shader
            Shader holographicShader = Shader.Find("BlockBattle/Holographic");
            
            if (holographicShader == null)
            {
                // Try loading from asset path
                string shaderPath = $"{ShadersFolderPath}/Holographic.shader";
                holographicShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            }

            if (holographicShader == null)
            {
                Debug.LogError("Could not find Holographic shader! Please ensure the shader exists at Assets/BlockBattle/Shaders/Holographic.shader");
                return;
            }

            material = new Material(holographicShader);
            material.name = "HolographicMaterial";
        }

        // Set holographic properties
        material.SetColor("_BaseColor", new Color(0.2f, 0.6f, 1.0f, 0.5f)); // Cyan-blue base color with transparency
        material.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1.0f, 1.0f)); // Bright cyan emission
        material.SetFloat("_EmissionIntensity", 1.5f);
        material.SetFloat("_FresnelPower", 2.0f);
        material.SetFloat("_FresnelIntensity", 1.0f);
        material.SetFloat("_ScanlineSpeed", 2.0f);
        material.SetFloat("_ScanlineIntensity", 0.3f);
        material.SetFloat("_Transparency", 0.6f);

        if (material != null && AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
        {
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"BlockBattle: Created/Updated HolographicMaterial at {materialPath}");
    }

    /// <summary>
    /// Ensures the required folders exist.
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

        if (!AssetDatabase.IsValidFolder(ShadersFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle", "Shaders");
        }
    }
}

