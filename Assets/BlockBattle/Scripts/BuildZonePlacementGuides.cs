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

        [SerializeField, Tooltip("Scale factor for guide markers (relative to block size). Set to 1.0 for exact fit.")]
        private float m_GuideScale = 1.0f; // Changed default to 1.0 for exact footprint match

        [SerializeField, Tooltip("Opacity of guide markers")]
        [Range(0.1f, 1f)]
        private float m_GuideOpacity = 0.5f;

        [SerializeField, Tooltip("Maximum height above ground level to show guides (blocks above this are considered stacked)")]
        private float m_MaxGroundLevelHeight = 0.08f; // Only show guides for blocks at ground level

        [SerializeField, Tooltip("Additional rotation offset applied to all guide markers (in degrees)")]
        private float m_RotationOffset = 0f;

        [Header("Colors")]
        [SerializeField]
        private Color m_UnfilledGuideColor = new Color(1f, 1f, 1f, 0.3f);
        
        [SerializeField]
        private Color m_FilledGuideColor = new Color(0f, 1f, 0f, 0.5f);

        [Header("Duplicate Prevention")]
        [SerializeField, Tooltip("Minimum XZ distance between guide centers. Guides closer than this to an existing guide will be skipped.")]
        private float m_MinGuideSpacing = 0.05f; // 5cm minimum spacing to prevent visual overlap

        [Header("Material Reference (for builds)")]
        [SerializeField, Tooltip("Reference material for guides - assign a basic unlit transparent material")]
        private Material m_GuideMaterialSource;

        // Runtime data
        private List<PlacementGuide> m_Guides = new List<PlacementGuide>();
        private GameObject m_GuidesContainer;
        private bool m_Initialized = false;
        private ReferenceStructureSpawner m_ReferenceSpawner;
        private Shader m_CachedUnlitShader;

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
            public Vector3 ExpectedRelativePosition; // Expected position relative to build center (for matching)
            public Vector3 ExpectedScale;
            public bool IsFilled;
        }

        private void Start()
        {
            if (m_BuildValidator == null)
            {
                m_BuildValidator = FindAnyObjectByType<BuildValidator>();
            }
            if (m_BuildZone == null)
            {
                m_BuildZone = FindAnyObjectByType<BuildZone>();
            }

            // Subscribe to structure spawn events to update guides when structure changes
            if (m_ReferenceSpawner == null)
            {
                m_ReferenceSpawner = FindAnyObjectByType<ReferenceStructureSpawner>();
            }
            if (m_ReferenceSpawner != null)
            {
                m_ReferenceSpawner.OnStructureSpawned += OnStructureSpawned;
            }

            CreateGuides();
        }

        /// <summary>
        /// Called when a new reference structure is spawned. Refreshes the guides.
        /// </summary>
        private void OnStructureSpawned(BlockSpawnConfiguration config)
        {
            // Update validator's reference configuration if needed
            if (m_BuildValidator != null && config != null)
            {
                m_BuildValidator.ReferenceConfiguration = config;
            }
            
            // Refresh guides for the new structure
            RefreshGuides();
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

            // Prevent duplicate creation - if already initialized and container exists, skip
            if (m_Initialized && m_GuidesContainer != null)
            {
                Debug.LogWarning("BuildZonePlacementGuides: Guides already created. Call RefreshGuides() to recreate.");
                return;
            }

            // Clean up existing guides immediately
            if (m_GuidesContainer != null)
            {
                // Clean up materials first
                foreach (var guide in m_Guides)
                {
                    if (guide != null && guide.Material != null)
                    {
                        Destroy(guide.Material);
                    }
                }
                
                // Destroy container immediately in editor, deferred in play mode
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(m_GuidesContainer);
                }
                else
                {
                    Destroy(m_GuidesContainer);
                }
                #else
                Destroy(m_GuidesContainer);
                #endif
                m_GuidesContainer = null;
            }

            m_Guides.Clear();
            
            var config = m_BuildValidator.ReferenceConfiguration;
            var entries = config.SpawnEntries;
            if (entries == null || entries.Count == 0) return;

            // Calculate reference center (same as BuildValidator)
            Vector3 referenceCenter = Vector3.zero;
            int validEntryCount = 0;
            foreach (var entry in entries)
            {
                if (entry != null && IsValidVector(entry.Position))
                {
                    referenceCenter += entry.Position;
                    validEntryCount++;
                }
            }
            
            if (validEntryCount == 0)
            {
                Debug.LogWarning("BuildZonePlacementGuides: No valid entries found, cannot calculate reference center");
                return;
            }
            
            referenceCenter /= validEntryCount;
            
            // Validate reference center
            if (!IsValidVector(referenceCenter))
            {
                Debug.LogWarning("BuildZonePlacementGuides: Invalid reference center calculated, using zero");
                referenceCenter = Vector3.zero;
            }

            // Get build zone position
            Vector3 buildZonePos = m_BuildZone != null ? m_BuildZone.transform.position : transform.position;
            
            // Validate build zone position
            if (!IsValidVector(buildZonePos))
            {
                Debug.LogWarning("BuildZonePlacementGuides: Invalid build zone position, using zero");
                buildZonePos = Vector3.zero;
            }
            
            // Create container centered at build zone position so rotation works correctly
            m_GuidesContainer = new GameObject("PlacementGuides");
            m_GuidesContainer.transform.SetParent(transform, false); // Use local relative position
            m_GuidesContainer.transform.localPosition = Vector3.zero;
            m_GuidesContainer.transform.localRotation = Quaternion.Euler(0, m_RotationOffset, 0);

            // Get structure scale and height offset from BuildValidator (if available)
            float structureScale = 1.0f;
            float heightOffset = 0f;
            if (m_BuildValidator != null)
            {
                structureScale = m_BuildValidator.StructureScale;
                heightOffset = m_BuildValidator.HeightOffset;
            }
            
            // Validate scale and offset values
            if (float.IsNaN(structureScale) || float.IsInfinity(structureScale) || structureScale <= 0f)
            {
                Debug.LogWarning($"BuildZonePlacementGuides: Invalid structure scale {structureScale}, using 1.0");
                structureScale = 1.0f;
            }
            
            if (float.IsNaN(heightOffset) || float.IsInfinity(heightOffset))
            {
                Debug.LogWarning($"BuildZonePlacementGuides: Invalid height offset {heightOffset}, using 0.0");
                heightOffset = 0f;
            }
            
            // Apply same single-block adjustment as BuildValidator for consistency
            // Single-block structures at Y=0 need a smaller height offset since the block sits directly on floor
            if (entries.Count == 1 && Mathf.Abs(referenceCenter.y) < 0.01f)
            {
                heightOffset = 0.05f;
                Debug.Log($"BuildZonePlacementGuides: Single-block structure at Y=0, using adjusted height offset: {heightOffset}m");
            }

            // Find ground level (lowest Y position in reference structure)
            float groundLevelY = float.MaxValue;
            foreach (var entry in entries)
            {
                if (entry != null && entry.Position.y < groundLevelY)
                {
                    groundLevelY = entry.Position.y;
                }
            }

            // Create a guide for each block (only for ground-level blocks)
            // Track XZ positions to detect potential overlaps (guides are on the floor, so only XZ matters)
            List<Vector2> createdXZPositions = new List<Vector2>();
            
            foreach (var entry in entries)
            {
                if (entry == null) continue;

                // Skip stacked blocks (blocks significantly above ground level)
                float blockYRelativeToGround = entry.Position.y - groundLevelY;
                if (blockYRelativeToGround > m_MaxGroundLevelHeight)
                {
                    Debug.Log($"BuildZonePlacementGuides: Skipping guide for stacked block {entry.BlockType} at Y={entry.Position.y} (ground level: {groundLevelY})");
                    continue;
                }

                // Validate entry position
                if (!IsValidVector(entry.Position))
                {
                    Debug.LogWarning($"BuildZonePlacementGuides: Invalid position for {entry.BlockType} ({entry.BlockColor}), skipping");
                    continue;
                }

                // Calculate local position for this guide (relative to build zone center)
                Vector3 relativePos = (entry.Position - referenceCenter) * structureScale;
                relativePos.y = m_GuideHeight; // Place on floor (Y is height above build zone)
                
                // Validate relative position
                if (!IsValidVector(relativePos))
                {
                    Debug.LogWarning($"BuildZonePlacementGuides: Invalid relative position calculated for {entry.BlockType} ({entry.BlockColor}), skipping");
                    continue;
                }

                // Check for XZ position overlap with existing guides (Y doesn't matter since all guides are on floor)
                Vector2 xzPos = new Vector2(relativePos.x, relativePos.z);
                bool isTooClose = false;
                foreach (var existingXZ in createdXZPositions)
                {
                    float xzDistance = Vector2.Distance(xzPos, existingXZ);
                    if (xzDistance < m_MinGuideSpacing)
                    {
                        Debug.Log($"BuildZonePlacementGuides: Guide for {entry.BlockType} ({entry.BlockColor}) at XZ=({xzPos.x:F3}, {xzPos.y:F3}) is too close to existing guide (distance: {xzDistance:F3}m < {m_MinGuideSpacing}m). Skipping to prevent overlap.");
                        isTooClose = true;
                        break;
                    }
                }
                
                if (isTooClose)
                {
                    continue;
                }
                
                createdXZPositions.Add(xzPos);

                // Create the guide marker using local position
                PlacementGuide guide = CreateGuideMarker(entry, relativePos, structureScale);
                if (guide != null)
                {
                    guide.ExpectedRelativePosition = relativePos; // Store expected relative position for matching
                    m_Guides.Add(guide);
                    Debug.Log($"BuildZonePlacementGuides: Created guide for {entry.BlockType} ({entry.BlockColor}) at local pos {relativePos}");
                }
                else
                {
                    Debug.LogWarning($"BuildZonePlacementGuides: Failed to create guide marker for {entry.BlockType} ({entry.BlockColor})");
                }
            }

            m_Initialized = true;
            Debug.Log($"BuildZonePlacementGuides: Created {m_Guides.Count} placement guides");
        }

        /// <summary>
        /// Creates a single guide marker for a block entry.
        /// Shows the FOOTPRINT (base) of each block on the floor.
        /// Uses actual block dimensions and accounts for rotation.
        /// </summary>
        /// <param name="entry">The block spawn entry to create a guide for.</param>
        /// <param name="localPosition">Local position relative to the guides container (build zone center).</param>
        /// <param name="structureScale">Scale factor for the structure.</param>
        private PlacementGuide CreateGuideMarker(BlockSpawnEntry entry, Vector3 localPosition, float structureScale)
        {
            // Validate inputs
            if (entry == null)
            {
                Debug.LogWarning("BuildZonePlacementGuides: Cannot create guide marker for null entry");
                return null;
            }
            
            if (!IsValidVector(localPosition))
            {
                Debug.LogWarning($"BuildZonePlacementGuides: Cannot create guide marker at invalid position {localPosition}");
                return null;
            }
            
            if (float.IsNaN(structureScale) || float.IsInfinity(structureScale) || structureScale <= 0f)
            {
                Debug.LogWarning($"BuildZonePlacementGuides: Invalid structure scale {structureScale} for guide marker");
                structureScale = 1.0f;
            }
            
            PlacementGuide guide = new PlacementGuide
            {
                BlockType = entry.BlockType,
                BlockColor = entry.BlockColor,
                LocalPosition = localPosition
            };

            // Actual block dimensions (from BlockBattleBlockCreator.cs):
            // Cube: 0.1m × 0.1m × 0.1m
            // Cylinder: radius 0.05m, height 0.1m (diameter 0.1m)
            // Rectangle: 0.2m × 0.05m × 0.05m (long × wide × tall)
            // Triangle: 0.1m base × 0.1m height
            // Arch: ~0.1m × 0.1m × 0.05m

            // Check if the block is standing upright (rotated around X or Z)
            Vector3 eulerRot = entry.Rotation.eulerAngles;
            bool isStandingUpright = IsBlockStandingUpright(eulerRot);

            // Determine shape and size based on block type and orientation
            // We show the FOOTPRINT (base) on the floor, matching exactly the bottom face of the block
            GameObject marker;
            Vector3 footprintXZ; // X and Z dimensions of the footprint (Y is height, set separately)
            float yRotation = eulerRot.y; // Only apply Y rotation to floor marker

            switch (entry.BlockType)
            {
                case BlockType.Cube:
                    // Cube: 0.1m × 0.1m footprint
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    footprintXZ = new Vector2(0.1f, 0.1f) * structureScale;
                    break;
                    
                case BlockType.Cylinder:
                    // Cylinder: diameter 0.1m (radius 0.05m)
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    footprintXZ = new Vector2(0.1f, 0.1f) * structureScale; // Circular footprint
                    break;
                    
                case BlockType.Rectangle:
                    // Rectangle: 0.2m × 0.05m × 0.05m (long × wide × tall)
                    // The mesh is created with long dimension along X, wide along Z, tall along Y
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    if (isStandingUpright)
                    {
                        // Standing upright: footprint is the short end (0.05m × 0.05m)
                        footprintXZ = new Vector2(0.05f, 0.05f) * structureScale;
                    }
                    else
                    {
                        // Lying flat: footprint is 0.2m × 0.05m
                        // Long dimension (0.2m) is along local X, wide (0.05m) along local Z
                        footprintXZ = new Vector2(0.2f, 0.05f) * structureScale;
                    }
                    break;
                    
                case BlockType.Triangle:
                    // Triangle: 0.1m base × 0.1m height
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    if (isStandingUpright)
                    {
                        // Standing upright: thin edge footprint (~0.05m width)
                        footprintXZ = new Vector2(0.1f, 0.05f) * structureScale;
                    }
                    else
                    {
                        // Lying flat: triangular base (equilateral triangle, ~0.1m × 0.087m)
                        footprintXZ = new Vector2(0.1f, 0.087f) * structureScale;
                    }
                    break;
                    
                case BlockType.BigTriangle:
                    // Big Triangle: 0.2m base × 0.1m height
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    if (isStandingUpright)
                    {
                        // Standing upright: thin edge footprint (~0.05m width)
                        footprintXZ = new Vector2(0.2f, 0.05f) * structureScale;
                    }
                    else
                    {
                        // Lying flat: triangular base (~0.2m × 0.173m for equilateral)
                        footprintXZ = new Vector2(0.2f, 0.173f) * structureScale;
                    }
                    break;
                    
                case BlockType.Arch:
                    // Arch: ~0.1m × 0.1m × 0.05m
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    footprintXZ = new Vector2(0.1f, 0.1f) * structureScale;
                    break;
                    
                default:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    footprintXZ = new Vector2(0.1f, 0.1f) * structureScale;
                    break;
            }

            // Apply guide scale (default 1.0 for exact fit) and set height
            Vector3 scale = new Vector3(footprintXZ.x * m_GuideScale, 0.02f, footprintXZ.y * m_GuideScale);

            marker.name = $"Guide_{entry.BlockType}_{entry.BlockColor}";
            marker.transform.SetParent(m_GuidesContainer.transform, false);
            
            // Use local position so the container's rotation affects all guides as a group
            marker.transform.localPosition = localPosition;
            
            // Apply Y rotation from the block's original rotation (local rotation)
            marker.transform.localRotation = Quaternion.Euler(0, yRotation, 0);
            
            marker.transform.localScale = scale;
            guide.ExpectedScale = scale;

            // Remove collider
            Collider col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Create material with block color tint
            guide.Renderer = marker.GetComponent<MeshRenderer>();
            guide.Material = CreateGuideMaterial();
            
            if (guide.Material == null)
            {
                Debug.LogError("BuildZonePlacementGuides: Failed to create guide material - shader not found in build");
                Destroy(marker);
                return null;
            }
            
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
        /// Creates a material for guide markers with proper fallback for builds.
        /// Uses serialized material reference if available, then loads from Resources, then tries Shader.Find.
        /// </summary>
        private Material CreateGuideMaterial()
        {
            // Priority 1: Use serialized material source (most reliable for builds)
            if (m_GuideMaterialSource != null)
            {
                return new Material(m_GuideMaterialSource);
            }

            // Priority 2: Load GuideMaterial from Resources folder (works in builds)
            Material guideMat = Resources.Load<Material>("GuideMaterial");
            if (guideMat != null)
            {
                m_GuideMaterialSource = guideMat; // Cache for future use
                return new Material(guideMat);
            }

            // Priority 3: Use cached shader if we found one before
            if (m_CachedUnlitShader != null)
            {
                return new Material(m_CachedUnlitShader);
            }

            // Priority 4: Try to find URP Unlit shader
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                m_CachedUnlitShader = shader;
                return new Material(shader);
            }

            // Priority 5: Try legacy Unlit/Color shader (more likely to be included)
            shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                m_CachedUnlitShader = shader;
                return new Material(shader);
            }

            // Priority 6: Last resort - use Sprites/Default which is always included
            shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                m_CachedUnlitShader = shader;
                return new Material(shader);
            }

            Debug.LogError("BuildZonePlacementGuides: Could not find any suitable shader for guide materials");
            return null;
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
        /// Matches guides to block results by expected position, not by index.
        /// This allows identical blocks (same type/color) to be placed interchangeably.
        /// </summary>
        private void UpdateGuideStates()
        {
            if (m_BuildValidator == null) return;

            var result = m_BuildValidator.ValidateBuild();
            if (result == null || result.BlockResults == null) return;

            // Reset all guides to unfilled state first
            foreach (var guide in m_Guides)
            {
                guide.IsFilled = false;
            }

            // Match guides to block results by comparing expected positions
            // This allows identical blocks to be placed interchangeably
            const float positionMatchTolerance = 0.01f; // 1cm tolerance for position matching
            
            foreach (var blockResult in result.BlockResults)
            {
                if (blockResult == null) continue;

                // Find the guide that matches this block result's expected position
                PlacementGuide matchingGuide = null;
                float bestDistance = float.MaxValue;

                foreach (var guide in m_Guides)
                {
                    // Check if type and color match
                    if (guide.BlockType != blockResult.BlockType || guide.BlockColor != blockResult.BlockColor)
                        continue;

                    // Check if expected positions match (within tolerance)
                    float distance = Vector3.Distance(guide.ExpectedRelativePosition, blockResult.ExpectedRelativePosition);
                    if (distance < positionMatchTolerance && distance < bestDistance)
                    {
                        bestDistance = distance;
                        matchingGuide = guide;
                    }
                }

                // Update the matching guide's state
                if (matchingGuide != null)
                {
                    matchingGuide.IsFilled = blockResult.IsPresent && blockResult.IsCorrect;
                }
            }

            // Update colors for all guides based on their current state
            // This ensures guides turn back to unfilled color when blocks are removed
            foreach (var guide in m_Guides)
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
            // Unsubscribe from events
            if (m_ReferenceSpawner != null)
            {
                m_ReferenceSpawner.OnStructureSpawned -= OnStructureSpawned;
            }
            
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

        /// <summary>
        /// Checks if a Vector3 has valid (finite, non-NaN) values.
        /// </summary>
        private bool IsValidVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
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


