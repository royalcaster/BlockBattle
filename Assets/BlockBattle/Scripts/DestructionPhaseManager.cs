using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

namespace BlockBattle
{
    /// <summary>
    /// Manages the destruction phase of the game where the player shoots at their built structure
    /// with a slingshot until all blocks are knocked off the table/build zone.
    /// </summary>
    public class DestructionPhaseManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("The slingshot used for shooting")]
        private VRSlingshot m_Slingshot;

        [SerializeField, Tooltip("The build zone to check for remaining blocks")]
        private BuildZone m_BuildZone;

        [SerializeField, Tooltip("Transform where the player should be teleported for shooting")]
        private Transform m_ShootingPosition;

        [Header("Settings")]
        [SerializeField, Tooltip("How often to check for remaining blocks (seconds)")]
        private float m_CheckInterval = 0.5f;

        [SerializeField, Tooltip("Delay after all blocks are cleared before completing phase")]
        private float m_CompletionDelay = 2f;

        [SerializeField, Tooltip("Grace period at start of destruction phase where completion cannot trigger (seconds)")]
        private float m_StartGracePeriod = 3f;

        [Header("Debug")]
        [SerializeField, Tooltip("Show debug logs")]
        private bool m_DebugMode = true;

        // Events
        /// <summary>
        /// Fired when the destruction phase starts.
        /// </summary>
        public event System.Action OnDestructionStarted;

        /// <summary>
        /// Fired when block count changes. Parameter is remaining block count.
        /// </summary>
        public event System.Action<int> OnBlockCountChanged;

        /// <summary>
        /// Fired when all blocks are cleared and destruction phase is complete.
        /// </summary>
        public event System.Action OnDestructionComplete;

        // Runtime state
        private bool _isActive = false;
        private float _checkTimer = 0f;
        private int _lastBlockCount = -1;
        private int _totalBlocks = 0;
        private bool _completionPending = false;
        private float _completionTimer = 0f;
        private float _startTime = 0f;

        /// <summary>
        /// Gets whether the destruction phase is currently active.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Gets the number of blocks remaining in the build zone.
        /// </summary>
        public int RemainingBlocks => _lastBlockCount;

        /// <summary>
        /// Gets the total number of blocks at the start of destruction phase.
        /// </summary>
        public int TotalBlocks => _totalBlocks;

        /// <summary>
        /// Gets the shooting position transform.
        /// </summary>
        public Transform ShootingPosition => m_ShootingPosition;

        private void Start()
        {
            // Find references if not assigned
            if (m_Slingshot == null)
                m_Slingshot = FindAnyObjectByType<VRSlingshot>();
            if (m_BuildZone == null)
                m_BuildZone = FindAnyObjectByType<BuildZone>();

            // Initially disable slingshot
            if (m_Slingshot != null)
            {
                m_Slingshot.SetEnabled(false);
            }
        }

        private void Update()
        {
            if (!_isActive)
                return;

            // Check for completion pending
            if (_completionPending)
            {
                _completionTimer += Time.deltaTime;
                if (_completionTimer >= m_CompletionDelay)
                {
                    CompleteDestructionPhase();
                }
                return;
            }

            // Don't check for completion during grace period
            if (Time.time - _startTime < m_StartGracePeriod)
                return;

            // Periodic check for remaining blocks
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= m_CheckInterval)
            {
                _checkTimer = 0f;
                CheckRemainingBlocks();
            }
        }

        #region Public API

        /// <summary>
        /// Starts the destruction phase.
        /// </summary>
        public void StartDestructionPhase()
        {
            if (_isActive)
            {
                Debug.LogWarning("DestructionPhaseManager: Destruction phase already active!");
                return;
            }

            _isActive = true;
            _completionPending = false;
            _completionTimer = 0f;
            _checkTimer = 0f;
            _lastBlockCount = -1;
            _startTime = Time.time;

            // Disable interaction on all blocks in the zone
            DisableBlockInteractions();

            // Count initial blocks
            _totalBlocks = CountBlocksInZone();
            _lastBlockCount = _totalBlocks;

            if (m_DebugMode)
            {
                Debug.Log($"DestructionPhaseManager: Starting destruction phase with {_totalBlocks} blocks. Grace period: {m_StartGracePeriod}s");
            }

            // Enable slingshot
            if (m_Slingshot != null)
            {
                m_Slingshot.SetEnabled(true);
            }

            OnDestructionStarted?.Invoke();
            OnBlockCountChanged?.Invoke(_totalBlocks);
        }

        /// <summary>
        /// Disables XR Grab interactions on all blocks in the zone.
        /// </summary>
        private void DisableBlockInteractions()
        {
            List<GameObject> blocks = GetBlocksInZone();
            foreach (GameObject block in blocks)
            {
                XRGrabInteractable grab = block.GetComponent<XRGrabInteractable>();
                if (grab != null)
                {
                    grab.enabled = false;
                }
            }
            
            if (m_DebugMode)
            {
                Debug.Log($"DestructionPhaseManager: Disabled interactions on {blocks.Count} blocks.");
            }
        }

        /// <summary>
        /// Re-enables XR Grab interactions on all blocks in the zone.
        /// </summary>
        private void EnableBlockInteractions()
        {
            // We need to find all player blocks, even those outside the zone if they were knocked out
            XRGrabInteractable[] allInteractables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var grab in allInteractables)
            {
                if (grab == null || grab.gameObject == null) continue;
                
                string name = grab.gameObject.name;
                if (name.Contains("Block_") || name.Contains("_Shelf") || name.Contains("_Spawned"))
                {
                    if (!name.StartsWith("ReferenceBlock_") && !name.Contains("Reference"))
                    {
                        grab.enabled = true;
                        count++;
                    }
                }
            }
            
            if (m_DebugMode)
            {
                Debug.Log($"DestructionPhaseManager: Re-enabled interactions on {count} blocks.");
            }
        }

        /// <summary>
        /// Stops the destruction phase without completing it.
        /// </summary>
        public void StopDestructionPhase()
        {
            _isActive = false;
            _completionPending = false;

            // Re-enable interactions when phase stops
            EnableBlockInteractions();

            // Disable slingshot
            if (m_Slingshot != null)
            {
                m_Slingshot.SetEnabled(false);
            }

            if (m_DebugMode)
            {
                Debug.Log("DestructionPhaseManager: Destruction phase stopped");
            }
        }

        /// <summary>
        /// Forces completion of the destruction phase (for testing/skip).
        /// </summary>
        public void ForceComplete()
        {
            if (_isActive)
            {
                CompleteDestructionPhase();
            }
        }

        #endregion

        #region Block Detection

        /// <summary>
        /// Checks how many blocks remain in the build zone.
        /// </summary>
        private void CheckRemainingBlocks()
        {
            int currentCount = CountBlocksInZone();

            // Check if count changed
            if (currentCount != _lastBlockCount)
            {
                Debug.Log($"DestructionPhaseManager: Block count changed from {_lastBlockCount} to {currentCount}");
                _lastBlockCount = currentCount;
                
                if (m_DebugMode)
                {
                    Debug.Log($"DestructionPhaseManager: {currentCount} blocks remaining in zone");
                }

                OnBlockCountChanged?.Invoke(currentCount);

                // Check if all blocks are cleared
                if (currentCount == 0)
                {
                    Debug.Log("DestructionPhaseManager: All blocks cleared! Starting completion delay...");
                    _completionPending = true;
                    _completionTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Counts the number of player blocks currently in the build zone.
        /// </summary>
        /// <returns>Number of blocks in the zone</returns>
        private int CountBlocksInZone()
        {
            if (m_BuildZone == null)
            {
                Debug.LogWarning("DestructionPhaseManager: No BuildZone assigned!");
                return 0;
            }

            int count = 0;

            // Find all XRGrabInteractable objects (player blocks)
            XRGrabInteractable[] allInteractables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);

            foreach (var interactable in allInteractables)
            {
                if (interactable == null || interactable.gameObject == null)
                    continue;

                // Skip if being held
                if (interactable.isSelected)
                    continue;

                string name = interactable.gameObject.name;

                // Skip reference blocks
                if (name.StartsWith("ReferenceBlock_") || name.Contains("Reference"))
                    continue;

                // Skip projectiles
                if (name.Contains("Projectile") || name.Contains("Ball"))
                    continue;

                // Check if it's a player block
                if (name.Contains("Block_") || name.Contains("_Shelf") || name.Contains("_Spawned"))
                {
                    // Check if in zone
                    if (m_BuildZone.IsInZone(interactable.gameObject))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Gets a list of all player blocks currently in the build zone.
        /// </summary>
        public List<GameObject> GetBlocksInZone()
        {
            List<GameObject> blocks = new List<GameObject>();

            if (m_BuildZone == null)
                return blocks;

            XRGrabInteractable[] allInteractables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);

            foreach (var interactable in allInteractables)
            {
                if (interactable == null || interactable.gameObject == null)
                    continue;

                string name = interactable.gameObject.name;

                if (name.StartsWith("ReferenceBlock_") || name.Contains("Reference"))
                    continue;

                if (name.Contains("Projectile") || name.Contains("Ball"))
                    continue;

                if (name.Contains("Block_") || name.Contains("_Shelf") || name.Contains("_Spawned"))
                {
                    if (m_BuildZone.IsInZone(interactable.gameObject))
                    {
                        blocks.Add(interactable.gameObject);
                    }
                }
            }

            return blocks;
        }

        #endregion

        #region Phase Completion

        /// <summary>
        /// Completes the destruction phase.
        /// </summary>
        private void CompleteDestructionPhase()
        {
            _isActive = false;
            _completionPending = false;

            // Re-enable interactions when phase complete
            EnableBlockInteractions();

            // Disable slingshot
            if (m_Slingshot != null)
            {
                m_Slingshot.SetEnabled(false);
            }

            if (m_DebugMode)
            {
                Debug.Log("DestructionPhaseManager: Destruction phase complete!");
            }

            OnDestructionComplete?.Invoke();
        }

        #endregion

        #region Editor Visualization

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw shooting position
            if (m_ShootingPosition != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(m_ShootingPosition.position, 0.3f);
                Gizmos.DrawLine(m_ShootingPosition.position, m_ShootingPosition.position + m_ShootingPosition.forward * 2f);
                
                // Draw player representation
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Gizmos.DrawCube(m_ShootingPosition.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.3f));
            }
        }
#endif

        #endregion
    }
}
