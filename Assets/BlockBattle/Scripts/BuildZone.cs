using UnityEngine;

namespace BlockBattle
{
    /// <summary>
    /// Defines a build zone area where players should place their blocks.
    /// Only blocks within this zone are validated.
    /// </summary>
    public class BuildZone : MonoBehaviour
    {
        [Header("Zone Settings")]
        [SerializeField, Tooltip("Size of the build zone (width, height, depth)")]
        private Vector3 m_ZoneSize = new Vector3(0.6f, 1.0f, 0.6f); // 1m height to accommodate stacked blocks

        [SerializeField, Tooltip("Color of the zone visualization")]
        private Color m_ZoneColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);

        [SerializeField, Tooltip("Color of the zone border")]
        private Color m_BorderColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);

        [SerializeField, Tooltip("Show the zone visualization at runtime")]
        private bool m_ShowVisualization = true;

        [SerializeField, Tooltip("Border line width")]
        private float m_BorderWidth = 0.01f;

        private GameObject m_Visualization;
        private MeshRenderer m_FloorRenderer;
        private LineRenderer m_BorderRenderer;

        /// <summary>
        /// Gets the world-space bounds of the build zone.
        /// </summary>
        public Bounds ZoneBounds
        {
            get
            {
                return new Bounds(transform.position + Vector3.up * (m_ZoneSize.y / 2f), m_ZoneSize);
            }
        }

        /// <summary>
        /// Gets or sets the zone size.
        /// </summary>
        public Vector3 ZoneSize
        {
            get => m_ZoneSize;
            set
            {
                m_ZoneSize = value;
                UpdateVisualization();
            }
        }

        /// <summary>
        /// Gets or sets whether visualization is shown.
        /// </summary>
        public bool ShowVisualization
        {
            get => m_ShowVisualization;
            set
            {
                m_ShowVisualization = value;
                if (m_Visualization != null)
                {
                    m_Visualization.SetActive(m_ShowVisualization);
                }
            }
        }

        private void Start()
        {
            CreateVisualization();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && m_Visualization != null)
            {
                UpdateVisualization();
            }
        }

        /// <summary>
        /// Checks if a position is within the build zone.
        /// </summary>
        /// <param name="position">World position to check</param>
        /// <returns>True if position is within the zone</returns>
        public bool IsInZone(Vector3 position)
        {
            return ZoneBounds.Contains(position);
        }

        /// <summary>
        /// Checks if a GameObject (block) is within the build zone.
        /// </summary>
        /// <param name="obj">GameObject to check</param>
        /// <returns>True if the object is within the zone</returns>
        public bool IsInZone(GameObject obj)
        {
            if (obj == null) return false;
            return IsInZone(obj.transform.position);
        }

        /// <summary>
        /// Creates the visual representation of the build zone.
        /// </summary>
        private void CreateVisualization()
        {
            if (m_Visualization != null)
            {
                Destroy(m_Visualization);
            }

            m_Visualization = new GameObject("BuildZone_Visualization");
            m_Visualization.transform.SetParent(transform, false);
            m_Visualization.transform.localPosition = Vector3.zero;
            m_Visualization.transform.localRotation = Quaternion.identity;

            // Create floor plane
            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floorObj.name = "Floor";
            floorObj.transform.SetParent(m_Visualization.transform, false);
            floorObj.transform.localPosition = new Vector3(0f, 0.001f, 0f); // Slightly above ground
            floorObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            floorObj.transform.localScale = new Vector3(m_ZoneSize.x, m_ZoneSize.z, 1f);

            // Remove collider from visualization
            Collider floorCollider = floorObj.GetComponent<Collider>();
            if (floorCollider != null)
            {
                Destroy(floorCollider);
            }

            // Create transparent material for floor
            m_FloorRenderer = floorObj.GetComponent<MeshRenderer>();
            Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.SetFloat("_Surface", 1); // Transparent
            floorMat.SetFloat("_Blend", 0); // Alpha
            floorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            floorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            floorMat.SetInt("_ZWrite", 0);
            floorMat.DisableKeyword("_ALPHATEST_ON");
            floorMat.EnableKeyword("_ALPHABLEND_ON");
            floorMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            floorMat.renderQueue = 3000;
            floorMat.color = m_ZoneColor;
            m_FloorRenderer.material = floorMat;

            // Create border using LineRenderer
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(m_Visualization.transform, false);
            m_BorderRenderer = borderObj.AddComponent<LineRenderer>();
            m_BorderRenderer.useWorldSpace = false;
            m_BorderRenderer.loop = true;
            m_BorderRenderer.startWidth = m_BorderWidth;
            m_BorderRenderer.endWidth = m_BorderWidth;
            
            // Create border material
            Material borderMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            borderMat.color = m_BorderColor;
            m_BorderRenderer.material = borderMat;

            // Set border positions (rectangle on the floor)
            float halfX = m_ZoneSize.x / 2f;
            float halfZ = m_ZoneSize.z / 2f;
            float y = 0.002f; // Slightly above floor
            m_BorderRenderer.positionCount = 4;
            m_BorderRenderer.SetPositions(new Vector3[]
            {
                new Vector3(-halfX, y, -halfZ),
                new Vector3(halfX, y, -halfZ),
                new Vector3(halfX, y, halfZ),
                new Vector3(-halfX, y, halfZ)
            });

            m_Visualization.SetActive(m_ShowVisualization);
        }

        /// <summary>
        /// Updates the visualization to match current settings.
        /// </summary>
        private void UpdateVisualization()
        {
            if (m_Visualization == null) return;

            // Update floor scale
            Transform floor = m_Visualization.transform.Find("Floor");
            if (floor != null)
            {
                floor.localScale = new Vector3(m_ZoneSize.x, m_ZoneSize.z, 1f);
                if (m_FloorRenderer != null)
                {
                    m_FloorRenderer.material.color = m_ZoneColor;
                }
            }

            // Update border
            if (m_BorderRenderer != null)
            {
                float halfX = m_ZoneSize.x / 2f;
                float halfZ = m_ZoneSize.z / 2f;
                float y = 0.002f;
                m_BorderRenderer.SetPositions(new Vector3[]
                {
                    new Vector3(-halfX, y, -halfZ),
                    new Vector3(halfX, y, -halfZ),
                    new Vector3(halfX, y, halfZ),
                    new Vector3(-halfX, y, halfZ)
                });
                m_BorderRenderer.startWidth = m_BorderWidth;
                m_BorderRenderer.endWidth = m_BorderWidth;
                m_BorderRenderer.material.color = m_BorderColor;
            }

            m_Visualization.SetActive(m_ShowVisualization);
        }

        /// <summary>
        /// Draw gizmos in editor for easier positioning.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = m_ZoneColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up * (m_ZoneSize.y / 2f), m_ZoneSize);
            
            // Draw filled floor in editor
            Gizmos.color = new Color(m_ZoneColor.r, m_ZoneColor.g, m_ZoneColor.b, 0.2f);
            Vector3 floorCenter = transform.position + Vector3.up * 0.01f;
            Vector3 floorSize = new Vector3(m_ZoneSize.x, 0.01f, m_ZoneSize.z);
            Gizmos.DrawCube(floorCenter, floorSize);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = m_BorderColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up * (m_ZoneSize.y / 2f), m_ZoneSize);
        }
    }
}

