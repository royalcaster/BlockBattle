using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor utility for setting up GameplayHUD components, including the restart button.
    /// </summary>
    public static class GameplayHUDSetup
    {
        [MenuItem("BlockBattle/UI/Setup Restart Button on GameplayHUD")]
        public static void SetupRestartButton()
        {
            // Find GameplayHUD in scene
            var gameplayHUD = Object.FindAnyObjectByType<GameplayHUD>();
            if (gameplayHUD == null)
            {
                EditorUtility.DisplayDialog("GameplayHUD Not Found",
                    "Could not find a GameplayHUD component in the scene.\n\n" +
                    "Please ensure a GameplayHUD is present in the scene.",
                    "OK");
                return;
            }

            // Find or create a Canvas for the button
            Canvas canvas = gameplayHUD.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = gameplayHUD.GetComponentInChildren<Canvas>();
            }
            if (canvas == null)
            {
                // Try to find any world space canvas in the scene
                var allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var c in allCanvases)
                {
                    if (c.renderMode == RenderMode.WorldSpace)
                    {
                        canvas = c;
                        break;
                    }
                }
            }

            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Canvas Not Found",
                    "Could not find a Canvas for the restart button.\n\n" +
                    "Please ensure GameplayHUD is under a Canvas or there is a World Space Canvas in the scene.",
                    "OK");
                return;
            }

            // Create the restart button
            GameObject buttonObj = CreateRestartButton(canvas.transform);

            // Get components
            Button button = buttonObj.GetComponent<Button>();
            XRSimpleInteractable interactable = buttonObj.GetComponent<XRSimpleInteractable>();

            // Assign to GameplayHUD using SerializedObject
            SerializedObject serializedHUD = new SerializedObject(gameplayHUD);
            
            SerializedProperty restartButtonProp = serializedHUD.FindProperty("m_RestartButton");
            SerializedProperty restartInteractableProp = serializedHUD.FindProperty("m_RestartInteractable");

            if (restartButtonProp != null)
            {
                restartButtonProp.objectReferenceValue = button;
            }

            if (restartInteractableProp != null)
            {
                restartInteractableProp.objectReferenceValue = interactable;
            }

            serializedHUD.ApplyModifiedProperties();

            // Mark scene dirty
            EditorUtility.SetDirty(gameplayHUD);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameplayHUD.gameObject.scene);

            // Select the new button
            Selection.activeGameObject = buttonObj;

            EditorUtility.DisplayDialog("Restart Button Created",
                $"Created restart button and assigned to GameplayHUD.\n\n" +
                $"Button: {buttonObj.name}\n" +
                $"Parent: {canvas.name}\n\n" +
                $"You may want to adjust the button's position in the Inspector.",
                "OK");
        }

        /// <summary>
        /// Creates the restart button GameObject with all required components.
        /// </summary>
        private static GameObject CreateRestartButton(Transform parent)
        {
            // Create button container
            GameObject buttonObj = new GameObject("RestartButton");
            buttonObj.transform.SetParent(parent, false);

            // Add RectTransform
            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 0f);  // Bottom right
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = new Vector2(-20f, 20f);
            rectTransform.sizeDelta = new Vector2(150f, 50f);

            // Add Image (button background)
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.8f, 0.2f, 0.2f, 0.9f); // Red-ish color
            buttonImage.raycastTarget = true;

            // Add Button component
            Button button = buttonObj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);
            colors.highlightedColor = new Color(1f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
            colors.selectedColor = new Color(0.9f, 0.25f, 0.25f, 1f);
            button.colors = colors;

            // Add BoxCollider for XR interaction (world space canvas needs collider)
            BoxCollider collider = buttonObj.AddComponent<BoxCollider>();
            collider.size = new Vector3(rectTransform.sizeDelta.x, rectTransform.sizeDelta.y, 1f);
            collider.center = Vector3.zero;

            // Add XRSimpleInteractable for VR controller interaction
            XRSimpleInteractable interactable = buttonObj.AddComponent<XRSimpleInteractable>();

            // Create text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "RESTART";
            buttonText.fontSize = 24;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            buttonText.raycastTarget = false;

            // Register undo
            Undo.RegisterCreatedObjectUndo(buttonObj, "Create Restart Button");

            return buttonObj;
        }

        [MenuItem("BlockBattle/UI/Add Restart Button to Selected Canvas")]
        public static void AddRestartButtonToSelectedCanvas()
        {
            // Get selected object
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No Selection",
                    "Please select a Canvas or UI element to add the restart button to.",
                    "OK");
                return;
            }

            // Find canvas in selection or parents
            Canvas canvas = selected.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = selected.GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                EditorUtility.DisplayDialog("No Canvas",
                    "Selected object is not a Canvas or child of a Canvas.\n\n" +
                    "Please select a Canvas or a UI element within a Canvas.",
                    "OK");
                return;
            }

            // Create button under the selected object (or canvas)
            Transform parent = selected.transform;
            GameObject buttonObj = CreateRestartButton(parent);

            Selection.activeGameObject = buttonObj;

            Debug.Log($"Created restart button: {buttonObj.name} under {parent.name}");
            
            EditorUtility.DisplayDialog("Restart Button Created",
                $"Created restart button under {parent.name}.\n\n" +
                "Don't forget to assign it to GameplayHUD:\n" +
                "1. Select the GameplayHUD object\n" +
                "2. Drag the button to 'Restart Button' field\n" +
                "3. Drag the button to 'Restart Interactable' field",
                "OK");
        }

        [MenuItem("BlockBattle/UI/Auto-Setup All GameplayHUD References")]
        public static void AutoSetupGameplayHUD()
        {
            var gameplayHUD = Object.FindAnyObjectByType<GameplayHUD>();
            if (gameplayHUD == null)
            {
                EditorUtility.DisplayDialog("GameplayHUD Not Found",
                    "Could not find a GameplayHUD component in the scene.",
                    "OK");
                return;
            }

            SerializedObject serializedHUD = new SerializedObject(gameplayHUD);
            int assignedCount = 0;

            // Try to find and assign BuildValidator
            var buildValidator = Object.FindAnyObjectByType<BuildValidator>();
            if (buildValidator != null)
            {
                SerializedProperty prop = serializedHUD.FindProperty("m_BuildValidator");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = buildValidator;
                    assignedCount++;
                }
            }

            // Try to find and assign ReferenceStructureSpawner
            var refSpawner = Object.FindAnyObjectByType<ReferenceStructureSpawner>();
            if (refSpawner != null)
            {
                SerializedProperty prop = serializedHUD.FindProperty("m_ReferenceSpawner");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = refSpawner;
                    assignedCount++;
                }
            }

            // Try to find restart button in children
            var restartButton = gameplayHUD.GetComponentInChildren<Button>();
            if (restartButton != null && restartButton.gameObject.name.Contains("Restart"))
            {
                SerializedProperty prop = serializedHUD.FindProperty("m_RestartButton");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = restartButton;
                    assignedCount++;
                }

                var interactable = restartButton.GetComponent<XRSimpleInteractable>();
                if (interactable != null)
                {
                    SerializedProperty intProp = serializedHUD.FindProperty("m_RestartInteractable");
                    if (intProp != null && intProp.objectReferenceValue == null)
                    {
                        intProp.objectReferenceValue = interactable;
                        assignedCount++;
                    }
                }
            }

            serializedHUD.ApplyModifiedProperties();
            EditorUtility.SetDirty(gameplayHUD);

            EditorUtility.DisplayDialog("Auto-Setup Complete",
                $"Assigned {assignedCount} references to GameplayHUD.\n\n" +
                "Check the Inspector to verify all fields are correctly assigned.",
                "OK");
        }
    }
}
