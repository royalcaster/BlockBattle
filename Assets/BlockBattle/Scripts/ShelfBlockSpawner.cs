using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

        [SerializeField, Tooltip("Number of blocks to spawn in a single row before starting a new row")]
        private int m_BlocksPerRow = 7;

        [SerializeField, Tooltip("Vertical offset between rows in meters")]
        private float m_RowVerticalOffset = -0.8f;

        [SerializeField, Tooltip("Direction to arrange blocks when spawning (normalized)")]
        private Vector3 m_SpawnDirection = Vector3.right;

        [SerializeField, Tooltip("Whether to randomize the order of spawned blocks")]
        private bool m_RandomizeSpawnOrder = true;

        #endregion

        #region Door Settings

        [Header("Door Settings")]
        [SerializeField, Tooltip("Left door HingeJoint reference")]
        private HingeJoint m_LeftDoor;

        [SerializeField, Tooltip("Right door HingeJoint reference")]
        private HingeJoint m_RightDoor;

        [SerializeField, Range(0f, 120f), Tooltip("Door angle at which blocks are ejected")]
        private float m_TriggerAngle = 70f;

        [SerializeField, Range(0f, 10f), Tooltip("Door angle below which the system resets and game can finish (must be very close to 0)")]
        private float m_ResetAngle = 5f;

        [SerializeField, Tooltip("Impulse force applied to doors when blocks are ejected")]
        private float m_DoorKickForce = 30f;

        #endregion

        #region Ejection Settings

        [Header("Ejection Settings")]
        [SerializeField, Tooltip("Transform whose forward direction defines the base ejection direction. If null, uses this transform's forward.")]
        private Transform m_EjectionDirection;

        [SerializeField, Tooltip("Base force applied to eject blocks (lower values = less chance of tunneling through floor)")]
        private float m_EjectionForce = 8f;

        [SerializeField, Range(0f, 1f), Tooltip("How much blocks spread when ejected (0 = straight line, 1 = wide spread)")]
        private float m_SpreadAmount = 0.3f;

        [SerializeField, Tooltip("Rotational force applied to blocks for realistic tumbling")]
        private float m_TumbleForce = 5f;

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

        /// <summary>
        /// Event fired when doors are closed after all blocks have been returned.
        /// Used for end-of-game detection on the last level.
        /// </summary>
        public event System.Action OnDoorsClosedWithBlocksReturned;

        #endregion

        #region Private State

        private bool _hasTriggered = false;
        private List<Rigidbody> _storedBlocks = new List<Rigidbody>();
        private List<GameObject> _spawnedBlockObjects = new List<GameObject>();
        private bool _waitingForDoorsToClose = false;
        private int _expectedBlockCountForClose = 0;
        private bool _isKicking = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ValidatePrefabs();
        }

        private void Start()
        {
            InitializeDoors();
        }

        /// <summary>
        /// Ensures doors start fully closed and stable.
        /// </summary>
        private void InitializeDoors()
        {
            // Ignore collision between doors and the shelf itself to prevent physics "pops"
            Collider shelfCollider = GetComponent<Collider>();

            if (m_LeftDoor != null)
            {
                m_LeftDoor.transform.localRotation = Quaternion.identity;
                Rigidbody rb = m_LeftDoor.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.Sleep();
                }

                if (shelfCollider != null)
                {
                    Collider doorCol = m_LeftDoor.GetComponent<Collider>();
                    if (doorCol != null) Physics.IgnoreCollision(doorCol, shelfCollider);
                }
            }

            if (m_RightDoor != null)
            {
                m_RightDoor.transform.localRotation = Quaternion.identity;
                Rigidbody rb = m_RightDoor.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.Sleep();
                }

                if (shelfCollider != null)
                {
                    Collider doorCol = m_RightDoor.GetComponent<Collider>();
                    if (doorCol != null) Physics.IgnoreCollision(doorCol, shelfCollider);
                }
            }
            
            Debug.Log("ShelfBlockSpawner: Doors initialized to closed state and shelf collisions ignored.");
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
            if (m_SpawnConfiguration == null)
            {
                Debug.LogWarning("ShelfBlockSpawner: No spawn configuration assigned!");
                return;
            }

            if (m_SpawnConfiguration.SpawnEntries == null || m_SpawnConfiguration.SpawnEntries.Count == 0)
            {
                Debug.LogWarning("ShelfBlockSpawner: Spawn configuration has no entries!");
                return;
            }

            Debug.Log($"ShelfBlockSpawner: Spawning {m_SpawnConfiguration.SpawnEntries.Count} blocks from configuration '{m_SpawnConfiguration.ConfigurationName}'");

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

            // Spawn each block in rows
            int blockIndex = 0;
            foreach (BlockSpawnEntry entry in entries)
            {
                int col = blockIndex % m_BlocksPerRow;
                int row = blockIndex / m_BlocksPerRow;

                // Calculate position with horizontal spacing and vertical row offset
                Vector3 spawnPosition = basePosition + 
                                      normalizedDirection * (col * m_SpawnSpacing) + 
                                      Vector3.up * (row * m_RowVerticalOffset);

                GameObject block = SpawnSingleBlock(entry, spawnPosition);

                if (block != null)
                {
                    _spawnedBlockObjects.Add(block);
                    blockIndex++;
                }
            }

            Debug.Log($"ShelfBlockSpawner: Successfully spawned {_spawnedBlockObjects.Count} blocks inside shelf. StoredBlocks count: {_storedBlocks.Count}");
            
            // Reset trigger state so doors can trigger ejection
            _hasTriggered = false;
            
            OnBlocksSpawned?.Invoke(_spawnedBlockObjects.Count);
        }

        /// <summary>
        /// Clears all spawned blocks from the shelf.
        /// </summary>
        public void ClearSpawnedBlocks()
        {
            foreach (GameObject block in _spawnedBlockObjects)
            {
                if (block != null)
                {
                    Destroy(block);
                }
            }
            _spawnedBlockObjects.Clear();
            _storedBlocks.Clear();
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
        /// Also handles detecting when doors close with blocks returned.
        /// </summary>
        private void MonitorDoors()
        {
            if (m_LeftDoor == null || m_RightDoor == null)
            {
                return;
            }

            float angleL = Mathf.Abs(m_LeftDoor.angle);
            float angleR = Mathf.Abs(m_RightDoor.angle);

            // 1. Stabilize doors INDIVIDUALLY whenever they aren't being grabbed or kicked.
            // This ensures they "stay where they are" instead of drifting.
            if (!_isKicking)
            {
                StabilizeDoor(m_LeftDoor);
                StabilizeDoor(m_RightDoor);
            }

            // 2. Trigger ejection when either door opens past trigger angle
            if ((angleL >= m_TriggerAngle || angleR >= m_TriggerAngle) && !_hasTriggered)
            {
                Debug.Log($"ShelfBlockSpawner: Door trigger! Left={angleL:F1}°, Right={angleR:F1}°, Threshold={m_TriggerAngle}°, StoredBlocks={_storedBlocks.Count}");
                EjectBlocks();
                _hasTriggered = true;
            }

            // 3. Check for game completion (both doors closed below reset angle)
            if (angleL < m_ResetAngle && angleR < m_ResetAngle)
            {
                // Check if we're waiting for doors to close after blocks returned (last level)
                if (_waitingForDoorsToClose)
                {
                    bool allReturned = AreAllBlocksReturned(_expectedBlockCountForClose);
                    if (allReturned)
                    {
                        _waitingForDoorsToClose = false;
                        Debug.Log($"ShelfBlockSpawner: Doors successfully closed (L:{angleL:F1}°, R:{angleR:F1}°) with all blocks returned! FINISHING GAME.");
                        OnDoorsClosedWithBlocksReturned?.Invoke();
                    }
                    else
                    {
                        // Log why it's not firing (only every few seconds to avoid spam)
                        if (Time.frameCount % 60 == 0)
                        {
                            Debug.Log($"ShelfBlockSpawner: Waiting for close, but not all blocks returned. Stored: {StoredBlockCount}/{_expectedBlockCountForClose}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Zeroes out velocity on a door to help it stay closed or settle.
        /// </summary>
        private void StabilizeDoor(HingeJoint door)
        {
            if (door == null) return;
            Rigidbody rb = door.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                // Check if being grabbed
                UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = door.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                bool isGrabbed = grab != null && grab.isSelected;

                if (isGrabbed)
                {
                    // When grabbed, ensure drag is low so it feels natural
                    rb.angularDamping = 0.05f;
                    return; 
                }

                // When NOT grabbed, we want it to "stay where it is"
                // 1. Immediately kill velocity
                rb.angularVelocity = Vector3.zero;
                rb.linearVelocity = Vector3.zero;

                // 2. Set high damping to fight any residual physics force (drifting)
                rb.angularDamping = 10f;

                // 3. Force it to sleep if velocity is low
                if (rb.angularVelocity.magnitude < 0.05f)
                {
                    rb.Sleep();
                }
            }
        }

        /// <summary>
        /// Starts waiting for doors to close after all blocks are returned.
        /// Call this on the last level after blocks are returned.
        /// </summary>
        /// <param name="expectedBlockCount">Number of blocks that should be in the shelf</param>
        public void StartWaitingForDoorsClose(int expectedBlockCount)
        {
            _waitingForDoorsToClose = true;
            _expectedBlockCountForClose = expectedBlockCount;
            Debug.Log($"ShelfBlockSpawner: Waiting for player to close shelf doors (blocks: {expectedBlockCount})");
        }

        /// <summary>
        /// Gets whether we are currently waiting for doors to close.
        /// </summary>
        public bool IsWaitingForDoorsClose => _waitingForDoorsToClose;

        /// <summary>
        /// Forces the doors to be perfectly closed and stable.
        /// Call this when the game finishes to ensure doors don't stay slightly ajar.
        /// </summary>
        public void ForceCloseDoors()
        {
            if (m_LeftDoor != null)
            {
                m_LeftDoor.transform.localRotation = Quaternion.identity;
                Rigidbody rb = m_LeftDoor.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                }
            }

            if (m_RightDoor != null)
            {
                m_RightDoor.transform.localRotation = Quaternion.identity;
                Rigidbody rb = m_RightDoor.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                }
            }
            
            Debug.Log("ShelfBlockSpawner: Doors forced to perfectly closed state.");
        }

        #endregion

        #region Block Ejection

        /// <summary>
        /// Ejects all stored blocks with randomized physics.
        /// </summary>
        private void EjectBlocks()
        {
            Debug.Log($"ShelfBlockSpawner: Ejecting {_storedBlocks.Count} blocks!");

            int ejectedCount = 0;

            for (int i = _storedBlocks.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = _storedBlocks[i];
                if (rb != null)
                {
                    // Get base ejection direction
                    Vector3 baseDir = m_EjectionDirection != null 
                        ? m_EjectionDirection.forward 
                        : transform.forward;

                    // Add random spread
                    float randomX = Random.Range(-m_SpreadAmount, m_SpreadAmount);
                    float randomY = Random.Range(-m_SpreadAmount, m_SpreadAmount) + 0.1f; // Slight upward bias
                    float randomZ = Random.Range(-m_SpreadAmount, m_SpreadAmount);
                    Vector3 randomDir = (baseDir + new Vector3(randomX, randomY, randomZ)).normalized;

                    // Randomize force (80%-120% of base)
                    float randomPower = m_EjectionForce * Random.Range(0.8f, 1.2f);

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

                    ejectedCount++;
                }
            }

            // Clear stored blocks list - blocks must physically re-enter the shelf
            // to be counted again (via OnTriggerEnter)
            _storedBlocks.Clear();

            // Kick doors open further
            _isKicking = true;
            Debug.Log($"ShelfBlockSpawner: Triggering kick for both doors. Left: {(m_LeftDoor != null ? m_LeftDoor.name : "NULL")}, Right: {(m_RightDoor != null ? m_RightDoor.name : "NULL")}");
            
            // Apply a strong kick to both doors
            KickDoor(m_LeftDoor);
            KickDoor(m_RightDoor);

            // Give a short window for the kick to actually move the doors
            // before the stabilization logic kicks back in
            StartCoroutine(ResetKickingState());

            OnBlocksEjected?.Invoke(ejectedCount);
        }

        private IEnumerator ResetKickingState()
        {
            // Wait 1 second for the doors to fly open
            yield return new WaitForSeconds(1.0f);
            
            // Disable motors after the kick is done
            if (m_LeftDoor != null) m_LeftDoor.useMotor = false;
            if (m_RightDoor != null) m_RightDoor.useMotor = false;
            
            _isKicking = false;
        }

        /// <summary>
        /// Uses the HingeJoint motor to "kick" the door open to its limit.
        /// </summary>
        /// <param name="door">The door HingeJoint to kick</param>
        private void KickDoor(HingeJoint door)
        {
            if (door == null) return;

            Rigidbody rb = door.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 1. Prepare physics
                rb.isKinematic = false;
                rb.WakeUp();
                rb.angularDamping = 0.05f;

                // 2. Use the Hinge motor to force it open to 120 degrees
                // Swapping directions: Left = 1, Right = -1
                JointMotor motor = door.motor;
                float direction = (door == m_LeftDoor) ? 1f : -1f;
                
                motor.targetVelocity = 300f * direction; 
                motor.force = 500f; 
                door.motor = motor;
                door.useMotor = true;

                // 3. If the player is holding the door, we force a release so it can fly open
                UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = door.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null && grab.isSelected)
                {
                    grab.interactionManager.SelectExit(grab.interactorsSelecting[0], grab);
                }
                
                Debug.Log($"ShelfBlockSpawner: Motor-Kicking door {door.gameObject.name} to open position (Velocity: {motor.targetVelocity}).");
            }
        }

        #endregion

        #region Block Spawning

        /// <summary>
        /// Spawns a single block at the specified position.
        /// </summary>
        /// <param name="entry">The spawn entry containing block type and color</param>
        /// <param name="spawnPosition">The position to spawn the block at</param>
        /// <returns>The spawned block GameObject, or null if failed</returns>
        private GameObject SpawnSingleBlock(BlockSpawnEntry entry, Vector3 spawnPosition)
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

                // Add movement timeout component if not present
                if (block.GetComponent<BlockMovementTimeout>() == null)
                {
                    block.AddComponent<BlockMovementTimeout>();
                }

                // Ignore collision with doors while inside the shelf to prevent doors from being pushed open
                IgnoreDoorCollisions(block);

                Debug.Log($"ShelfBlockSpawner: Spawned {entry.BlockType} ({entry.BlockColor}) at {spawnPosition}");
            }

            return block;
        }

        /// <summary>
        /// Ignores collision between a block and the shelf doors.
        /// </summary>
        private void IgnoreDoorCollisions(GameObject block)
        {
            Collider blockCollider = block.GetComponent<Collider>();
            if (blockCollider == null) return;

            if (m_LeftDoor != null)
            {
                Collider doorCol = m_LeftDoor.GetComponent<Collider>();
                if (doorCol != null) Physics.IgnoreCollision(blockCollider, doorCol);
            }

            if (m_RightDoor != null)
            {
                Collider doorCol = m_RightDoor.GetComponent<Collider>();
                if (doorCol != null) Physics.IgnoreCollision(blockCollider, doorCol);
            }
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
            
            // Visualize the grid layout
            for (int r = 0; r < 2; r++) // Show 2 rows
            {
                Vector3 rowStart = spawnPos + Vector3.up * (r * m_RowVerticalOffset);
                Gizmos.DrawLine(rowStart, rowStart + normalizedDir * ((m_BlocksPerRow - 1) * m_SpawnSpacing));
            }

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
