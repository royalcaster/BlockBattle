using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace BlockBattle.Network
{
    /// <summary>
    /// Manages player workspace assignments and coordinates multiplayer game flow.
    /// Handles assigning players to workspaces when they connect and manages
    /// workspace-related events across the network.
    /// </summary>
    public class PlayerWorkspaceManager : NetworkBehaviour
    {
        /// <summary>
        /// Returns true if this client is the session owner (host) in Distributed Authority mode.
        /// In DA mode, IsServer is always false, so we check CurrentSessionOwner instead.
        /// </summary>
        private bool IsSessionOwner => NetworkManager.Singleton != null && 
            NetworkManager.Singleton.LocalClientId == NetworkManager.Singleton.CurrentSessionOwner;
        #region Singleton

        /// <summary>
        /// Singleton instance of the PlayerWorkspaceManager.
        /// </summary>
        public static PlayerWorkspaceManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Workspaces")]
        [SerializeField, Tooltip("Array of player workspaces (should be 2 for two-player)")]
        private PlayerWorkspace[] _workspaces;

        [Header("Settings")]
        [SerializeField, Tooltip("Maximum number of players allowed")]
        private int _maxPlayers = 2;

        [SerializeField, Tooltip("Whether to automatically assign workspaces when players connect")]
        private bool _autoAssignOnConnect = true;

        #endregion

        #region Network Variables

        /// <summary>
        /// Tracks which workspace each player is assigned to.
        /// Key: Client ID, Value: Workspace Index
        /// </summary>
        private NetworkList<WorkspaceAssignment> _workspaceAssignments;

        /// <summary>
        /// Whether the game is ready to start (all players connected and assigned).
        /// </summary>
        private NetworkVariable<bool> _isGameReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        #endregion

        #region Private Fields

        private Dictionary<ulong, int> _clientToWorkspaceMap = new Dictionary<ulong, int>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the array of player workspaces.
        /// </summary>
        public PlayerWorkspace[] Workspaces => _workspaces;

        /// <summary>
        /// Gets whether the game is ready (all players assigned).
        /// </summary>
        public bool IsGameReady => _isGameReady.Value;

        /// <summary>
        /// Gets the maximum number of players.
        /// </summary>
        public int MaxPlayers => _maxPlayers;

        /// <summary>
        /// Gets the number of currently connected players.
        /// </summary>
        public int ConnectedPlayerCount => _clientToWorkspaceMap.Count;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when a player is assigned to a workspace.
        /// Parameters: clientId, workspaceIndex
        /// </summary>
        public event System.Action<ulong, int> OnPlayerAssignedToWorkspace;

        /// <summary>
        /// Event fired when a player is removed from a workspace.
        /// Parameters: clientId, workspaceIndex
        /// </summary>
        public event System.Action<ulong, int> OnPlayerRemovedFromWorkspace;

        /// <summary>
        /// Event fired when the game becomes ready (all players assigned).
        /// </summary>
        public event System.Action OnGameReady;

        /// <summary>
        /// Event fired when the game is no longer ready (player left).
        /// </summary>
        public event System.Action OnGameNotReady;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            BlockBattle.Debugging.DebugLogManager.Workspace($"Awake called on {gameObject.name}");
            
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                BlockBattle.Debugging.DebugLogManager.LogWarning(BlockBattle.Debugging.DebugLogManager.LogCategory.Workspace, "Duplicate instance found, destroying this one.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize network list
            _workspaceAssignments = new NetworkList<WorkspaceAssignment>();

            ValidateConfiguration();
            
            BlockBattle.Debugging.DebugLogManager.Workspace($"Initialized. Workspaces: {_workspaces?.Length ?? 0}, MaxPlayers: {_maxPlayers}, AutoAssign: {_autoAssignOnConnect}");
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
            
            BlockBattle.Debugging.DebugLogManager.Workspace("=== OnNetworkSpawn CALLED ===");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  NetworkObjectId: {NetworkObjectId}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  IsSpawned: {IsSpawned}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  IsOwner: {IsOwner}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  OwnerClientId: {OwnerClientId}");

            // Subscribe to network events - use IsSessionOwner for Distributed Authority mode
            // In DA mode, IsServer is always false, so we use session owner instead
            BlockBattle.Debugging.DebugLogManager.Workspace($"Checking authority. IsSessionOwner={IsSessionOwner}, LocalClientId={NetworkManager.Singleton?.LocalClientId}, CurrentSessionOwner={NetworkManager.Singleton?.CurrentSessionOwner}");
            
            if (IsSessionOwner)
            {
                BlockBattle.Debugging.DebugLogManager.Workspace("This client IS session owner - subscribing to connection events");
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                // Assign any already connected clients (including self)
                BlockBattle.Debugging.DebugLogManager.Workspace($"Checking {NetworkManager.Singleton.ConnectedClientsList.Count} already connected clients");
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (!_clientToWorkspaceMap.ContainsKey(client.ClientId))
                    {
                        BlockBattle.Debugging.DebugLogManager.Workspace($"Assigning existing client {client.ClientId} to workspace");
                        AssignWorkspaceToClient(client.ClientId);
                    }
                    else
                    {
                        BlockBattle.Debugging.DebugLogManager.Workspace($"Client {client.ClientId} already assigned to workspace {_clientToWorkspaceMap[client.ClientId]}");
                    }
                }
            }
            else
            {
                BlockBattle.Debugging.DebugLogManager.Workspace("This client is NOT session owner - waiting for workspace assignment");
            }

            // Subscribe to network variable changes
            _workspaceAssignments.OnListChanged += OnWorkspaceAssignmentsChanged;
            _isGameReady.OnValueChanged += OnGameReadyChanged;

            // Rebuild local map from network list
            RebuildLocalMap();

            BlockBattle.Debugging.DebugLogManager.Workspace($"=== OnNetworkSpawn COMPLETE ===");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  IsSessionOwner: {IsSessionOwner}, IsClient: {IsClient}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  Workspaces: {_workspaces?.Length ?? 0}, MaxPlayers: {_maxPlayers}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  ConnectedClients: {NetworkManager.Singleton?.ConnectedClientsList?.Count ?? 0}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  WorkspaceAssignments count: {_workspaceAssignments.Count}");
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from network events
            if (IsSessionOwner && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            // Unsubscribe from network variable changes
            _workspaceAssignments.OnListChanged -= OnWorkspaceAssignmentsChanged;
            _isGameReady.OnValueChanged -= OnGameReadyChanged;

            base.OnNetworkDespawn();
        }

        #endregion

        #region Client Connection Handling

        /// <summary>
        /// Called when a client connects to the server.
        /// </summary>
        private void OnClientConnected(ulong clientId)
        {
            BlockBattle.Debugging.DebugLogManager.Workspace($"=== OnClientConnected: clientId={clientId} ===");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  IsSessionOwner: {IsSessionOwner}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  IsSpawned: {IsSpawned}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  LocalClientId: {NetworkManager.Singleton?.LocalClientId}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  CurrentSessionOwner: {NetworkManager.Singleton?.CurrentSessionOwner}");
            
            if (!IsSessionOwner)
            {
                BlockBattle.Debugging.DebugLogManager.Workspace("  Not session owner - skipping");
                return;
            }

            BlockBattle.Debugging.DebugLogManager.Workspace($"  Session owner processing connection. Workspaces: {_workspaces?.Length ?? 0}, MaxPlayers: {_maxPlayers}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  AutoAssignOnConnect: {_autoAssignOnConnect}");

            if (_autoAssignOnConnect)
            {
                BlockBattle.Debugging.DebugLogManager.Workspace($"  Calling AssignWorkspaceToClient({clientId})");
                AssignWorkspaceToClient(clientId);
            }
        }

        /// <summary>
        /// Called when a client disconnects from the server.
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsSessionOwner) return;

            Debug.Log($"PlayerWorkspaceManager: Client {clientId} disconnected");

            RemoveClientFromWorkspace(clientId);
        }

        #endregion

        #region Workspace Assignment

        /// <summary>
        /// Assigns a workspace to a client.
        /// </summary>
        /// <param name="clientId">The client to assign</param>
        /// <returns>The assigned workspace index, or -1 if no workspace available</returns>
        public int AssignWorkspaceToClient(ulong clientId)
        {
            BlockBattle.Debugging.DebugLogManager.Workspace($"=== AssignWorkspaceToClient({clientId}) ===");
            
            if (!IsSessionOwner)
            {
                BlockBattle.Debugging.DebugLogManager.LogError(BlockBattle.Debugging.DebugLogManager.LogCategory.Workspace, "AssignWorkspaceToClient can only be called by session owner");
                return -1;
            }

            // Check if client is already assigned
            if (_clientToWorkspaceMap.ContainsKey(clientId))
            {
                BlockBattle.Debugging.DebugLogManager.Workspace($"  Client {clientId} already assigned to workspace {_clientToWorkspaceMap[clientId]}");
                return _clientToWorkspaceMap[clientId];
            }

            // Find an available workspace
            int workspaceIndex = FindAvailableWorkspace();
            BlockBattle.Debugging.DebugLogManager.Workspace($"  FindAvailableWorkspace result: {workspaceIndex}");
            
            if (workspaceIndex == -1)
            {
                BlockBattle.Debugging.DebugLogManager.LogWarning(BlockBattle.Debugging.DebugLogManager.LogCategory.Workspace, $"No available workspace for client {clientId}");
                return -1;
            }

            // Assign the workspace
            _clientToWorkspaceMap[clientId] = workspaceIndex;
            BlockBattle.Debugging.DebugLogManager.Workspace($"  Added to local map: client {clientId} -> workspace {workspaceIndex}");

            // Update the workspace component
            if (_workspaces[workspaceIndex] != null)
            {
                _workspaces[workspaceIndex].AssignToPlayer(clientId);
                BlockBattle.Debugging.DebugLogManager.Workspace($"  Called AssignToPlayer on workspace {workspaceIndex}");
            }
            else
            {
                BlockBattle.Debugging.DebugLogManager.LogWarning(BlockBattle.Debugging.DebugLogManager.LogCategory.Workspace, $"Workspace {workspaceIndex} is null!");
            }

            // Add to network list
            try
            {
                _workspaceAssignments.Add(new WorkspaceAssignment
                {
                    ClientId = clientId,
                    WorkspaceIndex = workspaceIndex
                });
                BlockBattle.Debugging.DebugLogManager.Workspace($"  Added to NetworkList. New count: {_workspaceAssignments.Count}");
            }
            catch (System.Exception e)
            {
                BlockBattle.Debugging.DebugLogManager.LogError(BlockBattle.Debugging.DebugLogManager.LogCategory.Workspace, $"Failed to add to NetworkList: {e.Message}");
            }

            BlockBattle.Debugging.DebugLogManager.Workspace($"=== Assigned client {clientId} to workspace {workspaceIndex} ===");

            // Check if game is ready
            UpdateGameReadyState();

            // Notify clients
            OnPlayerAssignedClientRpc(clientId, workspaceIndex);

            return workspaceIndex;
        }

        /// <summary>
        /// Removes a client from their assigned workspace.
        /// </summary>
        /// <param name="clientId">The client to remove</param>
        public void RemoveClientFromWorkspace(ulong clientId)
        {
            if (!IsSessionOwner)
            {
                Debug.LogError("PlayerWorkspaceManager: RemoveClientFromWorkspace can only be called by session owner");
                return;
            }

            if (!_clientToWorkspaceMap.TryGetValue(clientId, out int workspaceIndex))
            {
                Debug.Log($"PlayerWorkspaceManager: Client {clientId} is not assigned to any workspace");
                return;
            }

            // Remove from local map
            _clientToWorkspaceMap.Remove(clientId);

            // Update the workspace component
            if (_workspaces[workspaceIndex] != null)
            {
                _workspaces[workspaceIndex].Unassign();
            }

            // Remove from network list
            for (int i = 0; i < _workspaceAssignments.Count; i++)
            {
                if (_workspaceAssignments[i].ClientId == clientId)
                {
                    _workspaceAssignments.RemoveAt(i);
                    break;
                }
            }

            Debug.Log($"PlayerWorkspaceManager: Removed client {clientId} from workspace {workspaceIndex}");

            // Update game ready state
            UpdateGameReadyState();

            // Notify clients
            OnPlayerRemovedClientRpc(clientId, workspaceIndex);
        }

        /// <summary>
        /// Finds an available workspace.
        /// </summary>
        /// <returns>The index of an available workspace, or -1 if none available</returns>
        private int FindAvailableWorkspace()
        {
            for (int i = 0; i < _workspaces.Length && i < _maxPlayers; i++)
            {
                bool isOccupied = false;
                foreach (var kvp in _clientToWorkspaceMap)
                {
                    if (kvp.Value == i)
                    {
                        isOccupied = true;
                        break;
                    }
                }

                if (!isOccupied)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Updates the game ready state based on connected players.
        /// </summary>
        private void UpdateGameReadyState()
        {
            BlockBattle.Debugging.DebugLogManager.Workspace($"=== UpdateGameReadyState ===");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  _clientToWorkspaceMap.Count: {_clientToWorkspaceMap.Count}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  _maxPlayers: {_maxPlayers}");
            
            bool wasReady = _isGameReady.Value;
            bool isNowReady = _clientToWorkspaceMap.Count >= _maxPlayers;
            
            BlockBattle.Debugging.DebugLogManager.Workspace($"  wasReady: {wasReady}, isNowReady: {isNowReady}");

            if (wasReady != isNowReady)
            {
                BlockBattle.Debugging.DebugLogManager.Workspace($"  State changing! Setting _isGameReady to {isNowReady}");
                try
                {
                    _isGameReady.Value = isNowReady;
                    BlockBattle.Debugging.DebugLogManager.Workspace($"  _isGameReady.Value set successfully");
                }
                catch (System.Exception e)
                {
                    BlockBattle.Debugging.DebugLogManager.LogError(BlockBattle.Debugging.DebugLogManager.LogCategory.Workspace, $"Failed to set _isGameReady: {e.Message}");
                }
            }
            else
            {
                BlockBattle.Debugging.DebugLogManager.Workspace($"  No state change needed");
            }
        }

        #endregion

        #region Network Variable Callbacks

        /// <summary>
        /// Called when the workspace assignments list changes.
        /// </summary>
        private void OnWorkspaceAssignmentsChanged(NetworkListEvent<WorkspaceAssignment> changeEvent)
        {
            BlockBattle.Debugging.DebugLogManager.Workspace($"=== OnWorkspaceAssignmentsChanged ===");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  Event Type: {changeEvent.Type}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  List Count: {_workspaceAssignments.Count}");
            RebuildLocalMap();
        }

        /// <summary>
        /// Called when the game ready state changes.
        /// </summary>
        private void OnGameReadyChanged(bool previousValue, bool newValue)
        {
            BlockBattle.Debugging.DebugLogManager.Workspace($"=== OnGameReadyChanged: {previousValue} -> {newValue} ===");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  OnGameReady subscribers: {(OnGameReady != null ? OnGameReady.GetInvocationList().Length : 0)}");
            BlockBattle.Debugging.DebugLogManager.Workspace($"  OnGameNotReady subscribers: {(OnGameNotReady != null ? OnGameNotReady.GetInvocationList().Length : 0)}");
            
            if (newValue)
            {
                BlockBattle.Debugging.DebugLogManager.Workspace("  >>> GAME IS NOW READY! Invoking OnGameReady <<<");
                OnGameReady?.Invoke();
            }
            else
            {
                BlockBattle.Debugging.DebugLogManager.Workspace("  Game is no longer ready. Invoking OnGameNotReady");
                OnGameNotReady?.Invoke();
            }
        }

        /// <summary>
        /// Rebuilds the local client-to-workspace map from the network list.
        /// </summary>
        private void RebuildLocalMap()
        {
            BlockBattle.Debugging.DebugLogManager.Workspace($"RebuildLocalMap: Starting rebuild, NetworkList count={_workspaceAssignments.Count}");
            _clientToWorkspaceMap.Clear();

            foreach (var assignment in _workspaceAssignments)
            {
                _clientToWorkspaceMap[assignment.ClientId] = assignment.WorkspaceIndex;
                BlockBattle.Debugging.DebugLogManager.Workspace($"RebuildLocalMap: Client {assignment.ClientId} -> Workspace {assignment.WorkspaceIndex}");

                // Update local workspace references
                if (assignment.WorkspaceIndex < _workspaces.Length && _workspaces[assignment.WorkspaceIndex] != null)
                {
                    _workspaces[assignment.WorkspaceIndex].AssignToPlayer(assignment.ClientId);
                }
            }
            
            BlockBattle.Debugging.DebugLogManager.Workspace($"RebuildLocalMap: Complete, map now has {_clientToWorkspaceMap.Count} entries");
        }

        #endregion

        #region Client RPCs

        /// <summary>
        /// Notifies all clients that a player was assigned to a workspace.
        /// </summary>
        [ClientRpc]
        private void OnPlayerAssignedClientRpc(ulong clientId, int workspaceIndex)
        {
            OnPlayerAssignedToWorkspace?.Invoke(clientId, workspaceIndex);
        }

        /// <summary>
        /// Notifies all clients that a player was removed from a workspace.
        /// </summary>
        [ClientRpc]
        private void OnPlayerRemovedClientRpc(ulong clientId, int workspaceIndex)
        {
            OnPlayerRemovedFromWorkspace?.Invoke(clientId, workspaceIndex);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the workspace assigned to a specific client.
        /// </summary>
        /// <param name="clientId">The client ID</param>
        /// <returns>The workspace, or null if not assigned</returns>
        public PlayerWorkspace GetWorkspaceForClient(ulong clientId)
        {
            if (_clientToWorkspaceMap.TryGetValue(clientId, out int index))
            {
                if (index < _workspaces.Length)
                {
                    return _workspaces[index];
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the local player's workspace.
        /// </summary>
        /// <returns>The local player's workspace, or null if not assigned</returns>
        public PlayerWorkspace GetLocalPlayerWorkspace()
        {
            if (NetworkManager.Singleton == null) return null;
            return GetWorkspaceForClient(NetworkManager.Singleton.LocalClientId);
        }

        /// <summary>
        /// Gets the opponent's workspace for the local player.
        /// </summary>
        /// <returns>The opponent's workspace, or null if not available</returns>
        public PlayerWorkspace GetOpponentWorkspace()
        {
            var localWorkspace = GetLocalPlayerWorkspace();
            if (localWorkspace != null)
            {
                return localWorkspace.OpponentWorkspace;
            }
            return null;
        }

        /// <summary>
        /// Gets the workspace index for a client.
        /// </summary>
        /// <param name="clientId">The client ID</param>
        /// <returns>The workspace index, or -1 if not assigned</returns>
        public int GetWorkspaceIndexForClient(ulong clientId)
        {
            if (_clientToWorkspaceMap.TryGetValue(clientId, out int index))
            {
                return index;
            }
            
            // Log for debugging - this should not happen if the client is properly connected
            BlockBattle.Debugging.DebugLogManager.Workspace(
                $"GetWorkspaceIndexForClient: Client {clientId} not found in map. " +
                $"Map has {_clientToWorkspaceMap.Count} entries, NetworkList has {_workspaceAssignments.Count} entries");
            
            return -1;
        }

        /// <summary>
        /// Checks if a client is assigned to a workspace.
        /// </summary>
        /// <param name="clientId">The client ID</param>
        /// <returns>True if assigned</returns>
        public bool IsClientAssigned(ulong clientId)
        {
            return _clientToWorkspaceMap.ContainsKey(clientId);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Validates the workspace configuration.
        /// </summary>
        private void ValidateConfiguration()
        {
            if (_workspaces == null || _workspaces.Length == 0)
            {
                Debug.LogError("PlayerWorkspaceManager: No workspaces configured!");
                return;
            }

            if (_workspaces.Length < _maxPlayers)
            {
                Debug.LogWarning($"PlayerWorkspaceManager: Only {_workspaces.Length} workspaces configured for {_maxPlayers} max players");
            }

            for (int i = 0; i < _workspaces.Length; i++)
            {
                if (_workspaces[i] == null)
                {
                    Debug.LogError($"PlayerWorkspaceManager: Workspace {i} is null!");
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Serializable struct for workspace assignments in NetworkList.
    /// </summary>
    public struct WorkspaceAssignment : INetworkSerializable, System.IEquatable<WorkspaceAssignment>
    {
        public ulong ClientId;
        public int WorkspaceIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref WorkspaceIndex);
        }

        public bool Equals(WorkspaceAssignment other)
        {
            return ClientId == other.ClientId && WorkspaceIndex == other.WorkspaceIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is WorkspaceAssignment other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(ClientId, WorkspaceIndex);
        }
    }
}
