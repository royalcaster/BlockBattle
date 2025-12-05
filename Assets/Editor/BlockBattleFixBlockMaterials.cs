using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor script to fix material references in block prefabs to ensure they work in builds.
/// </summary>
public static class BlockBattleFixBlockMaterials
{
    [MenuItem("BlockBattle/Fix Block Material References")]
    public static void FixBlockMaterials()
    {
        // Load or create the material asset
        string materialPath = "Assets/BlockBattle/Materials/BlockMaterial.mat";
        Material blockMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        
        if (blockMaterial == null)
        {
            Debug.LogWarning($"BlockMaterial not found at {materialPath}. Creating new material with URP shader.");
            
            // Create material with URP shader
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
            
            blockMaterial = new Material(urpShader);
            blockMaterial.name = "BlockMaterial";
            blockMaterial.SetColor("_BaseColor", new Color(0.6f, 0.4f, 0.2f, 1f)); // Fully opaque
            blockMaterial.SetFloat("_Smoothness", 0.3f);
            blockMaterial.SetFloat("_Metallic", 0f);
            
            // Ensure solid opaque rendering
            blockMaterial.SetFloat("_Surface", 0f);
            blockMaterial.SetFloat("_Blend", 0f);
            blockMaterial.SetFloat("_SrcBlend", 1f);
            blockMaterial.SetFloat("_DstBlend", 0f);
            blockMaterial.SetFloat("_ZWrite", 1f);
            blockMaterial.SetFloat("_AlphaClip", 0f);
            blockMaterial.SetFloat("_Cull", 2f);
            
            AssetDatabase.CreateAsset(blockMaterial, materialPath);
            AssetDatabase.SaveAssets();
        }
        else
        {
            // Check if material is using Standard shader and convert to URP
            if (blockMaterial.shader.name == "Standard" || blockMaterial.shader.name.Contains("Standard"))
            {
                Debug.Log("Converting BlockMaterial from Standard to URP shader...");
                
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null)
                {
                    urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                }
                
                if (urpShader != null)
                {
                    // Convert Standard material properties to URP
                    Color oldColor = blockMaterial.color;
                    float oldGlossiness = blockMaterial.GetFloat("_Glossiness");
                    float oldMetallic = blockMaterial.GetFloat("_Metallic");
                    
                    blockMaterial.shader = urpShader;
                    // Ensure color has full alpha for solid opaque
                    Color solidColor = oldColor;
                    solidColor.a = 1f;
                    blockMaterial.SetColor("_BaseColor", solidColor);
                    blockMaterial.SetFloat("_Smoothness", oldGlossiness);
                    blockMaterial.SetFloat("_Metallic", oldMetallic);
                    
                    // Ensure solid opaque rendering properties
                    blockMaterial.SetFloat("_Surface", 0f); // Opaque
                    blockMaterial.SetFloat("_Blend", 0f); // Alpha blend
                    blockMaterial.SetFloat("_SrcBlend", 1f);
                    blockMaterial.SetFloat("_DstBlend", 0f);
                    blockMaterial.SetFloat("_ZWrite", 1f); // Enable Z-write
                    blockMaterial.SetFloat("_AlphaClip", 0f); // Disable alpha clipping
                    
                    // Ensure back-face culling is enabled (2 = Back, which is correct)
                    // 0 = Off (double-sided), 1 = Front, 2 = Back
                    blockMaterial.SetFloat("_Cull", 2f);
                    
                    EditorUtility.SetDirty(blockMaterial);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Converted BlockMaterial to URP shader with back-face culling");
                }
            }
        }
        
        // Fix each block prefab
        string[] blockTypes = { "Block_Cube", "Block_Cylinder", "Block_Triangle" };
        string blocksPath = "Assets/BlockBattle/Prefabs/Blocks";
        
        foreach (string blockType in blockTypes)
        {
            string prefabPath = $"{blocksPath}/{blockType}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (prefab == null)
            {
                Debug.LogWarning($"Could not find {blockType}.prefab at {prefabPath}");
                continue;
            }
            
            // Find the Visuals child
            Transform visualsTransform = prefab.transform.Find("Visuals");
            if (visualsTransform == null)
            {
                Debug.LogWarning($"Visuals child not found in {blockType}.prefab");
                continue;
            }
            
            MeshRenderer meshRenderer = visualsTransform.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                Debug.LogWarning($"MeshRenderer not found in Visuals of {blockType}.prefab");
                continue;
            }
            
            // Use SerializedObject to properly set the material reference
            SerializedObject serializedRenderer = new SerializedObject(meshRenderer);
            SerializedProperty materialsProperty = serializedRenderer.FindProperty("m_Materials");
            
            if (materialsProperty != null && materialsProperty.isArray)
            {
                materialsProperty.arraySize = 1;
                SerializedProperty materialElement = materialsProperty.GetArrayElementAtIndex(0);
                materialElement.objectReferenceValue = blockMaterial;
                serializedRenderer.ApplyModifiedProperties();
                
                Debug.Log($"Fixed material reference in {blockType}.prefab");
            }
            else
            {
                Debug.LogWarning($"Could not find m_Materials property in {blockType}.prefab");
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("Block material references fixed! The blocks should now be visible in builds.");
    }
}

