using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.Netcode;
using BlockBattle.Network;

namespace BlockBattle
{
    /// <summary>
    /// VR Slingshot with pull-back-and-release mechanic.
    /// The slingshot frame is fixed in place. Player grabs the pouch and pulls back to aim and shoot.
    /// </summary>
    public class VRSlingshot : MonoBehaviour
    {
        [Header("Slingshot References")]
        [SerializeField, Tooltip("Transform representing the rest position of the pouch (between the Y forks)")]
        private Transform m_PouchRestPosition;

        [SerializeField, Tooltip("Left fork tip for rubber band")]
        private Transform m_LeftForkTip;

        [SerializeField, Tooltip("Right fork tip for rubber band")]
        private Transform m_RightForkTip;

        [SerializeField, Tooltip("The pouch object that player grabs")]
        private Transform m_Pouch;

        [Header("Projectile Settings")]
        [SerializeField, Tooltip("Prefab of the ball projectile to shoot")]
        private GameObject m_ProjectilePrefab;

        [SerializeField, Tooltip("Maximum pull distance in meters")]
        private float m_MaxPullDistance = 0.4f;

        [SerializeField, Tooltip("Force multiplier for launch (force = pullDistance * multiplier)")]
        private float m_LaunchForceMultiplier = 40f;

        [SerializeField, Tooltip("Minimum pull distance required to fire")]
        private float m_MinPullDistance = 0.05f;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("LineRenderer for the left rubber band")]
        private LineRenderer m_LeftBandRenderer;

        [SerializeField, Tooltip("LineRenderer for the right rubber band")]
        private LineRenderer m_RightBandRenderer;

        [SerializeField, Tooltip("Color of the rubber band when relaxed")]
        private Color m_RelaxedColor = new Color(0.6f, 0.4f, 0.2f);

        [SerializeField, Tooltip("Color of the rubber band when fully stretched")]
        private Color m_StretchedColor = new Color(1f, 0.2f, 0.2f);

        [Header("Audio (Optional)")]
        [SerializeField, Tooltip("Sound when releasing/firing")]
        private AudioClip m_FireSound;

        [SerializeField]
        private AudioSource m_AudioSource;

        [Header("Collision Settings")]
        [SerializeField, Tooltip("Colliders to ignore when firing (e.g., slingshot forks)")]
        private Collider[] m_IgnoreColliders;

        [Header("Multiplayer")]
        [SerializeField, Tooltip("The workspace index this slingshot belongs to (for multiplayer)")]
        private int m_WorkspaceIndex = 0;

        [SerializeField, Tooltip("Target workspace index to fire at (same as own = shoot own structure)")]
        private int m_TargetWorkspaceIndex = 0;

        [SerializeField, Tooltip("Use network spawning for projectiles in multiplayer")]
        private bool m_UseNetworkSpawning = true;

        // Events
        /// <summary>
        /// Event fired when a projectile is launched.
        /// </summary>
        public event System.Action<GameObject> OnProjectileFired;

        // Runtime state
        private XRGrabInteractable _pouchInteractable;
        private bool _isPouchGrabbed = false;
        private Vector3 _currentPouchPosition;
        private float _currentPullDistance = 0f;
        private IXRSelectInteractor _currentInteractor;

        // Saved launch parameters (captured at moment of release)
        private Vector3 _savedLaunchPosition;
        private Vector3 _savedLaunchDirection;
        private float _savedPullDistance;

        // Cached slingshot colliders for ignoring
        private Collider[] _slingshotColliders;

        // Multiplayer state
        private bool _isMultiplayerMode = false;

        /// <summary>
        /// Gets or sets the workspace index for this slingshot.
        /// </summary>
        public int WorkspaceIndex
        {
            get => m_WorkspaceIndex;
            set => m_WorkspaceIndex = value;
        }

        /// <summary>
        /// Gets or sets the target workspace index (opponent's workspace).
        /// </summary>
        public int TargetWorkspaceIndex
        {
            get => m_TargetWorkspaceIndex;
            set => m_TargetWorkspaceIndex = value;
        }

        private void Start()
        {
            // Check multiplayer mode
            CheckMultiplayerMode();
            // Get or create the XRGrabInteractable on the pouch
            if (m_Pouch != null)
            {
                _pouchInteractable = m_Pouch.GetComponent<XRGrabInteractable>();
                if (_pouchInteractable == null)
                {
                    _pouchInteractable = m_Pouch.gameObject.AddComponent<XRGrabInteractable>();
                }

                // Configure for slingshot behavior - track position but don't move the object
                _pouchInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
                _pouchInteractable.trackPosition = false; // We'll handle position ourselves
                _pouchInteractable.trackRotation = false;
                _pouchInteractable.throwOnDetach = false;

                // Subscribe to events
                _pouchInteractable.selectEntered.AddListener(OnPouchGrabbed);
                _pouchInteractable.selectExited.AddListener(OnPouchReleased);
            }

            // Initialize rubber bands
            InitializeRubberBands();

            // Set initial pouch position
            if (m_PouchRestPosition != null)
            {
                _currentPouchPosition = m_PouchRestPosition.position;
                if (m_Pouch != null)
                {
                    m_Pouch.position = _currentPouchPosition;
                }
            }

            // Cache all colliders on the slingshot for collision ignoring
            CacheSlingshotColliders();
        }

        /// <summary>
        /// Caches all colliders on the slingshot to ignore when firing projectiles.
        /// </summary>
        private void CacheSlingshotColliders()
        {
            // Get all colliders in the slingshot hierarchy
            _slingshotColliders = GetComponentsInChildren<Collider>(true);
            
            // Also include manually specified colliders
            if (m_IgnoreColliders != null && m_IgnoreColliders.Length > 0)
            {
                var allColliders = new System.Collections.Generic.List<Collider>(_slingshotColliders);
                foreach (var col in m_IgnoreColliders)
                {
                    if (col != null && !allColliders.Contains(col))
                    {
                        allColliders.Add(col);
                    }
                }
                _slingshotColliders = allColliders.ToArray();
            }

            Debug.Log($"VRSlingshot: Cached {_slingshotColliders.Length} colliders to ignore");
        }

        private void OnDestroy()
        {
            if (_pouchInteractable != null)
            {
                _pouchInteractable.selectEntered.RemoveListener(OnPouchGrabbed);
                _pouchInteractable.selectExited.RemoveListener(OnPouchReleased);
            }
        }

        private void Update()
        {
            if (_isPouchGrabbed && _currentInteractor != null)
            {
                UpdatePouchPosition();
            }

            UpdateRubberBands();
        }

        #region Grab Events

        private void OnPouchGrabbed(SelectEnterEventArgs args)
        {
            _isPouchGrabbed = true;
            _currentInteractor = args.interactorObject;
            Debug.Log("VRSlingshot: Pouch grabbed");
        }

        private void OnPouchReleased(SelectExitEventArgs args)
        {
            _isPouchGrabbed = false;
            Debug.Log($"VRSlingshot: Pouch released, pull distance: {_currentPullDistance:F2}m");

            // Save launch parameters BEFORE resetting
            _savedLaunchPosition = _currentPouchPosition;
            _savedPullDistance = _currentPullDistance;
            
            // Calculate launch direction (from pouch back toward rest = forward toward target)
            Vector3 pullVector = _currentPouchPosition - m_PouchRestPosition.position;
            if (pullVector.sqrMagnitude > 0.0001f)
            {
                _savedLaunchDirection = -pullVector.normalized; // Opposite of pull = toward target
            }
            else
            {
                _savedLaunchDirection = transform.forward;
            }

            Debug.Log($"VRSlingshot: Launch dir: {_savedLaunchDirection}, Pull dist: {_savedPullDistance:F2}m");

            // Fire if pulled back enough
            if (_savedPullDistance >= m_MinPullDistance)
            {
                FireProjectile();
            }

            // Reset pouch position AFTER firing
            ResetPouchPosition();
            _currentInteractor = null;
        }

        #endregion

        #region Pouch Mechanics

        /// <summary>
        /// Updates the pouch position based on the grabbed hand position.
        /// Constrains to maximum pull distance.
        /// </summary>
        private void UpdatePouchPosition()
        {
            if (m_Pouch == null || m_PouchRestPosition == null || _currentInteractor == null)
                return;

            // Get the hand/controller position
            Transform interactorTransform = _currentInteractor.GetAttachTransform(_pouchInteractable);
            Vector3 handPosition = interactorTransform != null ? interactorTransform.position : ((MonoBehaviour)_currentInteractor).transform.position;
            Vector3 restPosition = m_PouchRestPosition.position;

            // Calculate pull vector (from rest to hand)
            Vector3 pullVector = handPosition - restPosition;
            _currentPullDistance = pullVector.magnitude;

            // Clamp to max pull distance
            if (_currentPullDistance > m_MaxPullDistance)
            {
                pullVector = pullVector.normalized * m_MaxPullDistance;
                _currentPullDistance = m_MaxPullDistance;
            }

            _currentPouchPosition = restPosition + pullVector;

            // Move the pouch to follow the hand (constrained)
            m_Pouch.position = _currentPouchPosition;
        }

        /// <summary>
        /// Resets the pouch to its rest position.
        /// </summary>
        private void ResetPouchPosition()
        {
            _currentPullDistance = 0f;
            if (m_PouchRestPosition != null)
            {
                _currentPouchPosition = m_PouchRestPosition.position;

                // Move the pouch back to rest position
                if (m_Pouch != null)
                {
                    m_Pouch.position = m_PouchRestPosition.position;
                }
            }
        }

        #endregion

        #region Multiplayer Support

        /// <summary>
        /// Checks if we're in multiplayer mode.
        /// </summary>
        private void CheckMultiplayerMode()
        {
            _isMultiplayerMode = m_UseNetworkSpawning && 
                                 NetworkManager.Singleton != null && 
                                 NetworkManager.Singleton.IsConnectedClient;
        }

        #endregion

        #region Firing

        /// <summary>
        /// Fires a projectile based on saved launch parameters.
        /// </summary>
        private void FireProjectile()
        {
            // Use saved parameters (captured at moment of release)
            Vector3 spawnPosition = _savedLaunchPosition;
            Vector3 launchDirection = _savedLaunchDirection;
            float pullDistance = _savedPullDistance;

            // Calculate launch velocity
            float launchSpeed = pullDistance * m_LaunchForceMultiplier;
            Vector3 velocity = launchDirection * launchSpeed;

            Debug.Log($"VRSlingshot: Firing! Spawn pos: {spawnPosition}, Dir: {launchDirection}, Speed: {launchSpeed:F1}");

            // In multiplayer mode, request server to spawn projectile
            if (_isMultiplayerMode)
            {
                FireNetworkedProjectile(spawnPosition, velocity);
            }
            else
            {
                // Single-player mode: spawn locally
                FireLocalProjectile(spawnPosition, velocity);
            }

            // Play fire sound (always local)
            if (m_FireSound != null && m_AudioSource != null)
            {
                m_AudioSource.PlayOneShot(m_FireSound);
            }
        }

        /// <summary>
        /// Fires a projectile locally (single-player mode).
        /// </summary>
        private void FireLocalProjectile(Vector3 spawnPosition, Vector3 velocity)
        {
            GameObject projectile;

            if (m_ProjectilePrefab != null)
            {
                // Spawn from prefab
                projectile = Instantiate(m_ProjectilePrefab, spawnPosition, Quaternion.identity);
                
                // Match the pouch size (0.05m diameter)
                projectile.transform.localScale = Vector3.one * 0.05f;
            }
            else
            {
                // Create simple ball matching pouch size
                projectile = CreateSimpleProjectile(spawnPosition);
            }

            // Setup rigidbody and apply velocity
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = projectile.AddComponent<Rigidbody>();
            }

            // Configure rigidbody for proper physics
            rb.mass = 0.2f; // Slightly heavier for better impact
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;

            // Ensure collider is set up properly
            Collider projectileCollider = projectile.GetComponent<Collider>();
            if (projectileCollider == null)
            {
                projectileCollider = projectile.AddComponent<SphereCollider>();
            }
            
            projectileCollider.isTrigger = false;

            // Ignore collisions with slingshot parts
            IgnoreSlingshotCollisions(projectileCollider);

            // Setup or fix trail renderer
            SetupTrailRenderer(projectile);

            // Apply velocity directly
            rb.linearVelocity = velocity;

            Debug.Log($"VRSlingshot: Local projectile launched with velocity {rb.linearVelocity} (magnitude: {rb.linearVelocity.magnitude:F1})");

            // Fire event
            OnProjectileFired?.Invoke(projectile);
        }

        /// <summary>
        /// Fires a networked projectile by requesting the server to spawn it.
        /// </summary>
        private void FireNetworkedProjectile(Vector3 spawnPosition, Vector3 velocity)
        {
            if (NetworkManager.Singleton == null) return;

            // If we're the server, spawn directly
            if (NetworkManager.Singleton.IsServer)
            {
                SpawnNetworkedProjectileOnServer(spawnPosition, velocity);
            }
            else
            {
                // Request server to spawn
                RequestProjectileSpawnServerRpc(spawnPosition, velocity, m_TargetWorkspaceIndex);
            }
        }

        /// <summary>
        /// Server RPC to request projectile spawning.
        /// </summary>
        private void RequestProjectileSpawnServerRpc(Vector3 position, Vector3 velocity, int targetWorkspace)
        {
            // Since VRSlingshot is not a NetworkBehaviour, we need to go through the NetworkedLevelManager
            // or use a dedicated network manager for projectiles

            // For now, if server, spawn directly
            if (NetworkManager.Singleton.IsServer)
            {
                SpawnNetworkedProjectileOnServer(position, velocity);
            }
            else
            {
                // Fallback to local spawning if we can't reach server
                Debug.LogWarning("VRSlingshot: Cannot reach server for networked projectile, using local spawn");
                FireLocalProjectile(position, velocity);
            }
        }

        /// <summary>
        /// Spawns a networked projectile on the server.
        /// </summary>
        private void SpawnNetworkedProjectileOnServer(Vector3 position, Vector3 velocity)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("VRSlingshot: SpawnNetworkedProjectileOnServer called on non-server!");
                return;
            }

            if (m_ProjectilePrefab == null)
            {
                Debug.LogError("VRSlingshot: Projectile prefab is null!");
                return;
            }

            // Check if prefab has NetworkObject
            if (m_ProjectilePrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogWarning("VRSlingshot: Projectile prefab missing NetworkObject, using local spawn");
                FireLocalProjectile(position, velocity);
                return;
            }

            // Use NetworkedProjectile helper to spawn
            ulong firingPlayerId = NetworkManager.Singleton.LocalClientId;
            var projectile = NetworkedProjectile.SpawnProjectile(
                m_ProjectilePrefab,
                position,
                velocity,
                firingPlayerId,
                m_TargetWorkspaceIndex
            );

            if (projectile != null)
            {
                // Ignore collisions with slingshot parts
                Collider projectileCollider = projectile.GetComponent<Collider>();
                if (projectileCollider != null)
                {
                    IgnoreSlingshotCollisions(projectileCollider);
                }

                SetupTrailRenderer(projectile.gameObject);

                Debug.Log($"VRSlingshot: Networked projectile spawned with velocity {velocity.magnitude:F1}");
                OnProjectileFired?.Invoke(projectile.gameObject);
            }
        }

        /// <summary>
        /// Creates a simple projectile without using a prefab.
        /// </summary>
        private GameObject CreateSimpleProjectile(Vector3 position)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "SlingshotBall";
            projectile.transform.position = position;
            projectile.transform.localScale = Vector3.one * 0.05f; // Same size as pouch

            // Set color - reddish orange
            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.9f, 0.35f, 0.2f);
                renderer.material = mat;
            }

            // Add auto-destroy component
            projectile.AddComponent<BallProjectile>();

            return projectile;
        }

        /// <summary>
        /// Ignores collisions between the projectile and all slingshot colliders.
        /// </summary>
        private void IgnoreSlingshotCollisions(Collider projectileCollider)
        {
            if (_slingshotColliders == null || projectileCollider == null)
                return;

            foreach (var slingshotCol in _slingshotColliders)
            {
                if (slingshotCol != null)
                {
                    Physics.IgnoreCollision(projectileCollider, slingshotCol, true);
                }
            }
        }

        /// <summary>
        /// Sets up or fixes the trail renderer on the projectile.
        /// </summary>
        private void SetupTrailRenderer(GameObject projectile)
        {
            TrailRenderer trail = projectile.GetComponent<TrailRenderer>();
            
            // Remove existing trail if it has issues
            if (trail != null)
            {
                // Clear any existing trail data
                trail.Clear();
            }
            else
            {
                // Add a new trail renderer
                trail = projectile.AddComponent<TrailRenderer>();
            }

            // Configure trail - it should be at the CENTER of the ball, not offset
            trail.time = 0.4f;
            trail.startWidth = 0.03f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.02f;
            
            // Use world space so trail follows actual path
            // Note: TrailRenderer doesn't have useWorldSpace, it always uses world space
            
            // Create a simple material for the trail
            Material trailMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            trail.material = trailMat;
            
            // Orange to transparent gradient
            trail.startColor = new Color(1f, 0.5f, 0.2f, 0.9f);
            trail.endColor = new Color(1f, 0.3f, 0.1f, 0f);

            // Ensure trail emits from the object's position (center)
            trail.emitting = true;
            
            // Clear any pre-existing points that might cause offset issues
            trail.Clear();
        }

        #endregion

        #region Rubber Band Visuals

        /// <summary>
        /// Initializes the rubber band LineRenderers.
        /// </summary>
        private void InitializeRubberBands()
        {
            SetupLineRenderer(m_LeftBandRenderer);
            SetupLineRenderer(m_RightBandRenderer);
        }

        private void SetupLineRenderer(LineRenderer lr)
        {
            if (lr == null) return;

            lr.positionCount = 2;
            lr.startWidth = 0.015f;
            lr.endWidth = 0.015f;
            lr.useWorldSpace = true;

            // Try to use unlit material
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lr.material = new Material(shader);
            }
            lr.startColor = m_RelaxedColor;
            lr.endColor = m_RelaxedColor;
        }

        /// <summary>
        /// Updates the rubber band visuals based on current pouch position.
        /// </summary>
        private void UpdateRubberBands()
        {
            // Use current pouch position for rubber bands
            Vector3 pouchPos = m_Pouch != null ? m_Pouch.position : _currentPouchPosition;

            // Calculate color based on stretch
            float stretchRatio = Mathf.Clamp01(_currentPullDistance / m_MaxPullDistance);
            Color currentColor = Color.Lerp(m_RelaxedColor, m_StretchedColor, stretchRatio);

            // Update left band
            if (m_LeftBandRenderer != null && m_LeftForkTip != null)
            {
                m_LeftBandRenderer.SetPosition(0, m_LeftForkTip.position);
                m_LeftBandRenderer.SetPosition(1, pouchPos);
                m_LeftBandRenderer.startColor = currentColor;
                m_LeftBandRenderer.endColor = currentColor;

                // Adjust width based on stretch
                float width = Mathf.Lerp(0.015f, 0.008f, stretchRatio);
                m_LeftBandRenderer.startWidth = width;
                m_LeftBandRenderer.endWidth = width;
            }

            // Update right band
            if (m_RightBandRenderer != null && m_RightForkTip != null)
            {
                m_RightBandRenderer.SetPosition(0, m_RightForkTip.position);
                m_RightBandRenderer.SetPosition(1, pouchPos);
                m_RightBandRenderer.startColor = currentColor;
                m_RightBandRenderer.endColor = currentColor;

                float width = Mathf.Lerp(0.015f, 0.008f, stretchRatio);
                m_RightBandRenderer.startWidth = width;
                m_RightBandRenderer.endWidth = width;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the current pull distance as a ratio (0-1).
        /// </summary>
        public float PullRatio => _currentPullDistance / m_MaxPullDistance;

        /// <summary>
        /// Gets whether the pouch is currently being held.
        /// </summary>
        public bool IsPouchGrabbed => _isPouchGrabbed;

        /// <summary>
        /// Enables or disables the slingshot interaction.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (_pouchInteractable != null)
                _pouchInteractable.enabled = enabled;

            // Show/hide visuals
            gameObject.SetActive(enabled);
        }

        #endregion
    }
}
