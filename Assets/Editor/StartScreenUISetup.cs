using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor tool to create and set up the Start Screen UI for VR.
    /// </summary>
    public class StartScreenUISetup : EditorWindow
    {
        [MenuItem("BlockBattle/Create Start Screen UI")]
        public static void CreateStartScreenUI()
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
            StartScreenUI existing = Object.FindAnyObjectByType<StartScreenUI>();
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Start Screen Already Exists",
                    "A StartScreenUI already exists. Replace it?",
                    "Replace", "Cancel");
                if (!replace) return;
                DestroyImmediate(existing.gameObject);
            }

            // Create the World Space Canvas
            GameObject canvasObj = new GameObject("StartScreen_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = vrCamera;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            // Use TrackedDeviceGraphicRaycaster for VR controller interaction!
            canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

            // Position in front of player at spawn
            XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            if (xrOrigin != null)
            {
                // Position in world space in front of XR Origin
                canvasObj.transform.position = xrOrigin.transform.position + xrOrigin.transform.forward * 2f + Vector3.up * 1.5f;
                canvasObj.transform.rotation = Quaternion.LookRotation(xrOrigin.transform.forward);
            }
            else
            {
                // Fallback - position in front of camera
                canvasObj.transform.position = vrCamera.transform.position + vrCamera.transform.forward * 2f;
                canvasObj.transform.LookAt(vrCamera.transform);
                canvasObj.transform.Rotate(0, 180, 0);
            }

            canvasObj.transform.localScale = Vector3.one * 0.002f;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(800, 600);

            // Create background panel
            GameObject panelObj = new GameObject("StartPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.08f, 0.15f, 0.95f);

            // Add vertical layout for content
            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 40f;
            layout.padding = new RectOffset(50, 50, 80, 80);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Create title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(0, 120);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "BLOCK BATTLE";
            titleText.fontSize = 72;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.95f, 0.85f, 0.4f);
            titleText.raycastTarget = false;

            // Create subtitle
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform subtitleRect = subtitleObj.AddComponent<RectTransform>();
            subtitleRect.sizeDelta = new Vector2(0, 50);

            TextMeshProUGUI subtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
            subtitleText.text = "Build. Destroy. Repeat.";
            subtitleText.fontSize = 28;
            subtitleText.fontStyle = FontStyles.Italic;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.7f, 0.75f, 0.85f);
            subtitleText.raycastTarget = false;

            // Create spacer
            GameObject spacerObj = new GameObject("Spacer");
            spacerObj.transform.SetParent(panelObj.transform, false);
            RectTransform spacerRect = spacerObj.AddComponent<RectTransform>();
            spacerRect.sizeDelta = new Vector2(0, 40);

            // Create Start button
            GameObject buttonObj = new GameObject("StartButton");
            buttonObj.transform.SetParent(panelObj.transform, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(300, 80);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.7f, 0.4f);

            Button button = buttonObj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.7f, 0.4f);
            colors.highlightedColor = new Color(0.3f, 0.85f, 0.5f);
            colors.pressedColor = new Color(0.15f, 0.5f, 0.3f);
            colors.selectedColor = new Color(0.25f, 0.75f, 0.45f);
            button.colors = colors;

            // Button text
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);

            RectTransform buttonTextRect = buttonTextObj.AddComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
            buttonTextRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "START";
            buttonText.fontSize = 42;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            buttonText.raycastTarget = false;

            // Create instructions text
            GameObject instructionsObj = new GameObject("Instructions");
            instructionsObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform instructionsRect = instructionsObj.AddComponent<RectTransform>();
            instructionsRect.sizeDelta = new Vector2(0, 40);

            TextMeshProUGUI instructionsText = instructionsObj.AddComponent<TextMeshProUGUI>();
            instructionsText.text = "Point and click to start";
            instructionsText.fontSize = 22;
            instructionsText.alignment = TextAlignmentOptions.Center;
            instructionsText.color = new Color(0.6f, 0.65f, 0.7f);
            instructionsText.raycastTarget = false;

            // Create last time text (initially hidden)
            GameObject lastTimeObj = new GameObject("LastTime");
            lastTimeObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform lastTimeRect = lastTimeObj.AddComponent<RectTransform>();
            lastTimeRect.sizeDelta = new Vector2(0, 40);

            TextMeshProUGUI lastTimeText = lastTimeObj.AddComponent<TextMeshProUGUI>();
            lastTimeText.text = "Last Time: --:--";
            lastTimeText.fontSize = 24;
            lastTimeText.alignment = TextAlignmentOptions.Center;
            lastTimeText.color = new Color(0.8f, 0.85f, 0.9f); // Light gray
            lastTimeText.raycastTarget = false;
            lastTimeObj.SetActive(false); // Hidden until player completes a game

            // Create personal best text (initially hidden)
            GameObject personalBestObj = new GameObject("PersonalBest");
            personalBestObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform personalBestRect = personalBestObj.AddComponent<RectTransform>();
            personalBestRect.sizeDelta = new Vector2(0, 50);

            TextMeshProUGUI personalBestText = personalBestObj.AddComponent<TextMeshProUGUI>();
            personalBestText.text = "Personal Best: --:--";
            personalBestText.fontSize = 26;
            personalBestText.fontStyle = FontStyles.Bold;
            personalBestText.alignment = TextAlignmentOptions.Center;
            personalBestText.color = new Color(1f, 0.85f, 0.3f); // Gold color
            personalBestText.raycastTarget = false;
            personalBestObj.SetActive(false); // Hidden until player has a best time

            // Add StartScreenUI component
            StartScreenUI startScreen = canvasObj.AddComponent<StartScreenUI>();

            // Wire up references via SerializedObject
            SerializedObject so = new SerializedObject(startScreen);
            so.FindProperty("m_StartButton").objectReferenceValue = button;
            so.FindProperty("m_StartPanel").objectReferenceValue = panelObj;
            so.FindProperty("m_TitleText").objectReferenceValue = titleText;
            so.FindProperty("m_LastTimeText").objectReferenceValue = lastTimeText;
            so.FindProperty("m_PersonalBestText").objectReferenceValue = personalBestText;
            
            // Find and assign LevelManager
            LevelManager levelManager = Object.FindAnyObjectByType<LevelManager>();
            if (levelManager != null)
            {
                so.FindProperty("m_LevelManager").objectReferenceValue = levelManager;
            }
            
            so.ApplyModifiedProperties();

            // Mark dirty
            EditorUtility.SetDirty(canvasObj);

            // Select the new object
            Selection.activeGameObject = canvasObj;

            Debug.Log("StartScreenUISetup: Start Screen UI created successfully!");
            EditorUtility.DisplayDialog("Success", 
                "Start Screen UI created!\n\nThe game will now wait for the player to press 'Start' before beginning.", 
                "OK");
        }

        /// <summary>
        /// Finds the VR camera in the scene.
        /// </summary>
        private static Camera FindVRCamera()
        {
            // Try XR Origin first
            XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            if (xrOrigin != null)
            {
                Camera cam = xrOrigin.Camera;
                if (cam != null) return cam;
            }

            // Try Main Camera tag
            Camera mainCam = Camera.main;
            if (mainCam != null) return mainCam;

            // Find any camera
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length > 0) return cameras[0];

            return null;
        }
    }
}
