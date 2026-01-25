using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.Netcode;
using BlockBattle.Network;

namespace BlockBattle
{
    /// <summary>
    /// Unified component that spawns blocks inside a shelf and ejects them when doors are opened.
    /// Combines block spawning logic with door-triggered ejection mechanics.
    /// </summary>
    public class ShelfBlockSpawner : MonoBehaviour
    {
        #region Block Spawning Settings

        [Header("Block Spawning")]
        [SerializeField, Tooltip("Spawn configuration defining which blocks to spawn")]
        private BlockSpawnConfiguration m_SpawnConfiguration;

        /// <summary>
        /// Gets or sets the spawn configuration used for spawning blocks.
        /// </summary>
        public BlockSpawnConfiguration SpawnConfiguration
        {
            get => m_SpawnConfiguration;
            set => m_SpawnConfiguration = value;
        }

        [Header("Block Prefabs")]
        [SerializeField, Tooltip("Cube block prefab")]
        private GameObject m_CubeBlockPrefab;

        [SerializeField, Tooltip("Cylinder block prefab")]
        private GameObject m_CylinderBlockPrefab;

        [SerializeField, Tooltip("Triangle block prefab")]
        private GameObject m_TriangleBlockPrefab;

        [SerializeField, Tooltip("Rectangle block prefab")]
        private GameObject m_RectangleBlockPrefab;

        [SerializeField, Tooltip("Arch block prefab")]
        private GameObject m_ArchBlockPrefab;

        [SerializeField, Tooltip("Big Triangle block prefab")]
        private GameObject m_BigTriangleBlockPrefab;

        #endregion

        #region Spawn Position Settings

        [Header("Spawn Position")]
        [SerializeField, Tooltip("Anchor point for spawning blocks (center of shelf interior). If null, uses this transform.")]
        private Transform m_SpawnAnchor;

        [SerializeField, Tooltip("Spacing between blocks when spawning in meters")]
        private float m_SpawnSpacing = 0.12f;

        [SerializeField, Tooltip("Direction to arrange blocks when spawning (normalized)")]
        private Vector3 m_SpawnDirection = Vector3.right;

        [SerializeField, Tooltip("Whether to randomize the order of spawned blocks. Disable for multiplayer to ensure consistent block order.")]
        private bool m_RandomizeSpawnOrder = false;

        #endregion

        #region Door Settings

        [Header("Door Settings")]
        [SerializeField, Tooltip("Left door HingeJoint reference")]
        private HingeJoint m_LeftDoor;

        [SerializeField, Tooltip("Right door HingeJoint reference")]
        private HingeJoint m_RightDoor;

        [SerializeField, Range(0f, 120f), Tooltip("Door angle at which blocks are ejected. Lower values trigger earlier (recommended: 30-50 degrees)")]
        private float m_TriggerAngle = 40f;

        [SerializeField, Range(0f, 120f), Tooltip("Door angle below which the system resets (must be less than trigger angle)")]
        private float m_ResetAngle = 30f;

        [SerializeField, Tooltip("Impulse force applied to doors when blocks are ejected")]
        private float m_DoorKickForce = 30f;

        #endregion

        #region Ejection Settings

        [Header("Ejection Settings")]
        [SerializeField, Tooltip("Transform whose forward direction defines the base ejection direction. If null, uses this transform's forward.")]
        private Transform m_EjectionDirection;

        [SerializeField, Tooltip("Base force applied to eject blocks. Recommended: 3-5 for gentle drop, 8-12 for stronger push")]
        private float m_EjectionForce = 4f;

        [SerializeField, Range(0f, 1f), Tooltip("How much blocks spread when ejected (0 = straight line, 1 = wide spread). Lower values help blocks not get stuck.")]
        private float m_SpreadAmount = 0.15f;

        [SerializeField, Tooltip("Rotational force applied to blocks for realistic tumbling. Lower values prevent blocks from bouncing back.")]
        private float m_TumbleForce = 2f;

        [SerializeField, Tooltip("Use continuous collision detection to prevent blocks from passing through floor")]
        private bool m_UseContinuousCollision = true;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when blocks are spawned inside the shelf.
        /// </summary>
        public event System.Action<int> OnBlocksSpawned;

        /// <summary>
        /// Event fired when blocks are ejected from the shelf.
        /// </summary>
        public event System.Action<int> OnBlocksEjected;

        /// <summary>
        /// Event fired when the shelf is ready to be triggered again (doors closed).
        /// </summary>
        public event System.Action OnShelfReset;

        #endregion

        #region Multiplayer Settings

        [Header("Multiplayer")]
        [SerializeField, Tooltip("The workspace index this spawner belongs to (for multiplayer)")]
        private int m_WorkspaceIndex = 0;

        [SerializeField, Tooltip("Whether to use network spawning when in multiplayer mode")]
        private bool m_UseNetworkSpawning = true;

        [SerializeField, Tooltip("In multiplayer, auto-eject blocks after spawning (bypasses door trigger)")]
        private bool m_AutoEjectInMultiplayer = true;

        [SerializeField, Tooltip("Delay before auto-ejecting blocks in multiplayer (seconds)")]
        private float m_AutoEjectDelay = 1.5f;

        #endregion

        #region Private State

        private bool _hasTriggered = false;
        private List<Rigidbody> _storedBlocks = new List<Rigidbody>();
        private List<GameObject> _spawnedBlockObjects = new List<GameObject>();
        private List<NetworkObject> _spawnedNetworkObjects = new List<NetworkObject>();
        private bool _isMultiplayerMode = false;
        private bool _gameStarted = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the workspace index for this spawner.
        /// </summary>
        public int WorkspaceIndex
        {
            get => m_WorkspaceIndex;
            set => m_WorkspaceIndex = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ValidatePrefabs();
            CheckMultiplayerMode();
        }

        private void Update()
        {
            MonitorDoors();
            
            // Use physics overlap for more reliable block detection
            // This catches blocks that triggers might miss
            DetectBlocksInShelf();
        }
        
        /// <summary>
        /// Detects blocks inside the shelf using Physics.OverlapBox.
        /// More reliable than OnTriggerStay for slow-moving or resting objects.
        /// </summary>
        private void DetectBlocksInShelf()
        {
            // Get the box collider bounds
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null || !boxCollider.isTrigger)
                return;

            // Calculate world-space center and half extents
            Vector3 worldCenter = transform.TransformPoint(boxCollider.center);
            Vector3 halfExtents = Vector3.Scale(boxCollider.size, transform.lossyScale) * 0.5f;

            // Find all colliders in the box
            Collider[] colliders = Physics.OverlapBox(worldCenter, halfExtents, transform.rotation);

            // Track which blocks are currently in the shelf
            HashSet<Rigidbody> currentBlocksInShelf = new HashSet<Rigidbody>();

            foreach (Collider col in colliders)
            {
                if (col == boxCollider) continue; // Skip self

                // Get rigidbody from collider or parent
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = col.GetComponentInParent<Rigidbody>();
                
                if (rb == null) continue;

                string name = rb.gameObject.name;
                
                // Skip reference blocks
                if (name.StartsWith("ReferenceBlock_") || name.Contains("Reference"))
                    continue;

                // Check if it's a player block
                if (name.Contains("Block_") || name.Contains("_Shelf") || name.Contains("_Spawned"))
                {
                    currentBlocksInShelf.Add(rb);
                    
                    // Add to stored blocks if not already there
                    if (!_storedBlocks.Contains(rb))
                    {
                        _storedBlocks.Add(rb);
                        Debug.Log($"ShelfBlockSpawner: Block detected in shelf - {name}. Total stored: {_storedBlocks.Count}");
                    }
                }
            }

            // Remove blocks that are no longer in the shelf
            for (int i = _storedBlocks.Count - 1; i >= 0; i--)
            {
                if (_storedBlocks[i] == null || !currentBlocksInShelf.Contains(_storedBlocks[i]))
                {
                    if (_storedBlocks[i] != null)
                    {
                        Debug.Log($"ShelfBlockSpawner: Block left shelf - {_storedBlocks[i].gameObject.name}. Total stored: {_storedBlocks.Count - 1}");
                    }
                    _storedBlocks.RemoveAt(i);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryAddBlockToStorage(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Use OnTriggerStay for more reliable detection when blocks are
            // placed slowly or are already inside the trigger
            TryAddBlockToStorage(other);
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && _storedBlocks.Contains(rb))
            {
                _storedBlocks.Remove(rb);
            }
        }

        /// <summary>
        /// Attempts to add a block to the stored blocks list.
        /// Only adds blocks that are player blocks (not reference blocks).
        /// </summary>
        private void TryAddBlockToStorage(Collider other)
        {
            // Get rigidbody - check both the collider's object and its parent
            // (blocks may have collider on child but rigidbody on root)
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = other.GetComponentInParent<Rigidbody>();
            }
            
            if (rb == null)
                return;

            // Skip if already stored
            if (_storedBlocks.Contains(rb))
                return;

            // Get the root object name for checking (rigidbody's gameobject)
            string name = rb.gameObject.name;
            
            // Skip reference blocks (they shouldn't be stored)
            if (name.StartsWith("ReferenceBlock_") || name.Contains("Reference"))
                return;

            // Only add player blocks
            if (name.Contains("Block_") || name.Contains("_Shelf") || name.Contains("_Spawned"))
            {
                _storedBlocks.Add(rb);
                Debug.Log($"ShelfBlockSpawner: Block entered shelf - {name}. Total stored: {_storedBlocks.Count}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Spawns blocks inside the shelf based on the current spawn configuration.
        /// Blocks are spawned as kinematic (no physics) until ejected.
        /// </summary>
        public void SpawnBlocks()
        {
            Debug.Log($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: SpawnBlocks() ENTERED ###");
            
            // Re-check multiplayer mode
            CheckMultiplayerMode();
            
            bool isSessionOwner = NetworkManager.Singleton != null && 
                NetworkManager.Singleton.LocalClientId == NetworkManager.Singleton.CurrentSessionOwner;
            
            Debug.Log($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: Multiplayer={_isMultiplayerMode}, IsSessionOwner={isSessionOwner}, NetworkManager={NetworkManager.Singleton != null} ###");
            
            if (m_SpawnConfiguration == null)
            {
                Debug.LogError($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: SpawnConfiguration is NULL! Cannot spawn! ###");
                return;
            }

            if (m_SpawnConfiguration.SpawnEntries == null || m_SpawnConfiguration.SpawnEntries.Count == 0)
            {
                Debug.LogError($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: SpawnConfiguration has NO ENTRIES! Cannot spawn! ###");
                return;
            }

            Debug.Log($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: Will spawn {m_SpawnConfiguration.SpawnEntries.Count} blocks from '{m_SpawnConfiguration.ConfigurationName}' ###");

            // Clear any previously spawned blocks
            ClearSpawnedBlocks();

            // Get spawn entries (optionally shuffled)
            List<BlockSpawnEntry> entries = m_SpawnConfiguration.SpawnEntries.ToList();
            if (m_RandomizeSpawnOrder)
            {
                ShuffleList(entries);
            }

            // Calculate spawn base position
            Vector3 basePosition = m_SpawnAnchor != null ? m_SpawnAnchor.position : transform.position;
            Vector3 normalizedDirection = m_SpawnDirection.normalized;

            // Spawn each block
            int blockIndex = 0;
            foreach (BlockSpawnEntry entry in entries)
            {
                Vector3 spawnPosition = basePosition + normalizedDirection * (blockIndex * m_SpawnSpacing);
                GameObject block = SpawnSingleBlock(entry, spawnPosition);

                if (block != null)
                {
                    _spawnedBlockObjects.Add(block);
                    blockIndex++;
                }
            }

            Debug.Log($"ShelfBlockSpawner[W{m_WorkspaceIndex}]: Successfully spawned {_spawnedBlockObjects.Count} blocks inside shelf. StoredBlocks count: {_storedBlocks.Count}, NetworkObjects: {_spawnedNetworkObjects.Count}");
            
            // Reset trigger state so doors can trigger ejection
            _hasTriggered = false;
            
            // Enable door monitoring now that blocks are spawned
            _gameStarted = true;
            
            OnBlocksSpawned?.Invoke(_spawnedBlockObjects.Count);

            // In multiplayer, auto-eject blocks after a delay to ensure sync
            // This bypasses the door trigger which is hard to sync across clients
            if (_isMultiplayerMode && m_AutoEjectInMultiplayer && IsSessionOwner)
            {
                Debug.Log($"ShelfBlockSpawner: Scheduling auto-eject in {m_AutoEjectDelay}s for workspace {m_WorkspaceIndex}");
                StartCoroutine(AutoEjectAfterDelay());
            }
        }

        /// <summary>
        /// Auto-ejects blocks after a delay in multiplayer mode.
        /// This ensures blocks are ejected reliably without depending on door physics sync.
        /// </summary>
        private System.Collections.IEnumerator AutoEjectAfterDelay()
        {
            yield return new WaitForSeconds(m_AutoEjectDelay);
            
            if (!_hasTriggered)
            {
                Debug.Log($"ShelfBlockSpawner: Auto-ejecting blocks for workspace {m_WorkspaceIndex}");
                EjectBlocks();
                _hasTriggered = true;
            }
        }
        
        /// <summary>
        /// Sets whether the game has started (enables door monitoring).
        /// </summary>
        public void SetGameStarted(bool started)
        {
            _gameStarted = started;
            if (!started)
            {
                _hasTriggered = false;
            }
        }
        
        /// <summary>
        /// Gets whether the game has started.
        /// </summary>
        public bool IsGameStarted => _gameStarted;

        /// <summary>
        /// Clears all spawned blocks from the shelf.
        /// </summary>
        public void ClearSpawnedBlocks()
        {
            Debug.Log($"ShelfBlockSpawner: ClearSpawnedBlocks called. NetworkObjects: {_spawnedNetworkObjects.Count}, Blocks: {_spawnedBlockObjects.Count}");
            
            // In multiplayer mode, despawn network objects if we're owner/session owner
            if (_isMultiplayerMode && NetworkManager.Singleton != null)
            {
                bool isSessionOwner = NetworkManager.Singleton.LocalClientId == NetworkManager.Singleton.CurrentSessionOwner;
                if (isSessionOwner)
                {
                    foreach (NetworkObject networkObj in _spawnedNetworkObjects)
                    {
                        if (networkObj != null && networkObj.IsSpawned)
                        {
                            networkObj.Despawn(true);
                        }
                    }
                }
                // Always clear the list in multiplayer (blocks might be despawned elsewhere)
                _spawnedNetworkObjects.Clear();
            }

            // Clean up local references
            foreach (GameObject block in _spawnedBlockObjects)
            {
                if (block != null)
                {
                    // Only destroy if not a networked object (network objects are despawned above or elsewhere)
                    if (!_isMultiplayerMode || block.GetComponent<NetworkObject>() == null)
                    {
                        Destroy(block);
                    }
                }
            }
            _spawnedBlockObjects.Clear();
            _storedBlocks.Clear();
            
            Debug.Log($"ShelfBlockSpawner: ClearSpawnedBlocks complete. Lists cleared.");
        }

        /// <summary>
        /// Manually triggers block ejection (regardless of door angle).
        /// </summary>
        public void ForceEject()
        {
            if (_storedBlocks.Count > 0)
            {
                EjectBlocks();
            }
            else
            {
                Debug.LogWarning("ShelfBlockSpawner: No blocks to eject!");
            }
        }

        /// <summary>
        /// Gets the number of blocks currently stored in the shelf.
        /// </summary>
        public int StoredBlockCount => _storedBlocks.Count;

        /// <summary>
        /// Gets whether the shelf has been triggered (blocks ejected) and is waiting for reset.
        /// </summary>
        public bool HasTriggered => _hasTriggered;

        /// <summary>
        /// Gets whether both doors are closed (below reset angle).
        /// </summary>
        public bool AreDoorsClosed
        {
            get
            {
                if (m_LeftDoor == null || m_RightDoor == null)
                    return false;

                float angleL = Mathf.Abs(m_LeftDoor.angle);
                float angleR = Mathf.Abs(m_RightDoor.angle);
                return angleL < m_ResetAngle && angleR < m_ResetAngle;
            }
        }

        /// <summary>
        /// Checks if all blocks from the level have been returned to the shelf.
        /// </summary>
        /// <param name="expectedCount">The number of blocks expected (from level configuration)</param>
        /// <returns>True if all blocks are in the shelf</returns>
        public bool AreAllBlocksReturned(int expectedCount)
        {
            // Clean up any null references from destroyed blocks
            _storedBlocks.RemoveAll(rb => rb == null);
            return _storedBlocks.Count >= expectedCount;
        }

        /// <summary>
        /// Gets the list of blocks currently stored in the shelf.
        /// Useful for validation and debugging.
        /// </summary>
        /// <returns>Read-only list of Rigidbodies in the shelf</returns>
        public IReadOnlyList<Rigidbody> GetBlocksInShelf()
        {
            // Clean up any null references
            _storedBlocks.RemoveAll(rb => rb == null);
            return _storedBlocks.AsReadOnly();
        }

        /// <summary>
        /// Resets the trigger state, allowing the shelf to fire again.
        /// </summary>
        public void ResetTriggerState()
        {
            _hasTriggered = false;
            OnShelfReset?.Invoke();
        }

        #endregion

        #region Door Monitoring

        /// <summary>
        /// Monitors door angles and triggers ejection when threshold is reached.
        /// In multiplayer, only the session owner can trigger ejection.
        /// </summary>
        private void MonitorDoors()
        {
            // Don't monitor doors until the game has started
            if (!_gameStarted) return;
            
            if (m_LeftDoor == null || m_RightDoor == null)
            {
                return;
            }

            float angleL = Mathf.Abs(m_LeftDoor.angle);
            float angleR = Mathf.Abs(m_RightDoor.angle);

            // Trigger ejection when either door opens past trigger angle
            if ((angleL >= m_TriggerAngle || angleR >= m_TriggerAngle) && !_hasTriggered)
            {
                Debug.Log($"ShelfBlockSpawner: Door trigger! Left={angleL:F1}°, Right={angleR:F1}°, Threshold={m_TriggerAngle}°");
                
                // In multiplayer, only session owner can eject network blocks
                // Non-owners just mark as triggered so they don't spam the log
                if (_isMultiplayerMode)
                {
                    if (IsSessionOwner)
                    {
                        Debug.Log($"ShelfBlockSpawner: Session owner ejecting blocks for workspace {m_WorkspaceIndex}");
                        EjectBlocks();
                    }
                    else
                    {
                        Debug.Log($"ShelfBlockSpawner: Non-owner door opened - blocks should be ejected by session owner");
                    }
                }
                else
                {
                    // Single player - eject normally
                    EjectBlocks();
                }
                
                _hasTriggered = true;
            }

            // Reset when both doors close below reset angle
            if (angleL < m_ResetAngle && angleR < m_ResetAngle && _hasTriggered)
            {
                _hasTriggered = false;
                Debug.Log("ShelfBlockSpawner: System reset - ready to fire again");
                OnShelfReset?.Invoke();
            }
        }

        #endregion

        #region Block Ejection

        /// <summary>
        /// Ejects all stored blocks with randomized physics.
        /// In multiplayer, uses NetworkBlock.EjectBlock() to sync across clients.
        /// </summary>
        private void EjectBlocks()
        {
            // In multiplayer, also eject any spawned network objects that we own
            // (this handles the case where _storedBlocks is empty on non-owner clients)
            if (_isMultiplayerMode)
            {
                EjectNetworkBlocks();
            }
            else
            {
                EjectLocalBlocks();
            }

            // Kick doors open further
            KickDoor(m_LeftDoor);
            KickDoor(m_RightDoor);
        }

        /// <summary>
        /// Ejects blocks in single-player mode using local rigidbody control.
        /// </summary>
        private void EjectLocalBlocks()
        {
            Debug.Log($"ShelfBlockSpawner: Ejecting {_storedBlocks.Count} local blocks!");

            int ejectedCount = 0;

            for (int i = _storedBlocks.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = _storedBlocks[i];
                if (rb != null)
                {
                    EjectRigidbody(rb);
                    ejectedCount++;
                }
            }

            // Clear stored blocks list
            _storedBlocks.Clear();

            OnBlocksEjected?.Invoke(ejectedCount);
        }

        /// <summary>
        /// Ejects blocks in multiplayer mode using NetworkBlock.EjectBlock().
        /// This syncs the ejection state across all clients.
        /// </summary>
        private void EjectNetworkBlocks()
        {
            Debug.Log($"ShelfBlockSpawner: Ejecting network blocks! NetworkObjects: {_spawnedNetworkObjects.Count}, StoredBlocks: {_storedBlocks.Count}");

            int ejectedCount = 0;

            // Eject all spawned NetworkObjects (works even if _storedBlocks is empty)
            foreach (var networkObject in _spawnedNetworkObjects)
            {
                if (networkObject == null) continue;

                NetworkBlock networkBlock = networkObject.GetComponent<NetworkBlock>();
                if (networkBlock == null || networkBlock.IsEjected) continue;

                // Calculate ejection force and torque
                Vector3 baseDir = m_EjectionDirection != null 
                    ? m_EjectionDirection.forward 
                    : transform.forward;

                float randomX = Random.Range(-m_SpreadAmount, m_SpreadAmount);
                float randomY = Random.Range(-m_SpreadAmount, m_SpreadAmount) + 0.15f; // Upward bias
                float randomZ = Random.Range(-m_SpreadAmount, m_SpreadAmount);
                Vector3 randomDir = (baseDir + new Vector3(randomX, randomY, randomZ)).normalized;
                float randomPower = m_EjectionForce * Random.Range(0.9f, 1.1f);

                Vector3 force = randomDir * randomPower;
                Vector3 torque = Random.insideUnitSphere * m_TumbleForce;

                // Use NetworkBlock's networked ejection
                networkBlock.EjectBlock(force, torque);

                // Also set collision detection mode
                Rigidbody rb = networkObject.GetComponent<Rigidbody>();
                if (rb != null && m_UseContinuousCollision)
                {
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }

                ejectedCount++;
            }

            // Also eject any local stored blocks (fallback)
            foreach (var rb in _storedBlocks)
            {
                if (rb == null) continue;
                
                // Skip if already handled as network object
                NetworkBlock nb = rb.GetComponent<NetworkBlock>();
                if (nb != null && nb.IsEjected) continue;

                EjectRigidbody(rb);
                ejectedCount++;
            }

            _storedBlocks.Clear();

            Debug.Log($"ShelfBlockSpawner: Ejected {ejectedCount} blocks");
            OnBlocksEjected?.Invoke(ejectedCount);
        }

        /// <summary>
        /// Ejects a single rigidbody with randomized force.
        /// </summary>
        private void EjectRigidbody(Rigidbody rb)
        {
            // Get base ejection direction
            Vector3 baseDir = m_EjectionDirection != null 
                ? m_EjectionDirection.forward 
                : transform.forward;

            // Add random spread
            float randomX = Random.Range(-m_SpreadAmount, m_SpreadAmount);
            float randomY = Random.Range(-m_SpreadAmount, m_SpreadAmount) + 0.15f; // Slight upward bias
            float randomZ = Random.Range(-m_SpreadAmount, m_SpreadAmount);
            Vector3 randomDir = (baseDir + new Vector3(randomX, randomY, randomZ)).normalized;

            // Randomize force (90%-110% of base for consistency)
            float randomPower = m_EjectionForce * Random.Range(0.9f, 1.1f);

            // Enable physics and apply forces
            rb.WakeUp();
            rb.isKinematic = false;
            
            // Use continuous collision detection to prevent blocks from
            // tunneling through the floor at high speeds
            if (m_UseContinuousCollision)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
            
            rb.linearVelocity = randomDir * randomPower;

            // Add tumble rotation
            rb.AddTorque(Random.insideUnitSphere * m_TumbleForce, ForceMode.Impulse);
        }

        /// <summary>
        /// Applies an impulse to a door to kick it open further.
        /// Uses the HingeJoint's axis to be rotation-independent.
        /// </summary>
        /// <param name="door">The door HingeJoint to kick</param>
        private void KickDoor(HingeJoint door)
        {
            if (door == null) return;

            Rigidbody rb = door.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Determine kick direction based on current door angle
                float direction = Mathf.Sign(door.angle);
                if (direction == 0) direction = 1;
                
                // Use the hinge joint's axis in world space for rotation-independent behavior
                // The axis is defined in local space of the door, so we transform it to world space
                Vector3 hingeAxisWorld = door.transform.TransformDirection(door.axis);
                
                // Apply torque around the hinge axis in world space
                rb.AddTorque(hingeAxisWorld * m_DoorKickForce * direction, ForceMode.Impulse);
            }
        }

        #endregion

        #region Multiplayer Support

        /// <summary>
        /// Checks if we're in multiplayer mode.
        /// </summary>
        private void CheckMultiplayerMode()
        {
            if (!m_UseNetworkSpawning)
            {
                _isMultiplayerMode = false;
                return;
            }

            _isMultiplayerMode = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        }

        /// <summary>
        /// Gets the owner client ID for this workspace in multiplayer.
        /// </summary>
        private ulong GetWorkspaceOwnerClientId()
        {
            if (!_isMultiplayerMode) return 0;

            var workspaceManager = PlayerWorkspaceManager.Instance;
            if (workspaceManager == null) return NetworkManager.Singleton.LocalClientId;

            // Find the client assigned to this workspace
            foreach (var workspace in workspaceManager.Workspaces)
            {
                if (workspace != null && workspace.WorkspaceIndex == m_WorkspaceIndex)
                {
                    return workspace.AssignedPlayerId;
                }
            }

            return NetworkManager.Singleton.LocalClientId;
        }

        #endregion

        #region Block Spawning

        /// <summary>
        /// Returns true if this client is the session owner in Distributed Authority mode.
        /// </summary>
        private bool IsSessionOwner => NetworkManager.Singleton != null && 
            NetworkManager.Singleton.LocalClientId == NetworkManager.Singleton.CurrentSessionOwner;

        /// <summary>
        /// Spawns a single block at the specified position.
        /// </summary>
        /// <param name="entry">The spawn entry containing block type and color</param>
        /// <param name="spawnPosition">The position to spawn the block at</param>
        /// <returns>The spawned block GameObject, or null if failed</returns>
        private GameObject SpawnSingleBlock(BlockSpawnEntry entry, Vector3 spawnPosition)
        {
            // Re-check multiplayer mode at spawn time (Awake happens before network connects)
            CheckMultiplayerMode();
            
            Debug.Log($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: SpawnSingleBlock - Type={entry.BlockType}, Color={entry.BlockColor}, Multiplayer={_isMultiplayerMode}, IsSessionOwner={IsSessionOwner} ###");
            
            if (_isMultiplayerMode)
            {
                // In Distributed Authority mode, only the session owner spawns networked blocks.
                // These blocks are then replicated to all clients via NetworkObject.
                if (IsSessionOwner)
                {
                    Debug.Log($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: Session owner SPAWNING networked block ###");
                    return SpawnNetworkedBlock(entry, spawnPosition);
                }
                else
                {
                    // Non-session-owner clients don't spawn blocks - they receive them via network
                    Debug.Log($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: Non-owner SKIPPING spawn (will receive via network) ###");
                    return null;
                }
            }

            // Single-player mode: use local spawning
            Debug.Log($"### ShelfBlockSpawner[W{m_WorkspaceIndex}]: Single-player mode - spawning local block ###");
            return SpawnLocalBlock(entry, spawnPosition);
        }

        /// <summary>
        /// Spawns a block locally (single-player mode).
        /// </summary>
        private GameObject SpawnLocalBlock(BlockSpawnEntry entry, Vector3 spawnPosition)
        {
            GameObject prefab = GetPrefabForBlockType(entry.BlockType);
            if (prefab == null)
            {
                Debug.LogWarning($"ShelfBlockSpawner: No prefab assigned for block type {entry.BlockType}. Skipping.");
                return null;
            }

            // Spawn with identity rotation (blocks spawn upright)
            GameObject block = Instantiate(prefab, spawnPosition, Quaternion.identity);

            if (block != null)
            {
                block.name = $"Block_{entry.BlockType}_{entry.BlockColor}_Shelf";

                // Remove network components for local/single-player mode
                // This prevents GlobalObjectIdHash collisions
                var networkBlock = block.GetComponent<Network.NetworkBlock>();
                if (networkBlock != null) Destroy(networkBlock);
                
                var networkRigidbody = block.GetComponent<Unity.Netcode.Components.NetworkRigidbody>();
                if (networkRigidbody != null) Destroy(networkRigidbody);
                
                var networkObject = block.GetComponent<Unity.Netcode.NetworkObject>();
                if (networkObject != null) Destroy(networkObject);

                // Apply color material
                ApplyBlockColor(block, entry.BlockColor);

                // Make block kinematic initially (no physics until ejected)
                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    
                    // Manually register the block in storedBlocks since kinematic blocks
                    // won't trigger OnTriggerEnter
                    if (!_storedBlocks.Contains(rb))
                    {
                        _storedBlocks.Add(rb);
                    }
                }

                Debug.Log($"ShelfBlockSpawner: Spawned {entry.BlockType} ({entry.BlockColor}) at {spawnPosition}");
            }

            return block;
        }

        /// <summary>
        /// Spawns a networked block (multiplayer mode - session owner only in DA mode).
        /// </summary>
        private GameObject SpawnNetworkedBlock(BlockSpawnEntry entry, Vector3 spawnPosition)
        {
            // In DA mode, session owner spawns blocks. In server mode, server spawns.
            bool canSpawn = IsSessionOwner || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
            if (!canSpawn)
            {
                Debug.LogError("ShelfBlockSpawner: SpawnNetworkedBlock should only be called by session owner or server!");
                return null;
            }

            GameObject prefab = GetPrefabForBlockType(entry.BlockType);
            if (prefab == null)
            {
                Debug.LogWarning($"ShelfBlockSpawner: No prefab assigned for block type {entry.BlockType}. Skipping.");
                return null;
            }

            // Check if prefab has NetworkObject
            if (prefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogWarning($"ShelfBlockSpawner: Prefab {prefab.name} is missing NetworkObject component! Blocks won't sync.");
                // Still spawn locally so game works, but warn about sync issue
                return SpawnLocalBlock(entry, spawnPosition);
            }

            // Spawn with identity rotation
            GameObject block = Instantiate(prefab, spawnPosition, Quaternion.identity);

            if (block != null)
            {
                block.name = $"Block_{entry.BlockType}_{entry.BlockColor}_W{m_WorkspaceIndex}_Net";

                // Apply color material before network spawn
                ApplyBlockColor(block, entry.BlockColor);

                // Get NetworkObject and spawn on network
                NetworkObject networkObject = block.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    // In DA mode, spawning client becomes the owner automatically
                    // We spawn with our local client ID as owner
                    networkObject.Spawn();

                    // Track the network object
                    _spawnedNetworkObjects.Add(networkObject);

                    // Set workspace index and color on NetworkBlock if present
                    NetworkBlock networkBlock = block.GetComponent<NetworkBlock>();
                    if (networkBlock != null)
                    {
                        // In DA mode with WritePermission.Owner, owner can set these
                        networkBlock.SetWorkspaceIndex(m_WorkspaceIndex);
                        // Set the color via NetworkVariable so it syncs to all clients
                        networkBlock.SetBlockColor(entry.BlockColor);
                    }

                    Debug.Log($"ShelfBlockSpawner: Spawned networked {entry.BlockType} ({entry.BlockColor}) at {spawnPosition} for workspace {m_WorkspaceIndex}, NetworkId: {networkObject.NetworkObjectId}");
                }

                // Make block kinematic initially (in shelf, not affected by physics)
                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    
                    if (!_storedBlocks.Contains(rb))
                    {
                        _storedBlocks.Add(rb);
                    }
                }
            }

            return block;
        }

        /// <summary>
        /// Gets the prefab for the specified block type.
        /// </summary>
        /// <param name="blockType">The block type</param>
        /// <returns>The prefab GameObject, or null if not assigned</returns>
        private GameObject GetPrefabForBlockType(BlockType blockType)
        {
            switch (blockType)
            {
                case BlockType.Cube:
                    return m_CubeBlockPrefab;
                case BlockType.Cylinder:
                    return m_CylinderBlockPrefab;
                case BlockType.Triangle:
                    return m_TriangleBlockPrefab;
                case BlockType.Rectangle:
                    return m_RectangleBlockPrefab;
                case BlockType.Arch:
                    return m_ArchBlockPrefab;
                case BlockType.BigTriangle:
                    return m_BigTriangleBlockPrefab;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Applies a color material to a block.
        /// </summary>
        /// <param name="block">The block GameObject</param>
        /// <param name="blockColor">The color to apply</param>
        private void ApplyBlockColor(GameObject block, BlockColor blockColor)
        {
            if (block == null) return;

            // Load the colored material
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
                Debug.LogWarning($"ShelfBlockSpawner: Could not load material {materialName}. Using default material.");
                return;
            }

            // Find all MeshRenderers in the block (including children)
            MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                // Fallback: try the old method for backwards compatibility
                Transform visuals = block.transform.Find("Visuals");
                if (visuals != null)
                {
                    MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.material = coloredMaterial;
                        return;
                    }
                }
                Debug.LogWarning($"ShelfBlockSpawner: No MeshRenderers found in block {block.name}");
                return;
            }

            // Apply material to all renderers
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.material = coloredMaterial;
                }
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Shuffles a list using Fisher-Yates shuffle algorithm.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list</typeparam>
        /// <param name="list">The list to shuffle</param>
        private void ShuffleList<T>(List<T> list)
        {
            System.Random random = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Validates that prefabs are assigned and logs warnings for missing ones.
        /// </summary>
        private void ValidatePrefabs()
        {
            if (m_CubeBlockPrefab == null) Debug.LogWarning("ShelfBlockSpawner: Cube prefab is not assigned!");
            if (m_CylinderBlockPrefab == null) Debug.LogWarning("ShelfBlockSpawner: Cylinder prefab is not assigned!");
            if (m_TriangleBlockPrefab == null) Debug.LogWarning("ShelfBlockSpawner: Triangle prefab is not assigned!");
            if (m_RectangleBlockPrefab == null) Debug.LogWarning("ShelfBlockSpawner: Rectangle prefab is not assigned!");
            if (m_ArchBlockPrefab == null) Debug.LogWarning("ShelfBlockSpawner: Arch prefab is not assigned!");
            if (m_BigTriangleBlockPrefab == null) Debug.LogWarning("ShelfBlockSpawner: BigTriangle prefab is not assigned!");
        }

        #endregion

        #region Editor Visualization

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw spawn anchor position
            Vector3 spawnPos = m_SpawnAnchor != null ? m_SpawnAnchor.position : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPos, 0.05f);

            // Draw spawn direction
            Vector3 normalizedDir = m_SpawnDirection.normalized;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(spawnPos, spawnPos + normalizedDir * 0.5f);

            // Draw ejection direction
            Vector3 ejectDir = m_EjectionDirection != null 
                ? m_EjectionDirection.forward 
                : transform.forward;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(spawnPos, spawnPos + ejectDir * 0.5f);

            // Draw spread cone
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Vector3 spreadLeft = Quaternion.Euler(0, -m_SpreadAmount * 45f, 0) * ejectDir;
            Vector3 spreadRight = Quaternion.Euler(0, m_SpreadAmount * 45f, 0) * ejectDir;
            Gizmos.DrawLine(spawnPos, spawnPos + spreadLeft * 0.4f);
            Gizmos.DrawLine(spawnPos, spawnPos + spreadRight * 0.4f);
        }
#endif

        #endregion
    }
}
