using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlockBattle
{
    /// <summary>
    /// World-space UI that displays block return progress above the shelf.
    /// Shows a progress bar and text indicating how many blocks have been returned.
    /// </summary>
    public class ShelfProgressUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Reference to the ShelfBlockSpawner to monitor")]
        private ShelfBlockSpawner m_ShelfSpawner;

        [SerializeField, Tooltip("Reference to the LevelManager for expected block count")]
        private LevelManager m_LevelManager;

        [Header("UI Elements")]
        [SerializeField, Tooltip("The progress bar fill image (should use Image.fillAmount)")]
        private Image m_ProgressFill;

        [SerializeField, Tooltip("Text showing block count (e.g., '3/6 blocks')")]
        private TextMeshProUGUI m_CountText;

        [SerializeField, Tooltip("Text showing status message")]
        private TextMeshProUGUI m_StatusText;

        [SerializeField, Tooltip("The canvas or panel to show/hide")]
        private GameObject m_UIPanel;

        [Header("Colors")]
        [SerializeField]
        private Color m_EmptyColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [SerializeField]
        private Color m_PartialColor = new Color(1f, 0.8f, 0.2f, 1f);

        [SerializeField]
        private Color m_CompleteColor = new Color(0.2f, 1f, 0.4f, 1f);

        [Header("Settings")]
        [SerializeField, Tooltip("Only show UI during WaitingForReturn phase")]
        private bool m_OnlyShowDuringReturnPhase = true;

        private int m_ExpectedBlockCount = 0;
        private int m_LastStoredCount = -1;
        private bool _showingFinalResults = false;

        private void Start()
        {
            // Find references if not assigned
            if (m_ShelfSpawner == null)
                m_ShelfSpawner = FindAnyObjectByType<ShelfBlockSpawner>();
            if (m_LevelManager == null)
                m_LevelManager = FindAnyObjectByType<LevelManager>();

            // Subscribe to level events
            if (m_LevelManager != null)
            {
                m_LevelManager.OnLevelStarted += OnLevelStarted;
            }

            // Initially hide
            if (m_UIPanel != null && m_OnlyShowDuringReturnPhase)
            {
                m_UIPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (m_LevelManager != null)
            {
                m_LevelManager.OnLevelStarted -= OnLevelStarted;
            }
        }

        private void Update()
        {
            if (m_ShelfSpawner == null)
                return;

            // Check if we should show the UI
            bool shouldShow = (m_LevelManager != null && m_LevelManager.IsGameStarted) &&
                             (_showingFinalResults || 
                             (m_LevelManager.CurrentPhase == LevelPhase.WaitingForReturn));

            if (m_UIPanel != null)
            {
                m_UIPanel.SetActive(shouldShow);
            }

            if (!shouldShow)
                return;

            // Get expected block count from current level configuration through the spawner
            if (m_ShelfSpawner.SpawnConfiguration != null)
            {
                m_ExpectedBlockCount = m_ShelfSpawner.SpawnConfiguration.SpawnEntries?.Count ?? 0;
            }

            // Update progress
            int currentCount = m_ShelfSpawner.StoredBlockCount;
            
            // Only update UI when count changes
            if (currentCount != m_LastStoredCount)
            {
                m_LastStoredCount = currentCount;
                UpdateProgressUI(currentCount, m_ExpectedBlockCount);
            }
        }

        /// <summary>
        /// Displays the final completion time on the status UI.
        /// </summary>
        /// <param name="timeInSeconds">The final time to display</param>
        public void ShowFinalTime(float timeInSeconds)
        {
            _showingFinalResults = true;

            // Ensure the panel is actually visible
            if (m_UIPanel != null) m_UIPanel.SetActive(true);

            if (m_StatusText != null)
            {
                // Format using LevelManager's consistent timing format
                m_StatusText.text = $"Shelf Closed!\nTime needed: {LevelManager.FormatTime(timeInSeconds)}";
                m_StatusText.color = m_CompleteColor;
            }

            // Hide the count and fill bar as they are no longer relevant
            if (m_CountText != null) m_CountText.gameObject.SetActive(false);
            if (m_ProgressFill != null && m_ProgressFill.transform.parent != null) 
                m_ProgressFill.transform.parent.gameObject.SetActive(false);
        }

        private void OnLevelStarted(int levelNumber)
        {
            _showingFinalResults = false;

            // Restore UI elements if they were hidden by ShowFinalTime
            if (m_CountText != null) m_CountText.gameObject.SetActive(true);
            if (m_ProgressFill != null && m_ProgressFill.transform.parent != null) 
                m_ProgressFill.transform.parent.gameObject.SetActive(true);

            m_LastStoredCount = -1; // Force UI update
        }

        /// <summary>
        /// Updates the progress bar and text.
        /// </summary>
        private void UpdateProgressUI(int currentCount, int expectedCount)
        {
            if (expectedCount <= 0)
                expectedCount = 1; // Avoid division by zero

            float progress = Mathf.Clamp01((float)currentCount / expectedCount);

            // Update fill
            if (m_ProgressFill != null)
            {
                m_ProgressFill.fillAmount = progress;

                // Update color based on progress
                if (currentCount >= expectedCount)
                {
                    m_ProgressFill.color = m_CompleteColor;
                }
                else if (currentCount > 0)
                {
                    m_ProgressFill.color = Color.Lerp(m_PartialColor, m_CompleteColor, progress);
                }
                else
                {
                    m_ProgressFill.color = m_EmptyColor;
                }
            }

            // Update count text
            if (m_CountText != null)
            {
                m_CountText.text = $"{currentCount}/{expectedCount}";
                
                if (currentCount >= expectedCount)
                {
                    m_CountText.color = m_CompleteColor;
                }
                else
                {
                    m_CountText.color = Color.white;
                }
            }

            // Update status text
            if (m_StatusText != null)
            {
                if (currentCount >= expectedCount)
                {
                    m_StatusText.text = "Close the doors";
                    m_StatusText.color = m_CompleteColor;
                }
                else
                {
                    int remaining = expectedCount - currentCount;
                    m_StatusText.text = $"Return {remaining} more block{(remaining != 1 ? "s" : "")}";
                    m_StatusText.color = Color.white;
                }
            }
        }

        /// <summary>
        /// Manually sets the expected block count (if not using LevelManager).
        /// </summary>
        public void SetExpectedBlockCount(int count)
        {
            m_ExpectedBlockCount = count;
            m_LastStoredCount = -1; // Force UI update
        }

        /// <summary>
        /// Shows or hides the UI panel.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (m_UIPanel != null)
            {
                m_UIPanel.SetActive(visible);
            }
        }
    }
}
