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
        /// Player is destroying the structure with the slingshot.
        /// </summary>
        Destruction,

        /// <summary>
        /// Structure is destroyed, waiting for blocks to be returned to shelf.
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

        [SerializeField, Tooltip("Reference to the DestructionPhaseManager")]
        private DestructionPhaseManager m_DestructionManager;

        [SerializeField, Tooltip("Reference to the XR Setup for player teleportation")]
        private BlockBattleXRSetup m_XRSetup;

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

        [Header("Destruction Phase Settings")]
        [SerializeField, Tooltip("Position where player is teleported for destruction phase")]
        private Transform m_DestructionTeleportPosition;

        [SerializeField, Tooltip("Position where player is teleported back after destruction")]
        private Transform m_BuildingTeleportPosition;

        // Runtime state
        private int m_CurrentLevelIndex = 0;
        private LevelPhase m_CurrentPhase = LevelPhase.Building;
        private float m_ValidationTimer = 0f;
        private bool m_ValidationEnabled = false;
        private int m_ExpectedBlockCount = 0;
        private Coroutine m_CountdownCoroutine = null;
        private bool m_GameStarted = false;
        private float m_GameTimer = 0f;
        private bool m_TimerRunning = false;
        private float m_FinalTime = 0f;

        /// <summary>
        /// Gets whether the game has been started via StartGame().
        /// </summary>
        public bool IsGameStarted => m_GameStarted;

        /// <summary>
        /// Gets the current elapsed game time in seconds.
        /// </summary>
        public float ElapsedTime => m_TimerRunning ? m_GameTimer : m_FinalTime;

        /// <summary>
        /// Gets whether the timer is currently running.
        /// </summary>
        public bool IsTimerRunning => m_TimerRunning;

        /// <summary>
        /// Gets the final completion time (only valid after all levels complete).
        /// </summary>
        public float FinalCompletionTime => m_FinalTime;

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

        /// <summary>
        /// Event fired when destruction phase starts.
        /// </summary>
        public event System.Action OnDestructionPhaseStarted;

        /// <summary>
        /// Event fired when destruction phase completes.
        /// </summary>
        public event System.Action OnDestructionPhaseCompleted;

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
            if (m_DestructionManager == null)
                m_DestructionManager = FindAnyObjectByType<DestructionPhaseManager>();
            if (m_XRSetup == null)
                m_XRSetup = FindAnyObjectByType<BlockBattleXRSetup>();

            // Subscribe to destruction manager events
            if (m_DestructionManager != null)
            {
                m_DestructionManager.OnDestructionComplete += OnDestructionPhaseComplete;
            }

            // Subscribe to shelf events for last level door close detection
            if (m_ShelfSpawner != null)
            {
                m_ShelfSpawner.OnDoorsClosedWithBlocksReturned += OnShelfDoorsClosedWithBlocks;
            }

            // Hide success panel initially
            if (m_SuccessPanel != null)
                m_SuccessPanel.SetActive(false);

            // Game now waits for StartGame() to be called (via StartScreenUI)
            // Do NOT automatically start level here
        }

        /// <summary>
        /// Starts the game. Called by StartScreenUI when player presses the Start button.
        /// </summary>
        public void StartGame()
        {
            if (m_GameStarted)
            {
                Debug.LogWarning("LevelManager: Game already started!");
                return;
            }

            m_GameStarted = true;
            m_GameTimer = 0f;
            m_TimerRunning = true;
            Debug.Log("LevelManager: Game started! Beginning Level 1...");
            
            // Enable player movement
            if (m_XRSetup != null)
            {
                m_XRSetup.SetLocomotionEnabled(true);
            }
            
            // Start with Level 1
            StartLevel(0);
        }

        private void Update()
        {
            // Don't run game loop until game is started
            if (!m_GameStarted)
                return;

            // Update game timer
            if (m_TimerRunning)
            {
                m_GameTimer += Time.deltaTime;
            }

            switch (m_CurrentPhase)
            {
                case LevelPhase.Building:
                    UpdateBuildingPhase();
                    break;

                case LevelPhase.Destruction:
                    // Destruction phase is managed by DestructionPhaseManager
                    // We just wait for the OnDestructionComplete event
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

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (m_DestructionManager != null)
            {
                m_DestructionManager.OnDestructionComplete -= OnDestructionPhaseComplete;
            }
            if (m_ShelfSpawner != null)
            {
                m_ShelfSpawner.OnDoorsClosedWithBlocksReturned -= OnShelfDoorsClosedWithBlocks;
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
        /// On the last level, after blocks are returned, waits for doors to close.
        /// </summary>
        private void UpdateWaitingForReturnPhase()
        {
            if (m_ShelfSpawner == null)
                return;

            // Check if all blocks are returned to shelf
            if (m_ShelfSpawner.AreAllBlocksReturned(m_ExpectedBlockCount))
            {
                // Check if we're already waiting for doors to close
                if (m_ShelfSpawner.IsWaitingForDoorsClose)
                    return;

                bool isLastLevel = (m_CurrentLevelIndex >= m_LevelConfigurations.Count - 1);

                if (isLastLevel)
                {
                    // Last level: blocks returned, now wait for doors to close
                    Debug.Log($"LevelManager: All {m_ExpectedBlockCount} blocks returned! Close the shelf to finish.");
                    m_ShelfSpawner.StartWaitingForDoorsClose(m_ExpectedBlockCount);
                    ShowCloseShelfMessage();
                }
                else
                {
                    // Non-last level: this shouldn't happen (we clear blocks immediately)
                    Debug.Log($"LevelManager: All {m_ExpectedBlockCount} blocks returned to shelf! Starting countdown...");
                    StartCountdown();
                }
            }
        }

        /// <summary>
        /// Called when shelf doors are closed with all blocks returned (last level only).
        /// </summary>
        private void OnShelfDoorsClosedWithBlocks()
        {
            bool isLastLevel = (m_CurrentLevelIndex >= m_LevelConfigurations.Count - 1);
            
            if (isLastLevel && m_CurrentPhase == LevelPhase.WaitingForReturn)
            {
                Debug.Log("LevelManager: Shelf doors closed! Finishing game...");
                
                // Stop timer immediately when doors close
                m_TimerRunning = false;
                m_FinalTime = m_GameTimer;
                
                // Now start the countdown to show final results
                StartCountdown();
            }
        }

        /// <summary>
        /// Updates logic during the countdown phase.
        /// Monitors for conditions that should cancel the countdown.
        /// Only applies to the last level where blocks must stay in the shelf.
        /// </summary>
        private void UpdateCountdownPhase()
        {
            // Only check block return status on the last level
            bool isLastLevel = (m_CurrentLevelIndex >= m_LevelConfigurations.Count - 1);
            if (!isLastLevel)
                return; // No cancellation for non-last levels

            if (m_ShelfSpawner == null)
                return;

            // Cancel countdown if blocks leave the shelf (last level only)
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
        /// Transitions to Destruction phase.
        /// </summary>
        private void OnBuildingComplete()
        {
            // Fire event
            OnBuildingPhaseCompleted?.Invoke(CurrentLevelNumber);

            // Start destruction phase
            StartDestructionPhase();
        }

        /// <summary>
        /// Starts the destruction phase where player shoots at the structure.
        /// </summary>
        private void StartDestructionPhase()
        {
            m_CurrentPhase = LevelPhase.Destruction;
            
            Debug.Log($"LevelManager: Starting destruction phase - teleporting player to slingshot position...");

            // Teleport player to destruction position
            if (m_XRSetup != null && m_DestructionTeleportPosition != null)
            {
                m_XRSetup.TeleportPlayer(m_DestructionTeleportPosition);
            }
            else if (m_DestructionManager != null && m_DestructionManager.ShootingPosition != null)
            {
                // Fallback to destruction manager's shooting position
                if (m_XRSetup != null)
                {
                    m_XRSetup.TeleportPlayer(m_DestructionManager.ShootingPosition);
                }
            }

            // Start the destruction phase manager
            if (m_DestructionManager != null)
            {
                m_DestructionManager.StartDestructionPhase();
            }
            else
            {
                Debug.LogWarning("LevelManager: No DestructionPhaseManager found! Skipping destruction phase.");
                OnDestructionPhaseComplete();
                return;
            }

            // Show destruction UI
            ShowDestructionMessage();

            OnDestructionPhaseStarted?.Invoke();
        }

        /// <summary>
        /// Called when the destruction phase is complete (all blocks knocked off table).
        /// </summary>
        private void OnDestructionPhaseComplete()
        {
            Debug.Log($"LevelManager: Destruction phase complete!");

            // Teleport player back to building position (if specified)
            if (m_XRSetup != null && m_BuildingTeleportPosition != null)
            {
                m_XRSetup.TeleportPlayer(m_BuildingTeleportPosition);
            }

            OnDestructionPhaseCompleted?.Invoke();

            // Check if this is the last level
            bool isLastLevel = (m_CurrentLevelIndex >= m_LevelConfigurations.Count - 1);

            if (isLastLevel)
            {
                // Last level: wait for blocks to be returned to shelf
                Debug.Log($"LevelManager: Last level! Waiting for player to return {m_ExpectedBlockCount} blocks to shelf...");
                m_CurrentPhase = LevelPhase.WaitingForReturn;
                ShowReturnBlocksMessage();
            }
            else
            {
                // Not the last level: clear blocks immediately and proceed to next level
                Debug.Log($"LevelManager: Clearing blocks and proceeding to next level...");
                ClearPlacedBlocks();
                
                // Skip WaitingForReturn phase and go directly to countdown
                StartCountdown();
            }
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
        /// This is only used on the last level when blocks are removed from the shelf.
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
            
            // Resume timer since we're back to waiting
            m_TimerRunning = true;
            
            OnCountdownCancelled?.Invoke();
            
            // Show return blocks message again (only happens on last level)
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
                // All levels complete - stop timer and save final time
                m_TimerRunning = false;
                m_FinalTime = m_GameTimer;
                Debug.Log($"LevelManager: ALL LEVELS COMPLETE! Final time: {FormatTime(m_FinalTime)}");
                ShowAllLevelsCompleteMessage();
                OnAllLevelsCompleted?.Invoke();
            }
        }

        #endregion

        #region UI Messages

        /// <summary>
        /// Shows the destruction phase message.
        /// </summary>
        private void ShowDestructionMessage()
        {
            if (m_SuccessPanel != null)
            {
                m_SuccessPanel.SetActive(true);
            }

            if (m_SuccessText != null)
            {
                m_SuccessText.text = $"Structure Complete!\n\nDestroy your creation!";
            }

            // Also update HUD if available
            if (m_GameplayHUD != null)
            {
                m_GameplayHUD.ShowDestructionMessage(m_ExpectedBlockCount);
            }
        }

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
        /// Shows the "close the shelf" message (last level only).
        /// </summary>
        private void ShowCloseShelfMessage()
        {
            if (m_SuccessPanel != null)
            {
                m_SuccessPanel.SetActive(true);
            }

            if (m_SuccessText != null)
            {
                m_SuccessText.text = $"All blocks returned!\n\nClose the shelf to finish!";
            }

            // Also update HUD if available
            if (m_GameplayHUD != null)
            {
                m_GameplayHUD.ShowStatusMessage("Close the shelf to finish!");
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
                bool isLastLevel = (m_CurrentLevelIndex >= m_LevelConfigurations.Count - 1);
                
                if (isLastLevel)
                {
                    m_SuccessText.text = $"Shelf Closed!\n\nFinal time: {FormatTime(m_FinalTime)}\n\nResults in {secondsRemaining}...";
                }
                else
                {
                    m_SuccessText.text = $"Great job!\n\nLevel {nextLevel} starting in {secondsRemaining}...";
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

        /// <summary>
        /// Resets the game to initial state so it can be played again.
        /// Call this before showing the start screen for a new game.
        /// </summary>
        public void ResetGame()
        {
            // Cancel any ongoing countdown
            if (m_CountdownCoroutine != null)
            {
                StopCoroutine(m_CountdownCoroutine);
                m_CountdownCoroutine = null;
            }

            // Reset state
            m_GameStarted = false;
            m_CurrentLevelIndex = 0;
            m_CurrentPhase = LevelPhase.Building;
            m_ValidationEnabled = false;
            m_ValidationTimer = 0f;
            m_GameTimer = 0f;
            m_TimerRunning = false;

            // Clear existing blocks
            ClearPlacedBlocks();

            // Clear reference structure
            if (m_ReferenceSpawner != null)
            {
                m_ReferenceSpawner.ClearStructure();
            }

            // Hide success panel
            if (m_SuccessPanel != null)
            {
                m_SuccessPanel.SetActive(false);
            }

            // Reset HUD
            if (m_GameplayHUD != null)
            {
                m_GameplayHUD.ResetHUD();
            }

            // Disable player movement
            if (m_XRSetup != null)
            {
                m_XRSetup.SetLocomotionEnabled(false);
            }

            Debug.Log("LevelManager: Game reset. Ready for new game.");
        }

        /// <summary>
        /// Formats a time value in seconds to a readable MM:SS.ss format.
        /// </summary>
        /// <param name="timeInSeconds">Time in seconds</param>
        /// <returns>Formatted time string</returns>
        public static string FormatTime(float timeInSeconds)
        {
            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            float seconds = timeInSeconds % 60f;
            return $"{minutes:00}:{seconds:00.00}";
        }

        #endregion
    }
}
