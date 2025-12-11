using UnityEngine;
using System.Collections.Generic;

namespace BlockBattle
{
    /// <summary>
    /// [OBSOLETE] Use BlockSpawnConfiguration instead.
    /// This class is kept for backward compatibility but should not be used in new code.
    /// BlockSpawnConfiguration provides the same functionality plus color support.
    /// </summary>
    [System.Obsolete("StructureData is obsolete. Use BlockSpawnConfiguration instead, which includes color support.")]
    [CreateAssetMenu(fileName = "New Structure", menuName = "BlockBattle/Structure Data (Obsolete)", order = 1)]
    public class StructureData : ScriptableObject
    {
        [Header("Structure Information")]
        [Tooltip("Display name for this structure")]
        public string StructureName = "New Structure";

        [Tooltip("Description of the structure (optional)")]
        [TextArea(2, 4)]
        public string Description = "";

        [Header("Block References")]
        [Tooltip("List of blocks that make up this reference structure")]
        public List<BlockReference> Blocks = new List<BlockReference>();

        /// <summary>
        /// Gets the number of blocks in this structure.
        /// </summary>
        public int BlockCount => Blocks != null ? Blocks.Count : 0;

        /// <summary>
        /// Adds a block reference to this structure.
        /// </summary>
        /// <param name="blockReference">The block reference to add</param>
        public void AddBlock(BlockReference blockReference)
        {
            if (blockReference == null)
            {
                Debug.LogWarning("StructureData: Attempted to add null block reference");
                return;
            }

            if (Blocks == null)
            {
                Blocks = new List<BlockReference>();
            }

            Blocks.Add(blockReference);
        }

        /// <summary>
        /// Clears all blocks from this structure.
        /// </summary>
        public void ClearBlocks()
        {
            if (Blocks != null)
            {
                Blocks.Clear();
            }
        }

        /// <summary>
        /// Validates that all blocks in this structure have valid data.
        /// </summary>
        /// <returns>True if structure is valid, false otherwise</returns>
        public bool Validate()
        {
            if (Blocks == null || Blocks.Count == 0)
            {
                Debug.LogWarning($"StructureData '{StructureName}': No blocks defined");
                return false;
            }

            for (int i = 0; i < Blocks.Count; i++)
            {
                if (Blocks[i] == null)
                {
                    Debug.LogWarning($"StructureData '{StructureName}': Block at index {i} is null");
                    return false;
                }
            }

            return true;
        }
    }
}

