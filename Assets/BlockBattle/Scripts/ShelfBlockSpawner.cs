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

        [SerializeField, Range(0f, 120f), Tooltip("Door angle below which the system resets (must be less than trigger angle)")]
        private float m_ResetAngle = 60f;

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

        [Header("Auto-Open Door Settings")]
        [SerializeField, Tooltip("Angle threshold after which doors automatically swing fully open")]
        [Range(10f, 60f)]
        private float m_AutoOpenThreshold = 30f;

        [SerializeField, Tooltip("Motor velocity for auto-opening doors (degrees per second)")]
        private float m_AutoOpenMotorVelocity = 150f;

        [SerializeField, Tooltip("Motor force for auto-opening doors")]
        private float m_AutoOpenMotorForce = 50f;

        #region Private State

        private bool _hasTriggered = false;
        private List<Rigidbody> _storedBlocks = new List<Rigidbody>();
        private List<GameObject> _spawnedBlockObjects = new List<GameObject>();
        private bool _leftDoorAutoOpening = false;
        private bool _rightDoorAutoOpening = false;
        private bool _waitingForDoorsToClose = false;
        private int _expectedBlockCountForClose = 0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ValidatePrefabs();
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
        /// Also handles auto-opening doors and detecting when doors close with blocks returned.
        /// </summary>
        private void MonitorDoors()
        {
            if (m_LeftDoor == null || m_RightDoor == null)
            {
                return;
            }

            float angleL = Mathf.Abs(m_LeftDoor.angle);
            float angleR = Mathf.Abs(m_RightDoor.angle);

            // Auto-open left door when past threshold
            if (angleL >= m_AutoOpenThreshold && !_leftDoorAutoOpening)
            {
                EnableDoorMotor(m_LeftDoor, true);
                _leftDoorAutoOpening = true;
            }
            
            // Auto-open right door when past threshold
            if (angleR >= m_AutoOpenThreshold && !_rightDoorAutoOpening)
            {
                EnableDoorMotor(m_RightDoor, true);
                _rightDoorAutoOpening = true;
            }

            // Trigger ejection when either door opens past trigger angle
            if ((angleL >= m_TriggerAngle || angleR >= m_TriggerAngle) && !_hasTriggered)
            {
                Debug.Log($"ShelfBlockSpawner: Door trigger! Left={angleL:F1}°, Right={angleR:F1}°, Threshold={m_TriggerAngle}°, StoredBlocks={_storedBlocks.Count}");
                EjectBlocks();
                _hasTriggered = true;
            }

            // Reset when both doors close below reset angle
            if (angleL < m_ResetAngle && angleR < m_ResetAngle)
            {
                // Disable door motors when closed
                if (_leftDoorAutoOpening)
                {
                    EnableDoorMotor(m_LeftDoor, false);
                    _leftDoorAutoOpening = false;
                }
                if (_rightDoorAutoOpening)
                {
                    EnableDoorMotor(m_RightDoor, false);
                    _rightDoorAutoOpening = false;
                }

                if (_hasTriggered)
                {
                    _hasTriggered = false;
                    Debug.Log("ShelfBlockSpawner: System reset - ready to fire again");
                    OnShelfReset?.Invoke();
                }

                // Check if we're waiting for doors to close after blocks returned (last level)
                if (_waitingForDoorsToClose)
                {
                    // Verify all blocks are still in the shelf
                    if (AreAllBlocksReturned(_expectedBlockCountForClose))
                    {
                        _waitingForDoorsToClose = false;
                        Debug.Log("ShelfBlockSpawner: Doors closed with all blocks returned!");
                        OnDoorsClosedWithBlocksReturned?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Enables or disables the motor on a door hinge to auto-open/close.
        /// </summary>
        /// <param name="door">The door HingeJoint</param>
        /// <param name="enable">Whether to enable the motor</param>
        private void EnableDoorMotor(HingeJoint door, bool enable)
        {
            if (door == null) return;

            JointMotor motor = door.motor;
            
            if (enable)
            {
                // Determine direction based on current angle
                float direction = Mathf.Sign(door.angle);
                if (direction == 0) direction = 1;
                
                motor.targetVelocity = m_AutoOpenMotorVelocity * direction;
                motor.force = m_AutoOpenMotorForce;
                door.motor = motor;
                door.useMotor = true;
            }
            else
            {
                door.useMotor = false;
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
            KickDoor(m_LeftDoor);
            KickDoor(m_RightDoor);

            OnBlocksEjected?.Invoke(ejectedCount);
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

                Debug.Log($"ShelfBlockSpawner: Spawned {entry.BlockType} ({entry.BlockColor}) at {spawnPosition}");
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
