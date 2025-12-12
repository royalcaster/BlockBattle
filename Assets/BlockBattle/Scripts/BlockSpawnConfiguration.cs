using UnityEngine;
using System.Collections.Generic;

namespace BlockBattle
{
    /// <summary>
    /// Defines what rotations are allowed for a block during validation.
    /// Use the boolean flags to specify exactly which rotations are permitted.
    /// </summary>
    [System.Serializable]
    public class RotationRules
    {
        [Header("180° Flips (mirror the block)")]
        [Tooltip("Allow 180° flip around X axis (flip forward/backward)")]
        public bool FlipX = true;
        
        [Tooltip("Allow 180° flip around Y axis (rotate 180° on table)")]
        public bool FlipY = true;
        
        [Tooltip("Allow 180° flip around Z axis (flip left/right)")]
        public bool FlipZ = true;
        
        [Header("90° Step Rotations")]
        [Tooltip("Allow 90° step rotations around X axis (tilting forward/back)")]
        public bool Steps90X = false;
        
        [Tooltip("Allow 90° step rotations around Y axis (spinning on table)")]
        public bool Steps90Y = true;
        
        [Tooltip("Allow 90° step rotations around Z axis (tilting left/right)")]
        public bool Steps90Z = false;

        /// <summary>
        /// Creates default rotation rules (horizontal block - can spin and flip but not tilt).
        /// </summary>
        public RotationRules()
        {
        }

        /// <summary>
        /// Creates rotation rules with all options specified.
        /// </summary>
        public RotationRules(bool flipX, bool flipY, bool flipZ, bool steps90X, bool steps90Y, bool steps90Z)
        {
            FlipX = flipX;
            FlipY = flipY;
            FlipZ = flipZ;
            Steps90X = steps90X;
            Steps90Y = steps90Y;
            Steps90Z = steps90Z;
        }

        /// <summary>
        /// Preset: Block must match exactly (no rotation allowed).
        /// </summary>
        public static RotationRules Exact => new RotationRules(false, false, false, false, false, false);
        
        /// <summary>
        /// Preset: Full cube symmetry (any 90° rotation).
        /// </summary>
        public static RotationRules FullCube => new RotationRules(true, true, true, true, true, true);
        
        /// <summary>
        /// Preset: Horizontal block (can spin on Y and flip, but not tilt).
        /// </summary>
        public static RotationRules Horizontal => new RotationRules(true, true, true, false, true, false);
        
        /// <summary>
        /// Preset: Vertical block (can spin on Y and flip left/right, but not lay flat).
        /// </summary>
        public static RotationRules Vertical => new RotationRules(false, true, true, false, true, false);
    }

    /// <summary>
    /// Configuration for spawning a single block.
    /// </summary>
    [System.Serializable]
    public class BlockSpawnEntry
    {
        [Tooltip("Type of block to spawn")]
        public BlockType BlockType = BlockType.Cube;

        [Tooltip("Color of the block")]
        public BlockColor BlockColor = BlockColor.Natural;

        [Tooltip("Position to spawn the block (relative to spawner or table)")]
        public Vector3 Position = Vector3.zero;

        [Tooltip("Rotation of the block")]
        public Quaternion Rotation = Quaternion.identity;

        [Tooltip("Which rotations are allowed during validation")]
        public RotationRules AllowedRotations = new RotationRules();

        /// <summary>
        /// Default constructor for Unity serialization.
        /// </summary>
        public BlockSpawnEntry()
        {
        }

        /// <summary>
        /// Creates a new BlockSpawnEntry.
        /// </summary>
        public BlockSpawnEntry(BlockType blockType, BlockColor blockColor, Vector3 position, Quaternion rotation)
        {
            BlockType = blockType;
            BlockColor = blockColor;
            Position = position;
            Rotation = rotation;
        }
        
        /// <summary>
        /// Creates a new BlockSpawnEntry with rotation rules.
        /// </summary>
        public BlockSpawnEntry(BlockType blockType, BlockColor blockColor, Vector3 position, Quaternion rotation, RotationRules allowedRotations)
        {
            BlockType = blockType;
            BlockColor = blockColor;
            Position = position;
            Rotation = rotation;
            AllowedRotations = allowedRotations;
        }
    }

    /// <summary>
    /// ScriptableObject that defines a spawn configuration for blocks.
    /// Can be used to define different spawn setups for different levels.
    /// </summary>
    [CreateAssetMenu(fileName = "New Spawn Configuration", menuName = "BlockBattle/Spawn Configuration", order = 2)]
    public class BlockSpawnConfiguration : ScriptableObject
    {
        [Header("Configuration Info")]
        [Tooltip("Name of this spawn configuration")]
        public string ConfigurationName = "New Configuration";

        [Tooltip("Description of this configuration")]
        [TextArea(2, 4)]
        public string Description = "";

        [Header("Spawn Entries")]
        [Tooltip("List of blocks to spawn with their types, colors, positions, and rotations")]
        public List<BlockSpawnEntry> SpawnEntries = new List<BlockSpawnEntry>();

        /// <summary>
        /// Gets the number of spawn entries.
        /// </summary>
        public int EntryCount => SpawnEntries != null ? SpawnEntries.Count : 0;

        /// <summary>
        /// Validates that all spawn entries in this configuration have valid data.
        /// </summary>
        /// <returns>True if configuration is valid, false otherwise</returns>
        public bool Validate()
        {
            if (SpawnEntries == null || SpawnEntries.Count == 0)
            {
                Debug.LogWarning($"BlockSpawnConfiguration '{ConfigurationName}': No spawn entries defined");
                return false;
            }

            for (int i = 0; i < SpawnEntries.Count; i++)
            {
                if (SpawnEntries[i] == null)
                {
                    Debug.LogWarning($"BlockSpawnConfiguration '{ConfigurationName}': Spawn entry at index {i} is null");
                    return false;
                }
            }

            return true;
        }
    }
}

