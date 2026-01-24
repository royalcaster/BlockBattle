using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using BlockBattle.Network;

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

        [Header("Multiplayer")]
        [SerializeField, Tooltip("If true, delegates to NetworkedLevelManager when in multiplayer")]
        private bool m_UseNetworkManager = true;

        // Runtime state
        private int m_CurrentLevelIndex = 0;
        private LevelPhase m_CurrentPhase = LevelPhase.Building;
        private float m_ValidationTimer = 0f;
        private bool m_ValidationEnabled = false;
        private int m_ExpectedBlockCount = 0;
        private Coroutine m_CountdownCoroutine = null;
        private NetworkedLevelManager m_NetworkedLevelManager;
        private bool m_IsMultiplayerMode = false;
        private bool m_WaitingForMultiplayerConnection = false;
        private bool m_GameStarted = false;

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

        /// <summary>
        /// Gets whether the game is running in multiplayer mode.
        /// </summary>
        public bool IsMultiplayerMode => m_IsMultiplayerMode;

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

            // Hide success panel initially
            if (m_SuccessPanel != null)
                m_SuccessPanel.SetActive(false);

            // Check if Lobby UI exists - if so, we're expecting multiplayer
            // and should wait for connection before starting the game
            var lobbyUI = FindAnyObjectByType<XRMultiplayer.LobbyUI>();
            if (lobbyUI != null && m_UseNetworkManager)
            {
                // Wait for multiplayer connection
                m_WaitingForMultiplayerConnection = true;
                Debug.Log("LevelManager: Lobby UI detected - waiting for multiplayer connection before starting game");
                
                // Subscribe to connection events
                if (XRMultiplayer.XRINetworkGameManager.Instance != null)
                {
                    XRMultiplayer.XRINetworkGameManager.Connected.Subscribe(OnMultiplayerConnectionChanged);
                }
            }
            else
            {
                // No Lobby UI - check current multiplayer state and start immediately
                CheckMultiplayerMode();
                if (!m_IsMultiplayerMode)
                {
            StartLevel(0);
                    m_GameStarted = true;
                }
            }
        }
        
        /// <summary>
        /// Called when multiplayer connection state changes.
        /// </summary>
        private void OnMultiplayerConnectionChanged(bool connected)
        {
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"=== OnMultiplayerConnectionChanged: connected={connected}, m_GameStarted={m_GameStarted} ===");
            
            if (connected && !m_GameStarted)
            {
                BlockBattle.Debugging.DebugLogManager.LevelMgr("Multiplayer connected - processing...");
                m_WaitingForMultiplayerConnection = false;
                CheckMultiplayerMode();
                
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"After CheckMultiplayerMode: m_IsMultiplayerMode={m_IsMultiplayerMode}");
                
                // In multiplayer, NetworkedLevelManager handles the game start
                // But we still need to set up our local references
                if (!m_IsMultiplayerMode)
                {
                    // Fallback to single player if multiplayer setup failed
                    BlockBattle.Debugging.DebugLogManager.LogWarning(BlockBattle.Debugging.DebugLogManager.LogCategory.LevelManager, "Multiplayer mode not detected, falling back to single player");
                    StartLevel(0);
                }
                else
                {
                    BlockBattle.Debugging.DebugLogManager.LevelMgr("Multiplayer mode active - NetworkedLevelManager should handle game start");
                }
                m_GameStarted = true;
            }
            else if (!connected)
            {
                BlockBattle.Debugging.DebugLogManager.LevelMgr("Multiplayer disconnected");
            }
        }

        private void Update()
        {
            // In multiplayer mode, most logic is handled by NetworkedLevelManager
            if (m_IsMultiplayerMode)
            {
                // Only handle local validation reporting in multiplayer
                UpdateMultiplayerValidation();
                return;
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

            // Unsubscribe from networked level manager events
            if (m_NetworkedLevelManager != null)
            {
                m_NetworkedLevelManager.OnPhaseChanged -= OnNetworkPhaseChanged;
            }
            
            // Unsubscribe from multiplayer connection events
            XRMultiplayer.XRINetworkGameManager.Connected.Unsubscribe(OnMultiplayerConnectionChanged);
        }

        #region Multiplayer Support

        /// <summary>
        /// Checks if we're in multiplayer mode and sets up accordingly.
        /// </summary>
        private void CheckMultiplayerMode()
        {
            BlockBattle.Debugging.DebugLogManager.LevelMgr("=== CheckMultiplayerMode ===");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  m_UseNetworkManager: {m_UseNetworkManager}");
            
            if (!m_UseNetworkManager)
            {
                m_IsMultiplayerMode = false;
                BlockBattle.Debugging.DebugLogManager.LevelMgr("  UseNetworkManager is FALSE - single player mode");
                return;
            }

            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  NetworkManager.Singleton: {(NetworkManager.Singleton != null ? "EXISTS" : "NULL")}");
            
            if (NetworkManager.Singleton != null)
            {
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsConnectedClient: {NetworkManager.Singleton.IsConnectedClient}");
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsListening: {NetworkManager.Singleton.IsListening}");
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsHost: {NetworkManager.Singleton.IsHost}");
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsServer: {NetworkManager.Singleton.IsServer}");
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsClient: {NetworkManager.Singleton.IsClient}");
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"  LocalClientId: {NetworkManager.Singleton.LocalClientId}");
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"  CurrentSessionOwner: {NetworkManager.Singleton.CurrentSessionOwner}");
            }

            // Check if NetworkManager exists and we're connected
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                m_IsMultiplayerMode = true;
                m_NetworkedLevelManager = NetworkedLevelManager.Instance;
                
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"  NetworkedLevelManager.Instance: {(m_NetworkedLevelManager != null ? "EXISTS" : "NULL")}");

                if (m_NetworkedLevelManager != null)
                {
                    // Subscribe to networked events
                    m_NetworkedLevelManager.OnPhaseChanged += OnNetworkPhaseChanged;
                    BlockBattle.Debugging.DebugLogManager.LevelMgr("  Running in MULTIPLAYER mode, delegating to NetworkedLevelManager");
                    
                    // Check if NetworkedLevelManager is spawned
                    var no = m_NetworkedLevelManager.GetComponent<NetworkObject>();
                    if (no != null)
                    {
                        BlockBattle.Debugging.DebugLogManager.LevelMgr($"  NetworkedLevelManager.IsSpawned: {no.IsSpawned}");
                    }
                }
                else
                {
                    BlockBattle.Debugging.DebugLogManager.LogWarning(BlockBattle.Debugging.DebugLogManager.LogCategory.LevelManager, "NetworkManager connected but no NetworkedLevelManager found!");
                    m_IsMultiplayerMode = false;
                }
            }
            else
            {
                m_IsMultiplayerMode = false;
                BlockBattle.Debugging.DebugLogManager.LevelMgr("  Running in SINGLE-PLAYER mode (not connected)");
            }
            
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"=== CheckMultiplayerMode RESULT: m_IsMultiplayerMode={m_IsMultiplayerMode} ===");
        }

        /// <summary>
        /// Updates validation in multiplayer mode and reports to NetworkedLevelManager.
        /// </summary>
        private void UpdateMultiplayerValidation()
        {
            if (m_NetworkedLevelManager == null) return;
            if (m_NetworkedLevelManager.CurrentPhase != NetworkedLevelPhase.Building) return;

            // Wait for validation delay
            if (!m_ValidationEnabled)
            {
                m_ValidationTimer += Time.deltaTime;
                if (m_ValidationTimer >= m_ValidationStartDelay)
                {
                    m_ValidationEnabled = true;
                }
                return;
            }

            // Validate and report to network
            if (m_BuildValidator != null)
            {
                BuildValidationResult result = m_BuildValidator.ValidateBuild();
                if (result != null && result.AccuracyPercentage > 0)
                {
                    // Report accuracy to networked manager
                    m_NetworkedLevelManager.ReportBuildCompleteServerRpc(result.AccuracyPercentage);
                }
            }
        }

        /// <summary>
        /// Called when the networked phase changes.
        /// </summary>
        private void OnNetworkPhaseChanged(NetworkedLevelPhase phase)
        {
            // Map networked phase to local phase for UI and local logic
            switch (phase)
            {
                case NetworkedLevelPhase.Building:
                    m_CurrentPhase = LevelPhase.Building;
                    m_ValidationEnabled = false;
                    m_ValidationTimer = 0f;
                    break;

                case NetworkedLevelPhase.Destruction:
                    m_CurrentPhase = LevelPhase.Destruction;
                    OnDestructionPhaseStarted?.Invoke();
                    break;

                case NetworkedLevelPhase.Transitioning:
                    m_CurrentPhase = LevelPhase.Transitioning;
                    break;

                case NetworkedLevelPhase.WaitingForPlayers:
                    // Reset to building state while waiting
                    m_CurrentPhase = LevelPhase.Building;
                    break;

                case NetworkedLevelPhase.GameOver:
                    OnAllLevelsCompleted?.Invoke();
                    break;
            }
        }

        #endregion

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
            Debug.Log($"LevelManager: Destruction phase complete! Transitioning to WaitingForReturn...");

            m_CurrentPhase = LevelPhase.WaitingForReturn;

            // Teleport player back to building position (if specified)
            if (m_XRSetup != null && m_BuildingTeleportPosition != null)
            {
                m_XRSetup.TeleportPlayer(m_BuildingTeleportPosition);
            }

            OnDestructionPhaseCompleted?.Invoke();

            // Show return blocks message
            ShowReturnBlocksMessage();

            Debug.Log($"LevelManager: Waiting for player to return {m_ExpectedBlockCount} blocks to shelf...");
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
