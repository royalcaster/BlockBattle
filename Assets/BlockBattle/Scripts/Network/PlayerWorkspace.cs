using Unity.Netcode;
using UnityEngine;

namespace BlockBattle.Network
{
    /// <summary>
    /// Represents a player's workspace containing their table, build zone, shelf, and slingshot.
    /// This component manages all the gameplay elements for a single player's area.
    /// </summary>
    public class PlayerWorkspace : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Workspace Configuration")]
        [SerializeField, Tooltip("The index of this workspace (0 or 1 for two-player)")]
        private int _workspaceIndex;

        [SerializeField, Tooltip("The spawn point for the player in this workspace")]
        private Transform _playerSpawnPoint;

        [SerializeField, Tooltip("The position where the player is teleported for destruction phase (facing opponent)")]
        private Transform _destructionPhasePosition;

        [Header("Gameplay Components")]
        [SerializeField, Tooltip("The table in this workspace")]
        private GameObject _table;

        [SerializeField, Tooltip("The build zone in this workspace")]
        private BuildZone _buildZone;

        [SerializeField, Tooltip("The shelf spawner in this workspace")]
        private ShelfBlockSpawner _shelfBlockSpawner;

        [SerializeField, Tooltip("The reference structure spawner in this workspace")]
        private ReferenceStructureSpawner _referenceStructureSpawner;

        [SerializeField, Tooltip("The build validator for this workspace")]
        private BuildValidator _buildValidator;

        [SerializeField, Tooltip("The slingshot in this workspace")]
        private VRSlingshot _slingshot;

        [Header("Target Workspace")]
        [SerializeField, Tooltip("Reference to the opponent's workspace (for destruction phase targeting)")]
        private PlayerWorkspace _opponentWorkspace;

        #endregion

        #region Private Fields

        private ulong _assignedPlayerId = ulong.MaxValue;
        private bool _isAssigned = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the index of this workspace.
        /// </summary>
        public int WorkspaceIndex => _workspaceIndex;

        /// <summary>
        /// Gets the player ID assigned to this workspace.
        /// </summary>
        public ulong AssignedPlayerId => _assignedPlayerId;

        /// <summary>
        /// Gets whether this workspace has been assigned to a player.
        /// </summary>
        public bool IsAssigned => _isAssigned;

        /// <summary>
        /// Gets the player spawn point for this workspace.
        /// </summary>
        public Transform PlayerSpawnPoint => _playerSpawnPoint;

        /// <summary>
        /// Gets the destruction phase position for this workspace.
        /// </summary>
        public Transform DestructionPhasePosition => _destructionPhasePosition;

        /// <summary>
        /// Gets the table in this workspace.
        /// </summary>
        public GameObject Table => _table;

        /// <summary>
        /// Gets the build zone in this workspace.
        /// </summary>
        public BuildZone BuildZone => _buildZone;

        /// <summary>
        /// Gets the shelf block spawner in this workspace.
        /// </summary>
        public ShelfBlockSpawner ShelfBlockSpawner => _shelfBlockSpawner;

        /// <summary>
        /// Gets the reference structure spawner in this workspace.
        /// </summary>
        public ReferenceStructureSpawner ReferenceStructureSpawner => _referenceStructureSpawner;

        /// <summary>
        /// Gets the build validator for this workspace.
        /// </summary>
        public BuildValidator BuildValidator => _buildValidator;

        /// <summary>
        /// Gets the slingshot in this workspace.
        /// </summary>
        public VRSlingshot Slingshot => _slingshot;

        /// <summary>
        /// Gets the opponent's workspace.
        /// </summary>
        public PlayerWorkspace OpponentWorkspace => _opponentWorkspace;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when a player is assigned to this workspace.
        /// </summary>
        public event System.Action<ulong> OnPlayerAssigned;

        /// <summary>
        /// Event fired when the player is unassigned from this workspace.
        /// </summary>
        public event System.Action OnPlayerUnassigned;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ValidateReferences();
        }

        private void Start()
        {
            // Initially disable the slingshot until destruction phase
            if (_slingshot != null)
            {
                _slingshot.SetEnabled(false);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Assigns a player to this workspace.
        /// </summary>
        /// <param name="playerId">The network client ID of the player</param>
        public void AssignToPlayer(ulong playerId)
        {
            if (_isAssigned && _assignedPlayerId != playerId)
            {
                Debug.LogWarning($"PlayerWorkspace {_workspaceIndex}: Already assigned to player {_assignedPlayerId}, reassigning to {playerId}");
            }

            _assignedPlayerId = playerId;
            _isAssigned = true;

            Debug.Log($"PlayerWorkspace {_workspaceIndex}: Assigned to player {playerId}");

            OnPlayerAssigned?.Invoke(playerId);
        }

        /// <summary>
        /// Unassigns the current player from this workspace.
        /// </summary>
        public void Unassign()
        {
            if (!_isAssigned) return;

            ulong previousPlayer = _assignedPlayerId;
            _assignedPlayerId = ulong.MaxValue;
            _isAssigned = false;

            Debug.Log($"PlayerWorkspace {_workspaceIndex}: Unassigned player {previousPlayer}");

            OnPlayerUnassigned?.Invoke();
        }

        /// <summary>
        /// Checks if this workspace belongs to the local player.
        /// </summary>
        /// <returns>True if assigned to the local player</returns>
        public bool IsLocalPlayerWorkspace()
        {
            if (!_isAssigned) return false;
            if (NetworkManager.Singleton == null) return false;

            return _assignedPlayerId == NetworkManager.Singleton.LocalClientId;
        }

        /// <summary>
        /// Gets the build zone of the opponent's workspace.
        /// Used for destruction phase targeting.
        /// </summary>
        /// <returns>The opponent's build zone, or null if not set</returns>
        public BuildZone GetOpponentBuildZone()
        {
            return _opponentWorkspace != null ? _opponentWorkspace.BuildZone : null;
        }

        /// <summary>
        /// Spawns blocks in the shelf for this workspace.
        /// </summary>
        /// <param name="configuration">The block spawn configuration</param>
        public void SpawnBlocks(BlockSpawnConfiguration configuration)
        {
            if (_shelfBlockSpawner == null)
            {
                Debug.LogError($"PlayerWorkspace {_workspaceIndex}: ShelfBlockSpawner is not assigned!");
                return;
            }

            _shelfBlockSpawner.SpawnConfiguration = configuration;
            _shelfBlockSpawner.SpawnBlocks();
        }

        /// <summary>
        /// Spawns the reference structure for this workspace.
        /// </summary>
        /// <param name="configuration">The block spawn configuration</param>
        public void SpawnReferenceStructure(BlockSpawnConfiguration configuration)
        {
            if (_referenceStructureSpawner == null)
            {
                Debug.LogError($"PlayerWorkspace {_workspaceIndex}: ReferenceStructureSpawner is not assigned!");
                return;
            }

            _referenceStructureSpawner.SpawnStructure(configuration);
        }

        /// <summary>
        /// Sets up the build validator for this workspace.
        /// </summary>
        /// <param name="configuration">The reference configuration</param>
        public void SetupValidator(BlockSpawnConfiguration configuration)
        {
            if (_buildValidator == null)
            {
                Debug.LogError($"PlayerWorkspace {_workspaceIndex}: BuildValidator is not assigned!");
                return;
            }

            _buildValidator.ReferenceConfiguration = configuration;
            _buildValidator.ResetAlignmentLock();
        }

        /// <summary>
        /// Enables or disables the slingshot for destruction phase.
        /// </summary>
        /// <param name="enabled">Whether to enable the slingshot</param>
        public void SetSlingshotEnabled(bool enabled)
        {
            if (_slingshot != null)
            {
                _slingshot.SetEnabled(enabled);
            }
        }

        /// <summary>
        /// Clears all blocks from this workspace.
        /// </summary>
        public void ClearWorkspace()
        {
            if (_shelfBlockSpawner != null)
            {
                _shelfBlockSpawner.ClearSpawnedBlocks();
            }

            if (_referenceStructureSpawner != null)
            {
                _referenceStructureSpawner.ClearStructure();
            }
        }

        /// <summary>
        /// Validates the current build in this workspace.
        /// </summary>
        /// <returns>The validation result</returns>
        public BuildValidationResult ValidateBuild()
        {
            if (_buildValidator == null)
            {
                Debug.LogError($"PlayerWorkspace {_workspaceIndex}: BuildValidator is not assigned!");
                return null;
            }

            return _buildValidator.ValidateBuild();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Validates that all required references are assigned.
        /// </summary>
        private void ValidateReferences()
        {
            if (_playerSpawnPoint == null)
                Debug.LogWarning($"PlayerWorkspace {_workspaceIndex}: PlayerSpawnPoint is not assigned!");

            if (_table == null)
                Debug.LogWarning($"PlayerWorkspace {_workspaceIndex}: Table is not assigned!");

            if (_buildZone == null)
                Debug.LogWarning($"PlayerWorkspace {_workspaceIndex}: BuildZone is not assigned!");

            if (_shelfBlockSpawner == null)
                Debug.LogWarning($"PlayerWorkspace {_workspaceIndex}: ShelfBlockSpawner is not assigned!");

            if (_referenceStructureSpawner == null)
                Debug.LogWarning($"PlayerWorkspace {_workspaceIndex}: ReferenceStructureSpawner is not assigned!");

            if (_buildValidator == null)
                Debug.LogWarning($"PlayerWorkspace {_workspaceIndex}: BuildValidator is not assigned!");

            if (_slingshot == null)
                Debug.LogWarning($"PlayerWorkspace {_workspaceIndex}: Slingshot is not assigned!");
        }

        #endregion

        #region Editor Visualization

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw workspace bounds
            Gizmos.color = _workspaceIndex == 0 ? new Color(0, 0, 1, 0.3f) : new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(3f, 2f, 3f));

            // Draw player spawn point
            if (_playerSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_playerSpawnPoint.position, 0.3f);
                Gizmos.DrawLine(_playerSpawnPoint.position, _playerSpawnPoint.position + _playerSpawnPoint.forward * 0.5f);
            }

            // Draw destruction phase position
            if (_destructionPhasePosition != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_destructionPhasePosition.position, 0.3f);
                Gizmos.DrawLine(_destructionPhasePosition.position, _destructionPhasePosition.position + _destructionPhasePosition.forward * 0.5f);
            }

            // Draw connection to opponent workspace
            if (_opponentWorkspace != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, _opponentWorkspace.transform.position);
            }
        }
#endif

        #endregion
    }
}
