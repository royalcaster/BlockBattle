using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Automated setup for BlockBattle multiplayer scene.
    /// Duplicates workspace objects and configures all network components.
    /// </summary>
    public class BlockBattleMultiplayerSceneSetup : EditorWindow
    {
        // Configuration
        private float _workspaceSpacing = 4f;
        private bool _rotatePlayer2 = true;
        private Vector3 _player2Offset = new Vector3(4f, 0f, 0f);
        
        // Found objects
        private GameObject _existingTable;
        private GameObject _existingShelf;
        private GameObject _existingBuildZone;
        private GameObject _existingSlingshot;
        private GameObject _existingReferenceSpawner;
        private GameObject _existingBuildValidator;
        private GameObject _existingDestructionManager;
        private GameObject _existingLevelManager;
        private GameObject _networkManager;

        [MenuItem("BlockBattle/Setup Multiplayer Scene")]
        public static void ShowWindow()
        {
            var window = GetWindow<BlockBattleMultiplayerSceneSetup>("Multiplayer Setup");
            window.minSize = new Vector2(450, 600);
            window.FindExistingObjects();
        }

        private void OnGUI()
        {
            // Wrap in try-catch to prevent error spam
            try
            {
                DrawGUIContent();
            }
            catch (System.Exception)
            {
                // Silently ignore serialization errors during GUI
            }
        }

        private void DrawGUIContent()
        {
            EditorGUILayout.LabelField("BlockBattle Multiplayer Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This tool will:\n" +
                "1. Create Player 1 & Player 2 workspaces\n" +
                "2. Duplicate and configure all gameplay objects\n" +
                "3. Set up PlayerWorkspaceManager & NetworkedLevelManager\n" +
                "4. Configure cross-references (slingshots target opponent)", 
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Configuration section
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            _workspaceSpacing = EditorGUILayout.FloatField("Workspace Spacing (m)", _workspaceSpacing);
            _rotatePlayer2 = EditorGUILayout.Toggle("Rotate Player 2 (180°)", _rotatePlayer2);
            _player2Offset = EditorGUILayout.Vector3Field("Player 2 Offset", _player2Offset);

            EditorGUILayout.Space(10);

            // Found objects section
            EditorGUILayout.LabelField("Detected Scene Objects", EditorStyles.boldLabel);
            
            DrawObjectField("Table", ref _existingTable);
            DrawObjectField("Shelf (ShelfBlockSpawner)", ref _existingShelf);
            DrawObjectField("Build Zone", ref _existingBuildZone);
            DrawObjectField("Slingshot (VRSlingshot)", ref _existingSlingshot);
            DrawObjectField("Reference Spawner", ref _existingReferenceSpawner);
            DrawObjectField("Build Validator", ref _existingBuildValidator);
            DrawObjectField("Destruction Manager", ref _existingDestructionManager);
            DrawObjectField("Level Manager", ref _existingLevelManager);
            DrawObjectField("Network Manager", ref _networkManager);

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Re-scan Scene"))
            {
                FindExistingObjects();
            }

            EditorGUILayout.Space(15);

            // Validation
            bool canSetup = ValidateSetup();

            EditorGUI.BeginDisabledGroup(!canSetup);
            
            GUI.backgroundColor = canSetup ? Color.green : Color.gray;
            if (GUILayout.Button("Setup Multiplayer Scene", GUILayout.Height(40)))
            {
                SetupMultiplayerScene();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUI.EndDisabledGroup();

            if (!canSetup)
            {
                EditorGUILayout.HelpBox(
                    "Please ensure all required objects are found or assigned.\n" +
                    "At minimum: Table, Shelf, Build Zone, and Slingshot are required.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Network prefabs section
            EditorGUILayout.LabelField("Network Prefabs", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Prefabs to NetworkManager"))
            {
                AddPrefabsToNetworkManager();
            }
        }

        private void DrawObjectField(string label, ref GameObject obj)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Status indicator
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
            statusStyle.normal.textColor = obj != null ? Color.green : Color.red;
            EditorGUILayout.LabelField(obj != null ? "✓" : "✗", statusStyle, GUILayout.Width(20));
            
            obj = (GameObject)EditorGUILayout.ObjectField(label, obj, typeof(GameObject), true);
            
            EditorGUILayout.EndHorizontal();
        }

        private void FindExistingObjects()
        {
            // Find by component types
            var shelfSpawner = FindObjectOfType<ShelfBlockSpawner>();
            if (shelfSpawner != null) _existingShelf = shelfSpawner.gameObject;

            var buildZone = FindObjectOfType<BuildZone>();
            if (buildZone != null) _existingBuildZone = buildZone.gameObject;

            var slingshot = FindObjectOfType<VRSlingshot>();
            if (slingshot != null) _existingSlingshot = slingshot.gameObject;

            var refSpawner = FindObjectOfType<ReferenceStructureSpawner>();
            if (refSpawner != null) _existingReferenceSpawner = refSpawner.gameObject;

            var validator = FindObjectOfType<BuildValidator>();
            if (validator != null) _existingBuildValidator = validator.gameObject;

            var destructionMgr = FindObjectOfType<DestructionPhaseManager>();
            if (destructionMgr != null) _existingDestructionManager = destructionMgr.gameObject;

            var levelMgr = FindObjectOfType<LevelManager>();
            if (levelMgr != null) _existingLevelManager = levelMgr.gameObject;

            // Find table by name (common naming conventions)
            _existingTable = GameObject.Find("Table") ?? 
                            GameObject.Find("BuildTable") ?? 
                            GameObject.Find("WorkTable");

            // If still not found, try to find parent of build zone
            if (_existingTable == null && _existingBuildZone != null)
            {
                Transform parent = _existingBuildZone.transform.parent;
                while (parent != null)
                {
                    if (parent.name.ToLower().Contains("table"))
                    {
                        _existingTable = parent.gameObject;
                        break;
                    }
                    parent = parent.parent;
                }
            }

            // Find NetworkManager
            var networkManagerType = Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
            if (networkManagerType != null)
            {
                var networkMgr = FindObjectOfType(networkManagerType) as Component;
                if (networkMgr != null) _networkManager = networkMgr.gameObject;
            }

            Debug.Log("BlockBattle Multiplayer Setup: Scene scan complete.");
        }

        private bool ValidateSetup()
        {
            return _existingShelf != null && 
                   _existingBuildZone != null && 
                   _existingSlingshot != null;
        }

        private void SetupMultiplayerScene()
        {
            Undo.SetCurrentGroupName("Setup Multiplayer Scene");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                // Step 1: Create workspace containers
                GameObject player1Workspace = CreateWorkspaceContainer("Player1_Workspace", Vector3.zero, Quaternion.identity);
                GameObject player2Workspace = CreateWorkspaceContainer("Player2_Workspace", _player2Offset, 
                    _rotatePlayer2 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity);

                // Step 2: Move existing objects under Player 1 workspace
                MoveToWorkspace(player1Workspace, _existingTable, "Table");
                MoveToWorkspace(player1Workspace, _existingShelf, "Shelf");
                MoveToWorkspace(player1Workspace, _existingBuildZone, "BuildZone");
                MoveToWorkspace(player1Workspace, _existingSlingshot, "Slingshot");
                MoveToWorkspace(player1Workspace, _existingReferenceSpawner, "ReferenceSpawner");
                MoveToWorkspace(player1Workspace, _existingBuildValidator, "BuildValidator");

                // Step 3: Duplicate for Player 2
                GameObject p2Table = DuplicateForWorkspace(_existingTable, player2Workspace, "Table");
                GameObject p2Shelf = DuplicateForWorkspace(_existingShelf, player2Workspace, "Shelf");
                GameObject p2BuildZone = DuplicateForWorkspace(_existingBuildZone, player2Workspace, "BuildZone");
                GameObject p2Slingshot = DuplicateForWorkspace(_existingSlingshot, player2Workspace, "Slingshot");
                GameObject p2RefSpawner = DuplicateForWorkspace(_existingReferenceSpawner, player2Workspace, "ReferenceSpawner");
                GameObject p2Validator = DuplicateForWorkspace(_existingBuildValidator, player2Workspace, "BuildValidator");

                // Step 4: Create spawn points
                GameObject p1SpawnPoint = CreateSpawnPoint(player1Workspace, "PlayerSpawnPoint", new Vector3(0, 0, -1f));
                GameObject p1DestructionPoint = CreateSpawnPoint(player1Workspace, "DestructionPosition", new Vector3(0, 0, 2f));
                GameObject p2SpawnPoint = CreateSpawnPoint(player2Workspace, "PlayerSpawnPoint", new Vector3(0, 0, -1f));
                GameObject p2DestructionPoint = CreateSpawnPoint(player2Workspace, "DestructionPosition", new Vector3(0, 0, 2f));

                // Step 5: Add and configure PlayerWorkspace components
                var p1WorkspaceComp = ConfigurePlayerWorkspace(player1Workspace, 0, 
                    _existingTable, 
                    _existingBuildZone?.GetComponent<BuildZone>(),
                    _existingShelf?.GetComponent<ShelfBlockSpawner>(),
                    _existingReferenceSpawner?.GetComponent<ReferenceStructureSpawner>(),
                    _existingBuildValidator?.GetComponent<BuildValidator>(),
                    _existingSlingshot?.GetComponent<VRSlingshot>(),
                    p1SpawnPoint.transform,
                    p1DestructionPoint.transform);

                var p2WorkspaceComp = ConfigurePlayerWorkspace(player2Workspace, 1,
                    p2Table,
                    p2BuildZone?.GetComponent<BuildZone>(),
                    p2Shelf?.GetComponent<ShelfBlockSpawner>(),
                    p2RefSpawner?.GetComponent<ReferenceStructureSpawner>(),
                    p2Validator?.GetComponent<BuildValidator>(),
                    p2Slingshot?.GetComponent<VRSlingshot>(),
                    p2SpawnPoint.transform,
                    p2DestructionPoint.transform);

                // Step 6: Set opponent workspace references
                SetOpponentWorkspaces(p1WorkspaceComp, p2WorkspaceComp);

                // Step 7: Configure slingshots to target opponents
                ConfigureSlingshotTargets(_existingSlingshot?.GetComponent<VRSlingshot>(), 0, 1);
                ConfigureSlingshotTargets(p2Slingshot?.GetComponent<VRSlingshot>(), 1, 0);

                // Step 8: Configure workspace indices on spawners and validators
                ConfigureWorkspaceIndices(_existingShelf?.GetComponent<ShelfBlockSpawner>(), 
                                         _existingBuildValidator?.GetComponent<BuildValidator>(), 0);
                ConfigureWorkspaceIndices(p2Shelf?.GetComponent<ShelfBlockSpawner>(),
                                         p2Validator?.GetComponent<BuildValidator>(), 1);

                // Step 9: Create NetworkedGameManager
                GameObject networkGameManager = CreateNetworkGameManager(p1WorkspaceComp, p2WorkspaceComp);

                // Step 10: Update existing LevelManager if present
                if (_existingLevelManager != null)
                {
                    Debug.Log("Note: Existing LevelManager found. In multiplayer, NetworkedLevelManager will take over.");
                }

                Undo.CollapseUndoOperations(undoGroup);

                // Mark scene dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                EditorUtility.DisplayDialog("Setup Complete", 
                    "Multiplayer scene setup is complete!\n\n" +
                    "Created:\n" +
                    "• Player1_Workspace with all components\n" +
                    "• Player2_Workspace (duplicated)\n" +
                    "• NetworkedGameManager\n\n" +
                    "Next steps:\n" +
                    "1. Click 'Add Prefabs to NetworkManager'\n" +
                    "2. Assign Level Configurations to NetworkedLevelManager\n" +
                    "3. Adjust positions if needed\n" +
                    "4. Test with Multiplayer Play Mode",
                    "OK");

                Debug.Log("BlockBattle: Multiplayer scene setup complete!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during multiplayer setup: {e.Message}\n{e.StackTrace}");
                Undo.CollapseUndoOperations(undoGroup);
                Undo.PerformUndo();
                EditorUtility.DisplayDialog("Setup Failed", 
                    $"An error occurred during setup:\n{e.Message}\n\nChanges have been reverted.",
                    "OK");
            }
        }

        private GameObject CreateWorkspaceContainer(string name, Vector3 position, Quaternion rotation)
        {
            GameObject workspace = new GameObject(name);
            workspace.transform.position = position;
            workspace.transform.rotation = rotation;
            Undo.RegisterCreatedObjectUndo(workspace, "Create Workspace");
            return workspace;
        }

        private void MoveToWorkspace(GameObject workspace, GameObject obj, string newName)
        {
            if (obj == null) return;
            
            Undo.SetTransformParent(obj.transform, workspace.transform, "Move to Workspace");
            obj.name = newName;
        }

        private GameObject DuplicateForWorkspace(GameObject original, GameObject workspace, string newName)
        {
            if (original == null) return null;

            GameObject duplicate = Instantiate(original, workspace.transform);
            duplicate.name = newName;
            
            // Calculate local position relative to workspace
            // The duplicate should be at the same relative position as the original
            Vector3 localPos = original.transform.localPosition;
            duplicate.transform.localPosition = localPos;
            duplicate.transform.localRotation = original.transform.localRotation;
            duplicate.transform.localScale = original.transform.localScale;

            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate for Workspace");
            
            return duplicate;
        }

        private GameObject CreateSpawnPoint(GameObject workspace, string name, Vector3 localPosition)
        {
            GameObject spawnPoint = new GameObject(name);
            spawnPoint.transform.SetParent(workspace.transform);
            spawnPoint.transform.localPosition = localPosition;
            spawnPoint.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(spawnPoint, "Create Spawn Point");
            return spawnPoint;
        }

        private Network.PlayerWorkspace ConfigurePlayerWorkspace(GameObject workspaceObj, int index,
            GameObject table, BuildZone buildZone, ShelfBlockSpawner shelf, 
            ReferenceStructureSpawner refSpawner, BuildValidator validator, VRSlingshot slingshot,
            Transform spawnPoint, Transform destructionPoint)
        {
            var playerWorkspaceType = Type.GetType("BlockBattle.Network.PlayerWorkspace, Assembly-CSharp");
            if (playerWorkspaceType == null)
            {
                Debug.LogError("PlayerWorkspace type not found!");
                return null;
            }

            var comp = workspaceObj.AddComponent(playerWorkspaceType) as Network.PlayerWorkspace;
            if (comp == null) return null;

            Undo.RegisterCreatedObjectUndo(comp, "Add PlayerWorkspace");

            // Use SerializedObject to set private serialized fields
            SerializedObject so = new SerializedObject(comp);
            
            so.FindProperty("_workspaceIndex").intValue = index;
            so.FindProperty("_playerSpawnPoint").objectReferenceValue = spawnPoint;
            so.FindProperty("_destructionPhasePosition").objectReferenceValue = destructionPoint;
            so.FindProperty("_table").objectReferenceValue = table;
            so.FindProperty("_buildZone").objectReferenceValue = buildZone;
            so.FindProperty("_shelfBlockSpawner").objectReferenceValue = shelf;
            so.FindProperty("_referenceStructureSpawner").objectReferenceValue = refSpawner;
            so.FindProperty("_buildValidator").objectReferenceValue = validator;
            so.FindProperty("_slingshot").objectReferenceValue = slingshot;
            
            so.ApplyModifiedProperties();

            return comp;
        }

        private void SetOpponentWorkspaces(Network.PlayerWorkspace p1, Network.PlayerWorkspace p2)
        {
            if (p1 == null || p2 == null) return;

            SerializedObject so1 = new SerializedObject(p1);
            so1.FindProperty("_opponentWorkspace").objectReferenceValue = p2;
            so1.ApplyModifiedProperties();

            SerializedObject so2 = new SerializedObject(p2);
            so2.FindProperty("_opponentWorkspace").objectReferenceValue = p1;
            so2.ApplyModifiedProperties();
        }

        private void ConfigureSlingshotTargets(VRSlingshot slingshot, int ownIndex, int targetIndex)
        {
            if (slingshot == null) return;

            SerializedObject so = new SerializedObject(slingshot);
            so.FindProperty("m_WorkspaceIndex").intValue = ownIndex;
            so.FindProperty("m_TargetWorkspaceIndex").intValue = targetIndex;
            so.ApplyModifiedProperties();
        }

        private void ConfigureWorkspaceIndices(ShelfBlockSpawner shelf, BuildValidator validator, int index)
        {
            if (shelf != null)
            {
                SerializedObject so = new SerializedObject(shelf);
                so.FindProperty("m_WorkspaceIndex").intValue = index;
                so.ApplyModifiedProperties();
            }

            if (validator != null)
            {
                SerializedObject so = new SerializedObject(validator);
                so.FindProperty("m_WorkspaceIndex").intValue = index;
                so.ApplyModifiedProperties();
            }
        }

        private GameObject CreateNetworkGameManager(Network.PlayerWorkspace p1, Network.PlayerWorkspace p2)
        {
            GameObject managerObj = new GameObject("NetworkedGameManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create NetworkedGameManager");

            // CRITICAL: Add NetworkObject component - required for NetworkBehaviours to work!
            var networkObjectType = Type.GetType("Unity.Netcode.NetworkObject, Unity.Netcode.Runtime");
            if (networkObjectType != null)
            {
                managerObj.AddComponent(networkObjectType);
                Debug.Log("BlockBattleMultiplayerSceneSetup: Added NetworkObject to NetworkedGameManager");
            }
            else
            {
                Debug.LogError("BlockBattleMultiplayerSceneSetup: Could not find NetworkObject type! Make sure Netcode for GameObjects is installed.");
            }

            // Add PlayerWorkspaceManager
            var workspaceMgrType = Type.GetType("BlockBattle.Network.PlayerWorkspaceManager, Assembly-CSharp");
            if (workspaceMgrType != null)
            {
                var workspaceMgr = managerObj.AddComponent(workspaceMgrType);
                
                SerializedObject so = new SerializedObject(workspaceMgr);
                var workspacesArray = so.FindProperty("_workspaces");
                workspacesArray.arraySize = 2;
                workspacesArray.GetArrayElementAtIndex(0).objectReferenceValue = p1;
                workspacesArray.GetArrayElementAtIndex(1).objectReferenceValue = p2;
                so.FindProperty("_maxPlayers").intValue = 2;
                so.ApplyModifiedProperties();
            }

            // Add NetworkedLevelManager
            var levelMgrType = Type.GetType("BlockBattle.Network.NetworkedLevelManager, Assembly-CSharp");
            if (levelMgrType != null)
            {
                var levelMgr = managerObj.AddComponent(levelMgrType);
                
                // Copy level configurations from existing LevelManager if present
                if (_existingLevelManager != null)
                {
                    var existingLevelMgr = _existingLevelManager.GetComponent<LevelManager>();
                    if (existingLevelMgr != null)
                    {
                        SerializedObject oldSo = new SerializedObject(existingLevelMgr);
                        SerializedObject newSo = new SerializedObject(levelMgr);
                        
                        var oldConfigs = oldSo.FindProperty("m_LevelConfigurations");
                        var newConfigs = newSo.FindProperty("_levelConfigurations");
                        
                        if (oldConfigs != null && newConfigs != null)
                        {
                            newConfigs.arraySize = oldConfigs.arraySize;
                            for (int i = 0; i < oldConfigs.arraySize; i++)
                            {
                                newConfigs.GetArrayElementAtIndex(i).objectReferenceValue = 
                                    oldConfigs.GetArrayElementAtIndex(i).objectReferenceValue;
                            }
                        }
                        
                        newSo.ApplyModifiedProperties();
                    }
                }
            }

            return managerObj;
        }

        private void AddPrefabsToNetworkManager()
        {
            if (_networkManager == null)
            {
                EditorUtility.DisplayDialog("NetworkManager Not Found",
                    "Please ensure a NetworkManager is in the scene and re-scan.",
                    "OK");
                return;
            }

            string[] prefabPaths = new string[]
            {
                "Assets/BlockBattle/Prefabs/Blocks/Block_Cube.prefab",
                "Assets/BlockBattle/Prefabs/Blocks/Block_Cylinder.prefab",
                "Assets/BlockBattle/Prefabs/Blocks/Block_Triangle.prefab",
                "Assets/BlockBattle/Prefabs/Blocks/Block_Rectangle.prefab",
                "Assets/BlockBattle/Prefabs/Blocks/Block_Arch.prefab",
                "Assets/BlockBattle/Prefabs/Blocks/Block_BigTriangle.prefab",
                "Assets/BlockBattle/Prefabs/BallProjectile.prefab"
            };

            // Get NetworkManager component
            var networkManagerType = Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
            if (networkManagerType == null)
            {
                EditorUtility.DisplayDialog("Netcode Not Found",
                    "Unity Netcode for GameObjects is not installed.",
                    "OK");
                return;
            }

            var networkMgr = _networkManager.GetComponent(networkManagerType);
            if (networkMgr == null) return;

            SerializedObject so = new SerializedObject(networkMgr);
            
            // Find NetworkPrefabs or NetworkConfig property
            var prefabsListProp = so.FindProperty("NetworkPrefabs");
            if (prefabsListProp == null)
            {
                prefabsListProp = so.FindProperty("m_NetworkPrefabs");
            }

            if (prefabsListProp == null)
            {
                // Try to find through NetworkConfig
                var configProp = so.FindProperty("NetworkConfig");
                if (configProp != null)
                {
                    var configObj = configProp.objectReferenceValue;
                    if (configObj != null)
                    {
                        SerializedObject configSo = new SerializedObject(configObj);
                        prefabsListProp = configSo.FindProperty("Prefabs") ?? configSo.FindProperty("NetworkPrefabs");
                        so = configSo;
                    }
                }
            }

            if (prefabsListProp == null)
            {
                EditorUtility.DisplayDialog("Cannot Find Prefab List",
                    "Could not find NetworkPrefabs list on NetworkManager.\n\n" +
                    "Please manually add the prefabs:\n" +
                    "1. Select NetworkManager\n" +
                    "2. Find 'Network Prefabs' or similar\n" +
                    "3. Add all block prefabs and BallProjectile",
                    "OK");
                return;
            }

            int added = 0;
            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // Check if already in list
                bool exists = false;
                for (int i = 0; i < prefabsListProp.arraySize; i++)
                {
                    var element = prefabsListProp.GetArrayElementAtIndex(i);
                    var prefabProp = element.FindPropertyRelative("Prefab") ?? element;
                    if (prefabProp.objectReferenceValue == prefab)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    int newIndex = prefabsListProp.arraySize;
                    prefabsListProp.InsertArrayElementAtIndex(newIndex);
                    var newElement = prefabsListProp.GetArrayElementAtIndex(newIndex);
                    var prefabProp = newElement.FindPropertyRelative("Prefab") ?? newElement;
                    prefabProp.objectReferenceValue = prefab;
                    added++;
                }
            }

            so.ApplyModifiedProperties();

            EditorUtility.DisplayDialog("Prefabs Added",
                $"Added {added} prefabs to NetworkManager.\n" +
                $"({prefabPaths.Length - added} were already present)",
                "OK");
        }
    }
}
