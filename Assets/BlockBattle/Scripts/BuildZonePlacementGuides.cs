using System.Collections.Generic;
using UnityEngine;

namespace BlockBattle
{
    /// <summary>
    /// Shows visual placement guides on the build zone floor, indicating where each block should be placed.
    /// This removes rotation ambiguity by showing the user exactly where to build.
    /// </summary>
    public class BuildZonePlacementGuides : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Reference to the BuildValidator to get the reference configuration")]
        private BuildValidator m_BuildValidator;

        [SerializeField, Tooltip("Reference to the BuildZone")]
        private BuildZone m_BuildZone;

        [Header("Guide Appearance")]
        [SerializeField, Tooltip("Height of the guide markers above the floor")]
        private float m_GuideHeight = 0.005f;

        [SerializeField, Tooltip("Scale factor for guide markers (relative to block size)")]
        private float m_GuideScale = 0.8f;

        [SerializeField, Tooltip("Opacity of guide markers")]
        [Range(0.1f, 1f)]
        private float m_GuideOpacity = 0.5f;

        [SerializeField, Tooltip("Show block type labels")]
        private bool m_ShowLabels = false;

        [Header("Colors")]
        [SerializeField]
        private Color m_UnfilledGuideColor = new Color(1f, 1f, 1f, 0.3f);
        
        [SerializeField]
        private Color m_FilledGuideColor = new Color(0f, 1f, 0f, 0.5f);

        // Runtime data
        private List<PlacementGuide> m_Guides = new List<PlacementGuide>();
        private GameObject m_GuidesContainer;
        private bool m_Initialized = false;

        /// <summary>
        /// Data for a single placement guide.
        /// </summary>
        private class PlacementGuide
        {
            public GameObject MarkerObject;
            public MeshRenderer Renderer;
            public Material Material;
            public BlockType BlockType;
            public BlockColor BlockColor;
            public Vector3 LocalPosition;
            public Vector3 ExpectedScale;
            public bool IsFilled;
        }

        private void Start()
        {
            if (m_BuildValidator == null)
            {
                m_BuildValidator = FindObjectOfType<BuildValidator>();
            }
            if (m_BuildZone == null)
            {
                m_BuildZone = FindObjectOfType<BuildZone>();
            }

            CreateGuides();
        }

        private void Update()
        {
            if (!m_Initialized) return;
            UpdateGuideStates();
        }

        /// <summary>
        /// Creates visual guides for each block in the reference configuration.
        /// </summary>
        private void CreateGuides()
        {
            if (m_BuildValidator == null || m_BuildValidator.ReferenceConfiguration == null)
            {
                Debug.LogWarning("BuildZonePlacementGuides: No BuildValidator or ReferenceConfiguration found");
                return;
            }

            // Clean up existing guides
            if (m_GuidesContainer != null)
            {
                Destroy(m_GuidesContainer);
            }

            m_Guides.Clear();
            m_GuidesContainer = new GameObject("PlacementGuides");
            m_GuidesContainer.transform.SetParent(transform, false);

            var config = m_BuildValidator.ReferenceConfiguration;
            var entries = config.SpawnEntries;
            if (entries == null || entries.Count == 0) return;

            // Calculate reference center (same as BuildValidator)
            Vector3 referenceCenter = Vector3.zero;
            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    referenceCenter += entry.Position;
                }
            }
            referenceCenter /= entries.Count;

            // Get build zone position
            Vector3 buildZonePos = m_BuildZone != null ? m_BuildZone.transform.position : transform.position;

            // Create a guide for each block
            foreach (var entry in entries)
            {
                if (entry == null) continue;

                // Calculate world position for this guide
                Vector3 relativePos = entry.Position - referenceCenter;
                Vector3 guideWorldPos = buildZonePos + relativePos;
                guideWorldPos.y = buildZonePos.y + m_GuideHeight; // Place on floor

                // Create the guide marker
                PlacementGuide guide = CreateGuideMarker(entry, guideWorldPos);
                m_Guides.Add(guide);
            }

            m_Initialized = true;
            Debug.Log($"BuildZonePlacementGuides: Created {m_Guides.Count} placement guides");
        }

        /// <summary>
        /// Creates a single guide marker for a block entry.
        /// Shows the FOOTPRINT (base) of each block on the floor.
        /// </summary>
        private PlacementGuide CreateGuideMarker(BlockSpawnEntry entry, Vector3 worldPosition)
        {
            PlacementGuide guide = new PlacementGuide
            {
                BlockType = entry.BlockType,
                BlockColor = entry.BlockColor,
                LocalPosition = worldPosition - (m_BuildZone != null ? m_BuildZone.transform.position : transform.position)
            };

            // Check if the block is standing upright (rotated around X or Z)
            Vector3 eulerRot = entry.Rotation.eulerAngles;
            bool isStandingUpright = IsBlockStandingUpright(eulerRot);

            // Determine shape and size based on block type and orientation
            // We show the FOOTPRINT (base) on the floor, not the full block shape
            GameObject marker;
            Vector3 scale;
            float yRotation = eulerRot.y; // Only apply Y rotation to floor marker

            switch (entry.BlockType)
            {
                case BlockType.Cube:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    scale = new Vector3(0.1f, 0.02f, 0.1f) * m_GuideScale; // Square footprint
                    break;
                    
                case BlockType.Cylinder:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    scale = new Vector3(0.1f, 0.01f, 0.1f) * m_GuideScale; // Circular footprint
                    break;
                    
                case BlockType.Rectangle:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    if (isStandingUpright)
                    {
                        // Standing upright: small square footprint (the short end)
                        scale = new Vector3(0.1f, 0.02f, 0.05f) * m_GuideScale;
                    }
                    else
                    {
                        // Lying flat: rectangular footprint
                        scale = new Vector3(0.2f, 0.02f, 0.1f) * m_GuideScale;
                    }
                    break;
                    
                case BlockType.Triangle:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    if (isStandingUpright)
                    {
                        // Standing upright: thin rectangular footprint
                        scale = new Vector3(0.1f, 0.02f, 0.05f) * m_GuideScale;
                    }
                    else
                    {
                        // Lying flat: triangular base (approximated as rectangle)
                        scale = new Vector3(0.1f, 0.02f, 0.1f) * m_GuideScale;
                    }
                    break;
                    
                case BlockType.Arch:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    scale = new Vector3(0.15f, 0.02f, 0.1f) * m_GuideScale;
                    break;
                    
                default:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    scale = new Vector3(0.1f, 0.02f, 0.1f) * m_GuideScale;
                    break;
            }

            marker.name = $"Guide_{entry.BlockType}_{entry.BlockColor}";
            marker.transform.SetParent(m_GuidesContainer.transform, false);
            marker.transform.position = worldPosition;
            
            // Apply only Y rotation (horizontal orientation on floor)
            marker.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            
            marker.transform.localScale = scale;
            guide.ExpectedScale = scale;

            // Remove collider
            Collider col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Create material with block color tint
            guide.Renderer = marker.GetComponent<MeshRenderer>();
            guide.Material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            
            // Tint with block color
            Color blockColor = BlockColorUtility.GetColor(entry.BlockColor);
            Color guideColor = Color.Lerp(m_UnfilledGuideColor, blockColor, 0.5f);
            guideColor.a = m_GuideOpacity;
            
            // Make it transparent
            guide.Material.SetFloat("_Surface", 1);
            guide.Material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            guide.Material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            guide.Material.SetInt("_ZWrite", 0);
            guide.Material.EnableKeyword("_ALPHABLEND_ON");
            guide.Material.renderQueue = 3000;
            guide.Material.color = guideColor;
            
            guide.Renderer.material = guide.Material;
            guide.MarkerObject = marker;
            guide.IsFilled = false;

            return guide;
        }

        /// <summary>
        /// Checks if a block is standing upright based on its rotation.
        /// A block is upright if it's rotated significantly around X or Z axis.
        /// </summary>
        private bool IsBlockStandingUpright(Vector3 eulerAngles)
        {
            // Normalize angles to -180 to 180 range
            float xRot = eulerAngles.x;
            float zRot = eulerAngles.z;
            
            if (xRot > 180) xRot -= 360;
            if (zRot > 180) zRot -= 360;
            
            // Check if rotated ~90 degrees around X or Z (standing up)
            bool rotatedAroundX = Mathf.Abs(Mathf.Abs(xRot) - 90) < 15f;
            bool rotatedAroundZ = Mathf.Abs(Mathf.Abs(zRot) - 90) < 15f;
            
            return rotatedAroundX || rotatedAroundZ;
        }

        /// <summary>
        /// Updates guide visual states based on which blocks have been placed.
        /// </summary>
        private void UpdateGuideStates()
        {
            if (m_BuildValidator == null) return;

            var result = m_BuildValidator.ValidateBuild();
            if (result == null || result.BlockResults == null) return;

            // Match guide indices to block results
            for (int i = 0; i < m_Guides.Count && i < result.BlockResults.Count; i++)
            {
                var guide = m_Guides[i];
                var blockResult = result.BlockResults[i];

                bool wasFilled = guide.IsFilled;
                guide.IsFilled = blockResult.IsPresent && blockResult.IsCorrect;

                // Update color if state changed
                if (guide.IsFilled != wasFilled)
                {
                    Color targetColor;
                    if (guide.IsFilled)
                    {
                        targetColor = m_FilledGuideColor;
                    }
                    else
                    {
                        Color blockColor = BlockColorUtility.GetColor(guide.BlockColor);
                        targetColor = Color.Lerp(m_UnfilledGuideColor, blockColor, 0.5f);
                    }
                    targetColor.a = m_GuideOpacity;
                    guide.Material.color = targetColor;
                }
            }
        }

        /// <summary>
        /// Recreates guides (call after configuration change).
        /// </summary>
        public void RefreshGuides()
        {
            m_Initialized = false;
            CreateGuides();
        }

        private void OnDestroy()
        {
            // Clean up materials
            foreach (var guide in m_Guides)
            {
                if (guide.Material != null)
                {
                    Destroy(guide.Material);
                }
            }
            m_Guides.Clear();

            if (m_GuidesContainer != null)
            {
                Destroy(m_GuidesContainer);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw guide positions in editor
            if (m_BuildValidator == null || m_BuildValidator.ReferenceConfiguration == null) return;

            var config = m_BuildValidator.ReferenceConfiguration;
            var entries = config.SpawnEntries;
            if (entries == null) return;

            Vector3 referenceCenter = Vector3.zero;
            foreach (var entry in entries)
            {
                if (entry != null) referenceCenter += entry.Position;
            }
            if (entries.Count > 0) referenceCenter /= entries.Count;

            Vector3 basePos = transform.position;

            foreach (var entry in entries)
            {
                if (entry == null) continue;

                Vector3 relPos = entry.Position - referenceCenter;
                Vector3 worldPos = basePos + relPos;
                worldPos.y = basePos.y + 0.01f;

                Gizmos.color = BlockColorUtility.GetColor(entry.BlockColor);
                Gizmos.DrawWireCube(worldPos, new Vector3(0.1f, 0.02f, 0.1f));
            }
        }
    }
}

