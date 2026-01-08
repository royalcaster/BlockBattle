using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlockBattle
{
    /// <summary>
    /// Automatically creates and configures the UI elements needed by LevelManager.
    /// Attach this to the same GameObject as LevelManager and click the context menu to run setup.
    /// </summary>
    [RequireComponent(typeof(LevelManager))]
    public class LevelManagerUISetup : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField, Tooltip("Font size for level indicator")]
        private float m_LevelTextFontSize = 36f;

        [SerializeField, Tooltip("Font size for success message")]
        private float m_SuccessTextFontSize = 48f;

        [SerializeField, Tooltip("Success panel background color")]
        private Color m_PanelBackgroundColor = new Color(0f, 0f, 0f, 0.85f);

        [SerializeField, Tooltip("Success text color")]
        private Color m_SuccessTextColor = new Color(0.2f, 1f, 0.2f, 1f);

        [SerializeField, Tooltip("Level text color")]
        private Color m_LevelTextColor = Color.white;

        /// <summary>
        /// Creates all UI elements needed by LevelManager.
        /// Call this from the context menu in the Inspector.
        /// </summary>
        [ContextMenu("Create Level Manager UI")]
        public void CreateLevelManagerUI()
        {
            LevelManager levelManager = GetComponent<LevelManager>();
            if (levelManager == null)
            {
                Debug.LogError("LevelManagerUISetup: No LevelManager component found!");
                return;
            }

            // Find or create main canvas
            Canvas mainCanvas = FindMainCanvas();
            if (mainCanvas == null)
            {
                Debug.LogError("LevelManagerUISetup: No Canvas found in scene! Please create a Canvas first.");
                return;
            }

            // Create Level Text (top-left corner)
            TextMeshProUGUI levelText = CreateLevelText(mainCanvas.transform);

            // Create Success Panel with Text (centered)
            GameObject successPanel = CreateSuccessPanel(mainCanvas.transform, out TextMeshProUGUI successText);

            // Assign references to LevelManager using SerializedObject (editor only)
            #if UNITY_EDITOR
            UnityEditor.SerializedObject serializedManager = new UnityEditor.SerializedObject(levelManager);
            
            UnityEditor.SerializedProperty levelTextProp = serializedManager.FindProperty("m_LevelText");
            if (levelTextProp != null)
                levelTextProp.objectReferenceValue = levelText;

            UnityEditor.SerializedProperty successPanelProp = serializedManager.FindProperty("m_SuccessPanel");
            if (successPanelProp != null)
                successPanelProp.objectReferenceValue = successPanel;

            UnityEditor.SerializedProperty successTextProp = serializedManager.FindProperty("m_SuccessText");
            if (successTextProp != null)
                successTextProp.objectReferenceValue = successText;

            serializedManager.ApplyModifiedProperties();
            
            UnityEditor.EditorUtility.SetDirty(levelManager);

            // Also assign to GameplayHUD if it exists
            GameplayHUD gameplayHUD = FindAnyObjectByType<GameplayHUD>();
            if (gameplayHUD != null)
            {
                UnityEditor.SerializedObject serializedHUD = new UnityEditor.SerializedObject(gameplayHUD);
                UnityEditor.SerializedProperty hudLevelTextProp = serializedHUD.FindProperty("m_LevelText");
                if (hudLevelTextProp != null)
                {
                    hudLevelTextProp.objectReferenceValue = levelText;
                    serializedHUD.ApplyModifiedProperties();
                    UnityEditor.EditorUtility.SetDirty(gameplayHUD);
                    Debug.Log("LevelManagerUISetup: Also assigned LevelText to GameplayHUD.");
                }
            }
            #endif

            Debug.Log("LevelManagerUISetup: UI elements created and assigned successfully!");
        }

        /// <summary>
        /// Finds the main canvas in the scene.
        /// </summary>
        private Canvas FindMainCanvas()
        {
            // First try to find existing canvas
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            
            foreach (Canvas canvas in canvases)
            {
                // Prefer world space canvas for VR, or screen space overlay
                if (canvas.renderMode == RenderMode.WorldSpace || 
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvas;
                }
            }

            // Return any canvas if found
            if (canvases.Length > 0)
                return canvases[0];

            return null;
        }

        /// <summary>
        /// Creates the level indicator text in the top-left corner.
        /// </summary>
        private TextMeshProUGUI CreateLevelText(Transform canvasTransform)
        {
            // Check if it already exists
            Transform existing = canvasTransform.Find("LevelIndicator");
            if (existing != null)
            {
                Debug.Log("LevelManagerUISetup: LevelIndicator already exists, reusing.");
                return existing.GetComponent<TextMeshProUGUI>();
            }

            // Create new text object
            GameObject textObj = new GameObject("LevelIndicator");
            textObj.transform.SetParent(canvasTransform, false);

            // Add RectTransform
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            
            // Check if this is a world space canvas (VR)
            Canvas canvas = canvasTransform.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                // Position for world space (VR) - centered horizontally, at top
                rectTransform.anchorMin = new Vector2(0.5f, 1);
                rectTransform.anchorMax = new Vector2(0.5f, 1);
                rectTransform.pivot = new Vector2(0.5f, 1);
                rectTransform.anchoredPosition = new Vector2(0, -20);
                rectTransform.sizeDelta = new Vector2(200, 50);
            }
            else
            {
                // Position for screen space - top center
                rectTransform.anchorMin = new Vector2(0.5f, 1);
                rectTransform.anchorMax = new Vector2(0.5f, 1);
                rectTransform.pivot = new Vector2(0.5f, 1);
                rectTransform.anchoredPosition = new Vector2(0, -20);
                rectTransform.sizeDelta = new Vector2(300, 60);
            }

            // Add TextMeshPro component
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = "Level 1";
            textComponent.fontSize = m_LevelTextFontSize;
            textComponent.color = m_LevelTextColor;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.fontStyle = FontStyles.Bold;

            return textComponent;
        }

        /// <summary>
        /// Creates the success panel with centered text.
        /// </summary>
        private GameObject CreateSuccessPanel(Transform canvasTransform, out TextMeshProUGUI successText)
        {
            // Check if it already exists
            Transform existing = canvasTransform.Find("SuccessPanel");
            if (existing != null)
            {
                Debug.Log("LevelManagerUISetup: SuccessPanel already exists, reusing.");
                successText = existing.GetComponentInChildren<TextMeshProUGUI>();
                return existing.gameObject;
            }

            // Create panel
            GameObject panel = new GameObject("SuccessPanel");
            panel.transform.SetParent(canvasTransform, false);

            // Add RectTransform - centered and fills most of screen
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            
            // Check if this is a world space canvas (VR)
            Canvas canvas = canvasTransform.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                // Smaller size for VR world space
                panelRect.sizeDelta = new Vector2(400, 200);
            }
            else
            {
                panelRect.sizeDelta = new Vector2(600, 300);
            }

            // Add background image
            Image background = panel.AddComponent<Image>();
            background.color = m_PanelBackgroundColor;

            // Add rounded corners effect (optional - uses default UI sprite)
            // background.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            // background.type = Image.Type.Sliced;

            // Create text inside panel
            GameObject textObj = new GameObject("SuccessText");
            textObj.transform.SetParent(panel.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20, 20);
            textRect.offsetMax = new Vector2(-20, -20);

            successText = textObj.AddComponent<TextMeshProUGUI>();
            successText.text = "Level Complete!";
            successText.fontSize = m_SuccessTextFontSize;
            successText.color = m_SuccessTextColor;
            successText.alignment = TextAlignmentOptions.Center;
            successText.fontStyle = FontStyles.Bold;

            // Start with panel hidden
            panel.SetActive(false);

            return panel;
        }

        /// <summary>
        /// Removes the created UI elements.
        /// </summary>
        [ContextMenu("Remove Level Manager UI")]
        public void RemoveLevelManagerUI()
        {
            Canvas mainCanvas = FindMainCanvas();
            if (mainCanvas == null)
                return;

            Transform levelIndicator = mainCanvas.transform.Find("LevelIndicator");
            if (levelIndicator != null)
            {
                #if UNITY_EDITOR
                DestroyImmediate(levelIndicator.gameObject);
                #else
                Destroy(levelIndicator.gameObject);
                #endif
            }

            Transform successPanel = mainCanvas.transform.Find("SuccessPanel");
            if (successPanel != null)
            {
                #if UNITY_EDITOR
                DestroyImmediate(successPanel.gameObject);
                #else
                Destroy(successPanel.gameObject);
                #endif
            }

            // Clear references in LevelManager
            LevelManager levelManager = GetComponent<LevelManager>();
            if (levelManager != null)
            {
                #if UNITY_EDITOR
                UnityEditor.SerializedObject serializedManager = new UnityEditor.SerializedObject(levelManager);
                
                UnityEditor.SerializedProperty levelTextProp = serializedManager.FindProperty("m_LevelText");
                if (levelTextProp != null)
                    levelTextProp.objectReferenceValue = null;

                UnityEditor.SerializedProperty successPanelProp = serializedManager.FindProperty("m_SuccessPanel");
                if (successPanelProp != null)
                    successPanelProp.objectReferenceValue = null;

                UnityEditor.SerializedProperty successTextProp = serializedManager.FindProperty("m_SuccessText");
                if (successTextProp != null)
                    successTextProp.objectReferenceValue = null;

                serializedManager.ApplyModifiedProperties();
                #endif
            }

            // Also clear reference in GameplayHUD
            #if UNITY_EDITOR
            GameplayHUD gameplayHUD = FindAnyObjectByType<GameplayHUD>();
            if (gameplayHUD != null)
            {
                UnityEditor.SerializedObject serializedHUD = new UnityEditor.SerializedObject(gameplayHUD);
                UnityEditor.SerializedProperty hudLevelTextProp = serializedHUD.FindProperty("m_LevelText");
                if (hudLevelTextProp != null)
                {
                    hudLevelTextProp.objectReferenceValue = null;
                    serializedHUD.ApplyModifiedProperties();
                }
            }
            #endif

            Debug.Log("LevelManagerUISetup: UI elements removed.");
        }
    }
}

