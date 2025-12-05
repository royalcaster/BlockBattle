using UnityEngine;
using UnityEditor;
using System.IO;

namespace BlockBattle
{
    /// <summary>
    /// Editor script to fix triangle winding order in block meshes.
    /// Incorrect winding causes faces to be culled incorrectly.
    /// </summary>
    public static class BlockBattleFixMeshWinding
    {
        private const string MeshesFolderPath = "Assets/BlockBattle/Meshes";

        [MenuItem("BlockBattle/Fix Mesh Triangle Winding")]
        public static void FixMeshWinding()
        {
            Debug.Log("Fixing triangle winding order in block meshes...");

            // Fix cube mesh
            FixCubeMesh();
            
            // Fix triangle mesh
            FixTriangleMesh();
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("✓ All mesh triangle winding orders have been fixed!");
        }

        private static void FixCubeMesh()
        {
            string meshPath = $"{MeshesFolderPath}/CubeMesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            if (mesh == null)
            {
                Debug.LogWarning($"CubeMesh not found at {meshPath}. Skipping.");
                return;
            }

            // Get current vertices and triangles
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // Fix triangle winding - reverse all triangles to make them counter-clockwise
            // Unity uses counter-clockwise winding for front faces
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // Swap second and third vertex to reverse winding
                int temp = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temp;
            }

            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            EditorUtility.SetDirty(mesh);
            Debug.Log("Fixed CubeMesh triangle winding order");
        }

        private static void FixTriangleMesh()
        {
            string meshPath = $"{MeshesFolderPath}/TriangleMesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            if (mesh == null)
            {
                Debug.LogWarning($"TriangleMesh not found at {meshPath}. Skipping.");
                return;
            }

            // Get current vertices and triangles
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // Fix triangle winding - reverse all triangles to make them counter-clockwise
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // Swap second and third vertex to reverse winding
                int temp = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temp;
            }

            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            EditorUtility.SetDirty(mesh);
            Debug.Log("Fixed TriangleMesh triangle winding order");
        }
    }
}

