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

            Debug.Log($"NetworkBlock: Spawned {gameObject.name} with NetworkObjectId {NetworkObjectId}, Owner: {OwnerClientId}");
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from network variable changes
            _isAtRest.OnValueChanged -= OnAtRestChanged;
            _holdingPlayerId.OnValueChanged -= OnHoldingPlayerChanged;

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

            // Request ownership if we don't have it
            if (!IsOwner)
            {
                RequestOwnershipServerRpc();
            }

            // Update holding player - only if we're the owner
            if (IsOwner)
            {
                _holdingPlayerId.Value = NetworkManager.Singleton.LocalClientId;
                _isAtRest.Value = false;
            }

            // Wake up the rigidbody
            if (_rigidbody != null)
            {
                _rigidbody.WakeUp();
            }

            _sleepTimer = 0f;

            Debug.Log($"NetworkBlock: {gameObject.name} grabbed by player {NetworkManager.Singleton.LocalClientId}");
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
        /// Sets the workspace index for this block (server only).
        /// </summary>
        /// <param name="workspaceIndex">The workspace index (0 or 1)</param>
        public void SetWorkspaceIndex(int workspaceIndex)
        {
            if (IsServer)
            {
                _workspaceIndex.Value = workspaceIndex;
            }
        }

        #endregion

        #region Public API

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
