using UnityEngine;
using System.Collections.Generic;

namespace BlockBattle
{
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

