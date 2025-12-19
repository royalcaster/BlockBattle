using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace BlockBattle
{
    /// <summary>
    /// Beautiful gameplay HUD showing block indicators and animated accuracy progress bar.
    /// Blocks "light up" when present, and accuracy smoothly animates as you build.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BuildValidator m_BuildValidator;
        [SerializeField] private ReferenceStructureSpawner m_ReferenceSpawner;

        [Header("UI Elements")]
        [SerializeField] private RectTransform m_BlockIndicatorContainer;
        [SerializeField] private RectTransform m_ProgressBarFill;
        [SerializeField] private Image m_ProgressBarFillImage;
        [SerializeField] private TextMeshProUGUI m_PercentageText;

        [Header("Block Indicator Settings")]
        [SerializeField] private float m_IndicatorSize = 60f;
        [SerializeField] private float m_IndicatorSpacing = 10f;
        [SerializeField] private Color m_PresentColor = new Color(0.2f, 1f, 0.4f, 1f);
        [SerializeField] private Color m_CorrectColor = new Color(0.2f, 1f, 0.4f, 1f);
        [SerializeField] private Color m_MissingColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color m_WrongPositionColor = new Color(1f, 0.8f, 0.2f, 1f);

        [Header("Progress Bar Settings")]
        [SerializeField] private float m_ProgressAnimSpeed = 5f;
        [SerializeField] private Gradient m_ProgressGradient;

        [Header("Update Settings")]
        [SerializeField] private float m_UpdateInterval = 0.3f;

        [Header("Block Meshes (Optional - uses primitives if not assigned)")]
        [SerializeField] private Mesh m_CubeMesh;
        [SerializeField] private Mesh m_CylinderMesh;
        [SerializeField] private Mesh m_TriangleMesh;
        [SerializeField] private Mesh m_RectangleMesh;
        [SerializeField] private Mesh m_ArchMesh;

        // Runtime state
        private List<BlockIndicator> m_BlockIndicators = new List<BlockIndicator>();
        private float m_CurrentDisplayedAccuracy = 0f;
        private float m_TargetAccuracy = 0f;
        private float m_UpdateTimer = 0f;
        private BuildValidationResult m_LastResult;

        private class BlockIndicator
        {
            public GameObject Container;
            public GameObject MeshObject;
            public MeshRenderer MeshRenderer;
            public Material Material;
            public TextMeshProUGUI Label;
            public BlockType BlockType;
            public BlockColor BlockColor;
            public int ReferenceIndex;
            public bool IsActive;
        }

        // Cached meshes for each block type
        private Dictionary<BlockType, Mesh> m_BlockMeshes = new Dictionary<BlockType, Mesh>();

        private void Start()
        {
            // Find references
            if (m_BuildValidator == null)
                m_BuildValidator = FindObjectOfType<BuildValidator>();
            if (m_ReferenceSpawner == null)
                m_ReferenceSpawner = FindObjectOfType<ReferenceStructureSpawner>();

            // Auto-find fill image if not assigned but RectTransform is
            if (m_ProgressBarFillImage == null && m_ProgressBarFill != null)
                m_ProgressBarFillImage = m_ProgressBarFill.GetComponent<Image>();

            // Load block meshes
            LoadBlockMeshes();

            // Subscribe to structure changes
            if (m_ReferenceSpawner != null)
            {
                m_ReferenceSpawner.OnStructureSpawned += OnStructureChanged;
                if (m_ReferenceSpawner.CurrentSpawnConfiguration != null)
                {
                    OnStructureChanged(m_ReferenceSpawner.CurrentSpawnConfiguration);
                }
            }

            // Setup default gradient if not properly configured
            // Unity initializes Gradient with default white keys, so check if first key is white/default
            bool needsGradientSetup = m_ProgressGradient == null || 
                                       m_ProgressGradient.colorKeys.Length < 3 ||
                                       IsDefaultWhiteGradient(m_ProgressGradient);
            if (needsGradientSetup)
            {
                m_ProgressGradient = new Gradient();
                m_ProgressGradient.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0.9f, 0.2f, 0.2f), 0f),
                        new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0.4f),
                        new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.7f),
                        new GradientColorKey(new Color(0.2f, 1f, 0.4f), 1f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    }
                );
            }

            // Set initial progress bar color (after gradient is set up)
            if (m_ProgressBarFillImage != null)
                m_ProgressBarFillImage.color = m_ProgressGradient.Evaluate(0f);

            // Initialize with current config
            if (m_BuildValidator != null && m_BuildValidator.ReferenceConfiguration != null)
            {
                CreateBlockIndicators(m_BuildValidator.ReferenceConfiguration);
            }
        }

        private void OnDestroy()
        {
            if (m_ReferenceSpawner != null)
                m_ReferenceSpawner.OnStructureSpawned -= OnStructureChanged;

            // Clean up materials and mesh objects
            foreach (var indicator in m_BlockIndicators)
            {
                if (indicator.Material != null)
                    Destroy(indicator.Material);
                if (indicator.MeshObject != null)
                    Destroy(indicator.MeshObject);
            }
        }

        private void OnStructureChanged(BlockSpawnConfiguration config)
        {
            if (m_BuildValidator != null)
                m_BuildValidator.ReferenceConfiguration = config;
            CreateBlockIndicators(config);
        }

        private void Update()
        {
            // Update validation
            m_UpdateTimer += Time.deltaTime;
            if (m_UpdateTimer >= m_UpdateInterval && m_BuildValidator != null)
            {
                m_UpdateTimer = 0f;
                m_LastResult = m_BuildValidator.ValidateBuild();
                if (m_LastResult != null)
                {
                    m_TargetAccuracy = m_LastResult.AccuracyPercentage / 100f;
                    UpdateBlockIndicators(m_LastResult);
                }
            }

            // Animate progress bar
            AnimateProgressBar();
        }


        /// <summary>
        /// Creates block indicators for the given configuration.
        /// </summary>
        private void CreateBlockIndicators(BlockSpawnConfiguration config)
        {
            // Clear existing indicators and destroy materials/meshes
            foreach (var indicator in m_BlockIndicators)
            {
                if (indicator.Material != null)
                    Destroy(indicator.Material);
                if (indicator.MeshObject != null)
                    Destroy(indicator.MeshObject);
                if (indicator.Container != null)
                    Destroy(indicator.Container);
            }
            m_BlockIndicators.Clear();

            if (config == null || config.SpawnEntries == null || m_BlockIndicatorContainer == null)
                return;

            // Create indicator for each block
            for (int i = 0; i < config.SpawnEntries.Count; i++)
            {
                var entry = config.SpawnEntries[i];
                if (entry == null) continue;

                var indicator = CreateSingleIndicator(entry.BlockType, entry.BlockColor, i);
                m_BlockIndicators.Add(indicator);
            }

            // Center the indicators
            float totalWidth = m_BlockIndicators.Count * (m_IndicatorSize + m_IndicatorSpacing) - m_IndicatorSpacing;
            float startX = -totalWidth / 2f + m_IndicatorSize / 2f;

            for (int i = 0; i < m_BlockIndicators.Count; i++)
            {
                var rect = m_BlockIndicators[i].Container.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(startX + i * (m_IndicatorSize + m_IndicatorSpacing), 0);
            }
        }

        /// <summary>
        /// Loads mesh assets for all block types.
        /// Uses serialized meshes if assigned, falls back to primitives.
        /// </summary>
        private void LoadBlockMeshes()
        {
            m_BlockMeshes.Clear();
            
            // Use serialized meshes if assigned, otherwise fall back to primitives
            m_BlockMeshes[BlockType.Cube] = m_CubeMesh != null ? m_CubeMesh : GetPrimitiveMesh(PrimitiveType.Cube);
            m_BlockMeshes[BlockType.Cylinder] = m_CylinderMesh != null ? m_CylinderMesh : GetPrimitiveMesh(PrimitiveType.Cylinder);
            m_BlockMeshes[BlockType.Triangle] = m_TriangleMesh != null ? m_TriangleMesh : GetPrimitiveMesh(PrimitiveType.Cube);
            m_BlockMeshes[BlockType.Rectangle] = m_RectangleMesh != null ? m_RectangleMesh : GetPrimitiveMesh(PrimitiveType.Cube);
            m_BlockMeshes[BlockType.Arch] = m_ArchMesh != null ? m_ArchMesh : GetPrimitiveMesh(PrimitiveType.Cylinder);
            m_BlockMeshes[BlockType.BigTriangle] = m_TriangleMesh != null ? m_TriangleMesh : GetPrimitiveMesh(PrimitiveType.Cube); // Use triangle mesh for now
        }

        /// <summary>
        /// Gets a primitive mesh as fallback.
        /// </summary>
        private Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
            return mesh;
        }

        /// <summary>
        /// Creates a single block indicator with a 3D mesh.
        /// </summary>
        private BlockIndicator CreateSingleIndicator(BlockType blockType, BlockColor blockColor, int index)
        {
            var indicator = new BlockIndicator
            {
                BlockType = blockType,
                BlockColor = blockColor,
                ReferenceIndex = index,
                IsActive = false
            };

            // Container (uses RectTransform for positioning in canvas)
            indicator.Container = new GameObject($"BlockIndicator_{index}_{blockType}_{blockColor}");
            indicator.Container.transform.SetParent(m_BlockIndicatorContainer, false);

            RectTransform containerRect = indicator.Container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(m_IndicatorSize, m_IndicatorSize);

            // 3D Block - use CreatePrimitive, but replace mesh for triangles
            PrimitiveType primType = GetPrimitiveType(blockType);
            indicator.MeshObject = GameObject.CreatePrimitive(primType);
            indicator.MeshObject.name = $"BlockMesh_{index}_{blockType}";
            
            // Replace mesh for triangles
            if (blockType == BlockType.Triangle)
            {
                MeshFilter mf = indicator.MeshObject.GetComponent<MeshFilter>();
                mf.sharedMesh = CreateTriangleMesh();
            }
            
            // Remove collider (not needed for UI)
            Collider col = indicator.MeshObject.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            
            // Parent to container - will inherit canvas transform
            indicator.MeshObject.transform.SetParent(indicator.Container.transform, false);
            
            // Position above label, scale for visibility
            float baseScale = 25f;
            indicator.MeshObject.transform.localPosition = new Vector3(0, 30f, -80f);
            indicator.MeshObject.transform.localScale = GetMeshScale(blockType) * baseScale;
            indicator.MeshObject.transform.localRotation = Quaternion.Euler(15f, -30f, 0f);

            indicator.MeshRenderer = indicator.MeshObject.GetComponent<MeshRenderer>();
            
            // Create unlit material
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            
            indicator.Material = new Material(shader);
            indicator.Material.color = GetBlockDisplayColor(blockColor) * 0.35f; // Dim initially
            indicator.MeshRenderer.material = indicator.Material;
            indicator.MeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            indicator.MeshRenderer.receiveShadows = false;

            // Label below the block
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(indicator.Container.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 0.3f);
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = new Vector2(0, -5);
            indicator.Label = labelObj.AddComponent<TextMeshProUGUI>();
            indicator.Label.text = GetBlockShortName(blockType);
            indicator.Label.fontSize = 14;
            indicator.Label.alignment = TextAlignmentOptions.Center;
            indicator.Label.color = new Color(1, 1, 1, 0.5f);
            indicator.Label.raycastTarget = false;

            return indicator;
        }

        /// <summary>
        /// Gets the primitive type for a block type.
        /// </summary>
        private PrimitiveType GetPrimitiveType(BlockType blockType)
        {
            switch (blockType)
            {
                case BlockType.Cylinder:
                    return PrimitiveType.Cylinder;
                case BlockType.Triangle:
                    return PrimitiveType.Cube; // Will be replaced with triangle mesh
                case BlockType.BigTriangle:
                    return PrimitiveType.Cube; // Will be replaced with triangle mesh
                case BlockType.Arch:
                    return PrimitiveType.Cylinder;
                case BlockType.Rectangle:
                    return PrimitiveType.Cube;
                case BlockType.Cube:
                default:
                    return PrimitiveType.Cube;
            }
        }

        /// <summary>
        /// Creates a simple triangle prism mesh.
        /// </summary>
        private Mesh CreateTriangleMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "TrianglePrism";

            float h = 1f;    // height
            float w = 0.5f;  // half width
            float d = 0.3f;  // half depth

            Vector3[] vertices = new Vector3[]
            {
                // Front face
                new Vector3(-w, -h/2, -d),  // 0: bottom left
                new Vector3(w, -h/2, -d),   // 1: bottom right  
                new Vector3(0, h/2, -d),    // 2: top
                // Back face
                new Vector3(-w, -h/2, d),   // 3: bottom left
                new Vector3(w, -h/2, d),    // 4: bottom right
                new Vector3(0, h/2, d),     // 5: top
            };

            int[] triangles = new int[]
            {
                // Front
                0, 2, 1,
                // Back  
                3, 4, 5,
                // Bottom
                0, 1, 4, 0, 4, 3,
                // Left
                0, 3, 5, 0, 5, 2,
                // Right
                1, 2, 5, 1, 5, 4
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gets the appropriate scale for each block type mesh.
        /// </summary>
        private Vector3 GetMeshScale(BlockType blockType)
        {
            switch (blockType)
            {
                case BlockType.Rectangle:
                    return new Vector3(1.5f, 0.6f, 0.6f);
                case BlockType.Cylinder:
                    return new Vector3(0.8f, 0.8f, 0.8f);
                case BlockType.Triangle:
                    return new Vector3(1f, 1f, 0.8f);
                case BlockType.BigTriangle:
                    return new Vector3(2f, 1f, 0.8f); // Twice as wide
                case BlockType.Arch:
                    return new Vector3(1f, 0.8f, 0.6f);
                case BlockType.Cube:
                default:
                    return Vector3.one;
            }
        }

        /// <summary>
        /// Gets a short display name for block type.
        /// </summary>
        private string GetBlockShortName(BlockType type)
        {
            switch (type)
            {
                case BlockType.Cube: return "CUB";
                case BlockType.Cylinder: return "CYL";
                case BlockType.Triangle: return "TRI";
                case BlockType.Rectangle: return "REC";
                case BlockType.Arch: return "ARC";
                case BlockType.BigTriangle: return "BTRI";
                default: return "???";
            }
        }

        /// <summary>
        /// Gets the display color for a block color.
        /// </summary>
        private Color GetBlockDisplayColor(BlockColor color)
        {
            return BlockColorUtility.GetColor(color);
        }

        /// <summary>
        /// Checks if the gradient is Unity's default white gradient.
        /// </summary>
        private bool IsDefaultWhiteGradient(Gradient gradient)
        {
            if (gradient == null || gradient.colorKeys.Length == 0)
                return true;

            // Unity's default gradient is white to white
            Color firstColor = gradient.colorKeys[0].color;
            return firstColor.r > 0.95f && firstColor.g > 0.95f && firstColor.b > 0.95f;
        }

        /// <summary>
        /// Updates block indicators based on validation result.
        /// Instant state changes - no animation.
        /// </summary>
        private void UpdateBlockIndicators(BuildValidationResult result)
        {
            if (result == null || result.BlockResults == null) return;

            for (int i = 0; i < m_BlockIndicators.Count && i < result.BlockResults.Count; i++)
            {
                var indicator = m_BlockIndicators[i];
                var blockResult = result.BlockResults[i];

                Color blockColor = GetBlockDisplayColor(indicator.BlockColor);
                
                // Determine new state
                bool isPresent = blockResult.IsPresent;
                bool isCorrect = blockResult.IsCorrect;

                // Only update if state changed to avoid unnecessary work
                bool newActiveState = isPresent;
                if (newActiveState != indicator.IsActive || !indicator.IsActive)
                {
                    indicator.IsActive = newActiveState;
                    
                    if (!isPresent)
                    {
                        // Missing - dim
                        indicator.Material.color = blockColor * 0.35f;
                        indicator.Label.color = new Color(1, 1, 1, 0.4f);
                        
                        // Smaller scale for inactive
                        if (indicator.MeshObject != null)
                        {
                            indicator.MeshObject.transform.localScale = GetMeshScale(indicator.BlockType) * 22f;
                        }
                    }
                    else if (isCorrect)
                    {
                        // Correct - bright with slight green tint
                        Color correctColor = Color.Lerp(blockColor, m_CorrectColor, 0.25f) * 1.3f;
                        correctColor.a = 1f;
                        indicator.Material.color = correctColor;
                        indicator.Label.color = m_CorrectColor;
                        
                        // Larger scale for active
                        if (indicator.MeshObject != null)
                        {
                            indicator.MeshObject.transform.localScale = GetMeshScale(indicator.BlockType) * 32f;
                        }
                    }
                    else
                    {
                        // Present but wrong position - yellow tint
                        Color wrongColor = Color.Lerp(blockColor, m_WrongPositionColor, 0.35f);
                        wrongColor.a = 1f;
                        indicator.Material.color = wrongColor;
                        indicator.Label.color = m_WrongPositionColor;
                        
                        // Medium scale
                        if (indicator.MeshObject != null)
                        {
                            indicator.MeshObject.transform.localScale = GetMeshScale(indicator.BlockType) * 28f;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Animates the progress bar smoothly.
        /// </summary>
        private void AnimateProgressBar()
        {
            // Smooth animation toward target
            m_CurrentDisplayedAccuracy = Mathf.Lerp(
                m_CurrentDisplayedAccuracy,
                m_TargetAccuracy,
                Time.deltaTime * m_ProgressAnimSpeed
            );

            // Snap if very close
            if (Mathf.Abs(m_CurrentDisplayedAccuracy - m_TargetAccuracy) < 0.001f)
                m_CurrentDisplayedAccuracy = m_TargetAccuracy;

            // Update fill by modifying the RectTransform anchor
            if (m_ProgressBarFill != null)
            {
                // Scale the fill bar by adjusting anchorMax.x (0 = empty, 1 = full)
                Vector2 anchorMax = m_ProgressBarFill.anchorMax;
                anchorMax.x = Mathf.Clamp01(m_CurrentDisplayedAccuracy);
                m_ProgressBarFill.anchorMax = anchorMax;

                // Update color based on progress
                if (m_ProgressBarFillImage != null)
                {
                    m_ProgressBarFillImage.color = m_ProgressGradient.Evaluate(m_CurrentDisplayedAccuracy);
                }
            }

            // Update percentage text
            if (m_PercentageText != null)
            {
                int displayPercent = Mathf.RoundToInt(m_CurrentDisplayedAccuracy * 100f);
                m_PercentageText.text = $"{displayPercent}%";
                m_PercentageText.color = m_ProgressGradient.Evaluate(m_CurrentDisplayedAccuracy);
            }
        }

        /// <summary>
        /// Resets the HUD to initial state.
        /// </summary>
        public void ResetHUD()
        {
            m_CurrentDisplayedAccuracy = 0f;
            m_TargetAccuracy = 0f;
            
            // Reset progress bar
            if (m_ProgressBarFill != null)
            {
                Vector2 anchorMax = m_ProgressBarFill.anchorMax;
                anchorMax.x = 0f;
                m_ProgressBarFill.anchorMax = anchorMax;
            }
            
            // Reset all block indicators to inactive state
            foreach (var indicator in m_BlockIndicators)
            {
                indicator.IsActive = false;
                if (indicator.Material != null)
                {
                    Color blockColor = GetBlockDisplayColor(indicator.BlockColor);
                    indicator.Material.color = blockColor * 0.3f;
                }
                if (indicator.Label != null)
                    indicator.Label.color = new Color(1, 1, 1, 0.3f);
                indicator.Container.transform.localScale = Vector3.one;
            }
        }
    }
}


