using Unity.Netcode;
using UnityEngine;
using System.Collections;

namespace BlockBattle.Debugging
{
    /// <summary>
    /// Comprehensive network debug logger that tracks all network events.
    /// Add this to the same GameObject as the NetworkManager or NetworkedGameManager.
    /// </summary>
    public class NetworkDebugLogger : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private bool _logEveryFrame = false;
        [SerializeField] private float _statusLogInterval = 5f;
        
        [Header("Tracked Objects")]
        [SerializeField] private bool _logSceneObjects = true;
        [SerializeField] private bool _logSpawnedObjects = true;
        
        private NetworkManager _networkManager;
        private float _lastStatusLogTime;
        private bool _wasConnected;
        
        private void Start()
        {
            StartCoroutine(WaitForNetworkManager());
        }
        
        private IEnumerator WaitForNetworkManager()
        {
            Log("Waiting for NetworkManager...");
            
            while (NetworkManager.Singleton == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
            
            _networkManager = NetworkManager.Singleton;
            Log($"NetworkManager found! Transport: {_networkManager.NetworkConfig.NetworkTransport?.GetType().Name ?? "NULL"}");
            
            // Subscribe to all network events
            SubscribeToEvents();
            
            // Log initial state
            LogNetworkState("Initial State");
        }
        
        private void SubscribeToEvents()
        {
            if (_networkManager == null) return;
            
            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            _networkManager.OnTransportFailure += OnTransportFailure;
            _networkManager.OnConnectionEvent += OnConnectionEvent;
            
            // For scene management
            if (_networkManager.SceneManager != null)
            {
                _networkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            }
            
            Log("Subscribed to NetworkManager events");
        }
        
        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback -= OnClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                _networkManager.OnTransportFailure -= OnTransportFailure;
                _networkManager.OnConnectionEvent -= OnConnectionEvent;
                
                if (_networkManager.SceneManager != null)
                {
                    _networkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
                }
            }
        }
        
        private void Update()
        {
            if (!_enabled || _networkManager == null) return;
            
            // Detect connection state changes
            bool isConnected = _networkManager.IsConnectedClient;
            if (isConnected != _wasConnected)
            {
                _wasConnected = isConnected;
                Log($"Connection state changed: {(isConnected ? "CONNECTED" : "DISCONNECTED")}");
                
                if (isConnected)
                {
                    LogNetworkState("Just Connected");
                    LogSpawnedObjects();
                }
            }
            
            // Periodic status logging
            if (Time.time - _lastStatusLogTime > _statusLogInterval)
            {
                _lastStatusLogTime = Time.time;
                if (_logEveryFrame || _networkManager.IsConnectedClient)
                {
                    LogNetworkState("Periodic Status");
                }
            }
        }
        
        #region Event Handlers
        
        private void OnClientConnected(ulong clientId)
        {
            Log($"=== CLIENT CONNECTED: {clientId} ===");
            Log($"  LocalClientId: {_networkManager.LocalClientId}");
            Log($"  IsHost: {_networkManager.IsHost}");
            Log($"  IsServer: {_networkManager.IsServer}");
            Log($"  IsClient: {_networkManager.IsClient}");
            Log($"  IsSessionOwner: {_networkManager.LocalClientId == _networkManager.CurrentSessionOwner}");
            Log($"  CurrentSessionOwner: {_networkManager.CurrentSessionOwner}");
            Log($"  ConnectedClients: {_networkManager.ConnectedClientsList?.Count ?? 0}");
            
            // List all connected clients
            if (_networkManager.ConnectedClientsList != null)
            {
                foreach (var client in _networkManager.ConnectedClientsList)
                {
                    Log($"    - Client {client.ClientId}: PlayerObject={client.PlayerObject?.name ?? "NULL"}");
                }
            }
            
            // Log scene objects after connection
            StartCoroutine(LogSceneObjectsDelayed());
        }
        
        private IEnumerator LogSceneObjectsDelayed()
        {
            yield return new WaitForSeconds(1f);
            LogSpawnedObjects();
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            Log($"=== CLIENT DISCONNECTED: {clientId} ===");
        }
        
        private void OnTransportFailure()
        {
            LogError("=== TRANSPORT FAILURE ===");
        }
        
        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            Log($"Connection Event: {data.EventType} for client {data.ClientId}");
        }
        
        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            Log($"Scene Event: {sceneEvent.SceneEventType} - Scene: {sceneEvent.SceneName}");
        }
        
        #endregion
        
        #region Logging Methods
        
        private void LogNetworkState(string context)
        {
            if (_networkManager == null)
            {
                Log($"[{context}] NetworkManager is NULL");
                return;
            }
            
            Log($"=== NETWORK STATE ({context}) ===");
            Log($"  IsListening: {_networkManager.IsListening}");
            Log($"  IsConnectedClient: {_networkManager.IsConnectedClient}");
            Log($"  IsHost: {_networkManager.IsHost}");
            Log($"  IsServer: {_networkManager.IsServer}");
            Log($"  IsClient: {_networkManager.IsClient}");
            Log($"  LocalClientId: {_networkManager.LocalClientId}");
            Log($"  CurrentSessionOwner: {_networkManager.CurrentSessionOwner}");
            Log($"  IsSessionOwner: {_networkManager.LocalClientId == _networkManager.CurrentSessionOwner}");
            Log($"  ConnectedClients: {_networkManager.ConnectedClientsList?.Count ?? 0}");
            Log($"  SpawnedObjects: {_networkManager.SpawnManager?.SpawnedObjects?.Count ?? 0}");
        }
        
        private void LogSpawnedObjects()
        {
            if (!_logSpawnedObjects || _networkManager?.SpawnManager == null) return;
            
            Log("=== SPAWNED NETWORK OBJECTS ===");
            
            var spawnedObjects = _networkManager.SpawnManager.SpawnedObjects;
            if (spawnedObjects == null || spawnedObjects.Count == 0)
            {
                Log("  (No spawned objects)");
                return;
            }
            
            foreach (var kvp in spawnedObjects)
            {
                var networkObject = kvp.Value;
                if (networkObject == null) continue;
                
                string behaviours = "";
                var networkBehaviours = networkObject.GetComponents<NetworkBehaviour>();
                foreach (var nb in networkBehaviours)
                {
                    behaviours += nb.GetType().Name + ", ";
                }
                
                Log($"  [{kvp.Key}] {networkObject.name} - Owner: {networkObject.OwnerClientId}, IsSpawned: {networkObject.IsSpawned}, Behaviours: {behaviours}");
            }
        }
        
        private void Log(string message)
        {
            if (!_enabled) return;
            DebugLogManager.Log(DebugLogManager.LogCategory.Network, message, this);
            
            // Fallback if DebugLogManager not present
            if (DebugLogManager.Instance == null)
            {
                UnityEngine.Debug.Log($"<color=#00FF00>[Network]</color> {message}", this);
            }
        }
        
        private void LogError(string message)
        {
            DebugLogManager.LogError(DebugLogManager.LogCategory.Network, message, this);
            
            if (DebugLogManager.Instance == null)
            {
                UnityEngine.Debug.LogError($"<color=#FF0000>[Network]</color> {message}", this);
            }
        }
        
        #endregion
        
        #region Context Menu Actions
        
        [ContextMenu("Log Current Network State")]
        public void LogCurrentState()
        {
            LogNetworkState("Manual Request");
            LogSpawnedObjects();
        }
        
        [ContextMenu("Log All NetworkBehaviours in Scene")]
        public void LogAllNetworkBehaviours()
        {
            Log("=== ALL NETWORKBEHAVIOURS IN SCENE ===");
            
            var allBehaviours = FindObjectsByType<NetworkBehaviour>(FindObjectsSortMode.None);
            foreach (var nb in allBehaviours)
            {
                var no = nb.NetworkObject;
                Log($"  {nb.GetType().Name} on '{nb.gameObject.name}' - NetworkObject: {(no != null ? "Yes" : "NO!")}, IsSpawned: {nb.IsSpawned}");
            }
        }
        
        #endregion
    }
}
