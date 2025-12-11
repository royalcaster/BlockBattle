using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlockBattle
{
    /// <summary>
    /// Spawns test blocks on the table at runtime. Works in both editor and builds.
    /// </summary>
    public class BlockSpawner : MonoBehaviour
    {
        [Header("Block Spawning")]
        [SerializeField, Tooltip("Whether to spawn blocks automatically on Start")]
        private bool m_SpawnOnStart = true;

        [SerializeField, Tooltip("Number of each block type to spawn")]
        private int m_BlocksPerType = 2;

        [SerializeField, Tooltip("Spacing between blocks in meters")]
        private float m_BlockSpacing = 0.15f;

        [SerializeField, Tooltip("Direction to spawn blocks in a line (normalized vector)")]
        private Vector3 m_SpawnDirection = Vector3.right;

        [SerializeField, Tooltip("Offset from spawn base position before starting the line")]
        private Vector3 m_SpawnLineOffset = Vector3.zero;

        [Header("Spawn Mode")]
        [SerializeField, Tooltip("Use configuration-based spawning instead of simple spawning")]
        private bool m_UseConfiguration = false;

        [SerializeField, Tooltip("Spawn configuration to use (if UseConfiguration is true)")]
        private BlockSpawnConfiguration m_SpawnConfiguration;

        [Header("Spawn Settings")]
        [SerializeField, Tooltip("Delay between spawning each block (in seconds). Set to 0 to spawn all at once.")]
        private float m_SpawnDelay = 0.2f;

        [SerializeField, Tooltip("If true, spawned blocks will be static (no Rigidbody, no physics, no interaction). Useful for reference structures.")]
        private bool m_SpawnAsStatic = false;

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

        [Header("Spawn Location")]
        [SerializeField, Tooltip("Table GameObject. If null, will search for 'Table' in scene")]
        private GameObject m_Table;

        [SerializeField, Tooltip("Spawn height above table in meters")]
        private float m_SpawnHeight = 0.2f;

        /// <summary>
        /// Logs spawner initialization.
        /// </summary>
        private void Awake()
        {
            Debug.Log($"BlockSpawner: Awake() called. GameObject: {gameObject.name}, Active: {gameObject.activeSelf}, Enabled: {enabled}");
            Debug.Log($"BlockSpawner: SpawnOnStart = {m_SpawnOnStart}, BlocksPerType = {m_BlocksPerType}");
            Debug.Log($"BlockSpawner: Prefab assignments - Cube: {(m_CubeBlockPrefab != null ? m_CubeBlockPrefab.name : "NULL")}, " +
                      $"Cylinder: {(m_CylinderBlockPrefab != null ? m_CylinderBlockPrefab.name : "NULL")}, " +
                      $"Triangle: {(m_TriangleBlockPrefab != null ? m_TriangleBlockPrefab.name : "NULL")}");
        }

        /// <summary>
        /// Spawns blocks on Start if enabled.
        /// </summary>
        private void Start()
        {
            if (m_SpawnOnStart)
            {
                // Use a small delay to ensure all systems are initialized
                Invoke(nameof(SpawnBlocks), 0.1f);
            }
        }

        /// <summary>
        /// Spawns test blocks on the table.
        /// </summary>
        public void SpawnBlocks()
        {
            Debug.Log("BlockSpawner: SpawnBlocks() called");

            if (m_UseConfiguration && m_SpawnConfiguration != null)
            {
                if (m_SpawnDelay > 0f)
                {
                    StartCoroutine(SpawnBlocksFromConfigurationCoroutine());
                }
                else
                {
                    SpawnBlocksFromConfiguration();
                }
                return;
            }

            // Fall back to simple spawning
            SpawnBlocksSimple();
        }

        /// <summary>
        /// Spawns blocks using the spawn configuration (all at once).
        /// Uses colors from config but spawns blocks in a straight line in random order.
        /// </summary>
        private void SpawnBlocksFromConfiguration()
        {
            Debug.Log($"BlockSpawner: Spawning blocks from configuration '{m_SpawnConfiguration.ConfigurationName}'");

            if (m_SpawnConfiguration.SpawnEntries == null || m_SpawnConfiguration.SpawnEntries.Count == 0)
            {
                Debug.LogWarning("BlockSpawner: Spawn configuration has no entries!");
                return;
            }

            Vector3 basePosition = GetSpawnBasePosition();

            // Create a shuffled copy of the entries for random order
            List<BlockSpawnEntry> shuffledEntries = m_SpawnConfiguration.SpawnEntries.ToList();
            ShuffleList(shuffledEntries);

            // Normalize spawn direction to ensure consistent spacing
            Vector3 normalizedDirection = m_SpawnDirection.normalized;

            // Spawn blocks in a straight line
            int blockIndex = 0;
            foreach (BlockSpawnEntry entry in shuffledEntries)
            {
                Vector3 spawnPosition = basePosition + m_SpawnLineOffset + normalizedDirection * (blockIndex * m_BlockSpacing);
                SpawnSingleBlockFromConfig(entry, spawnPosition);
                blockIndex++;
            }

            Debug.Log($"BlockSpawner: Spawned {shuffledEntries.Count} blocks from configuration in random order");
        }

        /// <summary>
        /// Spawns blocks using the spawn configuration with delay between each block (coroutine).
        /// Uses colors from config but spawns blocks in a straight line in random order.
        /// </summary>
        private IEnumerator SpawnBlocksFromConfigurationCoroutine()
        {
            Debug.Log($"BlockSpawner: Spawning blocks from configuration '{m_SpawnConfiguration.ConfigurationName}' with delay of {m_SpawnDelay}s");

            if (m_SpawnConfiguration.SpawnEntries == null || m_SpawnConfiguration.SpawnEntries.Count == 0)
            {
                Debug.LogWarning("BlockSpawner: Spawn configuration has no entries!");
                yield break;
            }

            Vector3 basePosition = GetSpawnBasePosition();

            // Create a shuffled copy of the entries for random order
            List<BlockSpawnEntry> shuffledEntries = m_SpawnConfiguration.SpawnEntries.ToList();
            ShuffleList(shuffledEntries);

            // Normalize spawn direction to ensure consistent spacing
            Vector3 normalizedDirection = m_SpawnDirection.normalized;

            // Spawn blocks in a straight line with delay
            int blockIndex = 0;
            foreach (BlockSpawnEntry entry in shuffledEntries)
            {
                Vector3 spawnPosition = basePosition + m_SpawnLineOffset + normalizedDirection * (blockIndex * m_BlockSpacing);
                SpawnSingleBlockFromConfig(entry, spawnPosition);
                blockIndex++;
                
                // Wait before spawning next block
                yield return new WaitForSeconds(m_SpawnDelay);
            }

            Debug.Log($"BlockSpawner: Finished spawning {shuffledEntries.Count} blocks from configuration in random order");
        }

        /// <summary>
        /// Spawns a single block from a spawn entry using the specified position.
        /// Uses block type and color from config, but uses the provided position and identity rotation.
        /// </summary>
        /// <param name="entry">The spawn entry containing block type and color</param>
        /// <param name="spawnPosition">The position to spawn the block at</param>
        private void SpawnSingleBlockFromConfig(BlockSpawnEntry entry, Vector3 spawnPosition)
        {
            GameObject prefab = GetPrefabForBlockType(entry.BlockType);
            if (prefab == null)
            {
                Debug.LogWarning($"BlockSpawner: No prefab assigned for block type {entry.BlockType}. Skipping.");
                return;
            }

            // Use identity rotation (blocks spawn upright, not in their target rotation)
            GameObject block = Instantiate(prefab, spawnPosition, Quaternion.identity);
            
            if (block != null)
            {
                block.name = $"Block_{entry.BlockType}_{entry.BlockColor}_Spawned";
                
                // Apply color material from config
                ApplyBlockColor(block, entry.BlockColor);
                
                // Make block static if requested (remove physics and interaction)
                if (m_SpawnAsStatic)
                {
                    MakeBlockStatic(block);
                }
                
                Debug.Log($"BlockSpawner: Spawned {entry.BlockType} ({entry.BlockColor}) at {spawnPosition}");
            }
        }

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
        /// Makes a block static by removing or disabling physics and interaction components.
        /// </summary>
        /// <param name="block">The block GameObject to make static</param>
        private void MakeBlockStatic(GameObject block)
        {
            if (block == null)
            {
                return;
            }

            // Disable or remove Rigidbody
            Rigidbody rb = block.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                // Optionally remove it completely:
                // Object.Destroy(rb);
            }

            // Disable XR Grab Interactable
            XRGrabInteractable grabInteractable = block.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
            }

            // Disable BlockCollisionController if present
            BlockCollisionController collisionController = block.GetComponent<BlockCollisionController>();
            if (collisionController != null)
            {
                collisionController.enabled = false;
            }

            Debug.Log($"BlockSpawner: Made block '{block.name}' static (no physics, no interaction)");
        }

        /// <summary>
        /// Spawns blocks using the simple method (original behavior).
        /// </summary>
        private void SpawnBlocksSimple()
        {
            Debug.Log("BlockSpawner: Using simple spawn method");

            // Verify prefabs are assigned
            if (m_CubeBlockPrefab == null)
            {
                Debug.LogError("BlockSpawner: Cube block prefab is NULL! Please assign it in the Inspector.");
                return;
            }
            if (m_CylinderBlockPrefab == null)
            {
                Debug.LogError("BlockSpawner: Cylinder block prefab is NULL! Please assign it in the Inspector.");
                return;
            }
            if (m_TriangleBlockPrefab == null)
            {
                Debug.LogError("BlockSpawner: Triangle block prefab is NULL! Please assign it in the Inspector.");
                return;
            }

            Debug.Log($"BlockSpawner: All prefabs are assigned. Cube: {m_CubeBlockPrefab.name}, Cylinder: {m_CylinderBlockPrefab.name}, Triangle: {m_TriangleBlockPrefab.name}");

            // Find table if not assigned
            if (m_Table == null)
            {
                m_Table = GameObject.Find("Table");
            }

            Vector3 spawnPosition;
            if (m_Table != null)
            {
                spawnPosition = m_Table.transform.position + Vector3.up * m_SpawnHeight;
                Debug.Log($"BlockSpawner: Spawning blocks above table at {spawnPosition}");
            }
            else
            {
                spawnPosition = Vector3.up * 1f;
                Debug.LogWarning("BlockSpawner: Table not found. Spawning blocks at origin.");
            }

            int blockIndex = 0;
            int totalSpawned = 0;

            // Spawn cube blocks
            try
            {
                for (int i = 0; i < m_BlocksPerType; i++)
                {
                    Vector3 position = spawnPosition + Vector3.right * (blockIndex * m_BlockSpacing);
                    GameObject block = Instantiate(m_CubeBlockPrefab, position, Quaternion.identity);
                    if (block != null)
                    {
                        block.name = $"Block_Cube_Spawned_{i + 1}";
                        
                        // Verify renderer and material
                        Transform visuals = block.transform.Find("Visuals");
                        if (visuals != null)
                        {
                            MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
                            if (renderer != null)
                            {
                                Debug.Log($"BlockSpawner: Cube block {i + 1} - Renderer enabled: {renderer.enabled}, Material: {(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "NULL")}, GameObject active: {visuals.gameObject.activeSelf}");
                                
                                // Ensure renderer is enabled and has material
                                if (!renderer.enabled)
                                {
                                    renderer.enabled = true;
                                    Debug.LogWarning($"BlockSpawner: Enabled renderer on cube block {i + 1}");
                                }
                                
                                if (renderer.sharedMaterial == null)
                                {
                                    // Try to load and assign material
                                    Material blockMaterial = Resources.Load<Material>("BlockMaterial");
                                    if (blockMaterial == null)
                                    {
                                        // Try loading from asset path (won't work at runtime, but log it)
                                        Debug.LogError($"BlockSpawner: Material is NULL on cube block {i + 1}! Material needs to be assigned in prefab.");
                                    }
                                    else
                                    {
                                        renderer.sharedMaterial = blockMaterial;
                                        Debug.Log($"BlockSpawner: Assigned material to cube block {i + 1}");
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogError($"BlockSpawner: MeshRenderer not found on Visuals of cube block {i + 1}!");
                            }
                        }
                        else
                        {
                            Debug.LogError($"BlockSpawner: Visuals child not found in cube block {i + 1}!");
                        }
                        
                        blockIndex++;
                        totalSpawned++;
                        Debug.Log($"BlockSpawner: Spawned cube block {i + 1} at {position}");
                    }
                    else
                    {
                        Debug.LogError($"BlockSpawner: Failed to instantiate cube block {i + 1}!");
                    }
                }
                Debug.Log($"BlockSpawner: Successfully spawned {totalSpawned} cube blocks");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlockSpawner: Exception while spawning cube blocks: {e.Message}\n{e.StackTrace}");
            }

            // Spawn cylinder blocks
            try
            {
                for (int i = 0; i < m_BlocksPerType; i++)
                {
                    Vector3 position = spawnPosition + Vector3.right * (blockIndex * m_BlockSpacing);
                    GameObject block = Instantiate(m_CylinderBlockPrefab, position, Quaternion.identity);
                    if (block != null)
                    {
                        block.name = $"Block_Cylinder_Spawned_{i + 1}";
                        
                        // Verify renderer and material
                        Transform visuals = block.transform.Find("Visuals");
                        if (visuals != null)
                        {
                            MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
                            if (renderer != null)
                            {
                                Debug.Log($"BlockSpawner: Cylinder block {i + 1} - Renderer enabled: {renderer.enabled}, Material: {(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "NULL")}");
                                if (!renderer.enabled) renderer.enabled = true;
                            }
                        }
                        
                        blockIndex++;
                        totalSpawned++;
                        Debug.Log($"BlockSpawner: Spawned cylinder block {i + 1} at {position}");
                    }
                    else
                    {
                        Debug.LogError($"BlockSpawner: Failed to instantiate cylinder block {i + 1}!");
                    }
                }
                Debug.Log($"BlockSpawner: Successfully spawned {m_BlocksPerType} cylinder blocks");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlockSpawner: Exception while spawning cylinder blocks: {e.Message}\n{e.StackTrace}");
            }

            // Spawn triangle blocks
            try
            {
                for (int i = 0; i < m_BlocksPerType; i++)
                {
                    Vector3 position = spawnPosition + Vector3.right * (blockIndex * m_BlockSpacing);
                    GameObject block = Instantiate(m_TriangleBlockPrefab, position, Quaternion.identity);
                    if (block != null)
                    {
                        block.name = $"Block_Triangle_Spawned_{i + 1}";
                        
                        // Verify renderer and material
                        Transform visuals = block.transform.Find("Visuals");
                        if (visuals != null)
                        {
                            MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
                            if (renderer != null)
                            {
                                Debug.Log($"BlockSpawner: Triangle block {i + 1} - Renderer enabled: {renderer.enabled}, Material: {(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "NULL")}");
                                if (!renderer.enabled) renderer.enabled = true;
                            }
                        }
                        
                        blockIndex++;
                        totalSpawned++;
                        Debug.Log($"BlockSpawner: Spawned triangle block {i + 1} at {position}");
                    }
                    else
                    {
                        Debug.LogError($"BlockSpawner: Failed to instantiate triangle block {i + 1}!");
                    }
                }
                Debug.Log($"BlockSpawner: Successfully spawned {m_BlocksPerType} triangle blocks");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlockSpawner: Exception while spawning triangle blocks: {e.Message}\n{e.StackTrace}");
            }

            Debug.Log($"BlockSpawner: Spawning complete. Total blocks spawned: {totalSpawned}");
        }

        /// <summary>
        /// Gets the base spawn position (table position + spawn height).
        /// </summary>
        /// <returns>The base spawn position</returns>
        private Vector3 GetSpawnBasePosition()
        {
            if (m_Table == null)
            {
                m_Table = GameObject.Find("Table");
            }

            if (m_Table != null)
            {
                return m_Table.transform.position + Vector3.up * m_SpawnHeight;
            }
            else
            {
                return Vector3.up * 1f;
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
            if (block == null)
            {
                return;
            }

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
                Debug.LogWarning($"BlockSpawner: Could not load material {materialName}. Using default material.");
                return;
            }

            // Apply material to the block's visuals
            Transform visuals = block.transform.Find("Visuals");
            if (visuals != null)
            {
                MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = coloredMaterial; // Use material (not sharedMaterial) to create instance
                }
            }
        }
    }
}

