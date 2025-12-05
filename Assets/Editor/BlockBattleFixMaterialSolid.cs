using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace BlockBattle
{
    /// <summary>
    /// Editor script to ensure BlockMaterial is configured as a solid opaque material.
    /// Fixes all rendering properties to ensure proper solid appearance.
    /// </summary>
    public static class BlockBattleFixMaterialSolid
    {
        private const string MaterialsFolderPath = "Assets/BlockBattle/Materials";

        [MenuItem("BlockBattle/Fix Material - Make Solid Opaque")]
        public static void FixMaterialSolid()
        {
            string materialPath = $"{MaterialsFolderPath}/BlockMaterial.mat";
            Material blockMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (blockMaterial == null)
            {
                Debug.LogError($"BlockMaterial not found at {materialPath}. Please run 'BlockBattle > Create Block Prefabs' first.");
                return;
            }

            Debug.Log("Fixing BlockMaterial to ensure solid opaque rendering...");

            // Ensure URP shader
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                Debug.LogWarning("Universal Render Pipeline/Lit shader not found. Falling back to Universal Render Pipeline/Simple Lit.");
                urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            if (urpShader == null)
            {
                Debug.LogError("Neither Universal Render Pipeline/Lit nor Simple Lit shader found. Cannot fix material.");
                return;
            }

            if (blockMaterial.shader != urpShader)
            {
                blockMaterial.shader = urpShader;
                Debug.Log($"Updated shader to {urpShader.name}");
            }

            // Set all opaque rendering properties explicitly
            // Surface Type: Opaque (0 = Opaque, 1 = Transparent)
            blockMaterial.SetFloat("_Surface", 0f);
            
            // Blend Mode: Alpha (0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply)
            blockMaterial.SetFloat("_Blend", 0f);
            
            // Source Blend: One (1)
            blockMaterial.SetFloat("_SrcBlend", 1f);
            
            // Destination Blend: Zero (0)
            blockMaterial.SetFloat("_DstBlend", 0f);
            
            // Source Blend Alpha: One (1)
            blockMaterial.SetFloat("_SrcBlendAlpha", 1f);
            
            // Destination Blend Alpha: Zero (0)
            blockMaterial.SetFloat("_DstBlendAlpha", 0f);
            
            // Z-Write: Enabled (1 = On, 0 = Off)
            blockMaterial.SetFloat("_ZWrite", 1f);
            
            // Alpha Clipping: Disabled (0 = Off, 1 = On)
            blockMaterial.SetFloat("_AlphaClip", 0f);
            
            // Alpha Cutoff: 0.5 (not used when AlphaClip is 0, but set for consistency)
            blockMaterial.SetFloat("_Cutoff", 0.5f);
            
            // Culling: Back (2 = Back, 1 = Front, 0 = Off/Double-sided)
            blockMaterial.SetFloat("_Cull", 2f);
            
            // Ensure BaseColor has full alpha (solid)
            Color baseColor = blockMaterial.GetColor("_BaseColor");
            if (baseColor.a < 1f)
            {
                baseColor.a = 1f;
                blockMaterial.SetColor("_BaseColor", baseColor);
                Debug.Log("Fixed BaseColor alpha to 1.0 (fully opaque)");
            }
            
            // Set other material properties if not already set
            if (!blockMaterial.HasProperty("_Smoothness") || blockMaterial.GetFloat("_Smoothness") == 0f)
            {
                blockMaterial.SetFloat("_Smoothness", 0.3f);
            }
            
            if (!blockMaterial.HasProperty("_Metallic") || blockMaterial.GetFloat("_Metallic") == 0f)
            {
                blockMaterial.SetFloat("_Metallic", 0f);
            }

            // Set render queue to opaque (this is usually automatic, but ensure it)
            blockMaterial.renderQueue = (int)RenderQueue.Geometry;

            // Force material to update
            EditorUtility.SetDirty(blockMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("✓ BlockMaterial fixed! All opaque rendering properties set:");
            Debug.Log($"  - Surface: Opaque ({blockMaterial.GetFloat("_Surface")})");
            Debug.Log($"  - Blend: Alpha ({blockMaterial.GetFloat("_Blend")})");
            Debug.Log($"  - ZWrite: Enabled ({blockMaterial.GetFloat("_ZWrite")})");
            Debug.Log($"  - AlphaClip: Disabled ({blockMaterial.GetFloat("_AlphaClip")})");
            Debug.Log($"  - Cull: Back ({blockMaterial.GetFloat("_Cull")})");
            Debug.Log($"  - BaseColor Alpha: {blockMaterial.GetColor("_BaseColor").a}");
            Debug.Log($"  - Render Queue: {blockMaterial.renderQueue} (Geometry/Opaque)");
            Debug.Log("\nMaterial should now render as a solid opaque surface.");
        }
    }
}

