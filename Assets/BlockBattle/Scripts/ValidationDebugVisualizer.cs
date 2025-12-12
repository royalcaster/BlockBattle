using UnityEngine;
using System.Collections.Generic;

namespace BlockBattle
{
    /// <summary>
    /// Visualizes expected block positions for debugging validation.
    /// Shows wireframe markers at expected positions and lines connecting to actual placed blocks.
    /// Works in both editor (gizmos) and runtime (LineRenderers).
    /// </summary>
    public class ValidationDebugVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BuildValidator m_BuildValidator;
        [SerializeField] private BuildZone m_BuildZone;

        [Header("Visualization Settings")]
        [SerializeField] private bool m_ShowVisualization = true;
        [SerializeField] private bool m_ShowExpectedPositions = true;
        [SerializeField] private bool m_ShowConnectionLines = true;
        [SerializeField] private bool m_ShowToleranceSpheres = true;
        [SerializeField] private bool m_ShowBuildCenter = true;

        [Header("Visual Style")]
        [SerializeField] private Color m_CorrectColor = new Color(0.2f, 0.9f, 0.2f, 0.8f);
        [SerializeField] private Color m_PartialColor = new Color(1f, 0.8f, 0.2f, 0.8f);
        [SerializeField] private Color m_IncorrectColor = new Color(0.9f, 0.3f, 0.2f, 0.8f);
        [SerializeField] private Color m_MissingColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color m_ToleranceColor = new Color(0.3f, 0.8f, 0.3f, 0.15f);
        [SerializeField] private Color m_CenterColor = Color.cyan;

        [Header("Marker Settings")]
        [SerializeField] private float m_MarkerSize = 0.05f;
        [SerializeField] private float m_LineWidth = 0.005f;

        [Header("Update Settings")]
        [SerializeField] private float m_UpdateInterval = 0.5f;

        // Runtime visualization objects
        private List<GameObject> m_MarkerObjects = new List<GameObject>();
        private List<LineRenderer> m_ConnectionLines = new List<LineRenderer>();
        private GameObject m_CenterMarker;
        private BuildValidationResult m_LastResult;
        private float m_UpdateTimer;

        // Cached data
        private List<ExpectedPosition> m_ExpectedPositions = new List<ExpectedPosition>();
        private Vector3 m_BuildCenter;

        private struct ExpectedPosition
        {
            public Vector3 WorldPosition;
            public BlockType BlockType;
            public BlockColor BlockColor;
            public bool IsMatched;
            public bool IsCorrect;
            public GameObject MatchedBlock;
            public float PositionError;
        }

        private void Start()
        {
            if (m_BuildValidator == null)
                m_BuildValidator = FindObjectOfType<BuildValidator>();
            
            if (m_BuildZone == null)
                m_BuildZone = FindObjectOfType<BuildZone>();

            CreateCenterMarker();
        }

        private void Update()
        {
            if (!m_ShowVisualization) 
            {
                HideAllMarkers();
                return;
            }

            // Safety: Don't update if no validator
            if (m_BuildValidator == null)
            {
                HideAllMarkers();
                return;
            }

            m_UpdateTimer += Time.deltaTime;
            if (m_UpdateTimer >= m_UpdateInterval)
            {
                m_UpdateTimer = 0f;
                try
                {
                    UpdateVisualization();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ValidationDebugVisualizer: Error updating visualization: {e.Message}");
                    HideAllMarkers();
                    m_ShowVisualization = false; // Disable to prevent spam
                }
            }
        }

        /// <summary>
        /// Updates all visualization elements based on current validation state.
        /// </summary>
        public void UpdateVisualization()
        {
            if (m_BuildValidator == null) return;

            m_LastResult = m_BuildValidator.ValidateBuild();
            if (m_LastResult == null) return;

            // Calculate expected positions
            CalculateExpectedPositions();

            // Update visual markers
            UpdateMarkers();
            UpdateConnectionLines();
            UpdateCenterMarker();
        }

        /// <summary>
        /// Calculates world positions where blocks should be placed.
        /// Uses the same calculation as BuildValidator INCLUDING auto-alignment rotation.
        /// </summary>
        private void CalculateExpectedPositions()
        {
            m_ExpectedPositions.Clear();
            
            // Validate build center - must be finite
            if (m_LastResult == null) return;
            
            m_BuildCenter = m_LastResult.BuildCenter;
            if (!IsValidVector(m_BuildCenter))
            {
                Debug.LogWarning("ValidationDebugVisualizer: Invalid build center, skipping visualization");
                m_BuildCenter = Vector3.zero;
                return;
            }

            if (m_BuildValidator == null || m_BuildValidator.ReferenceConfiguration == null) return;

            var referenceEntries = m_BuildValidator.ReferenceConfiguration.SpawnEntries;
            if (referenceEntries == null || referenceEntries.Count == 0) return;
            
            Vector3 referenceCenter = m_LastResult.ReferenceCenter;
            if (!IsValidVector(referenceCenter))
            {
                Debug.LogWarning("ValidationDebugVisualizer: Invalid reference center, skipping visualization");
                return;
            }

            float structureScale = 1.0f;
            float heightOffset = 0.12f; // Must match BuildValidator's m_HeightOffset
            
            // GET THE AUTO-ALIGNMENT ROTATION FROM BUILD VALIDATOR
            float alignmentRotation = m_BuildValidator.LastAlignmentRotation;
            Quaternion rotOffset = Quaternion.Euler(0, alignmentRotation, 0);
            
            if (alignmentRotation != 0f)
            {
                Debug.Log($"[VISUALIZER] Applying rotation {alignmentRotation:F0}° to bubble positions");
            }

            for (int i = 0; i < referenceEntries.Count; i++)
            {
                var entry = referenceEntries[i];
                if (entry == null) continue;

                // Calculate expected world position WITH ROTATION
                Vector3 relativePos = (entry.Position - referenceCenter) * structureScale;
                relativePos.y += heightOffset;
                // APPLY THE AUTO-ALIGNMENT ROTATION
                relativePos = rotOffset * relativePos;
                Vector3 worldPos = m_BuildCenter + relativePos;
                
                // Validate the calculated position
                if (!IsValidVector(worldPos))
                {
                    Debug.LogWarning($"ValidationDebugVisualizer: Invalid world position for {entry.BlockType}, skipping");
                    continue;
                }

                // Find corresponding block result
                BlockValidationResult blockResult = null;
                if (i < m_LastResult.BlockResults.Count)
                {
                    blockResult = m_LastResult.BlockResults[i];
                }

                m_ExpectedPositions.Add(new ExpectedPosition
                {
                    WorldPosition = worldPos,
                    BlockType = entry.BlockType,
                    BlockColor = entry.BlockColor,
                    IsMatched = blockResult?.IsPresent ?? false,
                    IsCorrect = blockResult?.IsCorrect ?? false,
                    MatchedBlock = blockResult?.PlacedBlock,
                    PositionError = blockResult?.PositionError ?? float.MaxValue
                });
            }
        }

        /// <summary>
        /// Updates or creates marker objects at expected positions.
        /// </summary>
        private void UpdateMarkers()
        {
            if (!m_ShowExpectedPositions)
            {
                foreach (var marker in m_MarkerObjects)
                {
                    if (marker != null) marker.SetActive(false);
                }
                return;
            }

            // Ensure we have enough marker objects
            while (m_MarkerObjects.Count < m_ExpectedPositions.Count)
            {
                m_MarkerObjects.Add(CreateMarker());
            }

            // Update each marker
            for (int i = 0; i < m_ExpectedPositions.Count; i++)
            {
                ExpectedPosition expected = m_ExpectedPositions[i];
                GameObject marker = m_MarkerObjects[i];
                
                if (marker == null)
                {
                    marker = CreateMarker();
                    m_MarkerObjects[i] = marker;
                }

                // Validate position before setting
                if (!IsValidVector(expected.WorldPosition))
                {
                    marker.SetActive(false);
                    continue;
                }

                marker.SetActive(true);
                SafeSetPosition(marker.transform, expected.WorldPosition);

                // Set color based on status
                Color markerColor;
                if (!expected.IsMatched)
                    markerColor = m_MissingColor;
                else if (expected.IsCorrect)
                    markerColor = m_CorrectColor;
                else
                    markerColor = m_IncorrectColor;

                MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material.color = markerColor;
                }

                // Scale based on tolerance
                if (m_ShowToleranceSpheres)
                {
                    float tolerance = m_BuildValidator.PositionTolerance;
                    marker.transform.localScale = Vector3.one * tolerance * 2f;
                }
                else
                {
                    marker.transform.localScale = Vector3.one * m_MarkerSize;
                }
            }

            // Hide unused markers
            for (int i = m_ExpectedPositions.Count; i < m_MarkerObjects.Count; i++)
            {
                if (m_MarkerObjects[i] != null)
                    m_MarkerObjects[i].SetActive(false);
            }
        }

        /// <summary>
        /// Updates connection lines between expected and actual positions.
        /// </summary>
        private void UpdateConnectionLines()
        {
            if (!m_ShowConnectionLines)
            {
                foreach (var line in m_ConnectionLines)
                {
                    if (line != null) line.enabled = false;
                }
                return;
            }

            // Ensure we have enough line renderers
            while (m_ConnectionLines.Count < m_ExpectedPositions.Count)
            {
                m_ConnectionLines.Add(CreateLineRenderer());
            }

            // Update each line
            for (int i = 0; i < m_ExpectedPositions.Count; i++)
            {
                ExpectedPosition expected = m_ExpectedPositions[i];
                LineRenderer line = m_ConnectionLines[i];
                
                if (line == null)
                {
                    line = CreateLineRenderer();
                    m_ConnectionLines[i] = line;
                }

                // Only show line if block is matched but not correct
                if (expected.IsMatched && !expected.IsCorrect && expected.MatchedBlock != null)
                {
                    Vector3 startPos = expected.WorldPosition;
                    Vector3 endPos = expected.MatchedBlock.transform.position;
                    
                    // Validate both positions
                    if (IsValidVector(startPos) && IsValidVector(endPos))
                    {
                        line.enabled = true;
                        line.SetPosition(0, startPos);
                        line.SetPosition(1, endPos);
                        
                        Color lineColor = expected.IsCorrect ? m_CorrectColor : m_IncorrectColor;
                        line.startColor = lineColor;
                        line.endColor = lineColor;
                    }
                    else
                    {
                        line.enabled = false;
                    }
                }
                else
                {
                    line.enabled = false;
                }
            }

            // Hide unused lines
            for (int i = m_ExpectedPositions.Count; i < m_ConnectionLines.Count; i++)
            {
                if (m_ConnectionLines[i] != null)
                    m_ConnectionLines[i].enabled = false;
            }
        }

        /// <summary>
        /// Updates the center marker position.
        /// </summary>
        private void UpdateCenterMarker()
        {
            if (m_CenterMarker == null)
                CreateCenterMarker();

            bool shouldShow = m_ShowBuildCenter && m_ShowVisualization && IsValidVector(m_BuildCenter);
            m_CenterMarker.SetActive(shouldShow);
            if (shouldShow)
            {
                SafeSetPosition(m_CenterMarker.transform, m_BuildCenter);
            }
        }

        /// <summary>
        /// Creates a sphere marker for expected positions.
        /// </summary>
        private GameObject CreateMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "ValidationMarker";
            marker.transform.SetParent(transform);
            marker.transform.localScale = Vector3.one * m_MarkerSize;

            // Remove collider
            Collider col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Create transparent material
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            mat.color = m_ToleranceColor;
            renderer.material = mat;

            return marker;
        }

        /// <summary>
        /// Creates a line renderer for connection lines.
        /// </summary>
        private LineRenderer CreateLineRenderer()
        {
            GameObject lineObj = new GameObject("ConnectionLine");
            lineObj.transform.SetParent(transform);
            
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = m_LineWidth;
            line.endWidth = m_LineWidth;
            line.useWorldSpace = true;
            
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = m_IncorrectColor;
            line.material = mat;
            
            return line;
        }

        /// <summary>
        /// Creates the center marker.
        /// </summary>
        private void CreateCenterMarker()
        {
            m_CenterMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_CenterMarker.name = "BuildCenterMarker";
            m_CenterMarker.transform.SetParent(transform);
            m_CenterMarker.transform.localScale = Vector3.one * 0.03f;

            Collider col = m_CenterMarker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            MeshRenderer renderer = m_CenterMarker.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = m_CenterColor;
            renderer.material = mat;
        }

        /// <summary>
        /// Hides all visualization markers.
        /// </summary>
        private void HideAllMarkers()
        {
            foreach (var marker in m_MarkerObjects)
            {
                if (marker != null) marker.SetActive(false);
            }
            foreach (var line in m_ConnectionLines)
            {
                if (line != null) line.enabled = false;
            }
            if (m_CenterMarker != null)
                m_CenterMarker.SetActive(false);
        }

        /// <summary>
        /// Toggles visualization on/off.
        /// </summary>
        public void ToggleVisualization()
        {
            m_ShowVisualization = !m_ShowVisualization;
            if (m_ShowVisualization)
                UpdateVisualization();
            else
                HideAllMarkers();
        }

        /// <summary>
        /// Draw gizmos in editor for additional debugging.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!m_ShowVisualization || m_ExpectedPositions == null) return;

            // Draw expected positions
            foreach (var expected in m_ExpectedPositions)
            {
                Gizmos.color = expected.IsCorrect ? m_CorrectColor : 
                              (expected.IsMatched ? m_IncorrectColor : m_MissingColor);
                Gizmos.DrawWireSphere(expected.WorldPosition, m_MarkerSize);

                // Draw tolerance sphere
                if (m_ShowToleranceSpheres && m_BuildValidator != null)
                {
                    Gizmos.color = m_ToleranceColor;
                    Gizmos.DrawWireSphere(expected.WorldPosition, m_BuildValidator.PositionTolerance);
                }

                // Draw connection to matched block
                if (expected.MatchedBlock != null && !expected.IsCorrect)
                {
                    Gizmos.color = m_IncorrectColor;
                    Gizmos.DrawLine(expected.WorldPosition, expected.MatchedBlock.transform.position);
                }
            }

            // Draw build center
            if (m_ShowBuildCenter)
            {
                Gizmos.color = m_CenterColor;
                Gizmos.DrawWireCube(m_BuildCenter, Vector3.one * 0.05f);
            }
        }

        private void OnDestroy()
        {
            // Cleanup created objects
            foreach (var marker in m_MarkerObjects)
            {
                if (marker != null) Destroy(marker);
            }
            foreach (var line in m_ConnectionLines)
            {
                if (line != null) Destroy(line.gameObject);
            }
            if (m_CenterMarker != null)
                Destroy(m_CenterMarker);
        }

        /// <summary>
        /// Checks if a Vector3 has valid (finite, non-NaN) values.
        /// </summary>
        private bool IsValidVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        /// <summary>
        /// Safely sets position only if valid.
        /// </summary>
        private void SafeSetPosition(Transform t, Vector3 pos)
        {
            if (t != null && IsValidVector(pos))
            {
                t.position = pos;
            }
        }
    }
}

