using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BlockBattle
{
    /// <summary>
    /// Represents a single block in a reference structure with its type, position, and rotation.
    /// </summary>
    [System.Serializable]
    public class BlockReference
    {
        [Tooltip("Type of block (Cube, Cylinder, Triangle)")]
        public BlockType BlockType;

        [Tooltip("Color of the block")]
        public BlockColor BlockColor;

        [Tooltip("Position of the block in world space")]
        public Vector3 Position;

        [Tooltip("Rotation of the block as a quaternion")]
        public Quaternion Rotation;

        /// <summary>
        /// Creates a new BlockReference with the specified parameters.
        /// </summary>
        /// <param name="blockType">Type of the block</param>
        /// <param name="blockColor">Color of the block</param>
        /// <param name="position">World position of the block</param>
        /// <param name="rotation">World rotation of the block</param>
        public BlockReference(BlockType blockType, BlockColor blockColor, Vector3 position, Quaternion rotation)
        {
            BlockType = blockType;
            BlockColor = blockColor;
            Position = position;
            Rotation = rotation;
        }

        /// <summary>
        /// Creates a BlockReference from a GameObject's transform.
        /// Attempts to determine block type and color from the GameObject's name or prefab.
        /// </summary>
        /// <param name="gameObject">The GameObject to create a reference from</param>
        /// <returns>BlockReference if block type could be determined, null otherwise</returns>
        public static BlockReference FromGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            BlockType blockType = DetermineBlockType(gameObject);
            BlockColor blockColor = DetermineBlockColor(gameObject);
            
            return new BlockReference(blockType, blockColor, gameObject.transform.position, gameObject.transform.rotation);
        }

        private static BlockColor DetermineBlockColor(GameObject gameObject)
        {
            string name = gameObject.name.ToLower();
            List<string> searchStrings = new List<string> { name };
            
            MeshRenderer[] renderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null) searchStrings.Add(mat.name.ToLower());
                    }
                }
            }

            // More specific matches first
            if (searchStrings.Any(s => s.Contains("darkgreen") || s.Contains("dark_green"))) return BlockColor.DarkGreen;
            if (searchStrings.Any(s => s.Contains("white"))) return BlockColor.White;
            if (searchStrings.Any(s => s.Contains("red"))) return BlockColor.Red;
            if (searchStrings.Any(s => s.Contains("green"))) return BlockColor.Green;
            if (searchStrings.Any(s => s.Contains("yellow"))) return BlockColor.Yellow;
            if (searchStrings.Any(s => s.Contains("blue"))) return BlockColor.Blue;
            if (searchStrings.Any(s => s.Contains("orange"))) return BlockColor.Orange;
            if (searchStrings.Any(s => s.Contains("natural") || s.Contains("brown") || s.Contains("wood") || s.Contains("holz"))) 
                return BlockColor.Natural;

            return BlockColor.Natural;
        }

        /// <summary>
        /// Determines the block type from a GameObject by checking its name or prefab name.
        /// </summary>
        /// <param name="gameObject">The GameObject to check</param>
        /// <returns>The detected BlockType, or Cube as default if unable to determine</returns>
        private static BlockType DetermineBlockType(GameObject gameObject)
        {
            // Collect name and all material names
            string objectName = gameObject.name.ToLower();
            List<string> searchStrings = new List<string> { objectName };
            
            MeshRenderer[] renderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    // Add renderer's own name too, sometimes it helps
                    searchStrings.Add(renderer.gameObject.name.ToLower());
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null) searchStrings.Add(mat.name.ToLower());
                    }
                }
            }

            // High priority matches (BigTriangle must be before Triangle)
            if (searchStrings.Any(s => s.Contains("bigtriangle") || (s.Contains("big") && s.Contains("triangle"))))
                return BlockType.BigTriangle;
            
            if (searchStrings.Any(s => s.Contains("triangle") || s.Contains("dreieck")))
                return BlockType.Triangle;
            
            // Check for Rectangle / Rectangular / Rect
            if (searchStrings.Any(s => s.Contains("rectangle") || s.Contains("rectangular") || s.Contains("rechteck") || s.Contains("_rect_") || s.Contains("rect_")))
                return BlockType.Rectangle;
            
            // Check for Arch / Archway / Bogens
            if (searchStrings.Any(s => s.Contains("arch") || s.Contains("archway") || s.Contains("bogen") || s.Contains("tür") || s.Contains("tuer")))
                return BlockType.Arch;
            
            if (searchStrings.Any(s => s.Contains("cylinder") || s.Contains("zylinder")))
                return BlockType.Cylinder;
            
            if (searchStrings.Any(s => s.Contains("cube") || s.Contains("würfel") || s.Contains("wuerfel") || s.Contains("quad") || s.Contains("block")))
                return BlockType.Cube;

            // Try to check prefab name if this is a prefab instance
            #if UNITY_EDITOR
            var prefabAsset = UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            if (prefabAsset != null)
            {
                string prefabName = prefabAsset.name.ToLower();
                
                if (prefabName.Contains("bigtriangle") || (prefabName.Contains("big") && prefabName.Contains("triangle")))
                    return BlockType.BigTriangle;
                if (prefabName.Contains("triangle") || prefabName.Contains("dreieck"))
                    return BlockType.Triangle;
                if (prefabName.Contains("rectangle") || prefabName.Contains("rectangular") || prefabName.Contains("rechteck") || prefabName.Contains("rect"))
                    return BlockType.Rectangle;
                if (prefabName.Contains("arch") || prefabName.Contains("archway") || prefabName.Contains("bogen"))
                    return BlockType.Arch;
                if (prefabName.Contains("cylinder") || prefabName.Contains("zylinder"))
                    return BlockType.Cylinder;
                if (prefabName.Contains("cube") || prefabName.Contains("würfel") || prefabName.Contains("wuerfel") || prefabName.Contains("block"))
                    return BlockType.Cube;
            }
            #endif

            // Default to Cube if unable to determine
            return BlockType.Cube;
        }

    }
}

