using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace BlockBattle
{
    /// <summary>
    /// Manages level progression in BlockBattle.
    /// Starts with Level 1, detects completion, shows success message, and advances to next level.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("Level Configurations")]
        [SerializeField, Tooltip("List of level configurations in order (Level 1, Level 2, etc.)")]
        private List<BlockSpawnConfiguration> m_LevelConfigurations = new List<BlockSpawnConfiguration>();

        [Header("References")]
        [SerializeField, Tooltip("Reference to the ReferenceStructureSpawner")]
        private ReferenceStructureSpawner m_ReferenceSpawner;

        [SerializeField, Tooltip("Reference to the BlockSpawner")]
        private BlockSpawner m_BlockSpawner;

        [SerializeField, Tooltip("Reference to the BuildValidator")]
        private BuildValidator m_BuildValidator;

        [SerializeField, Tooltip("Reference to the BuildZonePlacementGuides")]
        private BuildZonePlacementGuides m_PlacementGuides;

        [SerializeField, Tooltip("Reference to the GameplayHUD")]
        private GameplayHUD m_GameplayHUD;

        [Header("UI Elements")]
        [SerializeField, Tooltip("Text element to show current level (optional - can also use GameplayHUD)")]
        private TextMeshProUGUI m_LevelText;

        [SerializeField, Tooltip("Panel for success message (will be shown/hidden)")]
        private GameObject m_SuccessPanel;

        [SerializeField, Tooltip("Text for success message")]
        private TextMeshProUGUI m_SuccessText;

        [Header("Settings")]
        [SerializeField, Tooltip("Accuracy percentage required to complete a level (0-100)")]
        [Range(90f, 100f)]
        private float m_CompletionThreshold = 100f;

        [SerializeField, Tooltip("How long to show success message before loading next level")]
        private float m_SuccessDisplayDuration = 3f;

        [SerializeField, Tooltip("Delay before starting validation after level loads")]
        private float m_ValidationStartDelay = 1f;

        // Runtime state
        private int m_CurrentLevelIndex = 0;
        private bool m_IsLevelComplete = false;
        private bool m_IsTransitioning = false;
        private float m_ValidationTimer = 0f;
        private bool m_ValidationEnabled = false;

        /// <summary>
        /// Gets the current level number (1-based for display).
        /// </summary>
        public int CurrentLevelNumber => m_CurrentLevelIndex + 1;

        /// <summary>
        /// Gets the total number of levels.
        /// </summary>
        public int TotalLevels => m_LevelConfigurations.Count;

        /// <summary>
        /// Gets whether all levels are completed.
        /// </summary>
        public bool AllLevelsComplete => m_CurrentLevelIndex >= m_LevelConfigurations.Count;

        /// <summary>
        /// Event fired when a level is completed.
        /// </summary>
        public event System.Action<int> OnLevelCompleted;

        /// <summary>
        /// Event fired when a new level starts.
        /// </summary>
        public event System.Action<int> OnLevelStarted;

        /// <summary>
        /// Event fired when all levels are completed.
        /// </summary>
        public event System.Action OnAllLevelsCompleted;

        private void Start()
        {
            // Find references if not assigned
            if (m_ReferenceSpawner == null)
                m_ReferenceSpawner = FindAnyObjectByType<ReferenceStructureSpawner>();
            if (m_BlockSpawner == null)
                m_BlockSpawner = FindAnyObjectByType<BlockSpawner>();
            if (m_BuildValidator == null)
                m_BuildValidator = FindAnyObjectByType<BuildValidator>();
            if (m_PlacementGuides == null)
                m_PlacementGuides = FindAnyObjectByType<BuildZonePlacementGuides>();
            if (m_GameplayHUD == null)
                m_GameplayHUD = FindAnyObjectByType<GameplayHUD>();

            // Hide success panel initially
            if (m_SuccessPanel != null)
                m_SuccessPanel.SetActive(false);

            // Start with Level 1
            StartLevel(0);
        }

        private void Update()
        {
            // Wait for validation delay after level load
            if (!m_ValidationEnabled)
            {
                m_ValidationTimer += Time.deltaTime;
                if (m_ValidationTimer >= m_ValidationStartDelay)
                {
                    m_ValidationEnabled = true;
                }
                return;
            }

            // Don't check completion during transitions or if already complete
            if (m_IsTransitioning || m_IsLevelComplete)
                return;

            // Check if current level is complete
            CheckLevelCompletion();
        }

        /// <summary>
        /// Starts a specific level by index.
        /// </summary>
        /// <param name="levelIndex">Zero-based level index</param>
        public void StartLevel(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= m_LevelConfigurations.Count)
            {
                Debug.LogError($"LevelManager: Invalid level index {levelIndex}. Available levels: 0-{m_LevelConfigurations.Count - 1}");
                return;
            }

            m_CurrentLevelIndex = levelIndex;
            m_IsLevelComplete = false;
            m_ValidationEnabled = false;
            m_ValidationTimer = 0f;

            BlockSpawnConfiguration levelConfig = m_LevelConfigurations[levelIndex];
            Debug.Log($"LevelManager: Starting Level {CurrentLevelNumber} - {levelConfig.ConfigurationName}");

            // Clear existing blocks in build zone
            ClearPlacedBlocks();

            // Spawn reference structure
            if (m_ReferenceSpawner != null)
            {
                m_ReferenceSpawner.SpawnStructure(levelConfig);
            }

            // Spawn player blocks
            if (m_BlockSpawner != null)
            {
                m_BlockSpawner.SpawnConfiguration = levelConfig;
                m_BlockSpawner.SpawnBlocks();
            }

            // Update validator
            if (m_BuildValidator != null)
            {
                m_BuildValidator.ReferenceConfiguration = levelConfig;
                m_BuildValidator.ResetAlignmentLock();
            }

            // Update UI
            UpdateLevelUI();

            // Fire event
            OnLevelStarted?.Invoke(CurrentLevelNumber);
        }

        /// <summary>
        /// Checks if the current level is complete.
        /// </summary>
        private void CheckLevelCompletion()
        {
            if (m_BuildValidator == null)
                return;

            BuildValidationResult result = m_BuildValidator.ValidateBuild();
            if (result == null)
                return;

            // Check if accuracy meets threshold
            if (result.AccuracyPercentage >= m_CompletionThreshold)
            {
                m_IsLevelComplete = true;
                Debug.Log($"LevelManager: Level {CurrentLevelNumber} COMPLETE! Accuracy: {result.AccuracyPercentage:F1}%");
                StartCoroutine(HandleLevelCompletion());
            }
        }

        /// <summary>
        /// Handles level completion - shows message and advances to next level.
        /// </summary>
        private IEnumerator HandleLevelCompletion()
        {
            m_IsTransitioning = true;

            // Fire completion event
            OnLevelCompleted?.Invoke(CurrentLevelNumber);

            // Show success message
            ShowSuccessMessage();

            // Wait for display duration
            yield return new WaitForSeconds(m_SuccessDisplayDuration);

            // Hide success message
            HideSuccessMessage();

            // Check if there are more levels
            int nextLevelIndex = m_CurrentLevelIndex + 1;
            if (nextLevelIndex < m_LevelConfigurations.Count)
            {
                // Start next level
                StartLevel(nextLevelIndex);
            }
            else
            {
                // All levels complete!
                Debug.Log("LevelManager: ALL LEVELS COMPLETE! Congratulations!");
                ShowAllLevelsCompleteMessage();
                OnAllLevelsCompleted?.Invoke();
            }

            m_IsTransitioning = false;
        }

        /// <summary>
        /// Shows the success message panel.
        /// </summary>
        private void ShowSuccessMessage()
        {
            if (m_SuccessPanel != null)
            {
                m_SuccessPanel.SetActive(true);
            }

            if (m_SuccessText != null)
            {
                int nextLevel = m_CurrentLevelIndex + 2; // +2 because index is 0-based and we want next level
                if (nextLevel <= m_LevelConfigurations.Count)
                {
                    m_SuccessText.text = $"Level {CurrentLevelNumber} Complete!\n\nGet ready for Level {nextLevel}...";
                }
                else
                {
                    m_SuccessText.text = $"Level {CurrentLevelNumber} Complete!\n\nYou've finished all levels!";
                }
            }
        }

        /// <summary>
        /// Hides the success message panel.
        /// </summary>
        private void HideSuccessMessage()
        {
            if (m_SuccessPanel != null)
            {
                m_SuccessPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Shows the final completion message.
        /// </summary>
        private void ShowAllLevelsCompleteMessage()
        {
            if (m_SuccessPanel != null)
            {
                m_SuccessPanel.SetActive(true);
            }

            if (m_SuccessText != null)
            {
                m_SuccessText.text = "Congratulations!\n\nYou've completed all levels!";
            }
        }

        /// <summary>
        /// Updates the level indicator in the UI.
        /// </summary>
        private void UpdateLevelUI()
        {
            // Update dedicated level text if assigned
            if (m_LevelText != null)
            {
                m_LevelText.text = $"Level {CurrentLevelNumber}";
            }

            // Also update GameplayHUD level text
            if (m_GameplayHUD != null)
            {
                m_GameplayHUD.SetLevelText(CurrentLevelNumber);
                m_GameplayHUD.ResetHUD();
            }
        }

        /// <summary>
        /// Clears all placed blocks in the build zone.
        /// </summary>
        private void ClearPlacedBlocks()
        {
            // Find all XRGrabInteractable blocks that aren't reference blocks
            XRGrabInteractable[] allInteractables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            
            foreach (var interactable in allInteractables)
            {
                if (interactable == null || interactable.gameObject == null)
                    continue;

                // Skip reference structure blocks
                string name = interactable.gameObject.name;
                if (name.StartsWith("ReferenceBlock_") || name.Contains("Reference"))
                    continue;

                // Destroy player blocks
                if (name.Contains("_Spawned") || name.Contains("Block_"))
                {
                    Destroy(interactable.gameObject);
                }
            }
        }

        /// <summary>
        /// Restarts the current level.
        /// </summary>
        public void RestartLevel()
        {
            StartLevel(m_CurrentLevelIndex);
        }

        /// <summary>
        /// Skips to the next level (for testing).
        /// </summary>
        public void SkipLevel()
        {
            int nextLevelIndex = m_CurrentLevelIndex + 1;
            if (nextLevelIndex < m_LevelConfigurations.Count)
            {
                StartLevel(nextLevelIndex);
            }
        }
    }
}

