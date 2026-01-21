using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor tool to create and set up the Shelf Progress UI.
    /// </summary>
    public class ShelfProgressUISetup : EditorWindow
    {
        [MenuItem("BlockBattle/Create Shelf Progress UI")]
        public static void CreateShelfProgressUI()
        {
            // Find the shelf (Regal)
            GameObject shelf = GameObject.Find("Regal");
            if (shelf == null)
            {
                // Try finding in hierarchy
                ShelfBlockSpawner spawner = Object.FindAnyObjectByType<ShelfBlockSpawner>();
                if (spawner != null)
                {
                    shelf = spawner.gameObject;
                }
            }

            if (shelf == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not find shelf (Regal) in scene!", "OK");
                return;
            }

            // Check if UI already exists
            ShelfProgressUI existingUI = shelf.GetComponentInChildren<ShelfProgressUI>();
            if (existingUI != null)
            {
                EditorUtility.DisplayDialog("Already Exists", "ShelfProgressUI already exists on the shelf!", "OK");
                Selection.activeGameObject = existingUI.gameObject;
                return;
            }

            // Create the World Space Canvas
            GameObject canvasObj = new GameObject("ShelfProgressCanvas");
            canvasObj.transform.SetParent(shelf.transform);
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;
            
            canvasObj.AddComponent<GraphicRaycaster>();

            // Position above the shelf
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.localPosition = new Vector3(0.8f, 2.5f, -1.4f); // Above and in front of shelf
            canvasRect.localRotation = Quaternion.Euler(0, 180, 0); // Face forward
            canvasRect.localScale = Vector3.one * 0.005f; // Scale down for world space
            canvasRect.sizeDelta = new Vector2(400, 100);

            // Create background panel
            GameObject panelObj = new GameObject("Panel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            // Create progress bar background
            GameObject progressBgObj = new GameObject("ProgressBarBackground");
            progressBgObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform progressBgRect = progressBgObj.AddComponent<RectTransform>();
            progressBgRect.anchorMin = new Vector2(0.05f, 0.15f);
            progressBgRect.anchorMax = new Vector2(0.95f, 0.45f);
            progressBgRect.sizeDelta = Vector2.zero;
            progressBgRect.anchoredPosition = Vector2.zero;

            Image progressBgImage = progressBgObj.AddComponent<Image>();
            progressBgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            // Create progress bar fill
            GameObject progressFillObj = new GameObject("ProgressBarFill");
            progressFillObj.transform.SetParent(progressBgObj.transform, false);
            
            RectTransform progressFillRect = progressFillObj.AddComponent<RectTransform>();
            progressFillRect.anchorMin = Vector2.zero;
            progressFillRect.anchorMax = Vector2.one;
            progressFillRect.sizeDelta = Vector2.zero;
            progressFillRect.anchoredPosition = Vector2.zero;

            Image progressFillImage = progressFillObj.AddComponent<Image>();
            progressFillImage.color = new Color(0.2f, 1f, 0.4f, 1f);
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = 0;
            progressFillImage.fillAmount = 0f;

            // Create count text
            GameObject countTextObj = new GameObject("CountText");
            countTextObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform countTextRect = countTextObj.AddComponent<RectTransform>();
            countTextRect.anchorMin = new Vector2(0.7f, 0.5f);
            countTextRect.anchorMax = new Vector2(0.95f, 0.95f);
            countTextRect.sizeDelta = Vector2.zero;
            countTextRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI countText = countTextObj.AddComponent<TextMeshProUGUI>();
            countText.text = "0/6";
            countText.fontSize = 36;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.Center;
            countText.color = Color.white;

            // Create status text
            GameObject statusTextObj = new GameObject("StatusText");
            statusTextObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform statusTextRect = statusTextObj.AddComponent<RectTransform>();
            statusTextRect.anchorMin = new Vector2(0.05f, 0.5f);
            statusTextRect.anchorMax = new Vector2(0.7f, 0.95f);
            statusTextRect.sizeDelta = Vector2.zero;
            statusTextRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI statusText = statusTextObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Return blocks to shelf";
            statusText.fontSize = 24;
            statusText.alignment = TextAlignmentOptions.MidlineLeft;
            statusText.color = Color.white;

            // Add ShelfProgressUI component
            ShelfProgressUI progressUI = canvasObj.AddComponent<ShelfProgressUI>();
            
            // Use SerializedObject to set private fields
            SerializedObject serializedUI = new SerializedObject(progressUI);
            serializedUI.FindProperty("m_ShelfSpawner").objectReferenceValue = shelf.GetComponent<ShelfBlockSpawner>();
            serializedUI.FindProperty("m_LevelManager").objectReferenceValue = Object.FindAnyObjectByType<LevelManager>();
            serializedUI.FindProperty("m_ProgressFill").objectReferenceValue = progressFillImage;
            serializedUI.FindProperty("m_CountText").objectReferenceValue = countText;
            serializedUI.FindProperty("m_StatusText").objectReferenceValue = statusText;
            serializedUI.FindProperty("m_UIPanel").objectReferenceValue = panelObj;
            serializedUI.ApplyModifiedProperties();

            // Mark scene dirty and select the new object
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Shelf Progress UI");
            EditorUtility.SetDirty(canvasObj);
            Selection.activeGameObject = canvasObj;

            Debug.Log("ShelfProgressUI created successfully! Adjust position as needed in the Scene view.");
            EditorUtility.DisplayDialog("Success", 
                "Shelf Progress UI created!\n\n" +
                "The UI will appear above the shelf during the 'Return blocks' phase.\n\n" +
                "You may need to adjust the position in the Scene view.", 
                "OK");
        }
    }
}
