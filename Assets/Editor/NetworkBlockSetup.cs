using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.IO;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor utility to set up block prefabs for networked multiplayer.
    /// Adds NetworkObject, NetworkRigidbody, and NetworkBlock components.
    /// </summary>
    public static class NetworkBlockSetup
    {
        private static readonly string BlockPrefabsPath = "Assets/BlockBattle/Prefabs/Blocks";
        
        [MenuItem("BlockBattle/Network/Setup Block Prefabs for Networking")]
        public static void SetupBlockPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { BlockPrefabsPath });
            int updatedCount = 0;
            
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Skip non-block prefabs
                if (!Path.GetFileName(path).StartsWith("Block_"))
                    continue;
                
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                
                // Open prefab for editing
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                
                bool modified = false;
                
                // Add NetworkObject if missing
                if (prefabRoot.GetComponent<NetworkObject>() == null)
                {
                    prefabRoot.AddComponent<NetworkObject>();
                    Debug.Log($"Added NetworkObject to {prefab.name}");
                    modified = true;
                }
                
                // Add NetworkRigidbody if missing (and has Rigidbody)
                if (prefabRoot.GetComponent<Rigidbody>() != null && 
                    prefabRoot.GetComponent<NetworkRigidbody>() == null)
                {
                    var networkRb = prefabRoot.AddComponent<NetworkRigidbody>();
                    // Configure for client-authoritative physics in DA mode
                    networkRb.AutoUpdateKinematicState = true;
                    Debug.Log($"Added NetworkRigidbody to {prefab.name}");
                    modified = true;
                }
                
                // Add NetworkBlock if missing
                var networkBlockType = System.Type.GetType("BlockBattle.Network.NetworkBlock, Assembly-CSharp");
                if (networkBlockType != null && prefabRoot.GetComponent(networkBlockType) == null)
                {
                    prefabRoot.AddComponent(networkBlockType);
                    Debug.Log($"Added NetworkBlock to {prefab.name}");
                    modified = true;
                }
                
                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    updatedCount++;
                }
                
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"NetworkBlockSetup: Updated {updatedCount} block prefabs for networking");
            
            if (updatedCount > 0)
            {
                EditorUtility.DisplayDialog("Block Prefabs Updated", 
                    $"Added networking components to {updatedCount} block prefabs.\n\n" +
                    "Next step: Run 'BlockBattle/Network/Register Block Prefabs with NetworkManager' " +
                    "to add them to the network prefab list.", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Changes", 
                    "All block prefabs already have networking components.", 
                    "OK");
            }
        }
        
        [MenuItem("BlockBattle/Network/Register Block Prefabs with NetworkManager")]
        public static void RegisterBlockPrefabsWithNetworkManager()
        {
            // Find the NetworkPrefabsList asset
            string[] prefabsListGuids = AssetDatabase.FindAssets("t:NetworkPrefabsList");
            NetworkPrefabsList networkPrefabsList = null;
            
            if (prefabsListGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabsListGuids[0]);
                networkPrefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            }
            
            if (networkPrefabsList == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Could not find NetworkPrefabsList asset. Please ensure you have a NetworkPrefabsList in your project.", 
                    "OK");
                return;
            }
            
            // Find all block prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { BlockPrefabsPath });
            int addedCount = 0;
            
            // Get the current list of prefabs
            var currentPrefabs = networkPrefabsList.PrefabList;
            
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Skip non-block prefabs
                if (!Path.GetFileName(path).StartsWith("Block_"))
                    continue;
                
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                
                // Check if prefab has NetworkObject
                if (prefab.GetComponent<NetworkObject>() == null)
                {
                    Debug.LogWarning($"Skipping {prefab.name} - no NetworkObject component");
                    continue;
                }
                
                // Check if already registered
                bool alreadyRegistered = false;
                foreach (var entry in currentPrefabs)
                {
                    if (entry.Prefab == prefab)
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }
                
                if (!alreadyRegistered)
                {
                    var newEntry = new NetworkPrefab { Prefab = prefab };
                    networkPrefabsList.Add(newEntry);
                    Debug.Log($"Registered {prefab.name} with NetworkPrefabsList");
                    addedCount++;
                }
            }
            
            if (addedCount > 0)
            {
                EditorUtility.SetDirty(networkPrefabsList);
                AssetDatabase.SaveAssets();
            }
            
            EditorUtility.DisplayDialog("Registration Complete", 
                $"Added {addedCount} block prefabs to NetworkPrefabsList.\n" +
                $"Total network prefabs: {networkPrefabsList.PrefabList.Count}", 
                "OK");
        }
        
        [MenuItem("BlockBattle/Network/Verify Block Networking Setup")]
        public static void VerifySetup()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { BlockPrefabsPath });
            int total = 0;
            int hasNetworkObject = 0;
            int hasNetworkRigidbody = 0;
            int hasNetworkBlock = 0;
            int hasOwnershipTransfer = 0;
            
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).StartsWith("Block_"))
                    continue;
                
                total++;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                
                var networkObject = prefab.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    hasNetworkObject++;
                    
                    // Check if ownership transfer is enabled using SerializedObject
                    var serializedObject = new SerializedObject(networkObject);
                    var ownershipProperty = serializedObject.FindProperty("Ownership");
                    if (ownershipProperty != null)
                    {
                        // Transferable = bit 1, so value should be >= 2 or value & 2 != 0
                        int value = ownershipProperty.intValue;
                        if ((value & 2) != 0) // Transferable flag is bit 1
                        {
                            hasOwnershipTransfer++;
                        }
                    }
                }
                
                if (prefab.GetComponent<NetworkRigidbody>() != null) hasNetworkRigidbody++;
                
                var networkBlockType = System.Type.GetType("BlockBattle.Network.NetworkBlock, Assembly-CSharp");
                if (networkBlockType != null && prefab.GetComponent(networkBlockType) != null) hasNetworkBlock++;
            }
            
            string message = $"Block Prefabs Status:\n\n" +
                $"Total Block Prefabs: {total}\n" +
                $"With NetworkObject: {hasNetworkObject}/{total}\n" +
                $"With NetworkRigidbody: {hasNetworkRigidbody}/{total}\n" +
                $"With NetworkBlock: {hasNetworkBlock}/{total}\n" +
                $"With Ownership Transfer: {hasOwnershipTransfer}/{total}";
            
            EditorUtility.DisplayDialog("Verification Results", message, "OK");
            Debug.Log(message);
        }
        
        /// <summary>
        /// Enables ownership transfer on all block prefabs.
        /// This is CRITICAL for Distributed Authority mode - without this flag,
        /// players cannot grab blocks that are owned by another client.
        /// </summary>
        [MenuItem("BlockBattle/Network/Enable Ownership Transfer on Blocks")]
        public static void EnableOwnershipTransfer()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { BlockPrefabsPath });
            int updatedCount = 0;
            
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                if (!Path.GetFileName(path).StartsWith("Block_"))
                    continue;
                
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                
                var networkObject = prefab.GetComponent<NetworkObject>();
                if (networkObject == null) continue;
                
                // Open prefab for editing
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                var prefabNetworkObject = prefabRoot.GetComponent<NetworkObject>();
                
                if (prefabNetworkObject != null)
                {
                    // Use SerializedObject to set the ownership flags
                    // This is more reliable than reflection for Unity serialized data
                    var serializedObject = new SerializedObject(prefabNetworkObject);
                    var ownershipProperty = serializedObject.FindProperty("Ownership");
                    
                    if (ownershipProperty != null)
                    {
                        // OwnershipFlags in Netcode:
                        // Distributable = 1 (bit 0)
                        // Transferable = 2 (bit 1)
                        // We need both: 1 | 2 = 3
                        int currentValue = ownershipProperty.intValue;
                        int requiredValue = 3; // Distributable | Transferable
                        
                        if (currentValue != requiredValue)
                        {
                            ownershipProperty.intValue = requiredValue;
                            serializedObject.ApplyModifiedPropertiesWithoutUndo();
                            
                            Debug.Log($"Enabled ownership transfer on {prefab.name} (flags: {currentValue} -> {requiredValue})");
                            
                            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                            updatedCount++;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find Ownership property on {prefab.name}");
                    }
                }
                
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            if (updatedCount > 0)
            {
                EditorUtility.DisplayDialog("Ownership Transfer Enabled", 
                    $"Enabled ownership transfer on {updatedCount} block prefabs.\n\n" +
                    "This allows players to grab blocks in multiplayer.", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Changes", 
                    "All block prefabs already have ownership transfer enabled.", 
                    "OK");
            }
        }
    }
}
