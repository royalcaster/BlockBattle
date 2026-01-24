using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR;
using UnityEngine.SpatialTracking;

namespace BlockBattle
{
    /// <summary>
    /// Script to fix XR setup issues: head tracking, controller visibility, and player scale.
    /// </summary>
    public class BlockBattleXRSetup : MonoBehaviour
    {
        [Header("XR Origin Configuration")]
        [SerializeField, Tooltip("The XR Origin in the scene. If null, will find automatically.")]
        private XROrigin m_XROrigin;

        [SerializeField, Tooltip("Player scale multiplier. 1.0 is normal size.")]
        private float m_PlayerScale = 1.0f;

        [SerializeField, Tooltip("Camera Y offset in meters (eye height). Default is 1.36m.")]
        private float m_CameraYOffset = 1.36f;

        [Header("Controller Visibility")]
        [SerializeField, Tooltip("Ensure controllers are visible")]
        private bool m_EnsureControllersVisible = true;

        [Header("Debug")]
        [SerializeField, Tooltip("Enable verbose console logging")]
        private bool m_VerboseLogging = false;

        private void Start()
        {
            SetupXR();
        }

        /// <summary>
        /// Sets up the XR Origin with correct tracking and controller visibility.
        /// </summary>
        private void SetupXR()
        {
            // Find XR Origin if not assigned
            if (m_XROrigin == null)
            {
                m_XROrigin = FindFirstObjectByType<XROrigin>();
            }

            if (m_XROrigin == null)
            {
                Debug.LogError("BlockBattleXRSetup: XR Origin not found in scene!");
                return;
            }

            // Ensure XR Origin is at world origin and stays there
            // The XR Origin root should NEVER move - only the camera should move for head tracking
            m_XROrigin.transform.position = Vector3.zero;
            m_XROrigin.transform.rotation = Quaternion.identity;

            // Ensure camera is active and enabled
            Camera xrCamera = m_XROrigin.Camera;
            if (xrCamera != null)
            {
                xrCamera.gameObject.SetActive(true);
                xrCamera.enabled = true;
                xrCamera.tag = "MainCamera";
                
                // Ensure TrackedPoseDriver exists and is properly configured for head tracking
                TrackedPoseDriver trackedPoseDriver = xrCamera.GetComponent<TrackedPoseDriver>();
                if (trackedPoseDriver == null)
                {
                    // Add TrackedPoseDriver if it doesn't exist
                    trackedPoseDriver = xrCamera.gameObject.AddComponent<TrackedPoseDriver>();
                    trackedPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
                    if (m_VerboseLogging) Debug.Log($"BlockBattleXRSetup: Added TrackedPoseDriver to camera '{xrCamera.gameObject.name}'");
                }
                else
                {
                    // Ensure it's enabled and properly configured
                    trackedPoseDriver.enabled = true;
                    trackedPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
                    if (m_VerboseLogging) Debug.Log($"BlockBattleXRSetup: TrackedPoseDriver found and configured on camera '{xrCamera.gameObject.name}'");
                }
                
                if (m_VerboseLogging) Debug.Log($"BlockBattleXRSetup: Camera '{xrCamera.gameObject.name}' is active and enabled");
            }
            else
            {
                Debug.LogWarning("BlockBattleXRSetup: XR Origin camera is null!");
            }

            // Find and configure Camera Offset
            Transform cameraOffset = m_XROrigin.transform.Find("Camera Offset");
            if (cameraOffset != null)
            {
                // Set camera offset to proper eye height
                cameraOffset.localPosition = new Vector3(0, m_CameraYOffset, 0);
                cameraOffset.gameObject.SetActive(true);
                if (m_VerboseLogging) Debug.Log($"BlockBattleXRSetup: Camera Offset set to Y: {m_CameraYOffset}");
            }
            else
            {
                Debug.LogWarning("BlockBattleXRSetup: Camera Offset not found in XR Origin!");
            }

            // Set camera Y offset in XROrigin component
            m_XROrigin.CameraYOffset = m_CameraYOffset;

            // Use Floor mode for proper room-scale tracking
            // Floor mode keeps the XR Origin fixed at the floor level, and only the camera moves
            m_XROrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // Apply player scale
            if (Mathf.Abs(m_PlayerScale - 1.0f) > 0.01f)
            {
                m_XROrigin.transform.localScale = Vector3.one * m_PlayerScale;
            }

            // Ensure controllers are visible
            if (m_EnsureControllersVisible)
            {
                EnsureControllersVisible();
            }

            if (m_VerboseLogging) Debug.Log($"BlockBattleXRSetup: XR setup complete. Tracking mode: {m_XROrigin.RequestedTrackingOriginMode}, Scale: {m_PlayerScale}, Camera Y Offset: {m_CameraYOffset}");
        }

        /// <summary>
        /// Ensures the XR Origin rotation stays fixed - only the camera should rotate for head tracking.
        /// Position changes are allowed for locomotion (movement via controllers).
        /// In proper VR setup, the XR Origin rotation should remain fixed at identity.
        /// Head rotation should only affect the camera (via TrackedPoseDriver), not the entire XR Origin.
        /// </summary>
        private void LateUpdate()
        {
            if (m_XROrigin != null)
            {
                // Keep rotation fixed - head rotation should only affect the camera via TrackedPoseDriver
                // Position changes are allowed for locomotion (player movement via controllers)
                Quaternion currentRot = m_XROrigin.transform.rotation;
                if (Quaternion.Angle(currentRot, Quaternion.identity) > 0.1f)
                {
                    m_XROrigin.transform.rotation = Quaternion.identity;
                    Debug.LogWarning($"BlockBattleXRSetup: XR Origin was rotated. Fixed to identity rotation.");
                }
            }
        }

        /// <summary>
        /// Ensures controller models are visible and properly positioned.
        /// </summary>
        private void EnsureControllersVisible()
        {
            // Find all XR Controller components
            var controllers = m_XROrigin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.XRController>();
            
            foreach (var controller in controllers)
            {
                if (controller == null) continue;

                // Find the model child object
                Transform controllerTransform = controller.transform;
                
                // Look for all renderers in the controller hierarchy
                Renderer[] renderers = controllerTransform.GetComponentsInChildren<Renderer>(true);
                
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    // Enable the GameObject and renderer
                    renderer.gameObject.SetActive(true);
                    renderer.enabled = true;
                    
                    if (m_VerboseLogging) Debug.Log($"BlockBattleXRSetup: Enabled controller renderer: {renderer.name}");
                }
            }
        }

        #region Teleportation

        /// <summary>
        /// Teleports the player to a specific position and rotation.
        /// </summary>
        /// <param name="position">Target world position for the player's feet</param>
        /// <param name="rotation">Target rotation (Y rotation only, facing direction)</param>
        public void TeleportPlayer(Vector3 position, Quaternion rotation)
        {
            if (m_XROrigin == null)
            {
                m_XROrigin = FindFirstObjectByType<XROrigin>();
                if (m_XROrigin == null)
                {
                    Debug.LogError("BlockBattleXRSetup: Cannot teleport - XR Origin not found!");
                    return;
                }
            }

            // Move the XR Origin to the target position
            m_XROrigin.transform.position = position;
            
            // Note: We don't change the XR Origin rotation because head tracking should handle that.
            // The player will be at the position but facing based on their head orientation.
            // If you need to force a facing direction, you'd need to rotate the XR Origin,
            // but that conflicts with our LateUpdate that resets rotation.
            
            if (m_VerboseLogging) Debug.Log($"BlockBattleXRSetup: Teleported player to {position}");
        }

        /// <summary>
        /// Teleports the player to a transform's position and rotation.
        /// </summary>
        /// <param name="target">Target transform</param>
        public void TeleportPlayer(Transform target)
        {
            if (target == null)
            {
                Debug.LogError("BlockBattleXRSetup: Cannot teleport - target is null!");
                return;
            }

            TeleportPlayer(target.position, target.rotation);
        }

        /// <summary>
        /// Gets the current XR Origin reference.
        /// </summary>
        public XROrigin XROrigin => m_XROrigin;

        #endregion
    }
}

