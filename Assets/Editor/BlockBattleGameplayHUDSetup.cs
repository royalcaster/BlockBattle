using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor tool to set up the beautiful Gameplay HUD.
    /// </summary>
    public class BlockBattleGameplayHUDSetup : EditorWindow
    {
        [MenuItem("BlockBattle/Setup Gameplay HUD (Beautiful)")]
        public static void ShowWindow()
        {
            CreateGameplayHUD();
        }

        /// <summary>
        /// Creates the beautiful gameplay HUD.
        /// </summary>
        public static void CreateGameplayHUD()
        {
            // Find VR camera
            Camera vrCamera = FindVRCamera();
            if (vrCamera == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not find VR camera! Make sure you have an XR Origin or Main Camera.",
                    "OK");
                return;
            }

            // Check if already exists
            GameplayHUD existing = Object.FindObjectOfType<GameplayHUD>();
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "HUD Already Exists",
                    "A GameplayHUD already exists. Replace it?",
                    "Replace", "Cancel");
                if (!replace) return;
                DestroyImmediate(existing.gameObject);
            }

            // Create Canvas (World Space, no background)
            GameObject canvasObj = new GameObject("GameplayHUD_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = vrCamera;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Parent to camera
            canvasObj.transform.SetParent(vrCamera.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0.5f, 1.5f);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * 0.001f;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1200, 200);

            // === MAIN CONTAINER (no background) ===
            GameObject mainContainer = new GameObject("MainContainer");
            mainContainer.transform.SetParent(canvasObj.transform, false);
            RectTransform mainRect = mainContainer.AddComponent<RectTransform>();
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.sizeDelta = Vector2.zero;
            mainRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup mainLayout = mainContainer.AddComponent<VerticalLayoutGroup>();
            mainLayout.spacing = 50f; // More space between progress bar and blocks
            mainLayout.childControlHeight = false;
            mainLayout.childControlWidth = true;
            mainLayout.childForceExpandHeight = false;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childAlignment = TextAnchor.MiddleCenter;
            mainLayout.padding = new RectOffset(20, 20, 10, 10);

            // === PROGRESS BAR SECTION ===
            GameObject progressSection = CreateProgressBarSection(mainContainer);

            // === BLOCK INDICATORS SECTION ===
            GameObject blockSection = CreateBlockIndicatorSection(mainContainer);

            // === Add GameplayHUD component ===
            GameplayHUD hud = canvasObj.AddComponent<GameplayHUD>();

            // Wire up references
            SerializedObject hudSO = new SerializedObject(hud);
            
            BuildValidator validator = Object.FindObjectOfType<BuildValidator>();
            ReferenceStructureSpawner spawner = Object.FindObjectOfType<ReferenceStructureSpawner>();
            
            if (validator != null)
                hudSO.FindProperty("m_BuildValidator").objectReferenceValue = validator;
            if (spawner != null)
                hudSO.FindProperty("m_ReferenceSpawner").objectReferenceValue = spawner;

            // Find and assign UI elements
            hudSO.FindProperty("m_BlockIndicatorContainer").objectReferenceValue = 
                blockSection.transform.Find("IndicatorContainer")?.GetComponent<RectTransform>();
            
            Transform fillTransform = progressSection.transform.Find("ProgressBar/Fill");
            hudSO.FindProperty("m_ProgressBarFill").objectReferenceValue = 
                fillTransform?.GetComponent<RectTransform>();
            hudSO.FindProperty("m_ProgressBarFillImage").objectReferenceValue = 
                fillTransform?.GetComponent<Image>();
            
            hudSO.FindProperty("m_PercentageText").objectReferenceValue = 
                progressSection.transform.Find("PercentageText")?.GetComponent<TextMeshProUGUI>();

            // Setup the progress gradient (red -> orange -> yellow -> green)
            SerializedProperty gradientProp = hudSO.FindProperty("m_ProgressGradient");
            if (gradientProp != null)
            {
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0.9f, 0.2f, 0.2f), 0f),      // Red at 0%
                        new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0.4f),      // Orange at 40%
                        new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.7f),      // Yellow at 70%
                        new GradientColorKey(new Color(0.2f, 1f, 0.4f), 1f)         // Green at 100%
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    }
                );
                gradientProp.gradientValue = gradient;
            }

            // Try to auto-assign block meshes from the project
            AssignBlockMesh(hudSO, "m_CubeMesh", "CubeMesh");
            AssignBlockMesh(hudSO, "m_CylinderMesh", "CylinderMesh");
            AssignBlockMesh(hudSO, "m_TriangleMesh", "TriangleMesh");
            AssignBlockMesh(hudSO, "m_RectangleMesh", "RectangleMesh");
            AssignBlockMesh(hudSO, "m_ArchMesh", "ArchMesh");

            hudSO.ApplyModifiedProperties();

            Selection.activeGameObject = canvasObj;
            EditorGUIUtility.PingObject(canvasObj);

            Debug.Log("GameplayHUD created! Beautiful HUD with block indicators and animated progress bar.");
        }

        /// <summary>
        /// Creates the progress bar section.
        /// </summary>
        private static GameObject CreateProgressBarSection(GameObject parent)
        {
            GameObject section = new GameObject("ProgressSection");
            section.transform.SetParent(parent.transform, false);

            RectTransform sectionRect = section.AddComponent<RectTransform>();
            sectionRect.sizeDelta = new Vector2(0, 60);

            LayoutElement sectionLayout = section.AddComponent<LayoutElement>();
            sectionLayout.preferredHeight = 60;
            sectionLayout.flexibleWidth = 1;

            // Progress bar background
            GameObject progressBar = new GameObject("ProgressBar");
            progressBar.transform.SetParent(section.transform, false);
            RectTransform progressRect = progressBar.AddComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.1f, 0.3f);
            progressRect.anchorMax = new Vector2(0.75f, 0.7f);
            progressRect.sizeDelta = Vector2.zero;
            progressRect.anchoredPosition = Vector2.zero;

            Image progressBg = progressBar.AddComponent<Image>();
            progressBg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            progressBg.raycastTarget = false;

            // Progress bar fill - uses RectTransform anchor scaling for reliable fill effect
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(progressBar.transform, false);
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            // Fill starts from left (anchorMin.x = 0) and grows right (anchorMax.x = 0 to 1)
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f); // Start with 0 width
            fillRect.pivot = new Vector2(0f, 0.5f); // Pivot on left side for proper scaling
            fillRect.sizeDelta = new Vector2(0f, -4f); // 0 width offset, 4px vertical padding
            fillRect.anchoredPosition = new Vector2(2f, 0f); // 2px left padding

            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 1f, 0.4f, 1f);
            fillImg.type = Image.Type.Simple; // Simple type works reliably without sprite
            fillImg.raycastTarget = false;

            // Percentage text
            GameObject percentText = new GameObject("PercentageText");
            percentText.transform.SetParent(section.transform, false);
            RectTransform percentRect = percentText.AddComponent<RectTransform>();
            percentRect.anchorMin = new Vector2(0.78f, 0.1f);
            percentRect.anchorMax = new Vector2(0.98f, 0.9f);
            percentRect.sizeDelta = Vector2.zero;
            percentRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI percentTMP = percentText.AddComponent<TextMeshProUGUI>();
            percentTMP.text = "0%";
            percentTMP.fontSize = 48;
            percentTMP.fontStyle = FontStyles.Bold;
            percentTMP.alignment = TextAlignmentOptions.Center;
            percentTMP.color = new Color(0.2f, 1f, 0.4f, 1f);
            percentTMP.raycastTarget = false;

            // "ACCURACY" label
            GameObject label = new GameObject("Label");
            label.transform.SetParent(section.transform, false);
            RectTransform labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.6f);
            labelRect.anchorMax = new Vector2(0.1f, 1f);
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI labelTMP = label.AddComponent<TextMeshProUGUI>();
            labelTMP.text = "ACC";
            labelTMP.fontSize = 18;
            labelTMP.alignment = TextAlignmentOptions.BottomLeft;
            labelTMP.color = new Color(1f, 1f, 1f, 0.5f);
            labelTMP.raycastTarget = false;

            return section;
        }

        /// <summary>
        /// Creates the block indicator section.
        /// </summary>
        private static GameObject CreateBlockIndicatorSection(GameObject parent)
        {
            GameObject section = new GameObject("BlockSection");
            section.transform.SetParent(parent.transform, false);

            RectTransform sectionRect = section.AddComponent<RectTransform>();
            sectionRect.sizeDelta = new Vector2(0, 80);

            LayoutElement sectionLayout = section.AddComponent<LayoutElement>();
            sectionLayout.preferredHeight = 80;
            sectionLayout.flexibleWidth = 1;

            // Container for block indicators (they will be created dynamically)
            GameObject indicatorContainer = new GameObject("IndicatorContainer");
            indicatorContainer.transform.SetParent(section.transform, false);
            RectTransform containerRect = indicatorContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            return section;
        }

        /// <summary>
        /// Finds the VR camera.
        /// </summary>
        private static Camera FindVRCamera()
        {
            XROrigin xrOrigin = Object.FindObjectOfType<XROrigin>();
            if (xrOrigin != null && xrOrigin.Camera != null)
                return xrOrigin.Camera;

            Camera main = Camera.main;
            if (main != null)
                return main;

            return Object.FindObjectOfType<Camera>();
        }

        /// <summary>
        /// Tries to find and assign a block mesh from the project.
        /// </summary>
        private static void AssignBlockMesh(SerializedObject hudSO, string propertyName, string meshName)
        {
            string[] guids = AssetDatabase.FindAssets($"{meshName} t:Mesh");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null)
                {
                    hudSO.FindProperty(propertyName).objectReferenceValue = mesh;
                }
            }
        }
    }
}

