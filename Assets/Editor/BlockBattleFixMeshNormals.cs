using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor script to check and fix mesh normals if they're flipped.
/// </summary>
public static class BlockBattleFixMeshNormals
{
    [MenuItem("BlockBattle/Fix Mesh Normals")]
    public static void FixMeshNormals()
    {
        string[] meshNames = { "CubeMesh", "CylinderMesh", "TriangularPrismMesh" };
        string meshesPath = "Assets/BlockBattle/Meshes";
        
        bool anyFixed = false;
        
        foreach (string meshName in meshNames)
        {
            string meshPath = $"{meshesPath}/{meshName}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            
            if (mesh == null)
            {
                Debug.LogWarning($"Mesh {meshName} not found at {meshPath}. Skipping.");
                continue;
            }
            
            // Recalculate normals to ensure they're correct
            // This recalculates normals based on triangle winding order
            mesh.RecalculateNormals();
            
            // Also recalculate tangents (needed for proper lighting)
            mesh.RecalculateTangents();
            
            // Recalculate bounds
            mesh.RecalculateBounds();
            
            EditorUtility.SetDirty(mesh);
            anyFixed = true;
            
            Debug.Log($"Fixed normals for {meshName}");
        }
        
        if (anyFixed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("All mesh normals have been recalculated. Blocks should now render correctly.");
        }
        else
        {
            Debug.LogWarning("No meshes were found to fix. Make sure block prefabs have been created first.");
        }
    }
}



