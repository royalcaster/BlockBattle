using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlockBattle.Network
{
    /// <summary>
    /// Network-aware block component that synchronizes block state across clients.
    /// Implements "at rest" optimization to reduce bandwidth by only syncing moving blocks.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkBlock : NetworkBehaviour
    {
        #region Network Variables

        /// <summary>
        /// Whether the block is currently at rest (sleeping).
        /// When true, transform sync is disabled to save bandwidth.
        /// </summary>
        private NetworkVariable<bool> _isAtRest = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// The player ID who currently owns/is holding this block.
        /// 0 means no one is holding it.
        /// </summary>
        private NetworkVariable<ulong> _holdingPlayerId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// The workspace index this block belongs to (0 or 1 for two-player).
        /// </summary>
        private NetworkVariable<int> _workspaceIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// The block color (synced across network).
        /// Using int to represent BlockColor enum for network serialization.
        /// </summary>
        private NetworkVariable<int> _blockColorIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// Whether the block has been ejected from the shelf (physics enabled).
        /// This is synced across the network to ensure all clients enable physics together.
        /// </summary>
        private NetworkVariable<bool> _isEjected = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        #endregion

        #region Serialized Fields

        [Header("At-Rest Optimization")]
        [SerializeField, Tooltip("Time in seconds the block must be sleeping before considered at rest")]
        private float _sleepThreshold = 0.5f;

        [SerializeField, Tooltip("Velocity threshold below which block is considered stationary")]
        private float _velocityThreshold = 0.01f;

        [SerializeField, Tooltip("Angular velocity threshold below which block is considered stationary")]
        private float _angularVelocityThreshold = 0.1f;

        #endregion

        #region Private Fields

        private Rigidbody _rigidbody;
        private XRGrabInteractable _grabInteractable;
        private NetworkRigidbody _networkRigidbody;
        private float _sleepTimer = 0f;
        private bool _wasAtRest = false;
        private Vector3 _lastSyncedPosition;
        private Quaternion _lastSyncedRotation;

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether this block is currently at rest.
        /// </summary>
        public bool IsAtRest => _isAtRest.Value;

        /// <summary>
        /// Gets the player ID currently holding this block.
        /// </summary>
        public ulong HoldingPlayerId => _holdingPlayerId.Value;

        /// <summary>
        /// Gets the workspace index this block belongs to.
        /// </summary>
        public int WorkspaceIndex => _workspaceIndex.Value;

        /// <summary>
        /// Gets whether this block is currently being held by any player.
        /// </summary>
        public bool IsHeld => _holdingPlayerId.Value != 0;

        /// <summary>
        /// Gets the block color.
        /// </summary>
        public BlockColor BlockColor => (BlockColor)_blockColorIndex.Value;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _grabInteractable = GetComponent<XRGrabInteractable>();
            _networkRigidbody = GetComponent<NetworkRigidbody>();
        }

        private void Start()
        {
            // Subscribe to grab events if XRGrabInteractable exists
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.AddListener(OnGrabbed);
                _grabInteractable.selectExited.AddListener(OnReleased);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from grab events
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                _grabInteractable.selectExited.RemoveListener(OnReleased);
            }
        }

        private void FixedUpdate()
        {
            // Skip if not spawned, not owner, or this is a reference block (display only)
            if (!IsSpawned || !IsOwner) return;

            UpdateAtRestState();
        }

        #endregion

        #region Network Callbacks

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Store initial position for sync comparison
            _lastSyncedPosition = transform.position;
            _lastSyncedRotation = transform.rotation;

            // Subscribe to network variable changes
            _isAtRest.OnValueChanged += OnAtRestChanged;
            _holdingPlayerId.OnValueChanged += OnHoldingPlayerChanged;
            _blockColorIndex.OnValueChanged += OnBlockColorChanged;
            _isEjected.OnValueChanged += OnEjectedChanged;
            _workspaceIndex.OnValueChanged += OnWorkspaceIndexChanged;

            // Non-owners should always apply the color from the NetworkVariable
            // (it may have been set by the owner before this client spawned the object)
            if (!IsOwner)
            {
                // Delay color application slightly to ensure NetworkVariable is synced
                StartCoroutine(ApplyColorAfterSync());
                
                // Apply ejection state for late-joining clients
                StartCoroutine(ApplyEjectionStateAfterSync());
                
                // Log workspace index when synced
                StartCoroutine(LogWorkspaceAfterSync());
            }

            Debug.Log($"NetworkBlock: Spawned {gameObject.name} with NetworkObjectId {NetworkObjectId}, Owner: {OwnerClientId}, Color: {(BlockColor)_blockColorIndex.Value}, WorkspaceIndex: {_workspaceIndex.Value}, IsEjected: {_isEjected.Value}");
        }

        /// <summary>
        /// Applies color after a short delay to ensure NetworkVariable sync on non-owners.
        /// </summary>
        private System.Collections.IEnumerator ApplyColorAfterSync()
        {
            Debug.Log($"NetworkBlock: ApplyColorAfterSync starting for {gameObject.name}, current colorIndex={_blockColorIndex.Value}");
            
            // Wait for network sync - try multiple times with increasing delays
            // Owner's color set might be deferred, so we need to wait longer
            int attempts = 0;
            int maxAttempts = 40; // Wait up to 40 frames for owner to set color
            
            while (attempts < maxAttempts)
            {
                // Wait a frame
                yield return null;
                attempts++;
                
                int colorIndex = _blockColorIndex.Value;
                
                // If color is set to something other than 0, apply immediately
                if (colorIndex != 0)
                {
                    ApplyBlockColor((BlockColor)colorIndex);
                    Debug.Log($"NetworkBlock: Applied synced color {(BlockColor)colorIndex} to {gameObject.name} (attempt {attempts})");
                    yield break;
                }
                
                // Log progress every 10 attempts
                if (attempts % 10 == 0)
                {
                    Debug.Log($"NetworkBlock: Waiting for color sync on {gameObject.name}, attempt {attempts}, colorIndex still {colorIndex}");
                }
            }
            
            // Final fallback after max attempts - apply whatever value we have (might be Natural/0)
            int finalColor = _blockColorIndex.Value;
            ApplyBlockColor((BlockColor)finalColor);
            Debug.Log($"NetworkBlock: Applied final color {(BlockColor)finalColor} to {gameObject.name} after {maxAttempts} attempts");
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from network variable changes
            _isAtRest.OnValueChanged -= OnAtRestChanged;
            _holdingPlayerId.OnValueChanged -= OnHoldingPlayerChanged;
            _blockColorIndex.OnValueChanged -= OnBlockColorChanged;
            _isEjected.OnValueChanged -= OnEjectedChanged;
            _workspaceIndex.OnValueChanged -= OnWorkspaceIndexChanged;

            base.OnNetworkDespawn();
        }

        #endregion

        #region At-Rest Optimization

        /// <summary>
        /// Updates the at-rest state based on rigidbody velocity.
        /// </summary>
        private void UpdateAtRestState()
        {
            // Double-check we can write to network variables
            if (_rigidbody == null || !IsOwner || !IsSpawned) return;

            // Check if block is sleeping or moving very slowly
            bool isCurrentlySleeping = _rigidbody.IsSleeping() ||
                (_rigidbody.linearVelocity.sqrMagnitude < _velocityThreshold * _velocityThreshold &&
                 _rigidbody.angularVelocity.sqrMagnitude < _angularVelocityThreshold * _angularVelocityThreshold);

            // Don't consider at rest if being held
            if (IsHeld)
            {
                _sleepTimer = 0f;
                if (_isAtRest.Value)
                {
                    _isAtRest.Value = false;
                }
                return;
            }

            if (isCurrentlySleeping)
            {
                _sleepTimer += Time.fixedDeltaTime;

                // Only mark as at rest after threshold time
                if (_sleepTimer >= _sleepThreshold && !_isAtRest.Value)
                {
                    _isAtRest.Value = true;
                    OnBlockCameToRest();
                }
            }
            else
            {
                _sleepTimer = 0f;

                if (_isAtRest.Value)
                {
                    _isAtRest.Value = false;
                    OnBlockStartedMoving();
                }
            }
        }

        /// <summary>
        /// Called when the block comes to rest.
        /// </summary>
        private void OnBlockCameToRest()
        {
            // Store final position for potential reconciliation
            _lastSyncedPosition = transform.position;
            _lastSyncedRotation = transform.rotation;

            // Optionally disable NetworkRigidbody sync when at rest
            // This is handled by the NetworkRigidbody component itself in Netcode 2.x
            Debug.Log($"NetworkBlock: {gameObject.name} came to rest at {transform.position}");
        }

        /// <summary>
        /// Called when the block starts moving.
        /// </summary>
        private void OnBlockStartedMoving()
        {
            Debug.Log($"NetworkBlock: {gameObject.name} started moving");
        }

        /// <summary>
        /// Callback when at-rest state changes on the network.
        /// </summary>
        private void OnAtRestChanged(bool previousValue, bool newValue)
        {
            if (!IsOwner)
            {
                // Non-owners can use this to optimize local physics
                if (newValue && _rigidbody != null)
                {
                    // Force rigidbody to sleep on clients when server says it's at rest
                    _rigidbody.Sleep();
                }
            }
        }

        #endregion

        #region Grab Handling

        /// <summary>
        /// Called when the block is grabbed by a player.
        /// </summary>
        private void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
        {
            if (!IsSpawned) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            Debug.Log($"NetworkBlock: {gameObject.name} OnGrabbed - IsOwner={IsOwner}, LocalClient={localClientId}, WorkspaceIndex={_workspaceIndex.Value}");

            // Check if this player is allowed to grab this block (must be from their workspace)
            if (!CanPlayerGrabBlock(localClientId))
            {
                Debug.Log($"NetworkBlock: {gameObject.name} - Player {localClientId} cannot grab block from workspace {_workspaceIndex.Value}");
                // Cancel the grab by forcing deselect
                if (_grabInteractable != null && args.interactorObject != null)
                {
                    // Use interactionManager to force deselect after a frame
                    StartCoroutine(ForceDeselectAfterFrame(args.interactorObject));
                }
                return;
            }

            // Request ownership if we don't have it
            if (!IsOwner)
            {
                // In Distributed Authority mode, use RequestOwnership() directly
                // In server mode, use ServerRpc
                if (NetworkManager.Singleton.NetworkConfig.NetworkTopology == NetworkTopologyTypes.DistributedAuthority)
                {
                    // DA mode: request ownership directly from the NetworkObject
                    NetworkObject.ChangeOwnership(localClientId);
                    Debug.Log($"NetworkBlock: DA mode - directly changing ownership to {localClientId}");
                }
                else
                {
                    // Server mode: use RPC
                    RequestOwnershipServerRpc();
                }
            }

            // Update holding player - defer if we just requested ownership
            StartCoroutine(SetHoldingPlayerAfterOwnership());

            // Wake up the rigidbody
            if (_rigidbody != null)
            {
                _rigidbody.WakeUp();
            }

            _sleepTimer = 0f;

            Debug.Log($"NetworkBlock: {gameObject.name} grabbed by player {localClientId}");
        }

        /// <summary>
        /// Checks if a player is allowed to grab this block.
        /// Players can only grab blocks from their own workspace.
        /// </summary>
        private bool CanPlayerGrabBlock(ulong clientId)
        {
            // Allow all players to grab any block - simplified for gameplay
            // No workspace restrictions needed
            return true;
        }

        /// <summary>
        /// Forces deselection of the block after a frame (for unauthorized grabs).
        /// </summary>
        private System.Collections.IEnumerator ForceDeselectAfterFrame(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
        {
            yield return null;
            
            if (_grabInteractable != null && _grabInteractable.isSelected)
            {
                // Force the interactor to drop this object
                var interactionManager = _grabInteractable.interactionManager;
                if (interactionManager != null)
                {
                    interactionManager.SelectExit(interactor, _grabInteractable);
                    Debug.Log($"NetworkBlock: Forced deselect of {gameObject.name}");
                }
            }
        }

        /// <summary>
        /// Waits briefly for ownership transfer then sets holding player.
        /// </summary>
        private System.Collections.IEnumerator SetHoldingPlayerAfterOwnership()
        {
            // Wait a frame for ownership to transfer
            yield return null;
            
            if (IsOwner)
            {
                _holdingPlayerId.Value = NetworkManager.Singleton.LocalClientId;
                _isAtRest.Value = false;
            }
        }

        /// <summary>
        /// Called when the block is released by a player.
        /// </summary>
        private void OnReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
        {
            if (!IsSpawned) return;

            // Clear holding player
            if (IsOwner)
            {
                _holdingPlayerId.Value = 0;
            }

            Debug.Log($"NetworkBlock: {gameObject.name} released");
        }

        /// <summary>
        /// Callback when holding player changes on the network.
        /// </summary>
        private void OnHoldingPlayerChanged(ulong previousValue, ulong newValue)
        {
            // Can be used for visual feedback (e.g., highlight blocks being held by others)
        }

        /// <summary>
        /// Callback when block color changes on the network.
        /// </summary>
        private void OnBlockColorChanged(int previousValue, int newValue)
        {
            Debug.Log($"NetworkBlock: OnBlockColorChanged {gameObject.name} from {(BlockColor)previousValue} to {(BlockColor)newValue}");
            ApplyBlockColor((BlockColor)newValue);
        }

        /// <summary>
        /// Callback when ejected state changes on the network.
        /// </summary>
        private void OnEjectedChanged(bool previousValue, bool newValue)
        {
            Debug.Log($"NetworkBlock: OnEjectedChanged {gameObject.name} from {previousValue} to {newValue}");
            if (newValue && !previousValue)
            {
                // Block was just ejected - enable physics
                EnablePhysicsLocally();
            }
        }

        /// <summary>
        /// Callback when workspace index changes on the network.
        /// </summary>
        private void OnWorkspaceIndexChanged(int previousValue, int newValue)
        {
            Debug.Log($"NetworkBlock: OnWorkspaceIndexChanged {gameObject.name} from {previousValue} to {newValue}");
        }

        /// <summary>
        /// Logs workspace index after sync for debugging late-joining clients.
        /// </summary>
        private System.Collections.IEnumerator LogWorkspaceAfterSync()
        {
            // Wait a bit longer for workspace to sync (owner needs time to set it after spawn)
            yield return new WaitForSeconds(0.5f);
            
            if (_workspaceIndex.Value >= 0)
            {
                Debug.Log($"NetworkBlock: Workspace synced for {gameObject.name}: {_workspaceIndex.Value}");
            }
            else
            {
                // Try again
                for (int attempt = 0; attempt < 5 && _workspaceIndex.Value < 0; attempt++)
                {
                    yield return new WaitForSeconds(0.2f);
                }
                Debug.Log($"NetworkBlock: Final workspace for {gameObject.name}: {_workspaceIndex.Value}");
            }
        }

        /// <summary>
        /// Applies ejection state after sync for non-owners.
        /// </summary>
        private System.Collections.IEnumerator ApplyEjectionStateAfterSync()
        {
            // Wait a couple frames for NetworkVariable to sync
            yield return null;
            yield return null;
            
            if (_isEjected.Value)
            {
                Debug.Log($"NetworkBlock: Applying synced ejection state to {gameObject.name}");
                EnablePhysicsLocally();
            }
        }

        /// <summary>
        /// Enables physics on this block locally.
        /// </summary>
        private void EnablePhysicsLocally()
        {
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.WakeUp();
                Debug.Log($"NetworkBlock: Physics enabled on {gameObject.name}");
            }
        }

        /// <summary>
        /// Applies the specified color material to this block.
        /// </summary>
        private void ApplyBlockColor(BlockColor blockColor)
        {
            // Load the colored material from Resources
            string materialName = BlockColorUtility.GetMaterialName(blockColor);
            Material coloredMaterial = Resources.Load<Material>(materialName);

            // If not in Resources, try loading from asset path (editor only)
#if UNITY_EDITOR
            if (coloredMaterial == null)
            {
                string materialPath = $"Assets/BlockBattle/Materials/{materialName}.mat";
                coloredMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
#endif

            if (coloredMaterial == null)
            {
                Debug.LogWarning($"NetworkBlock: Could not load material {materialName} from Resources or Assets");
                return;
            }

            // Find all MeshRenderers in the block
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.material = coloredMaterial;
                }
            }

            Debug.Log($"NetworkBlock: Applied color {blockColor} to {gameObject.name}");
        }

        #endregion

        #region Server RPCs

        /// <summary>
        /// Requests ownership of this block from the server.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestOwnershipServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong requestingClient = rpcParams.Receive.SenderClientId;

            // Only transfer ownership if block is not currently held by someone else
            if (_holdingPlayerId.Value == 0 || _holdingPlayerId.Value == requestingClient)
            {
                NetworkObject.ChangeOwnership(requestingClient);
                Debug.Log($"NetworkBlock: Ownership of {gameObject.name} transferred to client {requestingClient}");
            }
            else
            {
                Debug.Log($"NetworkBlock: Ownership request denied - {gameObject.name} is held by player {_holdingPlayerId.Value}");
            }
        }

        /// <summary>
        /// Sets the workspace index for this block (owner only).
        /// </summary>
        /// <param name="workspaceIndex">The workspace index (0 or 1)</param>
        public void SetWorkspaceIndex(int workspaceIndex)
        {
            // In DA mode, owner can write. In server mode, server can write.
            // After Spawn(), we should have ownership, but defer if needed
            if (IsSpawned && (IsOwner || IsServer))
            {
                _workspaceIndex.Value = workspaceIndex;
                Debug.Log($"NetworkBlock: Set workspace index to {workspaceIndex} for {gameObject.name}");
            }
            else
            {
                // Defer until ownership is established
                StartCoroutine(SetWorkspaceIndexDeferred(workspaceIndex));
            }
        }

        private System.Collections.IEnumerator SetWorkspaceIndexDeferred(int workspaceIndex)
        {
            // Wait for spawn and ownership
            int attempts = 0;
            while ((!IsSpawned || !IsOwner) && attempts < 30)
            {
                yield return null;
                attempts++;
            }
            
            if (IsOwner)
            {
                _workspaceIndex.Value = workspaceIndex;
                Debug.Log($"NetworkBlock: Deferred set workspace index to {workspaceIndex} for {gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"NetworkBlock: Failed to set workspace index - not owner after {attempts} frames");
            }
        }

        /// <summary>
        /// Sets the block color (owner only). This will sync to all clients.
        /// </summary>
        /// <param name="color">The block color to set</param>
        public void SetBlockColor(BlockColor color)
        {
            ulong localId = NetworkManager.Singleton?.LocalClientId ?? 0;
            Debug.Log($"NetworkBlock: SetBlockColor called for {gameObject.name} with color {color}. IsSpawned={IsSpawned}, IsOwner={IsOwner}, OwnerClientId={OwnerClientId}, LocalClientId={localId}");
            
            // Apply locally immediately for visual feedback
            ApplyBlockColor(color);
            
            // In DA mode, the spawning client becomes owner
            // Use direct OwnerClientId comparison for immediate check, as IsOwner might not update immediately
            bool isOwnerDirect = IsSpawned && NetworkManager.Singleton != null && 
                                 OwnerClientId == NetworkManager.Singleton.LocalClientId;
            
            if (IsSpawned && (IsOwner || isOwnerDirect))
            {
                _blockColorIndex.Value = (int)color;
                Debug.Log($"NetworkBlock: Set color immediately to {color} ({(int)color}) for {gameObject.name}");
            }
            else
            {
                // Defer to ensure ownership is established
                Debug.Log($"NetworkBlock: Deferring color set for {gameObject.name} - IsSpawned={IsSpawned}, IsOwner={IsOwner}, isOwnerDirect={isOwnerDirect}");
                StartCoroutine(SetBlockColorDeferred(color));
            }
        }

        private System.Collections.IEnumerator SetBlockColorDeferred(BlockColor color)
        {
            Debug.Log($"NetworkBlock: SetBlockColorDeferred starting for {gameObject.name}, color={color}");
            
            // Wait just 1 frame first - in most cases ownership is established by then
            yield return null;
            
            // Use direct OwnerClientId comparison as IsOwner might not update immediately
            bool CheckOwnership()
            {
                return IsSpawned && NetworkManager.Singleton != null && 
                       (IsOwner || OwnerClientId == NetworkManager.Singleton.LocalClientId);
            }
            
            // Check if we're now owner
            if (CheckOwnership())
            {
                _blockColorIndex.Value = (int)color;
                Debug.Log($"NetworkBlock: Set color to {color} for {gameObject.name} (deferred 1 frame, index={_blockColorIndex.Value})");
                yield break;
            }
            
            // Wait a bit longer if needed
            int attempts = 0;
            while (!CheckOwnership() && attempts < 30)
            {
                if (attempts % 5 == 0)
                {
                    Debug.Log($"NetworkBlock: SetBlockColorDeferred waiting... attempt {attempts}, IsSpawned={IsSpawned}, IsOwner={IsOwner}, OwnerClientId={OwnerClientId}, LocalClientId={NetworkManager.Singleton?.LocalClientId}");
                }
                yield return null;
                attempts++;
            }
            
            if (CheckOwnership())
            {
                _blockColorIndex.Value = (int)color;
                Debug.Log($"NetworkBlock: Set color to {color} for {gameObject.name} (deferred after {attempts+1} frames, index={_blockColorIndex.Value})");
            }
            else
            {
                Debug.LogWarning($"NetworkBlock: Failed to set color {color} for {gameObject.name} - IsOwner={IsOwner}, IsSpawned={IsSpawned} after {attempts+1} frames. OwnerClientId={OwnerClientId}, LocalClientId={NetworkManager.Singleton?.LocalClientId}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Ejects this block from the shelf, enabling physics and applying force.
        /// This method should only be called by the owner (session owner in DA mode).
        /// The ejection state is synced to all clients via NetworkVariable.
        /// </summary>
        /// <param name="force">The force to apply when ejecting</param>
        /// <param name="torque">The torque to apply for tumbling</param>
        public void EjectBlock(Vector3 force, Vector3 torque)
        {
            if (!IsSpawned)
            {
                Debug.LogWarning($"NetworkBlock: Cannot eject {gameObject.name} - not spawned yet");
                return;
            }

            // Enable physics locally
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.WakeUp();
                _rigidbody.linearVelocity = force;
                _rigidbody.AddTorque(torque, ForceMode.Impulse);
            }

            // Set ejection state - will sync to all clients
            if (IsOwner)
            {
                _isEjected.Value = true;
                Debug.Log($"NetworkBlock: Ejected {gameObject.name} with force {force}");
            }
            else
            {
                // If not owner yet, defer
                StartCoroutine(SetEjectedAfterOwnership(force, torque));
            }
        }

        private System.Collections.IEnumerator SetEjectedAfterOwnership(Vector3 force, Vector3 torque)
        {
            int attempts = 0;
            while (!IsOwner && attempts < 30)
            {
                yield return null;
                attempts++;
            }

            if (IsOwner)
            {
                _isEjected.Value = true;
                
                // Re-apply force in case it was missed
                if (_rigidbody != null)
                {
                    _rigidbody.linearVelocity = force;
                    _rigidbody.AddTorque(torque, ForceMode.Impulse);
                }
                
                Debug.Log($"NetworkBlock: Deferred ejection of {gameObject.name}");
            }
        }

        /// <summary>
        /// Gets whether this block has been ejected from the shelf.
        /// </summary>
        public bool IsEjected => _isEjected.Value;

        /// <summary>
        /// Forces the block to sync its current position to all clients.
        /// Useful after physics corrections or teleportation.
        /// </summary>
        public void ForceSync()
        {
            if (!IsOwner) return;

            _lastSyncedPosition = transform.position;
            _lastSyncedRotation = transform.rotation;

            // Wake up the rigidbody to trigger NetworkRigidbody sync
            if (_rigidbody != null)
            {
                _rigidbody.WakeUp();
            }

            _isAtRest.Value = false;
            _sleepTimer = 0f;
        }

        /// <summary>
        /// Resets the block to its initial state.
        /// </summary>
        public void ResetBlock()
        {
            if (!IsOwner) return;

            _holdingPlayerId.Value = 0;
            _isAtRest.Value = false;
            _sleepTimer = 0f;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        #endregion

        #region Editor Visualization

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Draw at-rest state indicator
            if (_isAtRest.Value)
            {
                Gizmos.color = Color.green;
            }
            else if (IsHeld)
            {
                Gizmos.color = Color.yellow;
            }
            else
            {
                Gizmos.color = Color.red;
            }

            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.12f);
        }
#endif

        #endregion
    }
}
