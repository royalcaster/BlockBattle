using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor tool to set up the slingshot destruction phase in the scene.
    /// Creates slingshot, ball projectile prefab, teleport positions, and wires up references.
    /// </summary>
    public class SlingshotSetup : EditorWindow
    {
        private Transform m_Table;
        private float m_ShootingDistance = 3f;
        private float m_SlingshotHeight = 0.9f; // Lower for comfortable reach

        [MenuItem("BlockBattle/Setup Slingshot Destruction Phase")]
        public static void ShowWindow()
        {
            GetWindow<SlingshotSetup>("Slingshot Setup");
        }

        [MenuItem("BlockBattle/Remove Teleport Position Indicators")]
        public static void RemoveTeleportIndicators()
        {
            // Find and remove the visual indicators from teleport positions
            string[] positionNames = { "DestructionTeleportPosition", "BuildingTeleportPosition" };
            int removed = 0;

            foreach (string posName in positionNames)
            {
                GameObject pos = GameObject.Find(posName);
                if (pos != null)
                {
                    // Find and destroy indicator children
                    Transform indicator = pos.transform.Find("PositionIndicator");
                    if (indicator != null)
                    {
                        DestroyImmediate(indicator.gameObject);
                        removed++;
                    }

                    Transform arrow = pos.transform.Find("DirectionArrow");
                    if (arrow != null)
                    {
                        DestroyImmediate(arrow.gameObject);
                        removed++;
                    }
                }
            }

            if (removed > 0)
            {
                Debug.Log($"Removed {removed} teleport position indicators.");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            else
            {
                Debug.Log("No teleport position indicators found to remove.");
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Slingshot Destruction Phase Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool creates the slingshot, ball projectile prefab, teleport positions, " +
                "and wires up all references for the destruction phase.",
                MessageType.Info);

            EditorGUILayout.Space();

            m_Table = EditorGUILayout.ObjectField("Table/Build Zone", m_Table, typeof(Transform), true) as Transform;
            m_ShootingDistance = EditorGUILayout.FloatField("Shooting Distance (m)", m_ShootingDistance);
            m_SlingshotHeight = EditorGUILayout.FloatField("Slingshot Height (m)", m_SlingshotHeight);

            EditorGUILayout.Space();

            if (GUILayout.Button("Setup Everything", GUILayout.Height(30)))
            {
                SetupSlingshotSystem();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Individual Setup Steps:", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Create Ball Projectile Prefab"))
            {
                CreateBallProjectilePrefab();
            }

            if (GUILayout.Button("2. Create Slingshot"))
            {
                CreateSlingshot();
            }

            if (GUILayout.Button("3. Create Teleport Positions"))
            {
                CreateTeleportPositions();
            }

            if (GUILayout.Button("4. Create Destruction Manager"))
            {
                CreateDestructionManager();
            }

            if (GUILayout.Button("5. Wire Up LevelManager References"))
            {
                WireLevelManagerReferences();
            }
        }

        /// <summary>
        /// Performs the complete setup.
        /// </summary>
        private void SetupSlingshotSystem()
        {
            // Find table if not assigned
            if (m_Table == null)
            {
                GameObject tableObj = GameObject.Find("Table");
                if (tableObj != null)
                    m_Table = tableObj.transform;
            }

            if (m_Table == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Table/Build Zone transform.", "OK");
                return;
            }

            CreateBallProjectilePrefab();
            CreateSlingshot();
            CreateTeleportPositions();
            CreateDestructionManager();
            WireLevelManagerReferences();

            EditorUtility.DisplayDialog("Setup Complete",
                "Slingshot destruction phase has been set up!\n\n" +
                "You may need to:\n" +
                "1. Adjust slingshot and teleport positions\n" +
                "2. Assign the ball prefab to the slingshot\n" +
                "3. Test the destruction phase flow",
                "OK");
        }

        /// <summary>
        /// Creates the ball projectile prefab.
        /// </summary>
        private void CreateBallProjectilePrefab()
        {
            // Check if prefab already exists
            string prefabPath = "Assets/BlockBattle/Prefabs/BallProjectile.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log("BallProjectile prefab already exists at " + prefabPath);
                return;
            }

            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/BlockBattle/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/BlockBattle", "Prefabs");
            }

            // Create ball - same size as the slingshot pouch (5cm diameter)
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "BallProjectile";
            ball.transform.localScale = Vector3.one * 0.05f; // 5cm diameter, matches pouch

            // Add Rigidbody
            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Add BallProjectile component
            ball.AddComponent<BallProjectile>();

            // Add trail renderer - properly configured
            TrailRenderer trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.4f;
            trail.startWidth = 0.03f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.02f;
            
            // Create trail material
            Material trailMat = new Material(Shader.Find("Sprites/Default"));
            trail.material = trailMat;
            
            // Orange gradient
            trail.startColor = new Color(1f, 0.5f, 0.2f, 0.9f);
            trail.endColor = new Color(1f, 0.3f, 0.1f, 0f);
            
            // Clear any initial trail points
            trail.Clear();

            // Set material color
            Renderer renderer = ball.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.8f, 0.3f, 0.2f);
                renderer.material = mat;

                // Save material
                AssetDatabase.CreateAsset(mat, "Assets/BlockBattle/Prefabs/BallProjectileMaterial.mat");
            }

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(ball, prefabPath);
            DestroyImmediate(ball);

            Debug.Log("Created BallProjectile prefab at " + prefabPath);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Creates the slingshot in the scene.
        /// </summary>
        private void CreateSlingshot()
        {
            // Check if slingshot already exists
            VRSlingshot existingSlingshot = FindObjectOfType<VRSlingshot>();
            if (existingSlingshot != null)
            {
                Debug.Log("Slingshot already exists in scene. Deleting and recreating...");
                DestroyImmediate(existingSlingshot.gameObject);
            }

            // Calculate position - lower height for comfortable use
            Vector3 slingshotPos = Vector3.zero;
            if (m_Table != null)
            {
                slingshotPos = m_Table.position - m_Table.forward * m_ShootingDistance;
                slingshotPos.y = m_SlingshotHeight; // Will be 0.9m by default now
            }

            // Create slingshot parent (this is the fixed frame)
            GameObject slingshot = new GameObject("Slingshot");
            slingshot.transform.position = slingshotPos;
            if (m_Table != null)
            {
                slingshot.transform.LookAt(new Vector3(m_Table.position.x, slingshotPos.y, m_Table.position.z));
            }

            // Add VRSlingshot component
            VRSlingshot slingshotComponent = slingshot.AddComponent<VRSlingshot>();

            // Create the Y-shaped frame (all static, no rigidbodies)
            // Main handle/stem
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Handle";
            handle.transform.SetParent(slingshot.transform);
            handle.transform.localPosition = new Vector3(0, -0.08f, 0);
            handle.transform.localScale = new Vector3(0.025f, 0.1f, 0.025f);
            // Remove collider - frame is not interactable
            DestroyImmediate(handle.GetComponent<Collider>());
            SetMaterial(handle, new Color(0.4f, 0.25f, 0.1f)); // Brown wood color

            // Create left fork (angled outward)
            GameObject leftFork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftFork.name = "LeftFork";
            leftFork.transform.SetParent(slingshot.transform);
            leftFork.transform.localPosition = new Vector3(-0.035f, 0.06f, 0);
            leftFork.transform.localRotation = Quaternion.Euler(0, 0, 20);
            leftFork.transform.localScale = new Vector3(0.012f, 0.06f, 0.012f);
            DestroyImmediate(leftFork.GetComponent<Collider>());
            SetMaterial(leftFork, new Color(0.4f, 0.25f, 0.1f));

            // Create left fork tip marker (at the top of the fork)
            GameObject leftTip = new GameObject("LeftForkTip");
            leftTip.transform.SetParent(slingshot.transform);
            // Position at the tip of the left fork
            leftTip.transform.localPosition = new Vector3(-0.055f, 0.115f, 0);

            // Create right fork (angled outward)
            GameObject rightFork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightFork.name = "RightFork";
            rightFork.transform.SetParent(slingshot.transform);
            rightFork.transform.localPosition = new Vector3(0.035f, 0.06f, 0);
            rightFork.transform.localRotation = Quaternion.Euler(0, 0, -20);
            rightFork.transform.localScale = new Vector3(0.012f, 0.06f, 0.012f);
            DestroyImmediate(rightFork.GetComponent<Collider>());
            SetMaterial(rightFork, new Color(0.4f, 0.25f, 0.1f));

            // Create right fork tip marker
            GameObject rightTip = new GameObject("RightForkTip");
            rightTip.transform.SetParent(slingshot.transform);
            rightTip.transform.localPosition = new Vector3(0.055f, 0.115f, 0);

            // Create pouch rest position (between the fork tips)
            GameObject pouchRest = new GameObject("PouchRestPosition");
            pouchRest.transform.SetParent(slingshot.transform);
            pouchRest.transform.localPosition = new Vector3(0, 0.1f, 0);

            // Create pouch (this is what the player grabs!)
            GameObject pouch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pouch.name = "Pouch";
            pouch.transform.SetParent(slingshot.transform);
            pouch.transform.localPosition = pouchRest.transform.localPosition;
            pouch.transform.localScale = Vector3.one * 0.05f; // Slightly larger for easier grabbing
            SetMaterial(pouch, new Color(0.3f, 0.2f, 0.15f)); // Dark leather color

            // The pouch needs a Rigidbody for XRGrabInteractable but should be kinematic
            Rigidbody pouchRb = pouch.AddComponent<Rigidbody>();
            pouchRb.isKinematic = true;
            pouchRb.useGravity = false;

            // XRGrabInteractable will be added by VRSlingshot.Start(), but we can pre-configure
            XRGrabInteractable pouchGrip = pouch.AddComponent<XRGrabInteractable>();
            pouchGrip.movementType = XRBaseInteractable.MovementType.Instantaneous;
            pouchGrip.trackPosition = false; // VRSlingshot handles position
            pouchGrip.trackRotation = false;
            pouchGrip.throwOnDetach = false;

            // Create rubber band line renderers
            GameObject leftBand = new GameObject("LeftBand");
            leftBand.transform.SetParent(slingshot.transform);
            LineRenderer leftLR = leftBand.AddComponent<LineRenderer>();
            SetupLineRenderer(leftLR);

            GameObject rightBand = new GameObject("RightBand");
            rightBand.transform.SetParent(slingshot.transform);
            LineRenderer rightLR = rightBand.AddComponent<LineRenderer>();
            SetupLineRenderer(rightLR);

            // Add colliders back to forks (for collision ignoring to work)
            // The projectile will ignore these colliders
            CapsuleCollider leftForkCol = leftFork.AddComponent<CapsuleCollider>();
            leftForkCol.radius = 0.015f;
            leftForkCol.height = 0.12f;
            leftForkCol.direction = 1; // Y-axis
            
            CapsuleCollider rightForkCol = rightFork.AddComponent<CapsuleCollider>();
            rightForkCol.radius = 0.015f;
            rightForkCol.height = 0.12f;
            rightForkCol.direction = 1; // Y-axis

            // Wire up serialized fields using SerializedObject
            SerializedObject so = new SerializedObject(slingshotComponent);
            so.FindProperty("m_Pouch").objectReferenceValue = pouch.transform;
            so.FindProperty("m_PouchRestPosition").objectReferenceValue = pouchRest.transform;
            so.FindProperty("m_LeftForkTip").objectReferenceValue = leftTip.transform;
            so.FindProperty("m_RightForkTip").objectReferenceValue = rightTip.transform;
            so.FindProperty("m_LeftBandRenderer").objectReferenceValue = leftLR;
            so.FindProperty("m_RightBandRenderer").objectReferenceValue = rightLR;

            // Set up the ignore colliders array
            SerializedProperty ignoreCollidersProperty = so.FindProperty("m_IgnoreColliders");
            if (ignoreCollidersProperty != null)
            {
                ignoreCollidersProperty.arraySize = 2;
                ignoreCollidersProperty.GetArrayElementAtIndex(0).objectReferenceValue = leftForkCol;
                ignoreCollidersProperty.GetArrayElementAtIndex(1).objectReferenceValue = rightForkCol;
            }

            // Assign ball prefab if it exists
            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BlockBattle/Prefabs/BallProjectile.prefab");
            if (ballPrefab != null)
            {
                so.FindProperty("m_ProjectilePrefab").objectReferenceValue = ballPrefab;
            }

            so.ApplyModifiedProperties();

            // Mark scene dirty
            EditorUtility.SetDirty(slingshot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(slingshot.scene);

            Debug.Log("Created Slingshot in scene at position: " + slingshotPos);
            Selection.activeGameObject = slingshot;
        }

        private void SetMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Try URP first, fall back to standard
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    mat.color = color;
                    renderer.material = mat;
                }
            }
        }

        private void SetupLineRenderer(LineRenderer lr)
        {
            lr.positionCount = 2;
            lr.startWidth = 0.015f;
            lr.endWidth = 0.015f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(0.6f, 0.4f, 0.2f);
            lr.endColor = new Color(0.6f, 0.4f, 0.2f);
            lr.useWorldSpace = true;
        }

        /// <summary>
        /// Creates the teleport position markers.
        /// </summary>
        private void CreateTeleportPositions()
        {
            // Find or create positions parent
            GameObject positionsParent = GameObject.Find("TeleportPositions");
            if (positionsParent == null)
            {
                positionsParent = new GameObject("TeleportPositions");
            }

            // Create destruction teleport position (where player shoots from)
            GameObject destructionPos = GameObject.Find("DestructionTeleportPosition");
            if (destructionPos == null)
            {
                destructionPos = new GameObject("DestructionTeleportPosition");
                destructionPos.transform.SetParent(positionsParent.transform);

                // Position near slingshot
                VRSlingshot slingshot = FindObjectOfType<VRSlingshot>();
                if (slingshot != null)
                {
                    destructionPos.transform.position = slingshot.transform.position + Vector3.back * 0.3f;
                    destructionPos.transform.position = new Vector3(
                        destructionPos.transform.position.x,
                        0f, // Floor level
                        destructionPos.transform.position.z
                    );
                    destructionPos.transform.LookAt(new Vector3(
                        slingshot.transform.position.x,
                        0f,
                        slingshot.transform.position.z
                    ) + slingshot.transform.forward * 5f);
                }
                else if (m_Table != null)
                {
                    destructionPos.transform.position = m_Table.position - m_Table.forward * m_ShootingDistance;
                    destructionPos.transform.position = new Vector3(
                        destructionPos.transform.position.x,
                        0f,
                        destructionPos.transform.position.z
                    );
                    destructionPos.transform.LookAt(m_Table.position);
                }

                // Add visual indicator (editor only)
                AddPositionGizmo(destructionPos, Color.red, "Destruction");
            }

            // Create building teleport position (where player builds)
            GameObject buildingPos = GameObject.Find("BuildingTeleportPosition");
            if (buildingPos == null)
            {
                buildingPos = new GameObject("BuildingTeleportPosition");
                buildingPos.transform.SetParent(positionsParent.transform);

                // Position near table
                if (m_Table != null)
                {
                    buildingPos.transform.position = m_Table.position + m_Table.forward * 1f;
                    buildingPos.transform.position = new Vector3(
                        buildingPos.transform.position.x,
                        0f,
                        buildingPos.transform.position.z
                    );
                    buildingPos.transform.LookAt(m_Table.position);
                }

                AddPositionGizmo(buildingPos, Color.green, "Building");
            }

            Debug.Log("Created teleport positions.");
            Selection.activeGameObject = positionsParent;
        }

        private void AddPositionGizmo(GameObject obj, Color color, string label)
        {
            // Don't add visual indicators anymore - they show up at runtime
            // The teleport positions are just empty GameObjects now
            // You can see them in the Scene view via their transform gizmos
            
            // If you want to visualize them in editor, add a TeleportPositionGizmo component
            // that uses OnDrawGizmos instead
        }

        /// <summary>
        /// Creates the DestructionPhaseManager in the scene.
        /// </summary>
        private void CreateDestructionManager()
        {
            // Check if already exists
            DestructionPhaseManager existing = FindObjectOfType<DestructionPhaseManager>();
            if (existing != null)
            {
                Debug.Log("DestructionPhaseManager already exists in scene.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create manager
            GameObject managerObj = new GameObject("DestructionPhaseManager");
            DestructionPhaseManager manager = managerObj.AddComponent<DestructionPhaseManager>();

            // Wire up references
            SerializedObject so = new SerializedObject(manager);

            VRSlingshot slingshot = FindObjectOfType<VRSlingshot>();
            if (slingshot != null)
            {
                so.FindProperty("m_Slingshot").objectReferenceValue = slingshot;
            }

            BuildZone buildZone = FindObjectOfType<BuildZone>();
            if (buildZone != null)
            {
                so.FindProperty("m_BuildZone").objectReferenceValue = buildZone;
            }

            GameObject shootingPos = GameObject.Find("DestructionTeleportPosition");
            if (shootingPos != null)
            {
                so.FindProperty("m_ShootingPosition").objectReferenceValue = shootingPos.transform;
            }

            so.ApplyModifiedProperties();

            Debug.Log("Created DestructionPhaseManager.");
            Selection.activeGameObject = managerObj;
        }

        /// <summary>
        /// Wires up the LevelManager references for the destruction phase.
        /// </summary>
        private void WireLevelManagerReferences()
        {
            LevelManager levelManager = FindObjectOfType<LevelManager>();
            if (levelManager == null)
            {
                Debug.LogWarning("LevelManager not found in scene!");
                return;
            }

            SerializedObject so = new SerializedObject(levelManager);

            // Wire destruction manager
            DestructionPhaseManager destructionManager = FindObjectOfType<DestructionPhaseManager>();
            if (destructionManager != null)
            {
                so.FindProperty("m_DestructionManager").objectReferenceValue = destructionManager;
            }

            // Wire XR setup
            BlockBattleXRSetup xrSetup = FindObjectOfType<BlockBattleXRSetup>();
            if (xrSetup != null)
            {
                so.FindProperty("m_XRSetup").objectReferenceValue = xrSetup;
            }

            // Wire teleport positions
            GameObject destructionPos = GameObject.Find("DestructionTeleportPosition");
            if (destructionPos != null)
            {
                so.FindProperty("m_DestructionTeleportPosition").objectReferenceValue = destructionPos.transform;
            }

            GameObject buildingPos = GameObject.Find("BuildingTeleportPosition");
            if (buildingPos != null)
            {
                so.FindProperty("m_BuildingTeleportPosition").objectReferenceValue = buildingPos.transform;
            }

            so.ApplyModifiedProperties();

            Debug.Log("Wired LevelManager references for destruction phase.");
            Selection.activeGameObject = levelManager.gameObject;
        }
    }
}
