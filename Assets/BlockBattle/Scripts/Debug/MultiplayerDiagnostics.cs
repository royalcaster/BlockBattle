using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace BlockBattle.Debugging
{
    /// <summary>
    /// Comprehensive diagnostics for multiplayer troubleshooting.
    /// Attach to a GameObject in the scene and run diagnostics from the context menu or at runtime.
    /// </summary>
    public class MultiplayerDiagnostics : MonoBehaviour
    {
        [Header("Auto-Run Settings")]
        [SerializeField] private bool _runOnStart = true;
        [SerializeField] private bool _runOnConnection = true;
        [SerializeField] private float _delayAfterConnection = 2f;
        
        [Header("Status (Read Only)")]
        [SerializeField] private string _lastDiagnosticResult = "";
        [SerializeField] private int _networkObjectsInScene = 0;
        [SerializeField] private int _networkBehavioursInScene = 0;
        [SerializeField] private bool _hasNetworkManager = false;
        [SerializeField] private bool _isConnected = false;
        [SerializeField] private string _sessionOwnerStatus = "";
        
        private bool _wasConnected = false;
        
        private void Start()
        {
            if (_runOnStart)
            {
                StartCoroutine(RunDiagnosticsDelayed(1f));
            }
        }
        
        private void Update()
        {
            if (NetworkManager.Singleton == null) return;
            
            bool isConnected = NetworkManager.Singleton.IsConnectedClient;
            _isConnected = isConnected;
            
            if (isConnected && !_wasConnected)
            {
                _wasConnected = true;
                if (_runOnConnection)
                {
                    StartCoroutine(RunDiagnosticsDelayed(_delayAfterConnection));
                }
            }
            else if (!isConnected && _wasConnected)
            {
                _wasConnected = false;
            }
        }
        
        private IEnumerator RunDiagnosticsDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            RunFullDiagnostics();
        }
        
        [ContextMenu("Run Full Diagnostics")]
        public void RunFullDiagnostics()
        {
            var results = new List<string>();
            results.Add("========== MULTIPLAYER DIAGNOSTICS ==========");
            results.Add($"Time: {System.DateTime.Now}");
            results.Add($"Scene: {SceneManager.GetActiveScene().name}");
            results.Add("");
            
            // 1. Check NetworkManager
            results.Add("--- NETWORK MANAGER ---");
            var nm = NetworkManager.Singleton;
            _hasNetworkManager = nm != null;
            if (nm == null)
            {
                results.Add("ERROR: NetworkManager.Singleton is NULL!");
                results.Add("  -> Add NetworkManager prefab to scene");
            }
            else
            {
                results.Add($"NetworkManager: Found ({nm.gameObject.name})");
                results.Add($"  Transport: {nm.NetworkConfig.NetworkTransport?.GetType().Name ?? "NULL"}");
                results.Add($"  IsListening: {nm.IsListening}");
                results.Add($"  IsConnectedClient: {nm.IsConnectedClient}");
                results.Add($"  IsHost: {nm.IsHost}");
                results.Add($"  IsServer: {nm.IsServer}");
                results.Add($"  IsClient: {nm.IsClient}");
                results.Add($"  LocalClientId: {nm.LocalClientId}");
                results.Add($"  CurrentSessionOwner: {nm.CurrentSessionOwner}");
                
                bool isSessionOwner = nm.LocalClientId == nm.CurrentSessionOwner;
                _sessionOwnerStatus = isSessionOwner ? "This client IS session owner" : "This client is NOT session owner";
                results.Add($"  IsSessionOwner (DA mode): {isSessionOwner}");
                
                if (nm.ConnectedClientsList != null)
                {
                    results.Add($"  ConnectedClients: {nm.ConnectedClientsList.Count}");
                    foreach (var client in nm.ConnectedClientsList)
                    {
                        results.Add($"    - Client {client.ClientId}: PlayerObject={client.PlayerObject?.name ?? "NULL"}");
                    }
                }
            }
            results.Add("");
            
            // 2. Check Scene NetworkObjects
            results.Add("--- SCENE NETWORK OBJECTS ---");
            var allNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _networkObjectsInScene = allNetworkObjects.Length;
            results.Add($"Total NetworkObjects in scene: {allNetworkObjects.Length}");
            
            int spawned = 0;
            int notSpawned = 0;
            foreach (var no in allNetworkObjects)
            {
                if (no.IsSpawned)
                    spawned++;
                else
                    notSpawned++;
            }
            results.Add($"  Spawned: {spawned}, Not Spawned: {notSpawned}");
            
            if (notSpawned > 0 && nm != null && nm.IsConnectedClient)
            {
                results.Add("WARNING: Some NetworkObjects are not spawned after connection!");
                foreach (var no in allNetworkObjects)
                {
                    if (!no.IsSpawned)
                    {
                        results.Add($"  NOT SPAWNED: {GetFullPath(no.gameObject)}");
                    }
                }
            }
            results.Add("");
            
            // 3. Check Critical Components
            results.Add("--- CRITICAL COMPONENTS ---");
            
            // PlayerWorkspaceManager
            var pwm = FindAnyObjectByType<Network.PlayerWorkspaceManager>();
            if (pwm == null)
            {
                results.Add("ERROR: PlayerWorkspaceManager NOT FOUND!");
            }
            else
            {
                var pwmNo = pwm.GetComponent<NetworkObject>();
                results.Add($"PlayerWorkspaceManager: Found ({pwm.gameObject.name})");
                results.Add($"  Has NetworkObject: {pwmNo != null}");
                if (pwmNo != null)
                {
                    results.Add($"  NetworkObjectId: {pwmNo.NetworkObjectId}");
                    results.Add($"  IsSpawned: {pwmNo.IsSpawned}");
                    results.Add($"  OwnerClientId: {pwmNo.OwnerClientId}");
                }
            }
            
            // NetworkedLevelManager
            var nlm = FindAnyObjectByType<Network.NetworkedLevelManager>();
            if (nlm == null)
            {
                results.Add("ERROR: NetworkedLevelManager NOT FOUND!");
            }
            else
            {
                var nlmNo = nlm.GetComponent<NetworkObject>();
                results.Add($"NetworkedLevelManager: Found ({nlm.gameObject.name})");
                results.Add($"  Has NetworkObject: {nlmNo != null}");
                if (nlmNo != null)
                {
                    results.Add($"  NetworkObjectId: {nlmNo.NetworkObjectId}");
                    results.Add($"  IsSpawned: {nlmNo.IsSpawned}");
                    results.Add($"  OwnerClientId: {nlmNo.OwnerClientId}");
                }
            }
            
            // XRINetworkGameManager
            var xriMgr = FindAnyObjectByType<XRMultiplayer.XRINetworkGameManager>();
            if (xriMgr == null)
            {
                results.Add("WARNING: XRINetworkGameManager NOT FOUND!");
                results.Add("  -> Add XRI Network Game Manager prefab to scene");
            }
            else
            {
                results.Add($"XRINetworkGameManager: Found ({xriMgr.gameObject.name})");
                results.Add($"  ConnectionState: {XRMultiplayer.XRINetworkGameManager.CurrentConnectionState.Value}");
                results.Add($"  Connected: {XRMultiplayer.XRINetworkGameManager.Connected.Value}");
            }
            results.Add("");
            
            // 4. Check Player Workspaces
            results.Add("--- PLAYER WORKSPACES ---");
            var workspaces = FindObjectsByType<Network.PlayerWorkspace>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            results.Add($"Player Workspaces found: {workspaces.Length}");
            foreach (var ws in workspaces)
            {
                results.Add($"  - {ws.gameObject.name} (Active: {ws.gameObject.activeInHierarchy})");
            }
            results.Add("");
            
            // 5. Check all NetworkBehaviours
            results.Add("--- ALL NETWORK BEHAVIOURS ---");
            var allBehaviours = FindObjectsByType<NetworkBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _networkBehavioursInScene = allBehaviours.Length;
            results.Add($"Total NetworkBehaviours: {allBehaviours.Length}");
            
            var behavioursWithoutNetworkObject = new List<NetworkBehaviour>();
            foreach (var nb in allBehaviours)
            {
                // Use NetworkBehaviour.NetworkObject which correctly finds the NetworkObject
                // on the same GameObject OR any parent (standard Netcode hierarchy pattern)
                if (nb.NetworkObject == null)
                {
                    behavioursWithoutNetworkObject.Add(nb);
                }
            }
            
            if (behavioursWithoutNetworkObject.Count > 0)
            {
                results.Add($"ERROR: {behavioursWithoutNetworkObject.Count} NetworkBehaviours WITHOUT NetworkObject!");
                foreach (var nb in behavioursWithoutNetworkObject)
                {
                    results.Add($"  - {nb.GetType().Name} on {GetFullPath(nb.gameObject)}");
                }
            }
            else
            {
                results.Add("All NetworkBehaviours have NetworkObject components");
            }
            results.Add("");
            
            // 6. Summary
            results.Add("--- SUMMARY ---");
            bool hasErrors = false;
            
            if (!_hasNetworkManager) { results.Add("ISSUE: Missing NetworkManager"); hasErrors = true; }
            if (xriMgr == null) { results.Add("ISSUE: Missing XRINetworkGameManager"); hasErrors = true; }
            if (pwm == null) { results.Add("ISSUE: Missing PlayerWorkspaceManager"); hasErrors = true; }
            if (nlm == null) { results.Add("ISSUE: Missing NetworkedLevelManager"); hasErrors = true; }
            if (behavioursWithoutNetworkObject.Count > 0) { results.Add($"ISSUE: {behavioursWithoutNetworkObject.Count} NetworkBehaviours missing NetworkObject"); hasErrors = true; }
            if (nm != null && nm.IsConnectedClient && notSpawned > 0) { results.Add($"ISSUE: {notSpawned} NetworkObjects not spawned while connected"); hasErrors = true; }
            
            if (!hasErrors)
            {
                results.Add("No critical issues detected");
            }
            
            results.Add("==============================================");
            
            // Output results
            string fullResult = string.Join("\n", results);
            _lastDiagnosticResult = fullResult;
            
            // Log to console with color
            UnityEngine.Debug.Log($"<color=#00FFFF>{fullResult}</color>");
        }
        
        private string GetFullPath(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
        
        [ContextMenu("Check NetworkObject on This GameObject")]
        public void CheckNetworkObjectOnThis()
        {
            var no = GetComponent<NetworkObject>();
            if (no != null)
            {
                UnityEngine.Debug.Log($"NetworkObject found: IsSpawned={no.IsSpawned}, NetworkObjectId={no.NetworkObjectId}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("No NetworkObject on this GameObject!");
            }
        }
        
        [ContextMenu("Force Log All Spawned Objects")]
        public void LogAllSpawnedObjects()
        {
            if (NetworkManager.Singleton?.SpawnManager == null)
            {
                UnityEngine.Debug.LogWarning("SpawnManager not available");
                return;
            }
            
            var spawned = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            UnityEngine.Debug.Log($"=== {spawned.Count} Spawned Objects ===");
            foreach (var kvp in spawned)
            {
                UnityEngine.Debug.Log($"  [{kvp.Key}] {kvp.Value?.name ?? "NULL"} - Owner: {kvp.Value?.OwnerClientId}");
            }
        }
    }
}
