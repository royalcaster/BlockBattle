using Unity.Netcode;
using UnityEngine;

namespace BlockBattle.Network
{
    /// <summary>
    /// Network-aware projectile component for slingshot balls.
    /// Handles spawning, physics sync, and collision detection across the network.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkedProjectile : NetworkBehaviour
    {
        #region Serialized Fields

        [Header("Projectile Settings")]
        [SerializeField, Tooltip("Time in seconds before the projectile is automatically destroyed")]
        private float _lifetime = 10f;

        [SerializeField, Tooltip("Minimum velocity to stay alive (projectile destroyed when slower)")]
        private float _minimumVelocity = 0.1f;

        [SerializeField, Tooltip("Time the projectile must be below minimum velocity before being destroyed")]
        private float _slowdownDestroyDelay = 2f;

        #endregion

        #region Network Variables

        /// <summary>
        /// The player ID who fired this projectile.
        /// </summary>
        private NetworkVariable<ulong> _firingPlayerId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        /// <summary>
        /// The workspace index this projectile is targeting (opponent's workspace).
        /// </summary>
        private NetworkVariable<int> _targetWorkspaceIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        #endregion

        #region Private Fields

        private Rigidbody _rigidbody;
        private float _spawnTime;
        private float _slowdownTimer;
        private bool _isInitialized;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the player ID who fired this projectile.
        /// </summary>
        public ulong FiringPlayerId => _firingPlayerId.Value;

        /// <summary>
        /// Gets the target workspace index.
        /// </summary>
        public int TargetWorkspaceIndex => _targetWorkspaceIndex.Value;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            // Only the server/owner should handle lifetime
            if (IsServer)
            {
                CheckLifetime();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Handle collision effects (can be expanded for impact sounds, particles, etc.)
            OnProjectileCollision(collision);
        }

        #endregion

        #region Network Callbacks

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _spawnTime = Time.time;
            _slowdownTimer = 0f;
            _isInitialized = true;

            Debug.Log($"NetworkedProjectile: Spawned with NetworkObjectId {NetworkObjectId}, fired by player {_firingPlayerId.Value}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the projectile with firing data. Called by the spawning system.
        /// </summary>
        /// <param name="firingPlayerId">The player who fired this projectile</param>
        /// <param name="targetWorkspaceIndex">The workspace this projectile targets</param>
        /// <param name="initialVelocity">The initial velocity of the projectile</param>
        public void Initialize(ulong firingPlayerId, int targetWorkspaceIndex, Vector3 initialVelocity)
        {
            if (!IsServer)
            {
                Debug.LogWarning("NetworkedProjectile: Initialize should only be called on the server");
                return;
            }

            _firingPlayerId.Value = firingPlayerId;
            _targetWorkspaceIndex.Value = targetWorkspaceIndex;

            // Apply initial velocity
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = initialVelocity;
            }

            // Broadcast initialization to clients
            InitializeClientRpc(initialVelocity);
        }

        /// <summary>
        /// Client-side initialization to ensure velocity is applied correctly.
        /// </summary>
        [ClientRpc]
        private void InitializeClientRpc(Vector3 initialVelocity)
        {
            if (IsServer) return; // Server already set this

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = initialVelocity;
            }
        }

        #endregion

        #region Lifetime Management

        /// <summary>
        /// Checks if the projectile should be destroyed based on lifetime or velocity.
        /// </summary>
        private void CheckLifetime()
        {
            if (!_isInitialized) return;

            // Check maximum lifetime
            if (Time.time - _spawnTime > _lifetime)
            {
                DestroyProjectile();
                return;
            }

            // Check minimum velocity (projectile came to rest)
            if (_rigidbody != null && _rigidbody.linearVelocity.magnitude < _minimumVelocity)
            {
                _slowdownTimer += Time.deltaTime;
                if (_slowdownTimer >= _slowdownDestroyDelay)
                {
                    DestroyProjectile();
                }
            }
            else
            {
                _slowdownTimer = 0f;
            }
        }

        /// <summary>
        /// Destroys the projectile across the network.
        /// </summary>
        private void DestroyProjectile()
        {
            if (!IsServer) return;

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                Debug.Log($"NetworkedProjectile: Destroying projectile {NetworkObjectId}");
                NetworkObject.Despawn(true);
            }
        }

        #endregion

        #region Collision Handling

        /// <summary>
        /// Handles collision events for the projectile.
        /// </summary>
        private void OnProjectileCollision(Collision collision)
        {
            // Check if we hit a block
            NetworkBlock networkBlock = collision.gameObject.GetComponent<NetworkBlock>();
            if (networkBlock != null)
            {
                OnBlockHit(networkBlock, collision);
            }

            // Could add impact effects here (particles, sounds)
        }

        /// <summary>
        /// Called when the projectile hits a networked block.
        /// </summary>
        private void OnBlockHit(NetworkBlock block, Collision collision)
        {
            Debug.Log($"NetworkedProjectile: Hit block {block.gameObject.name} in workspace {block.WorkspaceIndex}");

            // The physics engine handles the force transfer automatically
            // NetworkRigidbody on the block will sync the resulting movement

            // Could notify game manager of the hit for scoring
            if (IsServer)
            {
                NotifyBlockHitServerRpc(block.NetworkObjectId, collision.relativeVelocity.magnitude);
            }
        }

        /// <summary>
        /// Notifies the server about a block hit for potential scoring/effects.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void NotifyBlockHitServerRpc(ulong blockNetworkId, float impactForce)
        {
            // This can be used by the game manager to track destruction progress
            // or trigger effects
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// Spawns a networked projectile at the specified position with the given velocity.
        /// Must be called on the server.
        /// </summary>
        /// <param name="prefab">The projectile prefab to spawn</param>
        /// <param name="position">Spawn position</param>
        /// <param name="velocity">Initial velocity</param>
        /// <param name="firingPlayerId">The player who fired the projectile</param>
        /// <param name="targetWorkspaceIndex">The workspace being targeted</param>
        /// <returns>The spawned projectile, or null if failed</returns>
        public static NetworkedProjectile SpawnProjectile(
            GameObject prefab,
            Vector3 position,
            Vector3 velocity,
            ulong firingPlayerId,
            int targetWorkspaceIndex)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("NetworkedProjectile: SpawnProjectile must be called on the server");
                return null;
            }

            if (prefab == null)
            {
                Debug.LogError("NetworkedProjectile: Prefab is null");
                return null;
            }

            // Instantiate the projectile
            GameObject projectileObj = Object.Instantiate(prefab, position, Quaternion.identity);
            
            // Get network components
            NetworkObject networkObject = projectileObj.GetComponent<NetworkObject>();
            NetworkedProjectile projectile = projectileObj.GetComponent<NetworkedProjectile>();

            if (networkObject == null)
            {
                Debug.LogError("NetworkedProjectile: Prefab is missing NetworkObject component");
                Object.Destroy(projectileObj);
                return null;
            }

            // Spawn on the network
            networkObject.Spawn();

            // Initialize if NetworkedProjectile component exists
            if (projectile != null)
            {
                projectile.Initialize(firingPlayerId, targetWorkspaceIndex, velocity);
            }
            else
            {
                // Fallback: just apply velocity directly
                Rigidbody rb = projectileObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = velocity;
                }
            }

            return projectile;
        }

        #endregion
    }
}
