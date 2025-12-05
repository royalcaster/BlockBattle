using UnityEngine;

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

        [Header("Block Prefabs")]
        [SerializeField, Tooltip("Cube block prefab")]
        private GameObject m_CubeBlockPrefab;

        [SerializeField, Tooltip("Cylinder block prefab")]
        private GameObject m_CylinderBlockPrefab;

        [SerializeField, Tooltip("Triangle block prefab")]
        private GameObject m_TriangleBlockPrefab;

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
    }
}

