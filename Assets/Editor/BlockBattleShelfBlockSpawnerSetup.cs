using UnityEngine;
using UnityEditor;
using BlockBattle;

namespace BlockBattleEditor
{
    /// <summary>
    /// Editor utility to set up the ShelfBlockSpawner component on the shelf in BlockBattleScene.
    /// Automates the migration from the old BlockSpawner + ShelfLogic_TwoDoors setup.
    /// </summary>
    public class BlockBattleShelfBlockSpawnerSetup : EditorWindow
    {
        private const string MENU_PATH = "BlockBattle/Setup Shelf Block Spawner";
        
        // Scene object names (German names from the actual scene)
        private const string SHELF_ROOT_NAME = "Shelf";
        private const string SHELF_TRIGGER_NAME = "Regal";
        private const string LEFT_DOOR_NAME = "Tür_links";
        private const string RIGHT_DOOR_NAME = "Tür_Rechts";
        private const string EJECTION_DIRECTION_NAME = "Schuss_Richtung";
        private const string SPAWN_ANCHOR_NAME = "SpawnAnchor";
        
        // Prefab paths
        private const string CUBE_PREFAB_PATH = "Assets/BlockBattle/Prefabs/Blocks/Block_Cube.prefab";
        private const string CYLINDER_PREFAB_PATH = "Assets/BlockBattle/Prefabs/Blocks/Block_Cylinder.prefab";
        private const string TRIANGLE_PREFAB_PATH = "Assets/BlockBattle/Prefabs/Blocks/Block_Triangle.prefab";
        private const string RECTANGLE_PREFAB_PATH = "Assets/BlockBattle/Prefabs/Blocks/Block_Rectangle.prefab";
        private const string ARCH_PREFAB_PATH = "Assets/BlockBattle/Prefabs/Blocks/Block_Arch.prefab";
        private const string BIG_TRIANGLE_PREFAB_PATH = "Assets/BlockBattle/Prefabs/Blocks/Block_BigTriangle.prefab";

        [MenuItem(MENU_PATH)]
        public static void ShowWindow()
        {
            GetWindow<BlockBattleShelfBlockSpawnerSetup>("Shelf Spawner Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Shelf Block Spawner Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "This tool will:\n" +
                "1. Find the 'Regal' GameObject (shelf trigger)\n" +
                "2. Add ShelfBlockSpawner component\n" +
                "3. Auto-assign door references (Tür_links, Tür_Rechts)\n" +
                "4. Auto-assign ejection direction (Schuss_Richtung)\n" +
                "5. Create SpawnAnchor if needed\n" +
                "6. Assign all block prefabs\n" +
                "7. Copy settings from old ShelfLogic_TwoDoors\n" +
                "8. Update LevelManager reference",
                MessageType.Info);
            
            EditorGUILayout.Space();

            // Show current scene status
            ShowSceneStatus();

            EditorGUILayout.Space();

            if (GUILayout.Button("Setup ShelfBlockSpawner", GUILayout.Height(40)))
            {
                SetupShelfBlockSpawner();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create SpawnAnchor Only", GUILayout.Height(25)))
            {
                CreateSpawnAnchorOnly();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Scene Hierarchy Expected:\n\n" +
                "Shelf\n" +
                "└── Regal (trigger collider + ShelfBlockSpawner)\n" +
                "    ├── Tür_links (left door with HingeJoint)\n" +
                "    ├── Tür_Rechts (right door with HingeJoint)\n" +
                "    ├── Schuss_Richtung (ejection direction)\n" +
                "    └── SpawnAnchor (spawn position - created if missing)",
                MessageType.None);
        }

        private void ShowSceneStatus()
        {
            EditorGUILayout.LabelField("Scene Status:", EditorStyles.boldLabel);
            
            // Find objects
            GameObject shelfRoot = GameObject.Find(SHELF_ROOT_NAME);
            GameObject regal = FindChildByName(shelfRoot, SHELF_TRIGGER_NAME);
            GameObject leftDoor = FindChildByName(regal, LEFT_DOOR_NAME);
            GameObject rightDoor = FindChildByName(regal, RIGHT_DOOR_NAME);
            GameObject ejectionDir = FindChildByName(regal, EJECTION_DIRECTION_NAME);
            GameObject spawnAnchor = FindChildByName(regal, SPAWN_ANCHOR_NAME);

            // Show status
            ShowObjectStatus("Shelf (root)", shelfRoot);
            ShowObjectStatus("Regal (trigger)", regal);
            ShowObjectStatus("Tür_links (left door)", leftDoor);
            ShowObjectStatus("Tür_Rechts (right door)", rightDoor);
            ShowObjectStatus("Schuss_Richtung (ejection)", ejectionDir);
            ShowObjectStatus("SpawnAnchor", spawnAnchor);

            // Check for existing components
            if (regal != null)
            {
                ShelfBlockSpawner newSpawner = regal.GetComponent<ShelfBlockSpawner>();
                var oldLogic = regal.GetComponent("ShelfLogic_TwoDoors");
                
                EditorGUILayout.Space();
                ShowComponentStatus("ShelfBlockSpawner (new)", newSpawner != null);
                ShowComponentStatus("ShelfLogic_TwoDoors (old)", oldLogic != null);
            }
        }

        private void ShowObjectStatus(string name, GameObject obj)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, GUILayout.Width(200));
            if (obj != null)
            {
                EditorGUILayout.LabelField("✓ Found", EditorStyles.boldLabel);
            }
            else
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("✗ Not Found", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ShowComponentStatus(string name, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, GUILayout.Width(200));
            if (exists)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("✓ Attached", EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField("✗ Not Attached", EditorStyles.boldLabel);
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void SetupShelfBlockSpawner()
        {
            // Find shelf root
            GameObject shelfRoot = GameObject.Find(SHELF_ROOT_NAME);
            if (shelfRoot == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find '{SHELF_ROOT_NAME}' GameObject in scene.", "OK");
                return;
            }

            // Find Regal (the trigger object)
            GameObject regal = FindChildByName(shelfRoot, SHELF_TRIGGER_NAME);
            if (regal == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find '{SHELF_TRIGGER_NAME}' child under '{SHELF_ROOT_NAME}'.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(regal, "Setup ShelfBlockSpawner");

            // Add or get ShelfBlockSpawner component
            ShelfBlockSpawner spawner = regal.GetComponent<ShelfBlockSpawner>();
            if (spawner == null)
            {
                spawner = Undo.AddComponent<ShelfBlockSpawner>(regal);
                Debug.Log("ShelfBlockSpawnerSetup: Added ShelfBlockSpawner component to 'Regal'");
            }

            // Find and assign doors
            GameObject leftDoorObj = FindChildByName(regal, LEFT_DOOR_NAME);
            GameObject rightDoorObj = FindChildByName(regal, RIGHT_DOOR_NAME);

            if (leftDoorObj != null)
            {
                HingeJoint leftHinge = leftDoorObj.GetComponent<HingeJoint>();
                if (leftHinge != null)
                {
                    SetPrivateField(spawner, "m_LeftDoor", leftHinge);
                    Debug.Log("ShelfBlockSpawnerSetup: Assigned left door HingeJoint");
                }
            }

            if (rightDoorObj != null)
            {
                HingeJoint rightHinge = rightDoorObj.GetComponent<HingeJoint>();
                if (rightHinge != null)
                {
                    SetPrivateField(spawner, "m_RightDoor", rightHinge);
                    Debug.Log("ShelfBlockSpawnerSetup: Assigned right door HingeJoint");
                }
            }

            // Find and assign ejection direction
            GameObject ejectionDirObj = FindChildByName(regal, EJECTION_DIRECTION_NAME);
            if (ejectionDirObj != null)
            {
                SetPrivateField(spawner, "m_EjectionDirection", ejectionDirObj.transform);
                Debug.Log("ShelfBlockSpawnerSetup: Assigned ejection direction");
            }

            // Create or find SpawnAnchor
            Transform spawnAnchor = regal.transform.Find(SPAWN_ANCHOR_NAME);
            if (spawnAnchor == null)
            {
                GameObject anchorObj = new GameObject(SPAWN_ANCHOR_NAME);
                Undo.RegisterCreatedObjectUndo(anchorObj, "Create SpawnAnchor");
                anchorObj.transform.SetParent(regal.transform);
                
                // Position in center of shelf interior (adjust based on shelf dimensions)
                // Based on the trigger collider center: (0.8255397, 1.1007739, -1.409853)
                anchorObj.transform.localPosition = new Vector3(0.8f, 1.1f, -1.4f);
                anchorObj.transform.localRotation = Quaternion.identity;
                
                spawnAnchor = anchorObj.transform;
                Debug.Log("ShelfBlockSpawnerSetup: Created SpawnAnchor at shelf interior center");
            }
            SetPrivateField(spawner, "m_SpawnAnchor", spawnAnchor);

            // Load and assign prefabs
            AssignBlockPrefabs(spawner);

            // Copy settings from old ShelfLogic_TwoDoors if present
            CopySettingsFromOldComponent(regal, spawner);

            // Update LevelManager reference
            UpdateLevelManagerReference(spawner);

            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(regal);

            Debug.Log("ShelfBlockSpawnerSetup: Setup complete!");
            EditorUtility.DisplayDialog("Success", 
                "ShelfBlockSpawner has been set up!\n\n" +
                "Next steps:\n" +
                "1. Assign a BlockSpawnConfiguration\n" +
                "2. Optionally disable old ShelfLogic_TwoDoors\n" +
                "3. Test by calling SpawnBlocks() from LevelManager",
                "OK");
        }

        private void CreateSpawnAnchorOnly()
        {
            GameObject shelfRoot = GameObject.Find(SHELF_ROOT_NAME);
            if (shelfRoot == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find '{SHELF_ROOT_NAME}' GameObject.", "OK");
                return;
            }

            GameObject regal = FindChildByName(shelfRoot, SHELF_TRIGGER_NAME);
            if (regal == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find '{SHELF_TRIGGER_NAME}' child.", "OK");
                return;
            }

            Transform existing = regal.transform.Find(SPAWN_ANCHOR_NAME);
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Info", "SpawnAnchor already exists.", "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            GameObject anchorObj = new GameObject(SPAWN_ANCHOR_NAME);
            Undo.RegisterCreatedObjectUndo(anchorObj, "Create SpawnAnchor");
            anchorObj.transform.SetParent(regal.transform);
            anchorObj.transform.localPosition = new Vector3(0.8f, 1.1f, -1.4f);
            anchorObj.transform.localRotation = Quaternion.identity;

            Selection.activeGameObject = anchorObj;
            Debug.Log("ShelfBlockSpawnerSetup: Created SpawnAnchor");
        }

        private void AssignBlockPrefabs(ShelfBlockSpawner spawner)
        {
            GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CUBE_PREFAB_PATH);
            GameObject cylinderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CYLINDER_PREFAB_PATH);
            GameObject trianglePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TRIANGLE_PREFAB_PATH);
            GameObject rectanglePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RECTANGLE_PREFAB_PATH);
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ARCH_PREFAB_PATH);
            GameObject bigTrianglePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BIG_TRIANGLE_PREFAB_PATH);

            SetPrivateField(spawner, "m_CubeBlockPrefab", cubePrefab);
            SetPrivateField(spawner, "m_CylinderBlockPrefab", cylinderPrefab);
            SetPrivateField(spawner, "m_TriangleBlockPrefab", trianglePrefab);
            SetPrivateField(spawner, "m_RectangleBlockPrefab", rectanglePrefab);
            SetPrivateField(spawner, "m_ArchBlockPrefab", archPrefab);
            SetPrivateField(spawner, "m_BigTriangleBlockPrefab", bigTrianglePrefab);

            int assigned = 0;
            if (cubePrefab != null) assigned++;
            if (cylinderPrefab != null) assigned++;
            if (trianglePrefab != null) assigned++;
            if (rectanglePrefab != null) assigned++;
            if (archPrefab != null) assigned++;
            if (bigTrianglePrefab != null) assigned++;

            Debug.Log($"ShelfBlockSpawnerSetup: Assigned {assigned}/6 block prefabs");
        }

        private void CopySettingsFromOldComponent(GameObject regal, ShelfBlockSpawner spawner)
        {
            // Try to find the old ShelfLogic_TwoDoors component
            var oldComponents = regal.GetComponents<MonoBehaviour>();
            MonoBehaviour oldLogic = null;
            
            foreach (var comp in oldComponents)
            {
                if (comp != null && comp.GetType().Name == "ShelfLogic_TwoDoors")
                {
                    oldLogic = comp;
                    break;
                }
            }

            if (oldLogic == null)
            {
                Debug.Log("ShelfBlockSpawnerSetup: No old ShelfLogic_TwoDoors found to copy settings from");
                return;
            }

            // Use reflection to copy values
            System.Type oldType = oldLogic.GetType();
            
            // Copy ejection settings
            CopyFieldValue(oldType, oldLogic, spawner, "ejectionForce", "m_EjectionForce");
            CopyFieldValue(oldType, oldLogic, spawner, "spreadAmount", "m_SpreadAmount");
            CopyFieldValue(oldType, oldLogic, spawner, "tumbleForce", "m_TumbleForce");
            CopyFieldValue(oldType, oldLogic, spawner, "doorKickForce", "m_DoorKickForce");
            CopyFieldValue(oldType, oldLogic, spawner, "triggerAngle", "m_TriggerAngle");
            CopyFieldValue(oldType, oldLogic, spawner, "resetAngle", "m_ResetAngle");

            Debug.Log("ShelfBlockSpawnerSetup: Copied settings from old ShelfLogic_TwoDoors");
        }

        private void CopyFieldValue(System.Type sourceType, object source, object target, string sourceFieldName, string targetFieldName)
        {
            var sourceField = sourceType.GetField(sourceFieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (sourceField == null) return;

            object value = sourceField.GetValue(source);
            SetPrivateField(target, targetFieldName, value);
        }

        private void UpdateLevelManagerReference(ShelfBlockSpawner spawner)
        {
            LevelManager levelManager = Object.FindAnyObjectByType<LevelManager>();
            if (levelManager == null)
            {
                Debug.Log("ShelfBlockSpawnerSetup: No LevelManager found in scene");
                return;
            }

            // Update the m_ShelfSpawner reference in LevelManager
            Undo.RecordObject(levelManager, "Update LevelManager ShelfSpawner Reference");
            SetPrivateField(levelManager, "m_ShelfSpawner", spawner);
            EditorUtility.SetDirty(levelManager);
            
            Debug.Log("ShelfBlockSpawnerSetup: Updated LevelManager.m_ShelfSpawner reference");
        }

        private GameObject FindChildByName(GameObject parent, string name)
        {
            if (parent == null) return null;

            // Direct child search
            Transform child = parent.transform.Find(name);
            if (child != null) return child.gameObject;

            // Recursive search
            foreach (Transform t in parent.transform)
            {
                GameObject found = FindChildByName(t.gameObject, name);
                if (found != null) return found;
            }

            return null;
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Debug.LogWarning($"ShelfBlockSpawnerSetup: Could not find field '{fieldName}'");
            }
        }
    }
}
