using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor utility for setting up network components on BlockBattle prefabs.
    /// Adds NetworkObject, NetworkRigidbody, and NetworkBlock to block and projectile prefabs.
    /// </summary>
    public class BlockBattleNetworkSetup : EditorWindow
    {
        private static readonly string[] BlockPrefabPaths = new string[]
        {
            "Assets/BlockBattle/Prefabs/Blocks/Block_Cube.prefab",
            "Assets/BlockBattle/Prefabs/Blocks/Block_Cylinder.prefab",
            "Assets/BlockBattle/Prefabs/Blocks/Block_Triangle.prefab",
            "Assets/BlockBattle/Prefabs/Blocks/Block_Rectangle.prefab",
            "Assets/BlockBattle/Prefabs/Blocks/Block_Arch.prefab",
            "Assets/BlockBattle/Prefabs/Blocks/Block_BigTriangle.prefab"
        };

        private static readonly string ProjectilePrefabPath = "Assets/BlockBattle/Prefabs/BallProjectile.prefab";

        // Cached types for Netcode components
        private static Type _networkObjectType;
        private static Type _networkRigidbodyType;
        private static Type _networkBlockType;

        private static bool TryGetNetcodeTypes()
        {
            // Search all loaded assemblies for Netcode types
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;
                
                // Skip non-Netcode assemblies for performance
                if (!assemblyName.Contains("Netcode") && !assemblyName.Contains("Assembly-CSharp"))
                    continue;

                // Try to find NetworkObject
                if (_networkObjectType == null)
                {
                    _networkObjectType = assembly.GetType("Unity.Netcode.NetworkObject");
                }

                // Try to find NetworkRigidbody (might be NetworkRigidbody or NetworkRigidbody3D in different versions)
                if (_networkRigidbodyType == null)
                {
                    _networkRigidbodyType = assembly.GetType("Unity.Netcode.Components.NetworkRigidbody");
                    if (_networkRigidbodyType == null)
                    {
                        _networkRigidbodyType = assembly.GetType("Unity.Netcode.Components.NetworkRigidbody3D");
                    }
                }

                // Try to find NetworkBlock
                if (_networkBlockType == null)
                {
                    _networkBlockType = assembly.GetType("BlockBattle.Network.NetworkBlock");
                }
            }

            // Debug logging
            Debug.Log($"NetworkObject type: {(_networkObjectType != null ? _networkObjectType.FullName : "NOT FOUND")}");
            Debug.Log($"NetworkRigidbody type: {(_networkRigidbodyType != null ? _networkRigidbodyType.FullName : "NOT FOUND")}");
            Debug.Log($"NetworkBlock type: {(_networkBlockType != null ? _networkBlockType.FullName : "NOT FOUND")}");

            return _networkObjectType != null;
        }

        [MenuItem("BlockBattle/Setup Network Prefabs")]
        public static void AddNetworkComponentsToAllPrefabs()
        {
            if (!TryGetNetcodeTypes())
            {
                EditorUtility.DisplayDialog(
                    "Netcode Not Found",
                    "Unity Netcode for GameObjects package is not installed or not properly configured.\n\n" +
                    "Please install the package via Package Manager:\n" +
                    "Window > Package Manager > Unity Registry > Netcode for GameObjects",
                    "OK"
                );
                return;
            }

            int blocksProcessed = 0;
            int projectilesProcessed = 0;

            // Process block prefabs
            foreach (string prefabPath in BlockPrefabPaths)
            {
                if (AddNetworkComponentsToPrefab(prefabPath, true))
                {
                    blocksProcessed++;
                }
            }

            // Process projectile prefab
            if (AddNetworkComponentsToPrefab(ProjectilePrefabPath, false))
            {
                projectilesProcessed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"BlockBattle Network Setup: Processed {blocksProcessed} block prefabs and {projectilesProcessed} projectile prefabs.");
            EditorUtility.DisplayDialog(
                "Network Setup Complete",
                $"Added network components to:\n- {blocksProcessed} block prefabs\n- {projectilesProcessed} projectile prefabs\n\nDon't forget to add these prefabs to the NetworkManager's NetworkPrefabs list!",
                "OK"
            );
        }

        [MenuItem("BlockBattle/Setup Network Prefabs (Blocks Only)")]
        public static void AddNetworkComponentsToBlockPrefabs()
        {
            if (!TryGetNetcodeTypes())
            {
                EditorUtility.DisplayDialog(
                    "Netcode Not Found",
                    "Unity Netcode for GameObjects package is not installed.",
                    "OK"
                );
                return;
            }

            int processed = 0;

            foreach (string prefabPath in BlockPrefabPaths)
            {
                if (AddNetworkComponentsToPrefab(prefabPath, true))
                {
                    processed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"BlockBattle Network Setup: Processed {processed} block prefabs.");
            EditorUtility.DisplayDialog(
                "Network Setup Complete",
                $"Added network components to {processed} block prefabs.\n\nDon't forget to add these prefabs to the NetworkManager's NetworkPrefabs list!",
                "OK"
            );
        }

        [MenuItem("BlockBattle/Setup Network Prefabs (Projectile Only)")]
        public static void AddNetworkComponentsToProjectilePrefab()
        {
            if (!TryGetNetcodeTypes())
            {
                EditorUtility.DisplayDialog(
                    "Netcode Not Found",
                    "Unity Netcode for GameObjects package is not installed.",
                    "OK"
                );
                return;
            }

            if (AddNetworkComponentsToPrefab(ProjectilePrefabPath, false))
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("BlockBattle Network Setup: Processed projectile prefab.");
                EditorUtility.DisplayDialog(
                    "Network Setup Complete",
                    "Added network components to projectile prefab.\n\nDon't forget to add this prefab to the NetworkManager's NetworkPrefabs list!",
                    "OK"
                );
            }
        }

        [MenuItem("BlockBattle/Remove Network Components from Prefabs")]
        public static void RemoveNetworkComponentsFromAllPrefabs()
        {
            if (!EditorUtility.DisplayDialog(
                "Remove Network Components",
                "This will remove NetworkObject, NetworkRigidbody, and NetworkBlock components from all block and projectile prefabs. Are you sure?",
                "Yes, Remove",
                "Cancel"))
            {
                return;
            }

            int processed = 0;

            // Process block prefabs
            foreach (string prefabPath in BlockPrefabPaths)
            {
                if (RemoveNetworkComponentsFromPrefab(prefabPath))
                {
                    processed++;
                }
            }

            // Process projectile prefab
            if (RemoveNetworkComponentsFromPrefab(ProjectilePrefabPath))
            {
                processed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"BlockBattle Network Setup: Removed network components from {processed} prefabs.");
        }

        /// <summary>
        /// Adds network components to a prefab at the specified path.
        /// </summary>
        private static bool AddNetworkComponentsToPrefab(string prefabPath, bool addNetworkBlock)
        {
            // Load the prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"BlockBattle Network Setup: Could not find prefab at {prefabPath}");
                return false;
            }

            // Open prefab for editing
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            bool modified = false;

            try
            {
                // Add NetworkObject if not present
                if (_networkObjectType != null && prefabRoot.GetComponent(_networkObjectType) == null)
                {
                    prefabRoot.AddComponent(_networkObjectType);
                    Debug.Log($"Added NetworkObject to {prefab.name}");
                    modified = true;
                }

                // Add NetworkRigidbody if Rigidbody exists and NetworkRigidbody not present
                Rigidbody rb = prefabRoot.GetComponent<Rigidbody>();
                if (rb != null && _networkRigidbodyType != null && prefabRoot.GetComponent(_networkRigidbodyType) == null)
                {
                    prefabRoot.AddComponent(_networkRigidbodyType);
                    Debug.Log($"Added NetworkRigidbody to {prefab.name}");
                    modified = true;
                }

                // Add NetworkBlock if requested and not present
                if (addNetworkBlock && _networkBlockType != null)
                {
                    if (prefabRoot.GetComponent(_networkBlockType) == null)
                    {
                        prefabRoot.AddComponent(_networkBlockType);
                        Debug.Log($"Added NetworkBlock to {prefab.name}");
                        modified = true;
                    }
                }

                // Save changes if any were made
                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                    Debug.Log($"BlockBattle Network Setup: Updated prefab {prefab.name}");
                }
                else
                {
                    Debug.Log($"BlockBattle Network Setup: Prefab {prefab.name} already has all network components");
                }
            }
            finally
            {
                // Always unload the prefab contents
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return true;
        }

        /// <summary>
        /// Removes network components from a prefab at the specified path.
        /// </summary>
        private static bool RemoveNetworkComponentsFromPrefab(string prefabPath)
        {
            // Load the prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"BlockBattle Network Setup: Could not find prefab at {prefabPath}");
                return false;
            }

            // Open prefab for editing
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            bool modified = false;

            try
            {
                // Try to get types dynamically
                TryGetNetcodeTypes();

                // Remove NetworkBlock first (depends on NetworkObject)
                if (_networkBlockType != null)
                {
                    var networkBlock = prefabRoot.GetComponent(_networkBlockType);
                    if (networkBlock != null)
                    {
                        DestroyImmediate(networkBlock);
                        Debug.Log($"Removed NetworkBlock from {prefab.name}");
                        modified = true;
                    }
                }

                // Remove NetworkRigidbody
                if (_networkRigidbodyType != null)
                {
                    var networkRb = prefabRoot.GetComponent(_networkRigidbodyType);
                    if (networkRb != null)
                    {
                        DestroyImmediate(networkRb);
                        Debug.Log($"Removed NetworkRigidbody from {prefab.name}");
                        modified = true;
                    }
                }

                // Remove NetworkObject last
                if (_networkObjectType != null)
                {
                    var networkObj = prefabRoot.GetComponent(_networkObjectType);
                    if (networkObj != null)
                    {
                        DestroyImmediate(networkObj);
                        Debug.Log($"Removed NetworkObject from {prefab.name}");
                        modified = true;
                    }
                }

                // Save changes if any were made
                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                    Debug.Log($"BlockBattle Network Setup: Updated prefab {prefab.name}");
                }
            }
            finally
            {
                // Always unload the prefab contents
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return modified;
        }

        [MenuItem("BlockBattle/Validate Network Prefabs")]
        public static void ValidateNetworkPrefabList()
        {
            TryGetNetcodeTypes();

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Network Prefab Validation:\n");

            // Check each block prefab
            sb.AppendLine("Block Prefabs:");
            foreach (string prefabPath in BlockPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    sb.AppendLine($"  [MISSING] {prefabPath}");
                    continue;
                }

                bool hasNetworkObject = _networkObjectType != null && prefab.GetComponent(_networkObjectType) != null;
                bool hasNetworkRigidbody = _networkRigidbodyType != null && prefab.GetComponent(_networkRigidbodyType) != null;
                bool hasNetworkBlock = _networkBlockType != null && prefab.GetComponent(_networkBlockType) != null;

                string status = (hasNetworkObject && hasNetworkRigidbody && hasNetworkBlock) ? "[OK]" : "[INCOMPLETE]";
                sb.AppendLine($"  {status} {prefab.name}");
                if (!hasNetworkObject) sb.AppendLine("      - Missing NetworkObject");
                if (!hasNetworkRigidbody) sb.AppendLine("      - Missing NetworkRigidbody");
                if (!hasNetworkBlock) sb.AppendLine("      - Missing NetworkBlock");
            }

            // Check projectile prefab
            sb.AppendLine("\nProjectile Prefab:");
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            if (projectile == null)
            {
                sb.AppendLine($"  [MISSING] {ProjectilePrefabPath}");
            }
            else
            {
                bool hasNetworkObject = _networkObjectType != null && projectile.GetComponent(_networkObjectType) != null;
                bool hasNetworkRigidbody = _networkRigidbodyType != null && projectile.GetComponent(_networkRigidbodyType) != null;

                string status = (hasNetworkObject && hasNetworkRigidbody) ? "[OK]" : "[INCOMPLETE]";
                sb.AppendLine($"  {status} {projectile.name}");
                if (!hasNetworkObject) sb.AppendLine("      - Missing NetworkObject");
                if (!hasNetworkRigidbody) sb.AppendLine("      - Missing NetworkRigidbody");
            }

            sb.AppendLine("\n\nIMPORTANT: After adding network components, you must add the prefabs to the NetworkManager's NetworkPrefabs list!");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Network Prefab Validation", sb.ToString(), "OK");
        }
    }
}
