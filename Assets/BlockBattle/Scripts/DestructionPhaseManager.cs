using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;
using Unity.Netcode;
using BlockBattle.Network;

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
        private float m_CompletionDelay = 1f;

        [Header("Debug")]
        [SerializeField, Tooltip("Show debug logs")]
        private bool m_DebugMode = true;

        [Header("Multiplayer")]
        [SerializeField, Tooltip("The workspace index this manager belongs to")]
        private int m_WorkspaceIndex = 0;

        [SerializeField, Tooltip("The opponent's build zone to target in multiplayer")]
        private BuildZone m_OpponentBuildZone;

        [SerializeField, Tooltip("Whether to use opponent's build zone in multiplayer mode (false = shoot own structure)")]
        private bool m_UseOpponentZone = false;

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
        private bool _isMultiplayerMode = false;
        private BuildZone _activeBuildZone; // The zone to check (own or opponent's)

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

        /// <summary>
        /// Gets or sets the workspace index.
        /// </summary>
        public int WorkspaceIndex
        {
            get => m_WorkspaceIndex;
            set => m_WorkspaceIndex = value;
        }

        /// <summary>
        /// Gets or sets the opponent's build zone for multiplayer.
        /// </summary>
        public BuildZone OpponentBuildZone
        {
            get => m_OpponentBuildZone;
            set => m_OpponentBuildZone = value;
        }

        private void Start()
        {
            // Find references if not assigned
            if (m_Slingshot == null)
                m_Slingshot = FindAnyObjectByType<VRSlingshot>();
            if (m_BuildZone == null)
                m_BuildZone = FindAnyObjectByType<BuildZone>();

            // Check multiplayer mode
            CheckMultiplayerMode();

            // Initially disable slingshot
            if (m_Slingshot != null)
            {
                m_Slingshot.SetEnabled(false);
            }
        }

        /// <summary>
        /// Checks if we're in multiplayer mode and sets up accordingly.
        /// </summary>
        private void CheckMultiplayerMode()
        {
            _isMultiplayerMode = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

            if (_isMultiplayerMode)
            {
                // Try to find opponent's build zone through PlayerWorkspaceManager
                SetupMultiplayerZones();
            }
        }

        /// <summary>
        /// Sets up the build zones for multiplayer (target opponent's zone).
        /// </summary>
        private void SetupMultiplayerZones()
        {
            if (!m_UseOpponentZone)
            {
                _activeBuildZone = m_BuildZone;
                return;
            }

            // If opponent zone is already set, use it
            if (m_OpponentBuildZone != null)
            {
                _activeBuildZone = m_OpponentBuildZone;
                return;
            }

            // Try to find through workspace manager
            var workspaceManager = PlayerWorkspaceManager.Instance;
            if (workspaceManager != null)
            {
                var localWorkspace = workspaceManager.GetLocalPlayerWorkspace();
                if (localWorkspace != null)
                {
                    var opponentWorkspace = localWorkspace.OpponentWorkspace;
                    if (opponentWorkspace != null)
                    {
                        m_OpponentBuildZone = opponentWorkspace.BuildZone;
                        _activeBuildZone = m_OpponentBuildZone;
                        Debug.Log($"DestructionPhaseManager: Set up to target opponent's build zone");
                        return;
                    }
                }
            }

            // Fallback to own build zone
            Debug.LogWarning("DestructionPhaseManager: Could not find opponent's build zone, using own");
            _activeBuildZone = m_BuildZone;
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

            // In multiplayer, set up to target opponent's zone
            if (_isMultiplayerMode)
            {
                SetupMultiplayerZones();
            }
            else
            {
                _activeBuildZone = m_BuildZone;
            }

            // Count initial blocks
            _totalBlocks = CountBlocksInZone();
            _lastBlockCount = _totalBlocks;

            if (m_DebugMode)
            {
                string zoneInfo = _isMultiplayerMode ? "(opponent's zone)" : "(own zone)";
                Debug.Log($"DestructionPhaseManager: Starting destruction phase with {_totalBlocks} blocks {zoneInfo}");
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
        /// Stops the destruction phase without completing it.
        /// </summary>
        public void StopDestructionPhase()
        {
            _isActive = false;
            _completionPending = false;

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
                _lastBlockCount = currentCount;
                
                if (m_DebugMode)
                {
                    Debug.Log($"DestructionPhaseManager: {currentCount} blocks remaining in zone");
                }

                OnBlockCountChanged?.Invoke(currentCount);

                // Check if all blocks are cleared
                if (currentCount == 0)
                {
                    if (m_DebugMode)
                    {
                        Debug.Log("DestructionPhaseManager: All blocks cleared! Starting completion delay...");
                    }
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
            // Use the active build zone (opponent's in multiplayer, own in single-player)
            BuildZone zoneToCheck = _activeBuildZone ?? m_BuildZone;

            if (zoneToCheck == null)
            {
                Debug.LogWarning("DestructionPhaseManager: No BuildZone to check!");
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
                if (name.Contains("Block_") || name.Contains("_Shelf") || name.Contains("_Spawned") || name.Contains("_Net"))
                {
                    // Check if in zone
                    if (zoneToCheck.IsInZone(interactable.gameObject))
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

            // Use the active build zone
            BuildZone zoneToCheck = _activeBuildZone ?? m_BuildZone;
            if (zoneToCheck == null)
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

                if (name.Contains("Block_") || name.Contains("_Shelf") || name.Contains("_Spawned") || name.Contains("_Net"))
                {
                    if (zoneToCheck.IsInZone(interactable.gameObject))
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

            // Disable slingshot
            if (m_Slingshot != null)
            {
                m_Slingshot.SetEnabled(false);
            }

            if (m_DebugMode)
            {
                Debug.Log("DestructionPhaseManager: Destruction phase complete!");
            }

            // Report to network manager in multiplayer
            if (_isMultiplayerMode && NetworkedLevelManager.Instance != null)
            {
                NetworkedLevelManager.Instance.ReportDestructionCompleteServerRpc();
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
