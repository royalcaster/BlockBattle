using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        [SerializeField, Tooltip("Base force applied to eject blocks")]
        private float m_EjectionForce = 15f;

        [SerializeField, Range(0f, 1f), Tooltip("How much blocks spread when ejected (0 = straight line, 1 = wide spread)")]
        private float m_SpreadAmount = 0.3f;

        [SerializeField, Tooltip("Rotational force applied to blocks for realistic tumbling")]
        private float m_TumbleForce = 10f;

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

        #region Private State

        private bool _hasTriggered = false;
        private List<Rigidbody> _storedBlocks = new List<Rigidbody>();
        private List<GameObject> _spawnedBlockObjects = new List<GameObject>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ValidatePrefabs();
        }

        private void Update()
        {
            MonitorDoors();
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && !_storedBlocks.Contains(rb))
            {
                _storedBlocks.Add(rb);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && _storedBlocks.Contains(rb))
            {
                _storedBlocks.Remove(rb);
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
        /// </summary>
        private void MonitorDoors()
        {
            if (m_LeftDoor == null || m_RightDoor == null)
            {
                return;
            }

            float angleL = Mathf.Abs(m_LeftDoor.angle);
            float angleR = Mathf.Abs(m_RightDoor.angle);

            // Trigger ejection when either door opens past trigger angle
            if ((angleL >= m_TriggerAngle || angleR >= m_TriggerAngle) && !_hasTriggered)
            {
                Debug.Log($"ShelfBlockSpawner: Door trigger! Left={angleL:F1}°, Right={angleR:F1}°, Threshold={m_TriggerAngle}°, StoredBlocks={_storedBlocks.Count}");
                EjectBlocks();
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
                    rb.linearVelocity = randomDir * randomPower;

                    // Add tumble rotation
                    rb.AddTorque(Random.insideUnitSphere * m_TumbleForce, ForceMode.Impulse);

                    ejectedCount++;
                }
            }

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
