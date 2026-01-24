using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace BlockBattle.Network
{
    /// <summary>
    /// Network-synchronized level manager that coordinates game state across all clients.
    /// Handles phase transitions, timers, scoring, and win conditions for multiplayer.
    /// </summary>
    public class NetworkedLevelManager : NetworkBehaviour
    {
        /// <summary>
        /// Returns true if this client is the session owner (host) in Distributed Authority mode.
        /// In DA mode, IsServer is always false, so we check CurrentSessionOwner instead.
        /// </summary>
        private bool IsSessionOwner => NetworkManager.Singleton != null && 
            NetworkManager.Singleton.LocalClientId == NetworkManager.Singleton.CurrentSessionOwner;
        #region Singleton

        /// <summary>
        /// Singleton instance of the NetworkedLevelManager.
        /// </summary>
        public static NetworkedLevelManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Level Configurations")]
        [SerializeField, Tooltip("List of level configurations in order")]
        private List<BlockSpawnConfiguration> _levelConfigurations = new List<BlockSpawnConfiguration>();

        [Header("Settings")]
        [SerializeField, Tooltip("Accuracy percentage required to complete a level")]
        [Range(90f, 100f)]
        private float _completionThreshold = 100f;

        [SerializeField, Tooltip("Duration of the building phase in seconds (0 = unlimited)")]
        private float _buildPhaseDuration = 0f;

        [SerializeField, Tooltip("Delay after all blocks are destroyed before transitioning")]
        private float _destructionCompleteDelay = 2f;

        [SerializeField, Tooltip("Countdown duration before starting next level")]
        private float _levelTransitionCountdown = 3f;

        [Header("References")]
        [SerializeField, Tooltip("Reference to the PlayerWorkspaceManager")]
        private PlayerWorkspaceManager _workspaceManager;

        #endregion

        #region Network Variables

        /// <summary>
        /// Current game phase synchronized across all clients.
        /// </summary>
        private NetworkVariable<NetworkedLevelPhase> _currentPhase = new NetworkVariable<NetworkedLevelPhase>(
            NetworkedLevelPhase.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner  // Use Owner for DA mode (session owner owns this object)
        );

        /// <summary>
        /// Current level index (0-based). Initialized to -1 so first level (0) triggers OnValueChanged.
        /// </summary>
        private NetworkVariable<int> _currentLevelIndex = new NetworkVariable<int>(
            -1,  // Start at -1 so setting to 0 triggers the callback
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner  // Use Owner for DA mode (session owner owns this object)
        );

        /// <summary>
        /// Remaining time for the current phase.
        /// </summary>
        private NetworkVariable<float> _phaseTimer = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner  // Use Owner for DA mode
        );

        /// <summary>
        /// Player 1's current accuracy percentage.
        /// </summary>
        private NetworkVariable<float> _player1Accuracy = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// Player 2's current accuracy percentage.
        /// </summary>
        private NetworkVariable<float> _player2Accuracy = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// Player 1's total score across levels.
        /// </summary>
        private NetworkVariable<int> _player1Score = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// Player 2's total score across levels.
        /// </summary>
        private NetworkVariable<int> _player2Score = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// Which player completed the build first (0 = none, 1 or 2 = player number).
        /// </summary>
        private NetworkVariable<int> _buildWinner = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// Overall game winner (0 = ongoing, 1 or 2 = player number).
        /// </summary>
        private NetworkVariable<int> _gameWinner = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// Player 1's total completion time for current level (build + destroy).
        /// </summary>
        private NetworkVariable<float> _player1CompletionTime = new NetworkVariable<float>(
            -1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// Player 2's total completion time for current level (build + destroy).
        /// </summary>
        private NetworkVariable<float> _player2CompletionTime = new NetworkVariable<float>(
            -1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        #endregion

        #region Private Fields

        private bool[] _playerBuildComplete = new bool[2];
        private bool[] _playerDestructionComplete = new bool[2];
        private float[] _playerBuildCompleteTimes = new float[2];
        private Coroutine _phaseTimerCoroutine;
        private float _levelStartTime;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current phase.
        /// </summary>
        public NetworkedLevelPhase CurrentPhase => _currentPhase.Value;

        /// <summary>
        /// Gets the current level number (1-based for display).
        /// </summary>
        public int CurrentLevelNumber => _currentLevelIndex.Value + 1;

        /// <summary>
        /// Gets the current level index (0-based).
        /// </summary>
        public int CurrentLevelIndex => _currentLevelIndex.Value;

        /// <summary>
        /// Gets the total number of levels.
        /// </summary>
        public int TotalLevels => _levelConfigurations.Count;

        /// <summary>
        /// Gets the remaining phase time.
        /// </summary>
        public float PhaseTimeRemaining => _phaseTimer.Value;

        /// <summary>
        /// Gets player 1's accuracy.
        /// </summary>
        public float Player1Accuracy => _player1Accuracy.Value;

        /// <summary>
        /// Gets player 2's accuracy.
        /// </summary>
        public float Player2Accuracy => _player2Accuracy.Value;

        /// <summary>
        /// Gets player 1's score.
        /// </summary>
        public int Player1Score => _player1Score.Value;

        /// <summary>
        /// Gets player 2's score.
        /// </summary>
        public int Player2Score => _player2Score.Value;

        /// <summary>
        /// Gets the build phase winner (0 = none yet).
        /// </summary>
        public int BuildWinner => _buildWinner.Value;

        /// <summary>
        /// Gets the current level configuration.
        /// </summary>
        public BlockSpawnConfiguration CurrentLevelConfiguration
        {
            get
            {
                if (_currentLevelIndex.Value < _levelConfigurations.Count)
                {
                    return _levelConfigurations[_currentLevelIndex.Value];
                }
                return null;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when the game phase changes.
        /// </summary>
        public event System.Action<NetworkedLevelPhase> OnPhaseChanged;

        /// <summary>
        /// Event fired when a new level starts.
        /// </summary>
        public event System.Action<int> OnLevelStarted;

        /// <summary>
        /// Event fired when a player completes their build.
        /// Parameters: playerNumber (1 or 2), accuracy
        /// </summary>
        public event System.Action<int, float> OnPlayerBuildComplete;

        /// <summary>
        /// Event fired when a player completes their destruction phase.
        /// </summary>
        public event System.Action<int> OnPlayerDestructionComplete;

        /// <summary>
        /// Event fired when all levels are complete.
        /// </summary>
        public event System.Action<int> OnGameComplete; // Parameter: winning player

        /// <summary>
        /// Event fired when phase timer updates.
        /// </summary>
        public event System.Action<float> OnTimerUpdated;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"Awake called on {gameObject.name}");
            
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                BlockBattle.Debugging.DebugLogManager.LogWarning(BlockBattle.Debugging.DebugLogManager.LogCategory.LevelManager, "Duplicate instance found, destroying this one.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"Initialized. LevelConfigs: {_levelConfigurations?.Count ?? 0}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Network Callbacks

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            BlockBattle.Debugging.DebugLogManager.LevelMgr("=== OnNetworkSpawn CALLED ===");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  NetworkObjectId: {NetworkObjectId}");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsSpawned: {IsSpawned}");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsOwner: {IsOwner}");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  OwnerClientId: {OwnerClientId}");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsSessionOwner: {IsSessionOwner}");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  LocalClientId: {NetworkManager.Singleton?.LocalClientId}");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  CurrentSessionOwner: {NetworkManager.Singleton?.CurrentSessionOwner}");

            // Find workspace manager if not assigned
            if (_workspaceManager == null)
            {
                _workspaceManager = FindAnyObjectByType<PlayerWorkspaceManager>();
                BlockBattle.Debugging.DebugLogManager.LevelMgr($"WorkspaceManager lookup: {(_workspaceManager != null ? "FOUND" : "NOT FOUND")}");
            }

            // Subscribe to network variable changes
            _currentPhase.OnValueChanged += OnPhaseValueChanged;
            _currentLevelIndex.OnValueChanged += OnLevelIndexChanged;
            _phaseTimer.OnValueChanged += OnTimerValueChanged;

            // Subscribe to workspace manager events
            if (_workspaceManager != null)
            {
                _workspaceManager.OnGameReady += OnAllPlayersReady;
                _workspaceManager.OnGameNotReady += OnPlayerLeft;
                BlockBattle.Debugging.DebugLogManager.LevelMgr("Subscribed to WorkspaceManager events");
            }
            else
            {
                BlockBattle.Debugging.DebugLogManager.LogError(BlockBattle.Debugging.DebugLogManager.LogCategory.LevelManager, "WorkspaceManager is NULL - cannot subscribe to events!");
            }

            BlockBattle.Debugging.DebugLogManager.LevelMgr($"=== OnNetworkSpawn COMPLETE ===");
            
            // Handle late-joining clients: if the game is already in progress (Building or later),
            // set up the level locally since OnLevelIndexChanged won't fire for existing values
            if (_currentPhase.Value != NetworkedLevelPhase.WaitingForPlayers && _currentLevelIndex.Value >= 0)
            {
                Debug.Log($"NetworkedLevelManager: Late-join detected. Phase={_currentPhase.Value}, LevelIndex={_currentLevelIndex.Value}. Setting up level.");
                // Delay slightly to ensure workspace is assigned first
                StartCoroutine(DelayedLateJoinSetup());
            }
        }
        
        /// <summary>
        /// Handles late-join scenario where we need to set up the level after workspace assignment.
        /// </summary>
        private IEnumerator DelayedLateJoinSetup()
        {
            // Wait a short time for workspace assignment to complete
            yield return new WaitForSeconds(0.5f);
            
            if (_currentLevelIndex.Value >= 0)
            {
                SetupLevelLocally(_currentLevelIndex.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from events
            _currentPhase.OnValueChanged -= OnPhaseValueChanged;
            _currentLevelIndex.OnValueChanged -= OnLevelIndexChanged;
            _phaseTimer.OnValueChanged -= OnTimerValueChanged;

            if (_workspaceManager != null)
            {
                _workspaceManager.OnGameReady -= OnAllPlayersReady;
                _workspaceManager.OnGameNotReady -= OnPlayerLeft;
            }

            base.OnNetworkDespawn();
        }

        #endregion

        #region Phase Management

        /// <summary>
        /// Called when all players are connected and assigned.
        /// </summary>
        private void OnAllPlayersReady()
        {
            BlockBattle.Debugging.DebugLogManager.LevelMgr("=== OnAllPlayersReady CALLED ===");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  IsSessionOwner: {IsSessionOwner}");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  LocalClientId: {NetworkManager.Singleton?.LocalClientId}");
            BlockBattle.Debugging.DebugLogManager.LevelMgr($"  CurrentSessionOwner: {NetworkManager.Singleton?.CurrentSessionOwner}");
            
            if (!IsSessionOwner)
            {
                BlockBattle.Debugging.DebugLogManager.LevelMgr("  Not session owner - skipping game start");
                return;
            }

            BlockBattle.Debugging.DebugLogManager.LevelMgr("  >>> SESSION OWNER - STARTING GAME! <<<");
            StartLevel(0);
        }

        /// <summary>
        /// Called when a player leaves mid-game.
        /// </summary>
        private void OnPlayerLeft()
        {
            if (!IsSessionOwner) return;

            BlockBattle.Debugging.DebugLogManager.LevelMgr("Player left, pausing game.");
            _currentPhase.Value = NetworkedLevelPhase.WaitingForPlayers;

            // Stop any running timers
            if (_phaseTimerCoroutine != null)
            {
                StopCoroutine(_phaseTimerCoroutine);
                _phaseTimerCoroutine = null;
            }
        }

        /// <summary>
        /// Starts a specific level.
        /// </summary>
        /// <param name="levelIndex">The level index to start</param>
        public void StartLevel(int levelIndex)
        {
            if (!IsSessionOwner)
            {
                Debug.LogError("NetworkedLevelManager: StartLevel can only be called by session owner");
                return;
            }

            if (levelIndex < 0 || levelIndex >= _levelConfigurations.Count)
            {
                Debug.LogError($"NetworkedLevelManager: Invalid level index {levelIndex}");
                return;
            }

            // Reset per-level state
            _playerBuildComplete[0] = false;
            _playerBuildComplete[1] = false;
            _playerDestructionComplete[0] = false;
            _playerDestructionComplete[1] = false;
            _playerBuildCompleteTimes[0] = -1f;
            _playerBuildCompleteTimes[1] = -1f;
            _buildWinner.Value = 0;
            _player1Accuracy.Value = 0f;
            _player2Accuracy.Value = 0f;
            _player1CompletionTime.Value = -1f;
            _player2CompletionTime.Value = -1f;
            _levelStartTime = Time.time;

            // Set level index
            _currentLevelIndex.Value = levelIndex;

            // Notify clients to set up the level
            SetupLevelClientRpc(levelIndex);

            // Start building phase
            StartBuildingPhase();

            Debug.Log($"NetworkedLevelManager: Started level {levelIndex + 1}");
        }

        /// <summary>
        /// Starts the building phase.
        /// </summary>
        private void StartBuildingPhase()
        {
            _currentPhase.Value = NetworkedLevelPhase.Building;

            // Start timer if duration is set
            if (_buildPhaseDuration > 0)
            {
                StartPhaseTimer(_buildPhaseDuration);
            }
        }

        /// <summary>
        /// Starts the destruction phase.
        /// </summary>
        private void StartDestructionPhase()
        {
            _currentPhase.Value = NetworkedLevelPhase.Destruction;

            // Notify clients to enable slingshots
            StartDestructionClientRpc();
        }

        /// <summary>
        /// Transitions to the next level or ends the game.
        /// </summary>
        private void TransitionToNextLevel()
        {
            _currentPhase.Value = NetworkedLevelPhase.Transitioning;

            // Start countdown
            _phaseTimerCoroutine = StartCoroutine(LevelTransitionCoroutine());
        }

        /// <summary>
        /// Coroutine for level transition countdown.
        /// </summary>
        private IEnumerator LevelTransitionCoroutine()
        {
            _phaseTimer.Value = _levelTransitionCountdown;

            while (_phaseTimer.Value > 0)
            {
                yield return new WaitForSeconds(1f);
                _phaseTimer.Value -= 1f;
            }

            // Check if there are more levels
            int nextLevel = _currentLevelIndex.Value + 1;
            if (nextLevel < _levelConfigurations.Count)
            {
                StartLevel(nextLevel);
            }
            else
            {
                EndGame();
            }
        }

        /// <summary>
        /// Ends the game and determines the winner.
        /// </summary>
        private void EndGame()
        {
            _currentPhase.Value = NetworkedLevelPhase.GameOver;

            // Determine winner based on total score
            if (_player1Score.Value > _player2Score.Value)
            {
                _gameWinner.Value = 1;
            }
            else if (_player2Score.Value > _player1Score.Value)
            {
                _gameWinner.Value = 2;
            }
            else
            {
                _gameWinner.Value = 0; // Tie
            }

            GameOverClientRpc(_gameWinner.Value);

            Debug.Log($"NetworkedLevelManager: Game over! Winner: Player {_gameWinner.Value}");
        }

        /// <summary>
        /// Starts a phase timer.
        /// </summary>
        private void StartPhaseTimer(float duration)
        {
            if (_phaseTimerCoroutine != null)
            {
                StopCoroutine(_phaseTimerCoroutine);
            }

            _phaseTimerCoroutine = StartCoroutine(PhaseTimerCoroutine(duration));
        }

        /// <summary>
        /// Coroutine for phase timer.
        /// </summary>
        private IEnumerator PhaseTimerCoroutine(float duration)
        {
            _phaseTimer.Value = duration;

            while (_phaseTimer.Value > 0)
            {
                yield return new WaitForSeconds(1f);
                _phaseTimer.Value -= 1f;
            }

            // Timer expired - handle based on current phase
            OnPhaseTimerExpired();
        }

        /// <summary>
        /// Called when the phase timer expires.
        /// </summary>
        private void OnPhaseTimerExpired()
        {
            if (_currentPhase.Value == NetworkedLevelPhase.Building)
            {
                // Building time ran out - move to destruction with current progress
                StartDestructionPhase();
            }
        }

        #endregion

        #region Build Completion

        /// <summary>
        /// Called by clients to report their build completion.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReportBuildCompleteServerRpc(float accuracy, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(clientId);

            if (workspaceIndex < 0 || workspaceIndex > 1)
            {
                Debug.LogWarning($"NetworkedLevelManager: Invalid workspace index for client {clientId}");
                return;
            }

            int playerNumber = workspaceIndex + 1;

            // Update accuracy
            if (workspaceIndex == 0)
            {
                _player1Accuracy.Value = accuracy;
            }
            else
            {
                _player2Accuracy.Value = accuracy;
            }

            // Check if build is complete (meets threshold)
            if (accuracy >= _completionThreshold && !_playerBuildComplete[workspaceIndex])
            {
                _playerBuildComplete[workspaceIndex] = true;

                // Record build completion time
                _playerBuildCompleteTimes[workspaceIndex] = Time.time - _levelStartTime;

                // First to complete build gets noted
                if (_buildWinner.Value == 0)
                {
                    _buildWinner.Value = playerNumber;
                }

                Debug.Log($"NetworkedLevelManager: Player {playerNumber} completed build with {accuracy:F1}% accuracy in {_playerBuildCompleteTimes[workspaceIndex]:F1}s");

                // Notify clients
                PlayerBuildCompleteClientRpc(playerNumber, accuracy);

                // Each player immediately starts their own destruction phase
                // (they shoot their own structure)
                StartPlayerDestructionPhaseClientRpc(workspaceIndex);
            }
        }

        /// <summary>
        /// Called by clients to report their destruction phase completion.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReportDestructionCompleteServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(clientId);

            if (workspaceIndex < 0 || workspaceIndex > 1)
            {
                Debug.LogWarning($"NetworkedLevelManager: Invalid workspace index for client {clientId}");
                return;
            }

            int playerNumber = workspaceIndex + 1;

            if (!_playerDestructionComplete[workspaceIndex])
            {
                _playerDestructionComplete[workspaceIndex] = true;

                // Calculate total completion time (build + destroy)
                float totalTime = Time.time - _levelStartTime;
                
                if (workspaceIndex == 0)
                {
                    _player1CompletionTime.Value = totalTime;
                }
                else
                {
                    _player2CompletionTime.Value = totalTime;
                }

                Debug.Log($"NetworkedLevelManager: Player {playerNumber} completed level in {totalTime:F1}s total");

                // Notify all clients
                PlayerLevelCompleteClientRpc(playerNumber, totalTime);

                // Check if both players are done
                if (_playerDestructionComplete[0] && _playerDestructionComplete[1])
                {
                    // Both done - determine round winner and award points
                    DetermineRoundWinner();
                    
                    // Transition to next level after delay
                    StartCoroutine(DestructionCompleteDelayCoroutine());
                }
            }
        }

        /// <summary>
        /// Determines the winner of the current round based on completion time.
        /// </summary>
        private void DetermineRoundWinner()
        {
            float p1Time = _player1CompletionTime.Value;
            float p2Time = _player2CompletionTime.Value;

            if (p1Time < p2Time)
            {
                // Player 1 wins round
                _player1Score.Value += 10;
                _player2Score.Value += 5;
                Debug.Log($"NetworkedLevelManager: Player 1 wins round! ({p1Time:F1}s vs {p2Time:F1}s)");
            }
            else if (p2Time < p1Time)
            {
                // Player 2 wins round
                _player2Score.Value += 10;
                _player1Score.Value += 5;
                Debug.Log($"NetworkedLevelManager: Player 2 wins round! ({p2Time:F1}s vs {p1Time:F1}s)");
            }
            else
            {
                // Tie - both get same points
                _player1Score.Value += 7;
                _player2Score.Value += 7;
                Debug.Log($"NetworkedLevelManager: Round tie! Both players: {p1Time:F1}s");
            }

            RoundResultClientRpc(p1Time < p2Time ? 1 : (p2Time < p1Time ? 2 : 0), p1Time, p2Time);
        }

        /// <summary>
        /// Coroutine for delay after destruction phase.
        /// </summary>
        private IEnumerator DestructionCompleteDelayCoroutine()
        {
            yield return new WaitForSeconds(_destructionCompleteDelay);
            TransitionToNextLevel();
        }

        #endregion

        #region Network Variable Callbacks

        private void OnPhaseValueChanged(NetworkedLevelPhase previousValue, NetworkedLevelPhase newValue)
        {
            Debug.Log($"NetworkedLevelManager: Phase changed from {previousValue} to {newValue}");
            OnPhaseChanged?.Invoke(newValue);
        }

        private void OnLevelIndexChanged(int previousValue, int newValue)
        {
            OnLevelStarted?.Invoke(newValue + 1);
            
            // In Distributed Authority mode, ClientRPCs may not execute reliably.
            // Instead, we use NetworkVariable sync to trigger level setup on each client.
            // This callback fires on ALL clients when the level index changes.
            SetupLevelLocally(newValue);
        }
        
        /// <summary>
        /// Sets up the level locally for this client.
        /// Called when _currentLevelIndex NetworkVariable changes.
        /// </summary>
        private void SetupLevelLocally(int levelIndex)
        {
            Debug.Log($"NetworkedLevelManager: SetupLevelLocally called with levelIndex={levelIndex}, configCount={_levelConfigurations.Count}");
            
            if (levelIndex < 0 || levelIndex >= _levelConfigurations.Count)
            {
                Debug.LogError($"NetworkedLevelManager: Invalid level index {levelIndex}! Available: {_levelConfigurations.Count}");
                return;
            }

            var config = _levelConfigurations[levelIndex];
            Debug.Log($"NetworkedLevelManager: Got config: {(config != null ? config.ConfigurationName : "NULL")}");

            // Each client sets up their own workspace
            var workspace = _workspaceManager?.GetLocalPlayerWorkspace();
            Debug.Log($"NetworkedLevelManager: WorkspaceManager={(_workspaceManager != null ? "exists" : "NULL")}, LocalWorkspace={(workspace != null ? "exists" : "NULL")}");
            
            if (workspace != null)
            {
                Debug.Log($"NetworkedLevelManager: Setting up workspace for local player");
                workspace.ClearWorkspace();
                workspace.SpawnReferenceStructure(config);
                workspace.SpawnBlocks(config);
                workspace.SetupValidator(config);
                workspace.SetSlingshotEnabled(false);
            }
            else
            {
                Debug.LogWarning("NetworkedLevelManager: No workspace found for local player - may not be assigned yet");
            }
        }

        private void OnTimerValueChanged(float previousValue, float newValue)
        {
            OnTimerUpdated?.Invoke(newValue);
        }

        #endregion

        #region Client RPCs

        /// <summary>
        /// Notifies clients to set up a level.
        /// Note: In DA mode, ClientRPCs may not be reliable. The primary level setup 
        /// now happens via NetworkVariable sync in OnLevelIndexChanged.
        /// This RPC is kept for backward compatibility and as a fallback.
        /// </summary>
        [ClientRpc]
        private void SetupLevelClientRpc(int levelIndex)
        {
            // In DA mode, level setup is now primarily handled by OnLevelIndexChanged
            // when the _currentLevelIndex NetworkVariable syncs.
            // This RPC call serves as a backup - if the NetworkVariable already 
            // triggered setup, the workspace.ClearWorkspace() in SetupLevelLocally 
            // will handle any duplicate calls gracefully.
            Debug.Log($"NetworkedLevelManager: SetupLevelClientRpc received (levelIndex={levelIndex}) - delegating to SetupLevelLocally");
            SetupLevelLocally(levelIndex);
        }

        /// <summary>
        /// Notifies clients that destruction phase is starting.
        /// </summary>
        [ClientRpc]
        private void StartDestructionClientRpc()
        {
            var workspace = _workspaceManager?.GetLocalPlayerWorkspace();
            if (workspace != null)
            {
                workspace.SetSlingshotEnabled(true);
            }

            // TODO: Teleport player to destruction position
        }

        /// <summary>
        /// Notifies clients that a player completed their build.
        /// </summary>
        [ClientRpc]
        private void PlayerBuildCompleteClientRpc(int playerNumber, float accuracy)
        {
            OnPlayerBuildComplete?.Invoke(playerNumber, accuracy);
        }

        /// <summary>
        /// Notifies clients that the game is over.
        /// </summary>
        [ClientRpc]
        private void GameOverClientRpc(int winnerPlayerNumber)
        {
            OnGameComplete?.Invoke(winnerPlayerNumber);
        }

        /// <summary>
        /// Notifies a specific player to start their destruction phase (shooting own structure).
        /// </summary>
        [ClientRpc]
        private void StartPlayerDestructionPhaseClientRpc(int workspaceIndex)
        {
            // Only the player who owns this workspace should enable their slingshot
            var localWorkspace = _workspaceManager?.GetLocalPlayerWorkspace();
            if (localWorkspace != null && localWorkspace.WorkspaceIndex == workspaceIndex)
            {
                localWorkspace.SetSlingshotEnabled(true);
                Debug.Log($"NetworkedLevelManager: Starting destruction phase for local player (workspace {workspaceIndex})");
            }
        }

        /// <summary>
        /// Notifies clients that a player completed the entire level.
        /// </summary>
        [ClientRpc]
        private void PlayerLevelCompleteClientRpc(int playerNumber, float completionTime)
        {
            Debug.Log($"Player {playerNumber} finished level in {completionTime:F1}s!");
            OnPlayerDestructionComplete?.Invoke(playerNumber);
        }

        /// <summary>
        /// Notifies clients of the round result.
        /// </summary>
        [ClientRpc]
        private void RoundResultClientRpc(int winnerPlayerNumber, float p1Time, float p2Time)
        {
            string result = winnerPlayerNumber == 0 ? "TIE!" : $"Player {winnerPlayerNumber} wins!";
            Debug.Log($"Round Result: {result} (P1: {p1Time:F1}s, P2: {p2Time:F1}s)");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the local player's accuracy.
        /// </summary>
        public float GetLocalPlayerAccuracy()
        {
            if (_workspaceManager == null || NetworkManager.Singleton == null) return 0f;

            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(NetworkManager.Singleton.LocalClientId);
            return workspaceIndex == 0 ? _player1Accuracy.Value : _player2Accuracy.Value;
        }

        /// <summary>
        /// Gets the opponent's accuracy.
        /// </summary>
        public float GetOpponentAccuracy()
        {
            if (_workspaceManager == null || NetworkManager.Singleton == null) return 0f;

            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(NetworkManager.Singleton.LocalClientId);
            return workspaceIndex == 0 ? _player2Accuracy.Value : _player1Accuracy.Value;
        }

        /// <summary>
        /// Gets the local player's score.
        /// </summary>
        public int GetLocalPlayerScore()
        {
            if (_workspaceManager == null || NetworkManager.Singleton == null) return 0;

            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(NetworkManager.Singleton.LocalClientId);
            return workspaceIndex == 0 ? _player1Score.Value : _player2Score.Value;
        }

        /// <summary>
        /// Gets the opponent's score.
        /// </summary>
        public int GetOpponentScore()
        {
            if (_workspaceManager == null || NetworkManager.Singleton == null) return 0;

            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(NetworkManager.Singleton.LocalClientId);
            return workspaceIndex == 0 ? _player2Score.Value : _player1Score.Value;
        }

        /// <summary>
        /// Gets whether the local player won the build phase.
        /// </summary>
        public bool DidLocalPlayerWinBuild()
        {
            if (_workspaceManager == null || NetworkManager.Singleton == null) return false;

            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(NetworkManager.Singleton.LocalClientId);
            return _buildWinner.Value == (workspaceIndex + 1);
        }

        /// <summary>
        /// Gets the local player's completion time (-1 if not finished).
        /// </summary>
        public float GetLocalPlayerCompletionTime()
        {
            if (_workspaceManager == null || NetworkManager.Singleton == null) return -1f;

            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(NetworkManager.Singleton.LocalClientId);
            return workspaceIndex == 0 ? _player1CompletionTime.Value : _player2CompletionTime.Value;
        }

        /// <summary>
        /// Gets the opponent's completion time (-1 if not finished).
        /// </summary>
        public float GetOpponentCompletionTime()
        {
            if (_workspaceManager == null || NetworkManager.Singleton == null) return -1f;

            int workspaceIndex = _workspaceManager.GetWorkspaceIndexForClient(NetworkManager.Singleton.LocalClientId);
            return workspaceIndex == 0 ? _player2CompletionTime.Value : _player1CompletionTime.Value;
        }

        /// <summary>
        /// Gets the elapsed time since level started.
        /// </summary>
        public float GetElapsedTime()
        {
            if (_currentPhase.Value == NetworkedLevelPhase.WaitingForPlayers) return 0f;
            return Time.time - _levelStartTime;
        }

        #endregion
    }

    /// <summary>
    /// Phases of the networked game.
    /// </summary>
    public enum NetworkedLevelPhase
    {
        /// <summary>
        /// Waiting for all players to connect.
        /// </summary>
        WaitingForPlayers,

        /// <summary>
        /// Players are building their structures.
        /// </summary>
        Building,

        /// <summary>
        /// Players are destroying opponent structures.
        /// </summary>
        Destruction,

        /// <summary>
        /// Transitioning between levels.
        /// </summary>
        Transitioning,

        /// <summary>
        /// Game is over.
        /// </summary>
        GameOver
    }
}
