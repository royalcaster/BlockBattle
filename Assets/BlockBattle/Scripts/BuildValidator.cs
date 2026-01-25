using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.Netcode;
using BlockBattle.Network;

namespace BlockBattle
{
    /// <summary>
    /// Result of validating a single block against its reference.
    /// </summary>
    [System.Serializable]
    public class BlockValidationResult
    {
        public GameObject PlacedBlock { get; set; }
        public BlockSpawnEntry ReferenceEntry { get; set; }
        public float PositionError { get; set; }
        public float RotationError { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsPresent { get; set; } // Block of correct type/color is in zone
        public bool IsPositionCorrect { get; set; }
        public bool IsRotationCorrect { get; set; }
        public BlockType BlockType { get; set; }
        public BlockColor BlockColor { get; set; }
        public Vector3 ExpectedRelativePosition { get; set; }
        public Vector3 ActualRelativePosition { get; set; }
    }

    /// <summary>
    /// Result of validating an entire build against a reference structure.
    /// </summary>
    [System.Serializable]
    public class BuildValidationResult
    {
        public float AccuracyPercentage { get; set; }
        public float PresencePercentage { get; set; } // Blocks present regardless of position
        public float PositionAccuracy { get; set; } // Position-only accuracy
        public int CorrectBlocks { get; set; }
        public int PresentBlocks { get; set; } // Blocks with correct type/color in zone
        public int TotalReferenceBlocks { get; set; }
        public int PlacedBlocksFound { get; set; }
        public List<BlockValidationResult> BlockResults { get; set; }
        public List<GameObject> ExtraBlocks { get; set; }
        public List<BlockSpawnEntry> MissingBlocks { get; set; }
        public Vector3 BuildCenter { get; set; } // Center of player's build
        public Vector3 ReferenceCenter { get; set; } // Center of reference structure

        public BuildValidationResult()
        {
            BlockResults = new List<BlockValidationResult>();
            ExtraBlocks = new List<GameObject>();
            MissingBlocks = new List<BlockSpawnEntry>();
        }
    }

    /// <summary>
    /// Validates a player's build against a reference structure.
    /// Uses relative position matching to allow building anywhere in the build zone.
    /// </summary>
    public class BuildValidator : MonoBehaviour
    {
        [Header("Validation Mode")]
        [SerializeField, Tooltip("If true, only check presence of correct type/color (ignore position/rotation)")]
        private bool m_PresenceOnlyValidation = false;

        [Header("Position Validation")]
        [SerializeField, Tooltip("Maximum position error in meters for a block to be considered correctly placed")]
        private float m_PositionTolerance = 0.10f; // 10cm - reasonable for VR

        [SerializeField, Tooltip("Scale factor applied to reference structure positions (for size adjustment)")]
        private float m_StructureScale = 1.0f;

        [SerializeField, Tooltip("Height offset added to expected positions (to account for table height vs structure base)")]
        private float m_HeightOffset = 0.12f; // Offset to raise expected positions to table level

        [Header("Rotation Validation")]
        [SerializeField, Tooltip("If true, validate rotation as well as position")]
        private bool m_ValidateRotation = false;

        [SerializeField, Tooltip("Maximum rotation error in degrees for a block to be considered correct (15-20 recommended for placement guides)")]
        private float m_RotationTolerance = 20f;

        [Header("Auto-Alignment")]
        [SerializeField, Tooltip("Automatically rotate reference to best match player's build orientation. Disable when using placement guides.")]
        private bool m_AutoAlignToBuild = true;
        
        [SerializeField, Tooltip("Fixed rotation to use when auto-align is disabled (degrees)")]
        private float m_FixedRotation = 0f;

        [Header("Detection Settings")]
        [SerializeField, Tooltip("Maximum height above build zone to detect blocks")]
        private float m_MaxHeightAboveTable = 1.0f;

        [Header("References")]
        [SerializeField] private GameObject m_Table;
        [SerializeField] private BuildZone m_BuildZone;
        [SerializeField] private BlockSpawnConfiguration m_ReferenceConfiguration;
        [SerializeField] private ReferenceStructureSpawner m_ReferenceStructureSpawner;

        [Header("Debug Visualization")]
        [SerializeField, Tooltip("Show debug gizmos in scene view")]
        private bool m_ShowDebugGizmos = true;

        [SerializeField, Tooltip("Show expected block positions as wireframes")]
        private bool m_ShowExpectedPositions = true;
        
        [SerializeField, Tooltip("Enable verbose console logging (disable for cleaner logs)")]
        private bool m_VerboseLogging = false;

        [Header("Multiplayer")]
        [SerializeField, Tooltip("The workspace index this validator belongs to (for multiplayer)")]
        private int m_WorkspaceIndex = 0;

        [SerializeField, Tooltip("Automatically report completion to NetworkedLevelManager")]
        private bool m_AutoReportToNetwork = true;

        // Cached data for gizmo drawing
        private BuildValidationResult m_LastValidationResult;
        private List<Vector3> m_ExpectedWorldPositions = new List<Vector3>();
        private Vector3 m_LastBuildCenter;
        private float m_LastBestRotation = 0f;
        
        // Auto-alignment state: lock rotation after first block
        private bool m_RotationLocked = false;
        private float m_LockedRotation = 0f;
        private int m_LastPlacedBlockCount = 0;
        private bool m_Initialized = false;
        
        // Effective height offset (may be adjusted for single-block structures)
        private float m_EffectiveHeightOffset = 0.12f;

        // Multiplayer state
        private bool _isMultiplayerMode = false;
        private bool _hasReportedCompletion = false;
        private float _lastReportedAccuracy = 0f;

        private void Awake()
        {
            // Reset alignment state on start
            ResetAlignmentLock();
            
            // Check multiplayer mode
            CheckMultiplayerMode();
        }

        /// <summary>
        /// Checks if we're in multiplayer mode.
        /// </summary>
        private void CheckMultiplayerMode()
        {
            _isMultiplayerMode = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        }

        /// <summary>
        /// Gets or sets the workspace index for this validator.
        /// </summary>
        public int WorkspaceIndex
        {
            get => m_WorkspaceIndex;
            set => m_WorkspaceIndex = value;
        }

        public float PositionTolerance
        {
            get => m_PositionTolerance;
            set => m_PositionTolerance = Mathf.Max(0.01f, value);
        }

        public float RotationTolerance
        {
            get => m_RotationTolerance;
            set => m_RotationTolerance = Mathf.Max(1f, value);
        }

        public float StructureScale => m_StructureScale;

        public float HeightOffset => m_HeightOffset;

        public BlockSpawnConfiguration ReferenceConfiguration
        {
            get => m_ReferenceConfiguration;
            set => m_ReferenceConfiguration = value;
        }

        /// <summary>
        /// The last calculated auto-alignment rotation offset (in degrees).
        /// </summary>
        public float LastAlignmentRotation => m_LastBestRotation;

        /// <summary>
        /// Resets the auto-alignment rotation lock.
        /// Call this when starting a new build or clearing the zone.
        /// </summary>
        public void ResetAlignmentLock()
        {
            m_RotationLocked = false;
            m_LockedRotation = 0f;
            m_LastBestRotation = 0f;
            m_LastPlacedBlockCount = 0;
            m_Initialized = true; // Mark as initialized after reset

            // Reset network reporting state
            _hasReportedCompletion = false;
            _lastReportedAccuracy = 0f;

            if (m_VerboseLogging) Debug.Log("BuildValidator: Alignment lock reset");
        }

        /// <summary>
        /// Validates the current build against the reference configuration.
        /// </summary>
        public BuildValidationResult ValidateBuild()
        {
            BuildValidationResult result = new BuildValidationResult();

            // Re-check multiplayer mode (in case we started before network connected)
            CheckMultiplayerMode();

            // Validate configuration
            if (m_ReferenceConfiguration == null)
            {
                Debug.LogError("BuildValidator: No reference configuration assigned!");
                return result;
            }

            if (!m_ReferenceConfiguration.Validate())
            {
                Debug.LogError($"BuildValidator: Configuration '{m_ReferenceConfiguration.ConfigurationName}' is invalid!");
                return result;
            }

            // Find dependencies
            if (m_Table == null) m_Table = GameObject.Find("Table");
            if (m_BuildZone == null) m_BuildZone = FindAnyObjectByType<BuildZone>();

            if (m_Table == null && m_BuildZone == null)
            {
                Debug.LogError("BuildValidator: No Table or BuildZone found!");
                return result;
            }

            // Step 1: Collect all placed blocks in the build zone
            List<GameObject> placedBlocks = CollectPlacedBlocks();
            result.PlacedBlocksFound = placedBlocks.Count;

            // Step 2: Get reference entries
            List<BlockSpawnEntry> referenceEntries = m_ReferenceConfiguration.SpawnEntries.ToList();
            result.TotalReferenceBlocks = referenceEntries.Count;

            // Step 3: Calculate centers
            Vector3 referenceCenter = CalculateReferenceCenter(referenceEntries);
            Vector3 buildCenter = CalculateBuildCenter(placedBlocks, referenceEntries);
            
            result.ReferenceCenter = referenceCenter;
            result.BuildCenter = buildCenter;
            m_LastBuildCenter = buildCenter;

            // Special handling for single-block structures at Y=0: adjust height offset
            // When reference center Y is 0 and there's only one block, the block is likely sitting
            // directly on the floor, so its center is at ~0.05m (half cube height) rather than 0.12m
            m_EffectiveHeightOffset = m_HeightOffset;
            if (referenceEntries.Count == 1 && Mathf.Abs(referenceCenter.y) < 0.01f)
            {
                // For single block at Y=0, use a smaller offset that accounts for block center height
                // Cube blocks are 0.1m tall, so center is at 0.05m above floor
                m_EffectiveHeightOffset = 0.05f;
                if (m_VerboseLogging) Debug.Log($"BuildValidator: Single-block structure detected at Y=0, using adjusted height offset: {m_EffectiveHeightOffset}m (instead of {m_HeightOffset}m)");
            }

            // Verbose logging - only when enabled
            if (m_VerboseLogging)
            {
            if (m_BuildZone != null)
            {
                Debug.Log($"Build zone position: {m_BuildZone.transform.position}");
            }
            Debug.Log($"Mode: {(m_PresenceOnlyValidation ? "PRESENCE ONLY" : "POSITION + ROTATION")}");
            Debug.Log($"Tolerances: Position={m_PositionTolerance}m, Rotation={m_RotationTolerance}°");
            
            // Log expected positions for each reference block
            Debug.Log($"--- Expected positions (relative to zone center, with height offset {m_EffectiveHeightOffset}m) ---");
            foreach (var entry in referenceEntries)
            {
                Vector3 expectedRelPos = (entry.Position - referenceCenter) * m_StructureScale;
                expectedRelPos.y += m_EffectiveHeightOffset; // Apply effective height offset
                Vector3 expectedWorldPos = buildCenter + expectedRelPos;
                Debug.Log($"  {entry.BlockType} ({entry.BlockColor}): RelPos={expectedRelPos}, WorldPos={expectedWorldPos}");
                }
            }

            // Step 4: Match blocks
            if (m_PresenceOnlyValidation)
            {
                MatchBlocksPresenceOnly(placedBlocks, referenceEntries, result);
            }
            else if (m_AutoAlignToBuild && placedBlocks.Count > 0)
            {
                // Try multiple rotations and pick the best one
                MatchBlocksWithAutoAlignment(placedBlocks, referenceEntries, buildCenter, referenceCenter, result);
            }
            else if (!m_AutoAlignToBuild)
            {
                // Use fixed rotation (for placement guides mode)
                // Respect the user's m_ValidateRotation setting - don't force it ON
                // This allows users to choose whether rotation matters when using placement guides
                if (m_VerboseLogging)
                Debug.Log($"[PLACEMENT GUIDES MODE] Using fixed rotation {m_FixedRotation}°, rotation validation: {(m_ValidateRotation ? "ON" : "OFF")}");
                
                m_LastBestRotation = m_FixedRotation;
                MatchBlocksWithPosition(placedBlocks, referenceEntries, buildCenter, referenceCenter, result, m_FixedRotation);
            }
            else
            {
                MatchBlocksWithPosition(placedBlocks, referenceEntries, buildCenter, referenceCenter, result, 0f);
            }

            // Step 5: Calculate accuracies
            CalculateAccuracies(result);

            // Cache for gizmo drawing
            m_LastValidationResult = result;
            CacheExpectedPositions(referenceEntries, buildCenter, referenceCenter);

            if (m_VerboseLogging)
            {
            Debug.Log($"=== RESULT: {result.AccuracyPercentage:F1}% accuracy ({result.CorrectBlocks}/{result.TotalReferenceBlocks} correct) ===");
            Debug.Log($"Presence: {result.PresencePercentage:F1}% ({result.PresentBlocks}/{result.TotalReferenceBlocks} present)");
            }

            // Report to network in multiplayer mode
            ReportToNetworkIfNeeded(result);

            return result;
        }

        /// <summary>
        /// Reports validation results to the NetworkedLevelManager if in multiplayer mode.
        /// </summary>
        private void ReportToNetworkIfNeeded(BuildValidationResult result)
        {
            Debug.Log($"BuildValidator[W{m_WorkspaceIndex}]: ReportToNetworkIfNeeded called. Accuracy={result.AccuracyPercentage:F1}%, Multiplayer={_isMultiplayerMode}, AutoReport={m_AutoReportToNetwork}");
            
            if (!_isMultiplayerMode || !m_AutoReportToNetwork)
            {
                Debug.Log($"BuildValidator[W{m_WorkspaceIndex}]: Skipping report - Multiplayer={_isMultiplayerMode}, AutoReport={m_AutoReportToNetwork}");
                return;
            }
            
            if (NetworkedLevelManager.Instance == null)
            {
                Debug.LogWarning($"BuildValidator[W{m_WorkspaceIndex}]: NetworkedLevelManager.Instance is null!");
                return;
            }

            // Only report significant changes in accuracy (to avoid spamming)
            bool accuracyChanged = Mathf.Abs(result.AccuracyPercentage - _lastReportedAccuracy) > 1f;
            
            Debug.Log($"BuildValidator[W{m_WorkspaceIndex}]: AccuracyChanged={accuracyChanged}, LastReported={_lastReportedAccuracy:F1}%, HasReportedCompletion={_hasReportedCompletion}");
            
            // Report if accuracy reached 100% or changed significantly
            if (result.AccuracyPercentage >= 100f || accuracyChanged)
            {
                _lastReportedAccuracy = result.AccuracyPercentage;

                // Only call ServerRpc if we haven't already reported 100% completion
                if (!_hasReportedCompletion || result.AccuracyPercentage >= 100f)
                {
                    Debug.Log($"BuildValidator[W{m_WorkspaceIndex}]: Reporting accuracy {result.AccuracyPercentage:F1}% to NetworkedLevelManager");
                    
                    // In DA mode, ServerRpc doesn't work reliably - use direct call instead
                    // The NetworkedLevelManager handles the logic internally
                    NetworkedLevelManager.Instance.HandleBuildCompletion(m_WorkspaceIndex, result.AccuracyPercentage);
                    Debug.Log($"BuildValidator[W{m_WorkspaceIndex}]: HandleBuildCompletion call completed");

                    if (result.AccuracyPercentage >= 100f)
                    {
                        _hasReportedCompletion = true;
                        Debug.Log($"BuildValidator[W{m_WorkspaceIndex}]: Build COMPLETE! Reported to network.");
                    }
                }
            }
        }

        /// <summary>
        /// Collects all placed blocks within the build zone.
        /// In multiplayer mode, only collects blocks that belong to this workspace.
        /// </summary>
        private List<GameObject> CollectPlacedBlocks()
        {
            List<GameObject> placedBlocks = new List<GameObject>();
            XRGrabInteractable[] allInteractables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);

            foreach (XRGrabInteractable interactable in allInteractables)
            {
                if (interactable == null || interactable.gameObject == null) continue;
                if (interactable.isSelected) continue; // Skip held blocks
                
                GameObject block = interactable.gameObject;
                if (IsReferenceStructureBlock(block)) continue;
                if (!IsBlockInValidationArea(block)) continue;

                // In multiplayer mode, only count blocks belonging to this workspace
                if (_isMultiplayerMode)
                {
                    NetworkBlock networkBlock = block.GetComponent<NetworkBlock>();
                    if (networkBlock != null)
                    {
                        // Skip blocks from other workspaces
                        if (networkBlock.WorkspaceIndex != m_WorkspaceIndex && networkBlock.WorkspaceIndex >= 0)
                        {
                            continue;
                        }
                    }
                }

                placedBlocks.Add(block);
            }

            return placedBlocks;
        }

        /// <summary>
        /// Calculates the center of the reference structure from config.
        /// </summary>
        private Vector3 CalculateReferenceCenter(List<BlockSpawnEntry> entries)
        {
            if (entries == null || entries.Count == 0) return Vector3.zero;
            
            Vector3 sum = Vector3.zero;
            int validCount = 0;
            foreach (var entry in entries)
            {
                if (entry != null && IsValidVector(entry.Position))
                {
                    sum += entry.Position;
                    validCount++;
                }
            }
            
            if (validCount == 0) return Vector3.zero;
            
            Vector3 result = sum / validCount;
            
            // Safety check
            if (!IsValidVector(result))
            {
                Debug.LogWarning("BuildValidator: Invalid reference center calculated, using zero");
                return Vector3.zero;
            }
            
            return result;
        }

        /// <summary>
        /// Calculates the center of the player's build.
        /// ALWAYS uses the Build Zone center as the fixed anchor point.
        /// This ensures consistent positioning regardless of how many blocks are placed.
        /// </summary>
        private Vector3 CalculateBuildCenter(List<GameObject> placedBlocks, List<BlockSpawnEntry> referenceEntries)
        {
            Vector3 result = Vector3.zero;
            
            // ALWAYS use build zone center as the anchor point for player builds
            // This is critical - using placed blocks centroid fails when few blocks are placed
            if (m_BuildZone != null)
            {
                // Use the zone's floor center (zone position is at the floor)
                result = m_BuildZone.transform.position;
            }
            // Fallback: use table position if no build zone
            else if (m_Table != null)
            {
                result = m_Table.transform.position + Vector3.up * 0.1f;
            }
            // Last resort: if we have placed blocks, use their centroid
            else if (placedBlocks != null && placedBlocks.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                int validCount = 0;
                foreach (var block in placedBlocks)
                {
                    if (block != null && IsValidVector(block.transform.position))
                    {
                        sum += block.transform.position;
                        validCount++;
                    }
                }
                if (validCount > 0)
                {
                    result = sum / validCount;
                }
            }

            // Final safety check
            if (!IsValidVector(result))
            {
                Debug.LogWarning("BuildValidator: Invalid build center calculated, using zero");
                return Vector3.zero;
            }
            
            return result;
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
        /// Calculates the structure rotation from how the player placed their blocks.
        /// Tests all 4 cardinal rotations and picks the one that gives best match.
        /// </summary>
        private void MatchBlocksWithAutoAlignment(
            List<GameObject> placedBlocks,
            List<BlockSpawnEntry> referenceEntries,
            Vector3 buildCenter,
            Vector3 referenceCenter,
            BuildValidationResult result)
        {
            if (placedBlocks.Count == 0)
            {
                m_LastBestRotation = 0f;
                MatchBlocksWithPosition(placedBlocks, referenceEntries, buildCenter, referenceCenter, result, 0f);
                return;
            }

            // Simply test all 4 cardinal rotations and pick the best one
            float[] rotationsToTest = { 0f, 90f, 180f, 270f };
            float bestRotation = 0f;
            int bestCorrectCount = -1;
            float bestTotalError = float.MaxValue;

            foreach (float rotation in rotationsToTest)
            {
                var (correctCount, totalError) = EvaluateRotation(placedBlocks, referenceEntries, buildCenter, referenceCenter, rotation);
                
                if (correctCount > bestCorrectCount || 
                    (correctCount == bestCorrectCount && totalError < bestTotalError))
                {
                    bestCorrectCount = correctCount;
                    bestTotalError = totalError;
                    bestRotation = rotation;
                }
            }

            m_LastBestRotation = bestRotation;
            MatchBlocksWithPosition(placedBlocks, referenceEntries, buildCenter, referenceCenter, result, bestRotation);
        }

        /// <summary>
        /// Finds the most common rotation from a list of candidates (clusters similar values).
        /// </summary>
        private float FindBestRotationFromCandidates(List<float> candidates)
        {
            if (candidates.Count == 0) return 0f;
            if (candidates.Count == 1) return candidates[0];

            // Cluster rotations that are within 15 degrees of each other
            const float clusterThreshold = 15f;
            var clusters = new List<List<float>>();

            foreach (float rot in candidates)
            {
                bool addedToCluster = false;
                foreach (var cluster in clusters)
                {
                    float diff = Mathf.Abs(rot - cluster[0]);
                    // Handle wrap-around (e.g., 350° and 10° are close)
                    if (diff > 180f) diff = 360f - diff;
                    
                    if (diff < clusterThreshold)
                    {
                        cluster.Add(rot);
                        addedToCluster = true;
                        break;
                    }
                }
                if (!addedToCluster)
                {
                    clusters.Add(new List<float> { rot });
                }
            }

            // Find the largest cluster and return its average
            var bestCluster = clusters.OrderByDescending(c => c.Count).First();
            return bestCluster.Average();
        }

        /// <summary>
        /// Quickly evaluates how well a given rotation matches the placed blocks.
        /// Returns (correct count, total position error).
        /// </summary>
        private (int correctCount, float totalError) EvaluateRotation(
            List<GameObject> placedBlocks,
            List<BlockSpawnEntry> referenceEntries,
            Vector3 buildCenter,
            Vector3 referenceCenter,
            float yRotationOffset)
        {
            Quaternion rotOffset = Quaternion.Euler(0, yRotationOffset, 0);
            int correctCount = 0;
            float totalError = 0f;

            // Group by type+color for quick matching
            var refByTypeColor = referenceEntries
                .Where(e => e != null)
                .GroupBy(e => $"{e.BlockType}_{e.BlockColor}")
                .ToDictionary(g => g.Key, g => g.ToList());

            var placedByTypeColor = placedBlocks
                .Where(b => b != null)
                .GroupBy(b => $"{GetBlockType(b)}_{GetBlockColor(b)}")
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in refByTypeColor)
            {
                string key = kvp.Key;
                var refs = kvp.Value;
                
                if (!placedByTypeColor.TryGetValue(key, out var placed))
                    continue;

                // Calculate expected positions for this group
                var expectedPositions = refs.Select(entry =>
                {
                    Vector3 relPos = (entry.Position - referenceCenter) * m_StructureScale;
                    relPos.y += m_EffectiveHeightOffset;
                    return rotOffset * relPos;
                }).ToList();

                // Greedy match: for each placed block, find nearest expected position
                var usedPositions = new bool[expectedPositions.Count];
                
                foreach (var block in placed)
                {
                    Vector3 blockRelPos = block.transform.position - buildCenter;
                    float bestDist = float.MaxValue;
                    int bestIdx = -1;

                    for (int i = 0; i < expectedPositions.Count; i++)
                    {
                        if (usedPositions[i]) continue;
                        float dist = Vector3.Distance(blockRelPos, expectedPositions[i]);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestIdx = i;
                        }
                    }

                    if (bestIdx >= 0)
                    {
                        usedPositions[bestIdx] = true;
                        totalError += bestDist;
                        
                        if (bestDist <= m_PositionTolerance)
                        {
                            // Also check rotation if enabled
                            if (m_ValidateRotation)
                            {
                                Quaternion expectedRot = rotOffset * refs[bestIdx].Rotation;
                                float rotError = CalculateRotationError(block.transform.rotation, expectedRot, refs[bestIdx].AllowedRotations);
                                if (rotError <= m_RotationTolerance)
                                    correctCount++;
                            }
                            else
                            {
                                correctCount++;
                            }
                        }
                    }
                }
            }

            return (correctCount, totalError);
        }

        /// <summary>
        /// Matches blocks using position and rotation validation.
        /// Uses optimal matching for groups of identical blocks (same type+color).
        /// </summary>
        /// <param name="yRotationOffset">Y-axis rotation offset to apply to expected positions (in degrees)</param>
        private void MatchBlocksWithPosition(
            List<GameObject> placedBlocks, 
            List<BlockSpawnEntry> referenceEntries,
            Vector3 buildCenter,
            Vector3 referenceCenter,
            BuildValidationResult result,
            float yRotationOffset = 0f)
        {
            List<GameObject> unmatchedPlaced = placedBlocks.ToList();

            string rotInfo = yRotationOffset != 0f ? $" [Rotated {yRotationOffset}°]" : "";
            if (m_VerboseLogging) Debug.Log($"--- POSITION-BASED MATCHING{rotInfo} (with optimal assignment for identical blocks) ---");

            // Group reference entries by type+color for optimal matching
            var referenceGroups = new Dictionary<string, List<(BlockSpawnEntry entry, int index)>>();
            for (int i = 0; i < referenceEntries.Count; i++)
            {
                var entry = referenceEntries[i];
                if (entry == null) continue;
                
                string key = $"{entry.BlockType}_{entry.BlockColor}";
                if (!referenceGroups.ContainsKey(key))
                    referenceGroups[key] = new List<(BlockSpawnEntry, int)>();
                referenceGroups[key].Add((entry, i));
            }

            // Process each group with optimal matching
            var processedResults = new BlockValidationResult[referenceEntries.Count];
            
            foreach (var group in referenceGroups)
            {
                var refEntries = group.Value;
                
                // Find all placed blocks matching this type+color
                BlockType groupType = refEntries[0].entry.BlockType;
                BlockColor groupColor = refEntries[0].entry.BlockColor;
                
                var matchingPlaced = unmatchedPlaced
                    .Where(b => GetBlockType(b) == groupType && GetBlockColor(b) == groupColor)
                    .ToList();

                // Calculate expected positions for this group (with rotation offset)
                var expectedPositions = new List<Vector3>();
                Quaternion rotOffset = Quaternion.Euler(0, yRotationOffset, 0);
                foreach (var (entry, _) in refEntries)
                {
                    Vector3 relPos = (entry.Position - referenceCenter) * m_StructureScale;
                    relPos.y += m_EffectiveHeightOffset;
                    // Apply Y-axis rotation to the relative position
                    relPos = rotOffset * relPos;
                    expectedPositions.Add(relPos);
                }

                // Find optimal assignment using brute force for small groups
                var bestAssignment = FindOptimalAssignment(matchingPlaced, expectedPositions, buildCenter);

                // Apply the assignment
                for (int i = 0; i < refEntries.Count; i++)
                {
                    var (refEntry, originalIndex) = refEntries[i];
                    Vector3 refRelativePos = expectedPositions[i];

                    BlockValidationResult blockResult = new BlockValidationResult
                    {
                        ReferenceEntry = refEntry,
                        BlockType = refEntry.BlockType,
                        BlockColor = refEntry.BlockColor,
                        ExpectedRelativePosition = refRelativePos
                    };

                    GameObject matchedBlock = (i < bestAssignment.Count) ? bestAssignment[i] : null;

                    if (matchedBlock != null)
                    {
                        Vector3 placedWorldPos = matchedBlock.transform.position;
                        Vector3 placedRelativePos = placedWorldPos - buildCenter;
                        Vector3 expectedWorldPos = buildCenter + refRelativePos;
                        float posError = Vector3.Distance(placedRelativePos, refRelativePos);
                        
                        // Apply rotation offset to expected rotation as well
                        Quaternion expectedRotation = rotOffset * refEntry.Rotation;
                        float rotationError = CalculateRotationError(matchedBlock.transform.rotation, expectedRotation, refEntry.AllowedRotations);
                        
                        // PIVOT COMPENSATION: Blocks with FlipY allowed often have off-center pivots
                        // (especially custom Blender imports like arches). When flipped, the pivot shifts
                        // but the visual position stays roughly the same. Use increased tolerance for these.
                        RotationRules rules = refEntry.AllowedRotations ?? new RotationRules();
                        bool hasSymmetricRotation = rules.FlipX || rules.FlipY || rules.FlipZ;
                        float effectivePosTolerance = hasSymmetricRotation 
                            ? m_PositionTolerance * 2.5f  // More forgiving for blocks that can flip
                            : m_PositionTolerance;

                        blockResult.PlacedBlock = matchedBlock;
                        blockResult.IsPresent = true;
                        blockResult.ActualRelativePosition = placedRelativePos;
                        blockResult.PositionError = posError;
                        blockResult.RotationError = rotationError;
                        blockResult.IsPositionCorrect = posError <= effectivePosTolerance;
                        blockResult.IsRotationCorrect = !m_ValidateRotation || rotationError <= m_RotationTolerance;
                        blockResult.IsCorrect = blockResult.IsPositionCorrect && blockResult.IsRotationCorrect;

                        unmatchedPlaced.Remove(matchedBlock);
                        result.PresentBlocks++;

                        if (blockResult.IsCorrect)
                        {
                            result.CorrectBlocks++;
                        }

                        if (m_VerboseLogging)
                        {
                        string status = blockResult.IsCorrect ? "[OK]" : 
                                       (blockResult.IsPositionCorrect ? "[ROT FAIL]" : "[POS FAIL]");
                        string rotDetails = m_ValidateRotation ? $", RotErr={rotationError:F1}° (need <{m_RotationTolerance}°)" : "";
                        string posDetails = hasSymmetricRotation ? $" (extended tolerance: {effectivePosTolerance:F3}m)" : "";
                        Debug.Log($"    {status} #{i}: PosErr={posError:F3}m (need <{effectivePosTolerance:F3}m){posDetails}{rotDetails}");
                        }
                    }
                    else
                    {
                        blockResult.IsPresent = false;
                        blockResult.IsCorrect = false;
                        blockResult.PositionError = float.MaxValue;
                        blockResult.RotationError = float.MaxValue;
                        result.MissingBlocks.Add(refEntry);

                        if (m_VerboseLogging) Debug.Log($"    [MISSING] #{i}: No matching block available");
                    }

                    processedResults[originalIndex] = blockResult;
                }
            }

            // Add results in original order
            foreach (var blockResult in processedResults)
            {
                if (blockResult != null)
                    result.BlockResults.Add(blockResult);
            }

            result.ExtraBlocks.AddRange(unmatchedPlaced);
            
            if (m_VerboseLogging && unmatchedPlaced.Count > 0)
            {
                Debug.Log($"  Extra blocks in zone: {unmatchedPlaced.Count}");
            }
        }

        /// <summary>
        /// Finds the optimal assignment of placed blocks to expected positions.
        /// Handles cases where there are fewer placed blocks than positions.
        /// Each placed block is assigned to exactly one position to maximize correct matches.
        /// </summary>
        private List<GameObject> FindOptimalAssignment(
            List<GameObject> placedBlocks,
            List<Vector3> expectedPositions,
            Vector3 buildCenter)
        {
            int refCount = expectedPositions.Count;
            int placedCount = placedBlocks.Count;

            // Initialize result with nulls
            var result = new List<GameObject>(new GameObject[refCount]);
            
            if (placedCount == 0 || refCount == 0)
                return result;

            // Calculate distance matrix: distance[blockIdx][posIdx]
            var distances = new float[placedCount, refCount];
            for (int b = 0; b < placedCount; b++)
            {
                Vector3 blockRelPos = placedBlocks[b].transform.position - buildCenter;
                for (int p = 0; p < refCount; p++)
                {
                    distances[b, p] = Vector3.Distance(blockRelPos, expectedPositions[p]);
                }
            }

            // For small problems, use brute force to find optimal assignment
            if (placedCount <= 5 && refCount <= 5)
            {
                return FindOptimalByBruteForce(placedBlocks, expectedPositions, distances, refCount);
            }

            // For larger problems, use greedy assignment
            return FindGreedyAssignmentFromMatrix(placedBlocks, distances, refCount);
        }

        /// <summary>
        /// Finds optimal assignment by trying all possible ways to assign N blocks to M positions.
        /// </summary>
        private List<GameObject> FindOptimalByBruteForce(
            List<GameObject> placedBlocks,
            List<Vector3> expectedPositions,
            float[,] distances,
            int positionCount)
        {
            int blockCount = placedBlocks.Count;
            var bestAssignment = new int[blockCount]; // bestAssignment[blockIdx] = positionIdx (-1 = unassigned)
            int bestCorrectCount = -1;
            float bestTotalError = float.MaxValue;

            // Initialize with -1 (unassigned)
            for (int i = 0; i < blockCount; i++)
                bestAssignment[i] = -1;

            // Try all possible assignments
            // Each block can be assigned to any position (0 to positionCount-1)
            // But each position can only have one block
            var currentAssignment = new int[blockCount];
            TryAllAssignments(placedBlocks, distances, positionCount, currentAssignment, 0,
                ref bestAssignment, ref bestCorrectCount, ref bestTotalError);

            // Build result list
            var result = new List<GameObject>(new GameObject[positionCount]);
            for (int b = 0; b < blockCount; b++)
            {
                if (bestAssignment[b] >= 0 && bestAssignment[b] < positionCount)
                {
                    result[bestAssignment[b]] = placedBlocks[b];
                }
            }

            return result;
        }

        /// <summary>
        /// Recursively tries all possible assignments.
        /// </summary>
        private void TryAllAssignments(
            List<GameObject> blocks,
            float[,] distances,
            int positionCount,
            int[] currentAssignment,
            int blockIdx,
            ref int[] bestAssignment,
            ref int bestCorrectCount,
            ref float bestTotalError)
        {
            if (blockIdx >= blocks.Count)
            {
                // Evaluate this assignment
                int correctCount = 0;
                float totalError = 0f;

                for (int b = 0; b < blocks.Count; b++)
                {
                    int pos = currentAssignment[b];
                    if (pos >= 0)
                    {
                        float dist = distances[b, pos];
                        totalError += dist;
                        if (dist <= m_PositionTolerance)
                            correctCount++;
                    }
                }

                // Is this better?
                if (correctCount > bestCorrectCount ||
                    (correctCount == bestCorrectCount && totalError < bestTotalError))
                {
                    bestCorrectCount = correctCount;
                    bestTotalError = totalError;
                    Array.Copy(currentAssignment, bestAssignment, blocks.Count);
                }
                return;
            }

            // Try assigning this block to each position (or leaving it unassigned)
            for (int pos = 0; pos < positionCount; pos++)
            {
                // Check if this position is already taken
                bool taken = false;
                for (int b = 0; b < blockIdx; b++)
                {
                    if (currentAssignment[b] == pos)
                    {
                        taken = true;
                        break;
                    }
                }

                if (!taken)
                {
                    currentAssignment[blockIdx] = pos;
                    TryAllAssignments(blocks, distances, positionCount, currentAssignment, blockIdx + 1,
                        ref bestAssignment, ref bestCorrectCount, ref bestTotalError);
                }
            }

            // Also try NOT assigning this block (if there are more blocks than positions)
            if (blocks.Count > positionCount)
            {
                currentAssignment[blockIdx] = -1;
                TryAllAssignments(blocks, distances, positionCount, currentAssignment, blockIdx + 1,
                    ref bestAssignment, ref bestCorrectCount, ref bestTotalError);
            }
        }

        /// <summary>
        /// Greedy assignment using distance matrix.
        /// </summary>
        private List<GameObject> FindGreedyAssignmentFromMatrix(
            List<GameObject> placedBlocks,
            float[,] distances,
            int positionCount)
        {
            var result = new List<GameObject>(new GameObject[positionCount]);
            var usedBlocks = new bool[placedBlocks.Count];
            var usedPositions = new bool[positionCount];

            // Greedily assign blocks to positions, prioritizing smallest distances
            int assignmentsMade = 0;
            int maxAssignments = Mathf.Min(placedBlocks.Count, positionCount);

            while (assignmentsMade < maxAssignments)
            {
                float bestDist = float.MaxValue;
                int bestBlock = -1;
                int bestPos = -1;

                // Find the smallest distance among unused block-position pairs
                for (int b = 0; b < placedBlocks.Count; b++)
                {
                    if (usedBlocks[b]) continue;
                    for (int p = 0; p < positionCount; p++)
                    {
                        if (usedPositions[p]) continue;
                        if (distances[b, p] < bestDist)
                        {
                            bestDist = distances[b, p];
                            bestBlock = b;
                            bestPos = p;
                        }
                    }
                }

                if (bestBlock < 0) break;

                result[bestPos] = placedBlocks[bestBlock];
                usedBlocks[bestBlock] = true;
                usedPositions[bestPos] = true;
                assignmentsMade++;
            }

            return result;
        }

        /// <summary>
        /// Finds the best matching placed block for a reference entry.
        /// </summary>
        private (GameObject block, float positionError) FindBestMatchingBlock(
            List<GameObject> candidates,
            BlockSpawnEntry refEntry,
            Vector3 buildCenter,
            Vector3 expectedRelativePos)
        {
            GameObject bestMatch = null;
            float bestError = float.MaxValue;

            foreach (GameObject candidate in candidates)
            {
                if (candidate == null) continue;

                // Check type and color match
                BlockType type = GetBlockType(candidate);
                BlockColor color = GetBlockColor(candidate);

                if (type != refEntry.BlockType || color != refEntry.BlockColor)
                {
                    continue;
                }

                // Calculate position error (relative position comparison)
                Vector3 placedRelativePos = candidate.transform.position - buildCenter;
                float posError = Vector3.Distance(placedRelativePos, expectedRelativePos);

                if (posError < bestError)
                {
                    bestError = posError;
                    bestMatch = candidate;
                }
            }

            return (bestMatch, bestError);
        }

        /// <summary>
        /// Calculates rotation error based on the block's rotation rules (boolean flags).
        /// Tests if the actual rotation matches any allowed variant of the expected rotation.
        /// </summary>
        private float CalculateRotationError(Quaternion actual, Quaternion expected, RotationRules rules)
        {
            float minAngle = Quaternion.Angle(actual, expected);
            
            // If no rules provided, use restrictive default
            if (rules == null)
            {
                rules = new RotationRules { FlipX = false, FlipY = false, FlipZ = false, Steps90X = false, Steps90Y = false, Steps90Z = false };
            }
            
            // Build list of allowed rotations based on flags
            int[] xAngles = rules.Steps90X ? new[] { 0, 90, 180, 270 } : (rules.FlipX ? new[] { 0, 180 } : new[] { 0 });
            int[] yAngles = rules.Steps90Y ? new[] { 0, 90, 180, 270 } : (rules.FlipY ? new[] { 0, 180 } : new[] { 0 });
            int[] zAngles = rules.Steps90Z ? new[] { 0, 90, 180, 270 } : (rules.FlipZ ? new[] { 0, 180 } : new[] { 0 });
            
            // Only use ONE method: expected * orientation (apply allowed rotation in local space)
            // This is the correct interpretation: the block can be rotated from its expected 
            // orientation by any of the allowed amounts
            foreach (int x in xAngles)
            {
                foreach (int y in yAngles)
                {
                    foreach (int z in zAngles)
                    {
                        if (x == 0 && y == 0 && z == 0) continue; // Skip identity, already checked
                        
                        Quaternion allowedRotation = Quaternion.Euler(x, y, z);
                        Quaternion validOrientation = expected * allowedRotation;
                        float angle = Quaternion.Angle(actual, validOrientation);
                        
                        if (angle < minAngle)
                        {
                            minAngle = angle;
                        }
                    }
                }
            }
            
            return minAngle;
        }
        
        /// <summary>
        /// Overload for backwards compatibility - allows all rotations.
        /// </summary>
        private float CalculateRotationError(Quaternion actual, Quaternion expected)
        {
            return CalculateRotationError(actual, expected, RotationRules.FullCube);
        }

        /// <summary>
        /// Matches blocks using presence only (type + color, ignore position).
        /// </summary>
        private void MatchBlocksPresenceOnly(
            List<GameObject> placedBlocks,
            List<BlockSpawnEntry> referenceEntries,
            BuildValidationResult result)
        {
            List<GameObject> unmatchedPlaced = placedBlocks.ToList();

            foreach (BlockSpawnEntry refEntry in referenceEntries)
            {
                if (refEntry == null) continue;

                GameObject match = unmatchedPlaced.FirstOrDefault(block =>
                    GetBlockType(block) == refEntry.BlockType &&
                    GetBlockColor(block) == refEntry.BlockColor);

                BlockValidationResult blockResult = new BlockValidationResult
                {
                    ReferenceEntry = refEntry,
                    BlockType = refEntry.BlockType,
                    BlockColor = refEntry.BlockColor
                };

                if (match != null)
                {
                    blockResult.PlacedBlock = match;
                    blockResult.IsPresent = true;
                    blockResult.IsCorrect = true;
                    blockResult.PositionError = 0f;
                    blockResult.RotationError = 0f;

                    unmatchedPlaced.Remove(match);
                    result.PresentBlocks++;
                    result.CorrectBlocks++;
                }
                else
                {
                    blockResult.IsPresent = false;
                    blockResult.IsCorrect = false;
                    result.MissingBlocks.Add(refEntry);
                }

                result.BlockResults.Add(blockResult);
            }

            result.ExtraBlocks.AddRange(unmatchedPlaced);
        }

        /// <summary>
        /// Calculates all accuracy percentages.
        /// </summary>
        private void CalculateAccuracies(BuildValidationResult result)
        {
            if (result.TotalReferenceBlocks == 0)
            {
                result.AccuracyPercentage = 0f;
                result.PresencePercentage = 0f;
                result.PositionAccuracy = 0f;
                return;
            }

            float total = result.TotalReferenceBlocks;
            result.AccuracyPercentage = (result.CorrectBlocks / total) * 100f;
            result.PresencePercentage = (result.PresentBlocks / total) * 100f;
            
            // Position accuracy: among present blocks, how many are correctly positioned?
            if (result.PresentBlocks > 0)
            {
                int positionCorrect = result.BlockResults.Count(r => r.IsPresent && r.IsPositionCorrect);
                result.PositionAccuracy = (positionCorrect / (float)result.PresentBlocks) * 100f;
            }
        }

        /// <summary>
        /// Caches expected world positions for gizmo drawing.
        /// </summary>
        private void CacheExpectedPositions(List<BlockSpawnEntry> entries, Vector3 buildCenter, Vector3 referenceCenter)
        {
            m_ExpectedWorldPositions.Clear();
            Quaternion rotOffset = Quaternion.Euler(0, m_LastBestRotation, 0);
            
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                Vector3 relativePos = (entry.Position - referenceCenter) * m_StructureScale;
                relativePos.y += m_EffectiveHeightOffset;
                // Apply the auto-alignment rotation
                relativePos = rotOffset * relativePos;
                Vector3 worldPos = buildCenter + relativePos;
                m_ExpectedWorldPositions.Add(worldPos);
            }
        }

        // === Helper Methods ===

        private bool IsBlockInValidationArea(GameObject block)
        {
            if (block == null) return false;
            if (m_BuildZone != null) return m_BuildZone.IsInZone(block);
            return false;
        }

        private bool IsReferenceStructureBlock(GameObject block)
        {
            string name = block.name;
            if (name.StartsWith("ReferenceBlock_") || name.Contains("Reference")) return true;

            Transform parent = block.transform.parent;
            while (parent != null)
            {
                if (parent.name.Contains("ReferenceStructure") || parent.name.Contains("Reference"))
                    return true;
                parent = parent.parent;
            }

            MeshRenderer renderer = block.GetComponentInChildren<MeshRenderer>();
            if (renderer?.sharedMaterial?.name.Contains("Holographic") == true)
                return true;

            return false;
        }

        private BlockType GetBlockType(GameObject block)
        {
            BlockReference reference = BlockReference.FromGameObject(block);
            return reference?.BlockType ?? BlockType.Cube;
        }

        private BlockColor GetBlockColor(GameObject block)
        {
            if (block == null) return BlockColor.Natural;
            
            // FIRST: Check the block's name - this is the most reliable since spawner sets it
            // e.g., "Block_Rectangle_DarkGreen_Spawned" contains "DarkGreen"
            string blockName = block.name;
            
            // Check longer/more specific color names FIRST to avoid "Green" matching "DarkGreen"
            if (blockName.Contains("DarkGreen")) return BlockColor.DarkGreen;
            if (blockName.Contains("Natural")) return BlockColor.Natural;
            if (blockName.Contains("Red")) return BlockColor.Red;
            if (blockName.Contains("Green")) return BlockColor.Green;
            if (blockName.Contains("Yellow")) return BlockColor.Yellow;
            if (blockName.Contains("Blue")) return BlockColor.Blue;
            if (blockName.Contains("Orange")) return BlockColor.Orange;
            
            // FALLBACK: Check material names if block name didn't have color info
            MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>(true);
            
            if (renderers != null && renderers.Length > 0)
            {
                foreach (MeshRenderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    Material mat = renderer.material ?? renderer.sharedMaterial;
                    if (mat == null) continue;
                    
                    string materialName = mat.name.Replace(" (Instance)", "");
                    
                    // Check longer names first to avoid substring false matches
                    if (materialName.Contains("DarkGreen")) return BlockColor.DarkGreen;
                    if (materialName.Contains("Natural")) return BlockColor.Natural;
                    if (materialName.Contains("Red")) return BlockColor.Red;
                    if (materialName.Contains("Green")) return BlockColor.Green;
                    if (materialName.Contains("Yellow")) return BlockColor.Yellow;
                    if (materialName.Contains("Blue")) return BlockColor.Blue;
                    if (materialName.Contains("Orange")) return BlockColor.Orange;
                }
            }

            return BlockColor.Natural;
        }

        // === Debug Visualization ===

        private void OnDrawGizmos()
        {
            if (!m_ShowDebugGizmos || m_LastValidationResult == null) return;

            // Draw build center
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(m_LastBuildCenter, 0.05f);

            if (!m_ShowExpectedPositions) return;

            // Draw expected positions and connections to actual positions
            for (int i = 0; i < m_LastValidationResult.BlockResults.Count && i < m_ExpectedWorldPositions.Count; i++)
            {
                var blockResult = m_LastValidationResult.BlockResults[i];
                Vector3 expectedPos = m_ExpectedWorldPositions[i];

                // Draw expected position
                if (blockResult.IsCorrect)
                {
                    Gizmos.color = Color.green;
                }
                else if (blockResult.IsPresent)
                {
                    Gizmos.color = Color.yellow;
                }
                else
                {
                    Gizmos.color = Color.red;
                }

                Gizmos.DrawWireCube(expectedPos, Vector3.one * 0.05f);

                // Draw line from expected to actual
                if (blockResult.PlacedBlock != null)
                {
                    Gizmos.color = blockResult.IsCorrect ? Color.green : Color.yellow;
                    Gizmos.DrawLine(expectedPos, blockResult.PlacedBlock.transform.position);
                }
            }

            // Draw tolerance sphere around each expected position
            Gizmos.color = new Color(0, 1, 0, 0.1f);
            foreach (Vector3 pos in m_ExpectedWorldPositions)
            {
                Gizmos.DrawSphere(pos, m_PositionTolerance);
            }
        }
    }
}
