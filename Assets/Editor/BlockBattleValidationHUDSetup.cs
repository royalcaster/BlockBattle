using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor tool to automatically set up the Validation HUD in the scene.
    /// Creates a World Space Canvas attached to the VR camera for proper VR rendering.
    /// </summary>
    public class BlockBattleValidationHUDSetup : EditorWindow
    {
        private const float LargeFontSize = 36f;
        private const float MediumFontSize = 28f;
        private const float SmallFontSize = 22f;
        
        // HUD positioning (relative to camera)
        private const float HUDDistance = 1.5f;      // Distance in front of camera
        private const float HUDHeightOffset = 0.6f;  // Height above camera center (top of view)
        private const float HUDScale = 0.001f;       // Scale for world space canvas

        [MenuItem("BlockBattle/Setup Validation HUD")]
        public static void ShowWindow()
        {
            GetWindow<BlockBattleValidationHUDSetup>("Validation HUD Setup");
        }

        [MenuItem("BlockBattle/Create Build Zone")]
        public static void CreateBuildZone()
        {
            // Check if one already exists
            BuildZone existingZone = FindObjectOfType<BuildZone>();
            if (existingZone != null)
            {
                Selection.activeGameObject = existingZone.gameObject;
                EditorGUIUtility.PingObject(existingZone.gameObject);
                Debug.Log("BuildZone already exists in scene. Selected it.");
                return;
            }

            // Find table to position the zone
            GameObject table = GameObject.Find("Table");
            Vector3 zonePosition = Vector3.zero;
            
            if (table != null)
            {
                // Position on the left side of the table (player build area)
                Bounds tableBounds = table.GetComponent<Renderer>()?.bounds ?? new Bounds(table.transform.position, Vector3.one);
                zonePosition = table.transform.position + new Vector3(-tableBounds.extents.x * 0.5f, tableBounds.extents.y + 0.01f, 0f);
            }

            // Create BuildZone GameObject
            GameObject zoneObj = new GameObject("BuildZone");
            BuildZone zone = zoneObj.AddComponent<BuildZone>();
            zoneObj.transform.position = zonePosition;

            // Configure default size
            SerializedObject serializedZone = new SerializedObject(zone);
            serializedZone.FindProperty("m_ZoneSize").vector3Value = new Vector3(0.5f, 0.6f, 0.5f);
            serializedZone.FindProperty("m_ShowVisualization").boolValue = true;
            serializedZone.ApplyModifiedProperties();

            Selection.activeGameObject = zoneObj;
            EditorGUIUtility.PingObject(zoneObj);

            string tableStatus = table != null ? $"Positioned on table at {zonePosition}" : "Positioned at origin - move to your table";
            Debug.Log($"BuildZone created! {tableStatus}");
        }

        [MenuItem("BlockBattle/Create Debug Visualizer")]
        public static void CreateDebugVisualizer()
        {
            // Check if one already exists
            ValidationDebugVisualizer existingVisualizer = FindObjectOfType<ValidationDebugVisualizer>();
            if (existingVisualizer != null)
            {
                Selection.activeGameObject = existingVisualizer.gameObject;
                EditorGUIUtility.PingObject(existingVisualizer.gameObject);
                Debug.Log("ValidationDebugVisualizer already exists in scene. Selected it.");
                return;
            }

            // Create new Visualizer GameObject
            GameObject visualizerObj = new GameObject("ValidationDebugVisualizer");
            ValidationDebugVisualizer visualizer = visualizerObj.AddComponent<ValidationDebugVisualizer>();

            // Try to find and assign references
            BuildValidator validator = FindObjectOfType<BuildValidator>();
            BuildZone buildZone = FindObjectOfType<BuildZone>();

            SerializedObject serializedVisualizer = new SerializedObject(visualizer);
            
            if (validator != null)
            {
                serializedVisualizer.FindProperty("m_BuildValidator").objectReferenceValue = validator;
            }
            
            if (buildZone != null)
            {
                serializedVisualizer.FindProperty("m_BuildZone").objectReferenceValue = buildZone;
            }
            
            serializedVisualizer.ApplyModifiedProperties();

            Selection.activeGameObject = visualizerObj;
            EditorGUIUtility.PingObject(visualizerObj);

            Debug.Log("ValidationDebugVisualizer created! Shows expected block positions in VR.");
        }

        [MenuItem("BlockBattle/Create Build Validator")]
        public static void CreateBuildValidator()
        {
            // Check if one already exists
            BuildValidator existingValidator = FindObjectOfType<BuildValidator>();
            if (existingValidator != null)
            {
                Selection.activeGameObject = existingValidator.gameObject;
                EditorGUIUtility.PingObject(existingValidator.gameObject);
                Debug.Log("BuildValidator already exists in scene. Selected it.");
                return;
            }

            // Create new BuildValidator GameObject
            GameObject validatorObj = new GameObject("BuildValidator");
            BuildValidator validator = validatorObj.AddComponent<BuildValidator>();

            // Try to find and assign references
            GameObject table = GameObject.Find("Table");
            ReferenceStructureSpawner spawner = FindObjectOfType<ReferenceStructureSpawner>();
            BuildZone buildZone = FindObjectOfType<BuildZone>();

            SerializedObject serializedValidator = new SerializedObject(validator);
            
            if (table != null)
            {
                serializedValidator.FindProperty("m_Table").objectReferenceValue = table;
            }
            
            if (buildZone != null)
            {
                serializedValidator.FindProperty("m_BuildZone").objectReferenceValue = buildZone;
            }
            
            if (spawner != null && spawner.CurrentSpawnConfiguration != null)
            {
                serializedValidator.FindProperty("m_ReferenceConfiguration").objectReferenceValue = spawner.CurrentSpawnConfiguration;
                serializedValidator.FindProperty("m_ReferenceStructureSpawner").objectReferenceValue = spawner;
            }
            
            serializedValidator.ApplyModifiedProperties();

            Selection.activeGameObject = validatorObj;
            EditorGUIUtility.PingObject(validatorObj);

            string tableStatus = table != null ? "Table found." : "No Table found.";
            string zoneStatus = buildZone != null ? "BuildZone found and assigned!" : "No BuildZone found - create one first.";
            string spawnerStatus = spawner != null ? "ReferenceStructureSpawner found." : "";
            
            Debug.Log($"BuildValidator created! {zoneStatus} {tableStatus} {spawnerStatus}");
        }

        private void OnGUI()
        {
            GUILayout.Label("Validation HUD Setup", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Check current state
            BuildValidator existingValidator = FindObjectOfType<BuildValidator>();
            ValidationHUD existingHUD = FindObjectOfType<ValidationHUD>();
            ReferenceStructureSpawner existingSpawner = FindObjectOfType<ReferenceStructureSpawner>();
            BuildZone existingBuildZone = FindObjectOfType<BuildZone>();
            ValidationDebugVisualizer existingVisualizer = FindObjectOfType<ValidationDebugVisualizer>();

            // Status display
            EditorGUILayout.LabelField("Scene Status:", EditorStyles.boldLabel);
            DrawStatusLine("BuildZone", existingBuildZone != null);
            DrawStatusLine("BuildValidator", existingValidator != null);
            DrawStatusLine("ValidationHUD", existingHUD != null);
            DrawStatusLine("DebugVisualizer", existingVisualizer != null);
            DrawStatusLine("ReferenceStructureSpawner", existingSpawner != null);
            
            GUILayout.Space(10);

            // Step 1: Build Zone
            EditorGUILayout.LabelField("Step 1: Build Zone (Optional but Recommended)", EditorStyles.boldLabel);
            
            if (existingBuildZone == null)
            {
                EditorGUILayout.HelpBox("No BuildZone in scene. Create one to define where players should build.", MessageType.Warning);
                
                if (GUILayout.Button("Create Build Zone", GUILayout.Height(25)))
                {
                    CreateBuildZone();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("BuildZone exists! Only blocks in this area will be validated.", MessageType.Info);
                
                if (GUILayout.Button("Select Build Zone", GUILayout.Height(25)))
                {
                    Selection.activeGameObject = existingBuildZone.gameObject;
                }
            }

            GUILayout.Space(10);

            // Step 2: BuildValidator
            EditorGUILayout.LabelField("Step 2: Build Validator", EditorStyles.boldLabel);
            
            if (existingValidator == null)
            {
                EditorGUILayout.HelpBox("No BuildValidator in scene. Create one to validate builds!", MessageType.Warning);
                
                if (GUILayout.Button("Create Build Validator", GUILayout.Height(25)))
                {
                    CreateBuildValidator();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("BuildValidator exists!", MessageType.Info);
                
                if (GUILayout.Button("Select Build Validator", GUILayout.Height(25)))
                {
                    Selection.activeGameObject = existingValidator.gameObject;
                }
            }

            GUILayout.Space(10);

            // Step 3: ValidationHUD
            EditorGUILayout.LabelField("Step 3: Validation HUD", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Creates a World Space HUD attached to the VR camera.\n" +
                "Positioned at the top of your view like a health bar.",
                MessageType.Info);

            if (GUILayout.Button(existingHUD != null ? "Replace Validation HUD" : "Create Validation HUD", GUILayout.Height(30)))
            {
                CreateValidationHUD();
            }

            GUILayout.Space(10);

            // Step 4: Debug Visualizer (Optional)
            EditorGUILayout.LabelField("Step 4: Debug Visualizer (Optional)", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Shows spheres at expected block positions in VR.\n" +
                "Green = correct, Yellow = wrong position, Gray = missing.",
                MessageType.Info);

            if (existingVisualizer == null)
            {
                if (GUILayout.Button("Create Debug Visualizer", GUILayout.Height(25)))
                {
                    CreateDebugVisualizer();
                }
            }
            else
            {
                if (GUILayout.Button("Select Debug Visualizer", GUILayout.Height(25)))
                {
                    Selection.activeGameObject = existingVisualizer.gameObject;
                }
            }

            GUILayout.Space(10);

            // Tips
            EditorGUILayout.HelpBox(
                "Tips:\n" +
                "• Position Tolerance: Default 8cm - adjust in BuildValidator\n" +
                "• Presence Only Mode: Disabled by default for real challenge\n" +
                "• Debug Visualizer shows where blocks should be placed\n" +
                "• HUD shows [POS] or [ROT] errors for incorrect blocks",
                MessageType.None);
        }

        /// <summary>
        /// Draws a status line with a checkmark or X.
        /// </summary>
        private void DrawStatusLine(string label, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(exists ? "✓" : "✗", GUILayout.Width(20));
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(exists ? "Found" : "Missing", exists ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Creates the complete Validation HUD setup.
        /// </summary>
        private static void CreateValidationHUD()
        {
            // Find the VR camera
            Camera vrCamera = FindVRCamera();
            if (vrCamera == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Could not find VR camera! Make sure you have an XR Origin or Main Camera in the scene.", 
                    "OK");
                return;
            }

            // Check if HUD already exists
            ValidationHUD existingHUD = FindObjectOfType<ValidationHUD>();
            if (existingHUD != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "HUD Already Exists",
                    "A ValidationHUD already exists in the scene. Do you want to replace it?",
                    "Replace",
                    "Cancel");

                if (!replace)
                {
                    return;
                }

                DestroyImmediate(existingHUD.gameObject);
            }

            // Create Canvas (World Space for VR)
            GameObject canvasObj = new GameObject("ValidationHUD_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = vrCamera;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Parent to camera and position at top of view
            canvasObj.transform.SetParent(vrCamera.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, HUDHeightOffset, HUDDistance);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * HUDScale;

            // Set canvas size
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1600, 300);

            // Create background panel
            GameObject panelObj = new GameObject("HUD_Panel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.8f); // Semi-transparent black

            HorizontalLayoutGroup panelLayout = panelObj.AddComponent<HorizontalLayoutGroup>();
            panelLayout.spacing = 50f;
            panelLayout.padding = new RectOffset(40, 40, 20, 20);
            panelLayout.childControlHeight = true;
            panelLayout.childControlWidth = false;
            panelLayout.childForceExpandHeight = true;
            panelLayout.childForceExpandWidth = false;
            panelLayout.childAlignment = TextAnchor.MiddleLeft;

            // === LEFT SECTION: Accuracy & Correct Count ===
            GameObject leftSection = CreateSection("LeftSection", panelObj, 400f);
            
            TextMeshProUGUI accuracyText = CreateTextElement("AccuracyText", leftSection, LargeFontSize, FontStyles.Bold);
            accuracyText.text = "Accuracy: --%";
            accuracyText.color = Color.white;

            TextMeshProUGUI correctBlocksText = CreateTextElement("CorrectBlocksText", leftSection, MediumFontSize);
            correctBlocksText.text = "Correct: --/--";
            correctBlocksText.color = Color.white;

            // === MIDDLE SECTION: Block Results ===
            GameObject middleSection = CreateSection("MiddleSection", panelObj, 550f);
            
            TextMeshProUGUI blockResultsText = CreateTextElement("BlockResultsText", middleSection, SmallFontSize);
            blockResultsText.text = "Block Results:";
            blockResultsText.color = Color.white;
            blockResultsText.enableWordWrapping = true;
            blockResultsText.overflowMode = TextOverflowModes.Ellipsis;

            // === RIGHT SECTION: Missing & Extra ===
            GameObject rightSection = CreateSection("RightSection", panelObj, 450f);
            
            TextMeshProUGUI missingBlocksText = CreateTextElement("MissingBlocksText", rightSection, SmallFontSize);
            missingBlocksText.text = "Missing: None";
            missingBlocksText.color = new Color(1f, 0.9f, 0.3f); // Yellow

            TextMeshProUGUI extraBlocksText = CreateTextElement("ExtraBlocksText", rightSection, SmallFontSize);
            extraBlocksText.text = "Extra: None";
            extraBlocksText.color = new Color(0.7f, 0.7f, 0.7f); // Light gray

            // Add ValidationHUD component
            ValidationHUD validationHUD = canvasObj.AddComponent<ValidationHUD>();

            // Try to find BuildValidator and ReferenceStructureSpawner in scene
            BuildValidator buildValidator = FindObjectOfType<BuildValidator>();
            ReferenceStructureSpawner referenceSpawner = FindObjectOfType<ReferenceStructureSpawner>();

            // Set up ValidationHUD references using SerializedObject
            SerializedObject hudSerialized = new SerializedObject(validationHUD);
            
            if (buildValidator != null)
            {
                hudSerialized.FindProperty("m_BuildValidator").objectReferenceValue = buildValidator;
            }
            
            if (referenceSpawner != null)
            {
                hudSerialized.FindProperty("m_ReferenceSpawner").objectReferenceValue = referenceSpawner;
            }
            
            hudSerialized.FindProperty("m_AccuracyText").objectReferenceValue = accuracyText;
            hudSerialized.FindProperty("m_CorrectBlocksText").objectReferenceValue = correctBlocksText;
            hudSerialized.FindProperty("m_BlockResultsText").objectReferenceValue = blockResultsText;
            hudSerialized.FindProperty("m_MissingBlocksText").objectReferenceValue = missingBlocksText;
            hudSerialized.FindProperty("m_ExtraBlocksText").objectReferenceValue = extraBlocksText;
            hudSerialized.FindProperty("m_FollowCamera").boolValue = false; // Parented to camera, no need to follow
            hudSerialized.FindProperty("m_UpdateInterval").floatValue = 0.5f;
            hudSerialized.FindProperty("m_AutoUpdate").boolValue = true;
            hudSerialized.ApplyModifiedProperties();

            // Select the created object
            Selection.activeGameObject = canvasObj;
            EditorGUIUtility.PingObject(canvasObj);

            string validatorStatus = buildValidator != null 
                ? "BuildValidator found and assigned!" 
                : "WARNING: No BuildValidator found. Please assign one manually.";
            
            string spawnerStatus = referenceSpawner != null
                ? "ReferenceStructureSpawner found - will auto-sync configuration!"
                : "No ReferenceStructureSpawner found - assign configuration manually to BuildValidator.";
            
            Debug.Log($"ValidationHUD created and attached to camera '{vrCamera.name}'! {validatorStatus} {spawnerStatus}");
            
            if (buildValidator != null && buildValidator.ReferenceConfiguration == null && referenceSpawner == null)
            {
                Debug.LogWarning("BuildValidator has no ReferenceConfiguration assigned and no ReferenceStructureSpawner found. HUD will show 'No Reference' until you assign one.");
            }
        }

        /// <summary>
        /// Finds the VR camera in the scene.
        /// </summary>
        private static Camera FindVRCamera()
        {
            // First try to find XR Origin camera
            XROrigin xrOrigin = FindObjectOfType<XROrigin>();
            if (xrOrigin != null && xrOrigin.Camera != null)
            {
                return xrOrigin.Camera;
            }

            // Fall back to main camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            // Last resort: find any camera
            return FindObjectOfType<Camera>();
        }

        /// <summary>
        /// Creates a section container with vertical layout.
        /// </summary>
        private static GameObject CreateSection(string name, GameObject parent, float width)
        {
            GameObject sectionObj = new GameObject(name);
            sectionObj.transform.SetParent(parent.transform, false);

            RectTransform rect = sectionObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 0f);

            VerticalLayoutGroup layout = sectionObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperLeft;

            LayoutElement layoutElement = sectionObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0f;

            return sectionObj;
        }

        /// <summary>
        /// Creates a TextMeshProUGUI element with default settings.
        /// </summary>
        private static TextMeshProUGUI CreateTextElement(string name, GameObject parent, float fontSize, FontStyles fontStyle = FontStyles.Normal)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform, false);

            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = new Vector2(0f, fontSize * 1.4f);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.TopLeft;

            LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize * 1.4f;

            return text;
        }
    }
}
