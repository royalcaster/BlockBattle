using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

namespace BlockBattle
{
    /// <summary>
    /// Spawns a reference structure next to the main table and allows rotation via XR controllers.
    /// </summary>
    public class ReferenceStructureSpawner : MonoBehaviour
    {
        [Header("Structure Configuration")]
        [SerializeField, Tooltip("The spawn configuration to use (BlockSpawnConfiguration asset)")]
        private BlockSpawnConfiguration m_SpawnConfiguration;

        [Header("Spawn Settings")]
        [SerializeField, Tooltip("Table GameObject. If null, will search for 'Table' in scene")]
        private GameObject m_Table;

        [SerializeField, Tooltip("Offset from table position (X, Y, Z in meters). Structure will spawn at table position + offset")]
        private Vector3 m_TableOffset = new Vector3(1.5f, 0f, 0f); // 1.5m to the right of table

        [SerializeField, Tooltip("Whether to spawn structure automatically on Start")]
        private bool m_SpawnOnStart = true;

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

        [Header("Holographic Effect")]
        [SerializeField, Tooltip("Whether to apply holographic effect to reference structure blocks")]
        private bool m_UseHolographicEffect = true;

        [SerializeField, Tooltip("Base transparency of the holographic blocks (0 = fully transparent, 1 = opaque)")]
        [Range(0f, 1f)]
        private float m_HolographicTransparency = 0.6f;

        [SerializeField, Tooltip("Color of the holographic emission/glow effect")]
        private Color m_HolographicEmissionColor = new Color(0.3f, 0.8f, 1.0f, 1.0f);

        [SerializeField, Tooltip("Intensity of the emission glow effect")]
        [Range(0f, 5f)]
        private float m_HolographicEmissionIntensity = 1.5f;

        [SerializeField, Tooltip("Power of the fresnel effect (higher = sharper edge glow)")]
        [Range(0f, 5f)]
        private float m_HolographicFresnelPower = 2.0f;

        [SerializeField, Tooltip("Intensity of the fresnel edge glow effect")]
        [Range(0f, 2f)]
        private float m_HolographicFresnelIntensity = 1.0f;

        [SerializeField, Tooltip("Speed of the scanline animation")]
        [Range(0f, 10f)]
        private float m_HolographicScanlineSpeed = 2.0f;

        [SerializeField, Tooltip("Intensity of the scanline effect (0 = no scanlines, 1 = full effect)")]
        [Range(0f, 1f)]
        private float m_HolographicScanlineIntensity = 0.3f;

        [Header("Rotation Settings")]
        [SerializeField, Tooltip("Whether manual rotation via controllers is enabled")]
        private bool m_ManualRotationEnabled = true;

        [SerializeField, Tooltip("Manual rotation speed multiplier. Higher = faster rotation when dragging.")]
        private float m_ManualRotationSpeed = 2f;

        [SerializeField, Tooltip("How quickly rotation momentum slows down (0 = no friction, 1 = stops immediately)")]
        [Range(0.01f, 1f)]
        private float m_RotationFriction = 0.05f;

        [Header("Auto Rotation")]
        [SerializeField, Tooltip("Whether continuous auto-rotation is enabled (like a display turntable)")]
        private bool m_AutoRotationEnabled = true;

        [SerializeField, Tooltip("Auto rotation speed in degrees per second")]
        private float m_AutoRotationSpeed = 15f;

        private List<GameObject> m_SpawnedBlocks = new List<GameObject>();
        private GameObject m_StructureRoot;
        private bool m_IsRotating = false;
        private Vector3 m_LastControllerPosition;

        // Events for game loop integration
        /// <summary>
        /// Event fired when a structure is successfully spawned.
        /// </summary>
        public event Action<BlockSpawnConfiguration> OnStructureSpawned;

        /// <summary>
        /// Event fired when a structure is cleared/destroyed.
        /// </summary>
        public event Action OnStructureCleared;

        /// <summary>
        /// Gets or sets the spawn configuration to use.
        /// </summary>
        public BlockSpawnConfiguration SpawnConfiguration
        {
            get => m_SpawnConfiguration;
            set
            {
                m_SpawnConfiguration = value;
                if (Application.isPlaying)
                {
                    ClearStructure();
                    SpawnStructure();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether manual rotation is enabled.
        /// </summary>
        public bool ManualRotationEnabled
        {
            get => m_ManualRotationEnabled;
            set => m_ManualRotationEnabled = value;
        }

        /// <summary>
        /// Gets or sets whether auto rotation is enabled.
        /// </summary>
        public bool AutoRotationEnabled
        {
            get => m_AutoRotationEnabled;
            set => m_AutoRotationEnabled = value;
        }

        /// <summary>
        /// Gets whether a structure is currently spawned.
        /// </summary>
        public bool IsStructureSpawned => m_StructureRoot != null && m_SpawnedBlocks.Count > 0;

        /// <summary>
        /// Gets the currently spawned spawn configuration.
        /// </summary>
        public BlockSpawnConfiguration CurrentSpawnConfiguration => m_SpawnConfiguration;

        /// <summary>
        /// Gets the number of blocks in the currently spawned structure.
        /// </summary>
        public int SpawnedBlockCount => m_SpawnedBlocks != null ? m_SpawnedBlocks.Count : 0;

        /// <summary>
        /// Returns true if any rotation (manual or auto) is enabled.
        /// </summary>
        private bool IsAnyRotationEnabled => m_ManualRotationEnabled || m_AutoRotationEnabled;

        /// <summary>
        /// Initializes the spawner.
        /// </summary>
        private void Start()
        {
            if (m_SpawnOnStart)
            {
                SpawnStructure();
            }
        }

        /// <summary>
        /// Spawns the reference structure using the currently assigned SpawnConfiguration.
        /// </summary>
        public void SpawnStructure()
        {
            if (m_SpawnConfiguration == null)
            {
                Debug.LogWarning("ReferenceStructureSpawner: No spawn configuration assigned!");
                return;
            }

            SpawnFromSpawnConfiguration();
        }

        /// <summary>
        /// Spawns a specific structure by BlockSpawnConfiguration. Clears any existing structure first.
        /// </summary>
        /// <param name="spawnConfiguration">The spawn configuration to use</param>
        public void SpawnStructure(BlockSpawnConfiguration spawnConfiguration)
        {
            if (spawnConfiguration == null)
            {
                Debug.LogWarning("ReferenceStructureSpawner: Cannot spawn null BlockSpawnConfiguration!");
                return;
            }

            ClearStructure();
            m_SpawnConfiguration = spawnConfiguration;
            SpawnFromSpawnConfiguration();
        }

        /// <summary>
        /// Spawns structure from BlockSpawnConfiguration asset.
        /// </summary>
        private void SpawnFromSpawnConfiguration()
        {
            if (m_SpawnConfiguration == null)
            {
                return;
            }

            if (m_SpawnConfiguration.SpawnEntries == null || m_SpawnConfiguration.SpawnEntries.Count == 0)
            {
                Debug.LogWarning("ReferenceStructureSpawner: Spawn configuration has no entries!");
                return;
            }

            // Find table if not assigned
            if (m_Table == null)
            {
                m_Table = GameObject.Find("Table");
            }

            // Calculate structure center from spawn entry positions
            Vector3 structureCenter = CalculateStructureCenter(m_SpawnConfiguration.SpawnEntries);
            
            // Calculate spawn position (table offset)
            Vector3 spawnPosition = GetSpawnPosition();

            // Create root GameObject for the structure
            // Position root at spawn position - this will be the rotation pivot (structure center)
            m_StructureRoot = new GameObject($"ReferenceStructure_{m_SpawnConfiguration.ConfigurationName}");
            m_StructureRoot.transform.position = spawnPosition; // Root is at spawn position (where center should be)
            m_StructureRoot.transform.rotation = Quaternion.identity;
            m_StructureRoot.transform.SetParent(transform);

            // Spawn blocks relative to structure center
            foreach (BlockSpawnEntry entry in m_SpawnConfiguration.SpawnEntries)
            {
                if (entry == null)
                {
                    Debug.LogWarning("ReferenceStructureSpawner: Found null entry in spawn configuration. Skipping.");
                    continue;
                }
                
                // Adjust position relative to center (subtract center offset)
                Vector3 relativePosition = entry.Position - structureCenter;
                Debug.Log($"ReferenceStructureSpawner: Spawning {entry.BlockType} ({entry.BlockColor}) at relative position {relativePosition}");
                SpawnBlock(entry.BlockType, relativePosition, entry.Rotation, entry.BlockColor);
            }

            // Add rotation interactable component AFTER blocks are spawned (so bounds calculation works)
            if (IsAnyRotationEnabled)
            {
                SetupRotationInteraction();
            }

            Debug.Log($"ReferenceStructureSpawner: Spawned {m_SpawnedBlocks.Count} blocks for structure '{m_SpawnConfiguration.ConfigurationName}' (center: {structureCenter})");
            
            // Fire event for game loop integration
            OnStructureSpawned?.Invoke(m_SpawnConfiguration);
        }

        /// <summary>
        /// Spawns a single block.
        /// </summary>
        /// <param name="blockType">Type of block to spawn</param>
        /// <param name="localPosition">Local position relative to structure root</param>
        /// <param name="rotation">Rotation of the block</param>
        /// <param name="blockColor">Color of the block</param>
        private void SpawnBlock(BlockType blockType, Vector3 localPosition, Quaternion rotation, BlockColor blockColor)
        {
            GameObject prefab = GetPrefabForBlockType(blockType);
            if (prefab == null)
            {
                Debug.LogWarning($"ReferenceStructureSpawner: No prefab assigned for block type {blockType}. Skipping.");
                return;
            }

            // Spawn block as child of structure root with local position
            // This ensures blocks rotate around the root's pivot point
            GameObject block = Instantiate(prefab, m_StructureRoot.transform);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = rotation;
            
            if (block != null)
            {
                block.name = $"ReferenceBlock_{blockType}_{m_SpawnedBlocks.Count}";
                
                // Apply holographic effect or color material
                if (m_UseHolographicEffect)
                {
                    ApplyHolographicMaterial(block, blockColor);
                }
                else
                {
                    ApplyBlockColor(block, blockColor);
                }
                
                // Make block static (no physics, no interaction)
                MakeBlockStatic(block);
                
                m_SpawnedBlocks.Add(block);
                Debug.Log($"ReferenceStructureSpawner: Successfully spawned {blockType} ({blockColor}) - GameObject: {block.name}, Active: {block.activeSelf}, Position: {block.transform.position}");
            }
            else
            {
                Debug.LogError($"ReferenceStructureSpawner: Failed to instantiate block {blockType} - prefab was null or instantiation failed");
            }
        }

        /// <summary>
        /// Applies holographic material to a block for a hologram effect while preserving the block's color.
        /// Finds all MeshRenderers in the block and applies the holographic material to all of them.
        /// </summary>
        /// <param name="block">The block GameObject</param>
        /// <param name="blockColor">The color of the block to preserve</param>
        private void ApplyHolographicMaterial(GameObject block, BlockColor blockColor)
        {
            if (block == null)
            {
                return;
            }

            // Load the holographic material
            Material holographicMaterialSource = Resources.Load<Material>("HolographicMaterial");
            
            // If not in Resources, try loading from asset path (editor only)
            #if UNITY_EDITOR
            if (holographicMaterialSource == null)
            {
                string materialPath = "Assets/BlockBattle/Materials/HolographicMaterial.mat";
                holographicMaterialSource = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
            #endif

            if (holographicMaterialSource == null)
            {
                Debug.LogWarning("ReferenceStructureSpawner: Could not load HolographicMaterial. Using default material.");
                return;
            }

            // Find all MeshRenderers in the block (including children)
            MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>(true);
            
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning($"ReferenceStructureSpawner: No MeshRenderers found in block {block.name}");
                return;
            }

            // Get the block's original color
            Color blockOriginalColor = BlockColorUtility.GetColor(blockColor);
            
            // Apply holographic material to all renderers
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                // Create a new material instance from the source material
                // This ensures each block has its own material instance that can be modified independently
                Material holographicMaterialInstance = new Material(holographicMaterialSource);
                
                // Set the base color to the block's original color (preserving the color while applying holographic effect)
                // Use configurable transparency
                Color holographicBaseColor = new Color(blockOriginalColor.r, blockOriginalColor.g, blockOriginalColor.b, m_HolographicTransparency);
                holographicMaterialInstance.SetColor("_BaseColor", holographicBaseColor);
                
                // Set all configurable holographic effect properties
                holographicMaterialInstance.SetColor("_EmissionColor", m_HolographicEmissionColor);
                holographicMaterialInstance.SetFloat("_EmissionIntensity", m_HolographicEmissionIntensity);
                holographicMaterialInstance.SetFloat("_FresnelPower", m_HolographicFresnelPower);
                holographicMaterialInstance.SetFloat("_FresnelIntensity", m_HolographicFresnelIntensity);
                holographicMaterialInstance.SetFloat("_ScanlineSpeed", m_HolographicScanlineSpeed);
                holographicMaterialInstance.SetFloat("_ScanlineIntensity", m_HolographicScanlineIntensity);
                holographicMaterialInstance.SetFloat("_Transparency", m_HolographicTransparency);
                
                // Ensure render queue is set correctly for transparent rendering
                holographicMaterialInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                
                // Replace all materials in the materials array with the holographic material
                Material[] materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = holographicMaterialInstance;
                }
                
                renderer.materials = materials;
            }
        }

        /// <summary>
        /// Applies a color material to a block.
        /// Finds all MeshRenderers in the block and applies the colored material to all of them.
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
                Debug.LogWarning($"ReferenceStructureSpawner: Could not load material {materialName}. Using default material.");
                return;
            }

            // Find all MeshRenderers in the block (including children)
            // This is more robust than just looking for "Visuals" child
            MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>(true);
            
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning($"ReferenceStructureSpawner: No MeshRenderers found in block {block.name}");
                return;
            }

            // Apply material to all renderers
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.material = coloredMaterial; // Use material (not sharedMaterial) to create instance
                }
            }
        }

        /// <summary>
        /// Clears the spawned structure.
        /// </summary>
        public void ClearStructure()
        {
            bool hadStructure = IsStructureSpawned;

            if (m_StructureRoot != null)
            {
                Destroy(m_StructureRoot);
                m_StructureRoot = null;
            }

            m_SpawnedBlocks.Clear();

            // Fire event if structure was cleared
            if (hadStructure)
            {
                OnStructureCleared?.Invoke();
            }
        }

        /// <summary>
        /// Gets the spawn position (table position + offset).
        /// </summary>
        /// <returns>The spawn position</returns>
        private Vector3 GetSpawnPosition()
        {
            if (m_Table != null)
            {
                return m_Table.transform.position + m_TableOffset;
            }
            else
            {
                return transform.position + m_TableOffset;
            }
        }

        /// <summary>
        /// Sets up rotation interaction for the structure.
        /// </summary>
        private void SetupRotationInteraction()
        {
            if (m_StructureRoot == null)
            {
                return;
            }

            // Calculate bounds of all blocks to create appropriate collider
            Bounds structureBounds = CalculateStructureBounds();

            // Add BoxCollider for interaction (covers entire structure)
            BoxCollider structureCollider = m_StructureRoot.AddComponent<BoxCollider>();
            // Convert world bounds to local space relative to root
            structureCollider.center = m_StructureRoot.transform.InverseTransformPoint(structureBounds.center);
            structureCollider.size = structureBounds.size;
            structureCollider.isTrigger = false;

            // Add custom rotation handler
            ReferenceStructureRotator rotator = m_StructureRoot.AddComponent<ReferenceStructureRotator>();
            
            // Setup manual rotation if enabled
            XRSimpleInteractable simpleInteractable = null;
            if (m_ManualRotationEnabled)
            {
                // Use XRSimpleInteractable instead of XRGrabInteractable
                // XRSimpleInteractable detects hover/select but does NOT move the object
                simpleInteractable = m_StructureRoot.AddComponent<XRSimpleInteractable>();
            }
            
            rotator.Initialize(this, m_ManualRotationSpeed, m_RotationFriction, m_AutoRotationEnabled, m_AutoRotationSpeed, simpleInteractable);
        }

        /// <summary>
        /// Calculates the center point of all blocks from BlockSpawnConfiguration.
        /// </summary>
        /// <param name="entries">List of spawn entries</param>
        /// <returns>The center point of all block positions</returns>
        private Vector3 CalculateStructureCenter(List<BlockSpawnEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (BlockSpawnEntry entry in entries)
            {
                if (entry != null)
                {
                    sum += entry.Position;
                    count++;
                }
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        /// <summary>
        /// Calculates the bounding box of all spawned blocks.
        /// </summary>
        /// <returns>The bounds of the structure</returns>
        private Bounds CalculateStructureBounds()
        {
            if (m_SpawnedBlocks == null || m_SpawnedBlocks.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds bounds = new Bounds(m_SpawnedBlocks[0].transform.position, Vector3.zero);
            
            foreach (GameObject block in m_SpawnedBlocks)
            {
                if (block != null)
                {
                    Renderer renderer = block.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    else
                    {
                        bounds.Encapsulate(block.transform.position);
                    }
                }
            }

            return bounds;
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
        /// Makes a block static by removing or disabling physics and interaction components.
        /// </summary>
        /// <param name="block">The block GameObject to make static</param>
        private void MakeBlockStatic(GameObject block)
        {
            if (block == null)
            {
                return;
            }

            // Remove NetworkBlock if present (reference blocks don't need networking)
            var networkBlock = block.GetComponent<Network.NetworkBlock>();
            if (networkBlock != null)
            {
                Destroy(networkBlock);
            }

            // Remove NetworkObject if present (reference blocks are local only)
            var networkObject = block.GetComponent<Unity.Netcode.NetworkObject>();
            if (networkObject != null)
            {
                Destroy(networkObject);
            }

            // Remove NetworkRigidbody if present
            var networkRigidbody = block.GetComponent<Unity.Netcode.Components.NetworkRigidbody>();
            if (networkRigidbody != null)
            {
                Destroy(networkRigidbody);
            }

            // Disable or remove Rigidbody
            Rigidbody rb = block.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
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
        }

        /// <summary>
        /// Called when structure is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            ClearStructure();
        }
    }

    /// <summary>
    /// Component that handles rotation of the reference structure around Y-axis only.
    /// Supports both continuous auto-rotation and manual drag-to-rotate via controllers.
    /// Drag horizontally while grabbing to rotate the structure.
    /// Includes momentum - spin fast and release to keep it spinning.
    /// </summary>
    public class ReferenceStructureRotator : MonoBehaviour
    {
        private ReferenceStructureSpawner m_Spawner;
        private float m_ManualRotationSpeed;
        private float m_RotationFriction;
        private bool m_AutoRotationEnabled;
        private float m_AutoRotationSpeed;
        private XRSimpleInteractable m_Interactable;
        private IXRSelectInteractor m_CurrentInteractor;
        private Vector3 m_LastControllerPosition;
        private bool m_IsSelected;
        
        // Momentum/velocity tracking
        private float m_RotationVelocity;
        private const float MinVelocityThreshold = 0.5f; // Below this, momentum stops
        
        // Smoothing - uses a smoothed velocity approach for buttery smooth rotation
        private float m_SmoothedRotationVelocity;
        private const float SmoothTime = 0.08f; // Lower = more responsive, higher = smoother

        /// <summary>
        /// Initializes the rotator.
        /// </summary>
        /// <param name="spawner">The reference structure spawner</param>
        /// <param name="manualRotationSpeed">Manual rotation speed multiplier</param>
        /// <param name="rotationFriction">How quickly momentum slows down</param>
        /// <param name="autoRotationEnabled">Whether auto-rotation is enabled</param>
        /// <param name="autoRotationSpeed">Auto rotation speed in degrees per second</param>
        /// <param name="interactable">The XRSimpleInteractable component (can be null)</param>
        public void Initialize(ReferenceStructureSpawner spawner, float manualRotationSpeed, float rotationFriction, bool autoRotationEnabled, float autoRotationSpeed, XRSimpleInteractable interactable)
        {
            m_Spawner = spawner;
            m_ManualRotationSpeed = manualRotationSpeed;
            m_RotationFriction = rotationFriction;
            m_AutoRotationEnabled = autoRotationEnabled;
            m_AutoRotationSpeed = autoRotationSpeed;
            m_Interactable = interactable;
            m_RotationVelocity = 0f;
            m_SmoothedRotationVelocity = 0f;

            if (m_Interactable != null)
            {
                m_Interactable.selectEntered.AddListener(OnSelectEntered);
                m_Interactable.selectExited.AddListener(OnSelectExited);
            }
        }

        /// <summary>
        /// Called when structure is grabbed.
        /// </summary>
        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            m_CurrentInteractor = args.interactorObject;
            m_IsSelected = true;
            
            // Stop any existing momentum when grabbed
            m_RotationVelocity = 0f;
            
            // Store initial controller position
            if (m_CurrentInteractor != null && m_CurrentInteractor.transform != null)
            {
                m_LastControllerPosition = m_CurrentInteractor.transform.position;
            }
        }

        /// <summary>
        /// Called when structure is released.
        /// </summary>
        private void OnSelectExited(SelectExitEventArgs args)
        {
            m_CurrentInteractor = null;
            m_IsSelected = false;
            // m_RotationVelocity is preserved - momentum continues
        }

        /// <summary>
        /// Updates rotation - handles manual rotation, momentum, and auto-rotation.
        /// </summary>
        private void Update()
        {
            float targetVelocity = 0f;
            
            // Manual rotation takes priority when user is grabbing
            if (m_IsSelected && m_CurrentInteractor != null)
            {
                targetVelocity = CalculateManualRotationVelocity();
            }
            // Apply momentum when not grabbing
            else if (Mathf.Abs(m_RotationVelocity) > MinVelocityThreshold)
            {
                targetVelocity = m_RotationVelocity;
                // Apply friction to slow down momentum
                m_RotationVelocity *= (1f - m_RotationFriction);
                if (Mathf.Abs(m_RotationVelocity) < MinVelocityThreshold)
                {
                    m_RotationVelocity = 0f;
                }
            }
            // Auto-rotation only when no momentum
            else if (m_AutoRotationEnabled)
            {
                targetVelocity = m_AutoRotationSpeed;
            }
            
            // Smooth the velocity for buttery smooth rotation
            m_SmoothedRotationVelocity = Mathf.Lerp(
                m_SmoothedRotationVelocity, 
                targetVelocity, 
                1f - Mathf.Exp(-Time.deltaTime / SmoothTime)
            );
            
            // Apply the smoothed rotation
            if (Mathf.Abs(m_SmoothedRotationVelocity) > 0.01f)
            {
                transform.Rotate(0f, m_SmoothedRotationVelocity * Time.deltaTime, 0f, Space.World);
            }
        }

        /// <summary>
        /// Calculates the rotation velocity from manual controller movement.
        /// </summary>
        private float CalculateManualRotationVelocity()
        {
            Transform controllerTransform = m_CurrentInteractor.transform;
            if (controllerTransform == null)
            {
                return 0f;
            }

            Vector3 currentControllerPosition = controllerTransform.position;
            Vector3 positionDelta = currentControllerPosition - m_LastControllerPosition;
            
            // Convert horizontal movement to rotation velocity (degrees per second)
            // Moving controller left = rotate clockwise (positive Y)
            // Moving controller right = rotate counter-clockwise (negative Y)
            float rotationVelocity = -positionDelta.x * m_ManualRotationSpeed * 100f / Time.deltaTime;
            
            // Store velocity for momentum when released
            m_RotationVelocity = Mathf.Lerp(m_RotationVelocity, rotationVelocity, 0.5f);
            
            // Store position for next frame
            m_LastControllerPosition = currentControllerPosition;
            
            return rotationVelocity;
        }

        /// <summary>
        /// Called when component is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (m_Interactable != null)
            {
                m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
                m_Interactable.selectExited.RemoveListener(OnSelectExited);
            }
        }
    }
}

