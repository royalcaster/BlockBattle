using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace BlockBattle
{
    /// <summary>
    /// Tracks the current phase of level progression.
    /// </summary>
    public enum LevelPhase
    {
        /// <summary>
        /// Player is building the structure.
        /// </summary>
        Building,

        /// <summary>
        /// Structure is complete, waiting for blocks to be returned to shelf.
        /// </summary>
        WaitingForReturn,

        /// <summary>
        /// Blocks returned, counting down to next level.
        /// </summary>
        Countdown,

        /// <summary>
        /// Transitioning to next level.
        /// </summary>
        Transitioning
    }

    /// <summary>
    /// Manages level progression in BlockBattle.
    /// Starts with Level 1, detects completion, waits for block return, and advances to next level.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("Level Configurations")]
        [SerializeField, Tooltip("List of level configurations in order (Level 1, Level 2, etc.)")]
        private List<BlockSpawnConfiguration> m_LevelConfigurations = new List<BlockSpawnConfiguration>();

        [Header("References")]
        [SerializeField, Tooltip("Reference to the ReferenceStructureSpawner")]
        private ReferenceStructureSpawner m_ReferenceSpawner;

        [SerializeField, Tooltip("Reference to the ShelfBlockSpawner (spawns blocks inside shelf)")]
        private ShelfBlockSpawner m_ShelfSpawner;

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

        [Header("Block Return Settings")]
        [SerializeField, Tooltip("Countdown duration before starting next level (after blocks returned)")]
        private float m_CountdownDuration = 3f;

        // Runtime state
        private int m_CurrentLevelIndex = 0;
        private LevelPhase m_CurrentPhase = LevelPhase.Building;
        private float m_ValidationTimer = 0f;
        private bool m_ValidationEnabled = false;
        private int m_ExpectedBlockCount = 0;
        private Coroutine m_CountdownCoroutine = null;

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
        /// Gets the current level phase.
        /// </summary>
        public LevelPhase CurrentPhase => m_CurrentPhase;

        /// <summary>
        /// Event fired when a level's building phase is completed (structure built correctly).
        /// </summary>
        public event System.Action<int> OnBuildingPhaseCompleted;

        /// <summary>
        /// Event fired when a level is fully completed (blocks returned and doors closed).
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

        /// <summary>
        /// Event fired when the countdown starts.
        /// </summary>
        public event System.Action<float> OnCountdownStarted;

        /// <summary>
        /// Event fired during countdown with remaining time.
        /// </summary>
        public event System.Action<float> OnCountdownTick;

        /// <summary>
        /// Event fired when countdown is cancelled.
        /// </summary>
        public event System.Action OnCountdownCancelled;

        private void Start()
        {
            // Find references if not assigned
            if (m_ReferenceSpawner == null)
                m_ReferenceSpawner = FindAnyObjectByType<ReferenceStructureSpawner>();
            if (m_ShelfSpawner == null)
                m_ShelfSpawner = FindAnyObjectByType<ShelfBlockSpawner>();
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
            switch (m_CurrentPhase)
            {
                case LevelPhase.Building:
                    UpdateBuildingPhase();
                    break;

                case LevelPhase.WaitingForReturn:
                    UpdateWaitingForReturnPhase();
                    break;

                case LevelPhase.Countdown:
                    // Countdown is handled by coroutine, but we monitor for cancellation
                    UpdateCountdownPhase();
                    break;

                case LevelPhase.Transitioning:
                    // Nothing to do, waiting for transition to complete
                    break;
            }
        }

        #region Phase Updates

        /// <summary>
        /// Updates logic during the building phase.
        /// </summary>
        private void UpdateBuildingPhase()
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

            // Check if current level's structure is complete
            CheckBuildingCompletion();
        }

        /// <summary>
        /// Updates logic during the waiting for return phase.
        /// </summary>
        private void UpdateWaitingForReturnPhase()
        {
            if (m_ShelfSpawner == null)
                return;

            // Check if all blocks are returned to shelf
            if (m_ShelfSpawner.AreAllBlocksReturned(m_ExpectedBlockCount))
            {
                Debug.Log($"LevelManager: All {m_ExpectedBlockCount} blocks returned to shelf! Starting countdown...");
                StartCountdown();
            }
        }

        /// <summary>
        /// Updates logic during the countdown phase.
        /// Monitors for conditions that should cancel the countdown.
        /// </summary>
        private void UpdateCountdownPhase()
        {
            if (m_ShelfSpawner == null)
                return;

            // Cancel countdown if blocks leave the shelf
            if (!m_ShelfSpawner.AreAllBlocksReturned(m_ExpectedBlockCount))
            {
                CancelCountdown();
            }
        }

        #endregion

        #region Level Management

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
            m_CurrentPhase = LevelPhase.Building;
            m_ValidationEnabled = false;
            m_ValidationTimer = 0f;

            BlockSpawnConfiguration levelConfig = m_LevelConfigurations[levelIndex];
            m_ExpectedBlockCount = levelConfig.SpawnEntries?.Count ?? 0;
            
            Debug.Log($"LevelManager: Starting Level {CurrentLevelNumber} - {levelConfig.ConfigurationName} ({m_ExpectedBlockCount} blocks)");

            // Clear existing blocks in build zone
            ClearPlacedBlocks();

            // Spawn reference structure
            if (m_ReferenceSpawner != null)
            {
                m_ReferenceSpawner.SpawnStructure(levelConfig);
            }

            // Spawn player blocks inside shelf
            if (m_ShelfSpawner != null)
            {
                m_ShelfSpawner.SpawnConfiguration = levelConfig;
                m_ShelfSpawner.SpawnBlocks();
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
        /// Checks if the current level's building phase is complete.
        /// </summary>
        private void CheckBuildingCompletion()
        {
            if (m_BuildValidator == null)
                return;

            BuildValidationResult result = m_BuildValidator.ValidateBuild();
            if (result == null)
                return;

            // Check if accuracy meets threshold
            if (result.AccuracyPercentage >= m_CompletionThreshold)
            {
                Debug.Log($"LevelManager: Level {CurrentLevelNumber} BUILDING COMPLETE! Accuracy: {result.AccuracyPercentage:F1}%");
                OnBuildingComplete();
            }
        }

        /// <summary>
        /// Called when the building phase is complete.
        /// Transitions to WaitingForReturn phase.
        /// </summary>
        private void OnBuildingComplete()
        {
            m_CurrentPhase = LevelPhase.WaitingForReturn;
            
            // Fire event
            OnBuildingPhaseCompleted?.Invoke(CurrentLevelNumber);

            // Show return blocks message
            ShowReturnBlocksMessage();

            Debug.Log($"LevelManager: Waiting for player to return {m_ExpectedBlockCount} blocks to shelf and close doors...");
        }

        #endregion

        #region Countdown

        /// <summary>
        /// Starts the countdown to the next level.
        /// </summary>
        private void StartCountdown()
        {
            if (m_CountdownCoroutine != null)
            {
                StopCoroutine(m_CountdownCoroutine);
            }

            m_CurrentPhase = LevelPhase.Countdown;
            m_CountdownCoroutine = StartCoroutine(CountdownCoroutine());
            OnCountdownStarted?.Invoke(m_CountdownDuration);
        }

        /// <summary>
        /// Cancels the current countdown and returns to WaitingForReturn phase.
        /// </summary>
        private void CancelCountdown()
        {
            if (m_CountdownCoroutine != null)
            {
                StopCoroutine(m_CountdownCoroutine);
                m_CountdownCoroutine = null;
            }

            m_CurrentPhase = LevelPhase.WaitingForReturn;
            Debug.Log("LevelManager: Countdown cancelled - blocks removed from shelf!");
            
            OnCountdownCancelled?.Invoke();
            
            // Show return blocks message again
            ShowReturnBlocksMessage();
        }

        /// <summary>
        /// Countdown coroutine that waits and then transitions to the next level.
        /// </summary>
        private IEnumerator CountdownCoroutine()
        {
            float remainingTime = m_CountdownDuration;

            // Show countdown message
            ShowCountdownMessage(Mathf.CeilToInt(remainingTime));

            while (remainingTime > 0)
            {
                yield return new WaitForSeconds(1f);
                remainingTime -= 1f;

                if (remainingTime > 0)
                {
                    OnCountdownTick?.Invoke(remainingTime);
                    ShowCountdownMessage(Mathf.CeilToInt(remainingTime));
                }
            }

            // Countdown complete - transition to next level
            m_CountdownCoroutine = null;
            OnLevelFullyComplete();
        }

        /// <summary>
        /// Called when a level is fully complete (building done + blocks returned).
        /// </summary>
        private void OnLevelFullyComplete()
        {
            m_CurrentPhase = LevelPhase.Transitioning;

            // Fire completion event
            OnLevelCompleted?.Invoke(CurrentLevelNumber);

            // Show brief success message
            ShowSuccessMessage();

            // Start transition to next level
            StartCoroutine(TransitionToNextLevel());
        }

        /// <summary>
        /// Handles the transition to the next level.
        /// </summary>
        private IEnumerator TransitionToNextLevel()
        {
            // Brief pause to show success message
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
        }

        #endregion

        #region UI Messages

        /// <summary>
        /// Shows the "return blocks to shelf" message.
        /// </summary>
        private void ShowReturnBlocksMessage()
        {
            if (m_SuccessPanel != null)
            {
                m_SuccessPanel.SetActive(true);
            }

            if (m_SuccessText != null)
            {
                m_SuccessText.text = $"Structure Complete!\n\nReturn all blocks to the shelf.";
            }

            // Also update HUD if available
            if (m_GameplayHUD != null)
            {
                m_GameplayHUD.ShowReturnBlocksMessage(m_ExpectedBlockCount);
            }
        }

        /// <summary>
        /// Shows the countdown message.
        /// </summary>
        /// <param name="secondsRemaining">Seconds remaining in countdown</param>
        private void ShowCountdownMessage(int secondsRemaining)
        {
            if (m_SuccessText != null)
            {
                int nextLevel = m_CurrentLevelIndex + 2;
                if (nextLevel <= m_LevelConfigurations.Count)
                {
                    m_SuccessText.text = $"Blocks Returned!\n\nLevel {nextLevel} starting in {secondsRemaining}...";
                }
                else
                {
                    m_SuccessText.text = $"Blocks Returned!\n\nFinal results in {secondsRemaining}...";
                }
            }

            // Also update HUD if available
            if (m_GameplayHUD != null)
            {
                m_GameplayHUD.ShowCountdownMessage(secondsRemaining);
            }
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
                int nextLevel = m_CurrentLevelIndex + 2;
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

        #endregion

        #region Block Management

        /// <summary>
        /// Clears all placed blocks in the build zone.
        /// </summary>
        private void ClearPlacedBlocks()
        {
            // First, clear blocks from shelf spawner if available
            if (m_ShelfSpawner != null)
            {
                m_ShelfSpawner.ClearSpawnedBlocks();
            }

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

                // Destroy player blocks (handles both old and new naming)
                if (name.Contains("_Spawned") || name.Contains("_Shelf") || name.Contains("Block_"))
                {
                    Destroy(interactable.gameObject);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Restarts the current level.
        /// </summary>
        public void RestartLevel()
        {
            // Cancel any ongoing countdown
            if (m_CountdownCoroutine != null)
            {
                StopCoroutine(m_CountdownCoroutine);
                m_CountdownCoroutine = null;
            }

            StartLevel(m_CurrentLevelIndex);
        }

        /// <summary>
        /// Skips to the next level (for testing).
        /// </summary>
        public void SkipLevel()
        {
            // Cancel any ongoing countdown
            if (m_CountdownCoroutine != null)
            {
                StopCoroutine(m_CountdownCoroutine);
                m_CountdownCoroutine = null;
            }

            int nextLevelIndex = m_CurrentLevelIndex + 1;
            if (nextLevelIndex < m_LevelConfigurations.Count)
            {
                StartLevel(nextLevelIndex);
            }
        }

        #endregion
    }
}
