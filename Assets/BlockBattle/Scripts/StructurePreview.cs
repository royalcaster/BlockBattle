using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlockBattle
{
    /// <summary>
    /// Displays a hologram-style preview of a reference structure.
    /// Creates ghost/preview versions of blocks that players can see while building.
    /// </summary>
    public class StructurePreview : MonoBehaviour
    {
        [Header("Structure Configuration")]
        [SerializeField, Tooltip("The spawn configuration to preview")]
        private BlockSpawnConfiguration m_SpawnConfiguration;

        [Header("Preview Settings")]
        [SerializeField, Tooltip("Material for preview blocks (should be semi-transparent/hologram-like)")]
        private Material m_PreviewMaterial;

        [SerializeField, Tooltip("Scale factor for preview blocks (1.0 = same size as real blocks)")]
        private float m_PreviewScale = 1.0f;

        [SerializeField, Tooltip("Offset from structure origin")]
        private Vector3 m_PreviewOffset = Vector3.zero;

        [SerializeField, Tooltip("Whether to show the preview")]
        private bool m_ShowPreview = true;

        [Header("Block Prefabs")]
        [SerializeField, Tooltip("Cube block prefab for preview")]
        private GameObject m_CubeBlockPrefab;

        [SerializeField, Tooltip("Cylinder block prefab for preview")]
        private GameObject m_CylinderBlockPrefab;

        [SerializeField, Tooltip("Triangle block prefab for preview")]
        private GameObject m_TriangleBlockPrefab;

        [SerializeField, Tooltip("Rectangle block prefab for preview")]
        private GameObject m_RectangleBlockPrefab;

        [SerializeField, Tooltip("Arch block prefab for preview")]
        private GameObject m_ArchBlockPrefab;

        private List<GameObject> m_PreviewBlocks = new List<GameObject>();

        /// <summary>
        /// Gets or sets the spawn configuration to preview.
        /// </summary>
        public BlockSpawnConfiguration SpawnConfiguration
        {
            get => m_SpawnConfiguration;
            set
            {
                m_SpawnConfiguration = value;
                RefreshPreview();
            }
        }

        /// <summary>
        /// Gets or sets whether the preview is visible.
        /// </summary>
        public bool ShowPreview
        {
            get => m_ShowPreview;
            set
            {
                m_ShowPreview = value;
                UpdatePreviewVisibility();
            }
        }

        /// <summary>
        /// Initializes the preview on Start.
        /// </summary>
        private void Start()
        {
            RefreshPreview();
        }

        /// <summary>
        /// Refreshes the preview by clearing existing preview blocks and creating new ones from the structure data.
        /// </summary>
        public void RefreshPreview()
        {
            ClearPreview();

            if (m_SpawnConfiguration == null)
            {
                Debug.LogWarning("StructurePreview: No spawn configuration assigned");
                return;
            }

            if (!m_SpawnConfiguration.Validate())
            {
                Debug.LogWarning($"StructurePreview: Spawn configuration '{m_SpawnConfiguration.ConfigurationName}' is invalid");
                return;
            }

            if (m_SpawnConfiguration.SpawnEntries == null || m_SpawnConfiguration.SpawnEntries.Count == 0)
            {
                Debug.LogWarning($"StructurePreview: Spawn configuration '{m_SpawnConfiguration.ConfigurationName}' has no entries");
                return;
            }

            foreach (BlockSpawnEntry entry in m_SpawnConfiguration.SpawnEntries)
            {
                if (entry == null)
                {
                    continue;
                }

                GameObject previewBlock = CreatePreviewBlock(entry);
                if (previewBlock != null)
                {
                    m_PreviewBlocks.Add(previewBlock);
                }
            }

            UpdatePreviewVisibility();
            Debug.Log($"StructurePreview: Created {m_PreviewBlocks.Count} preview blocks");
        }

        /// <summary>
        /// Creates a preview block GameObject from a BlockSpawnEntry.
        /// </summary>
        /// <param name="entry">The spawn entry to create a preview for</param>
        /// <returns>The created preview GameObject, or null if creation failed</returns>
        private GameObject CreatePreviewBlock(BlockSpawnEntry entry)
        {
            GameObject prefab = GetPrefabForBlockType(entry.BlockType);
            if (prefab == null)
            {
                Debug.LogWarning($"StructurePreview: No prefab assigned for block type {entry.BlockType}");
                return null;
            }

            // Calculate preview position (relative to this transform)
            Vector3 previewPosition = transform.position + m_PreviewOffset + entry.Position;
            Quaternion previewRotation = entry.Rotation;

            // Instantiate preview block
            GameObject previewBlock = Instantiate(prefab, previewPosition, previewRotation, transform);
            previewBlock.name = $"Preview_{entry.BlockType}_{m_PreviewBlocks.Count}";

            // Apply preview scale
            previewBlock.transform.localScale = Vector3.one * m_PreviewScale;

            // Disable physics and interaction
            Rigidbody rb = previewBlock.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            XRGrabInteractable grabInteractable = previewBlock.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
            }

            // Apply preview material or block color material
            Transform visuals = previewBlock.transform.Find("Visuals");
            if (visuals != null)
            {
                MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    // Apply preview material if available, otherwise use block color material
                    if (m_PreviewMaterial != null)
                    {
                        renderer.material = m_PreviewMaterial;
                    }
                    else
                    {
                        // Try to apply the block color material
                        string materialName = BlockColorUtility.GetMaterialName(entry.BlockColor);
                        Material coloredMaterial = Resources.Load<Material>(materialName);
                        #if UNITY_EDITOR
                        if (coloredMaterial == null)
                        {
                            string materialPath = $"Assets/BlockBattle/Materials/{materialName}.mat";
                            coloredMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        }
                        #endif
                        if (coloredMaterial != null)
                        {
                            renderer.material = coloredMaterial;
                        }
                    }
                }
            }

            return previewBlock;
        }

        /// <summary>
        /// Gets the prefab for the specified block type.
        /// </summary>
        /// <param name="blockType">The block type to get a prefab for</param>
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
        /// Clears all preview blocks.
        /// </summary>
        public void ClearPreview()
        {
            foreach (GameObject previewBlock in m_PreviewBlocks)
            {
                if (previewBlock != null)
                {
                    DestroyImmediate(previewBlock);
                }
            }

            m_PreviewBlocks.Clear();
        }

        /// <summary>
        /// Updates the visibility of all preview blocks based on m_ShowPreview.
        /// </summary>
        private void UpdatePreviewVisibility()
        {
            foreach (GameObject previewBlock in m_PreviewBlocks)
            {
                if (previewBlock != null)
                {
                    previewBlock.SetActive(m_ShowPreview);
                }
            }
        }

        /// <summary>
        /// Cleans up preview blocks on destroy.
        /// </summary>
        private void OnDestroy()
        {
            ClearPreview();
        }

        /// <summary>
        /// Validates that all required prefabs are assigned (editor only).
        /// </summary>
        private void OnValidate()
        {
            #if UNITY_EDITOR
            if (m_CubeBlockPrefab == null || m_CylinderBlockPrefab == null || m_TriangleBlockPrefab == null)
            {
                Debug.LogWarning("StructurePreview: Some block prefabs are not assigned. Preview may not work correctly.");
            }
            #endif
        }
    }
}

