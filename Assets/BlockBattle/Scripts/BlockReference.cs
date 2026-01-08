using UnityEngine;

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

        [Tooltip("Position of the block in world space")]
        public Vector3 Position;

        [Tooltip("Rotation of the block as a quaternion")]
        public Quaternion Rotation;

        /// <summary>
        /// Creates a new BlockReference with the specified parameters.
        /// </summary>
        /// <param name="blockType">Type of the block</param>
        /// <param name="position">World position of the block</param>
        /// <param name="rotation">World rotation of the block</param>
        public BlockReference(BlockType blockType, Vector3 position, Quaternion rotation)
        {
            BlockType = blockType;
            Position = position;
            Rotation = rotation;
        }

        /// <summary>
        /// Creates a BlockReference from a GameObject's transform.
        /// Attempts to determine block type from the GameObject's name or prefab.
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
            
            // If we couldn't determine the type (defaulted to Cube), try to verify it's actually a block
            // by checking if it has XRGrabInteractable component
            bool isBlock = gameObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null;
            if (!isBlock && blockType == BlockType.Cube)
            {
                // Might not be a block, but we'll still create the reference
                // The recorder will filter these out
            }

            return new BlockReference(blockType, gameObject.transform.position, gameObject.transform.rotation);
        }

        /// <summary>
        /// Determines the block type from a GameObject by checking its name or prefab name.
        /// </summary>
        /// <param name="gameObject">The GameObject to check</param>
        /// <returns>The detected BlockType, or Cube as default if unable to determine</returns>
        private static BlockType DetermineBlockType(GameObject gameObject)
        {
            string name = gameObject.name.ToLower();

            if (name.Contains("cube"))
            {
                return BlockType.Cube;
            }
            else if (name.Contains("cylinder"))
            {
                return BlockType.Cylinder;
            }
            else if (name.Contains("bigtriangle") || (name.Contains("big") && name.Contains("triangle")))
            {
                return BlockType.BigTriangle;
            }
            else if (name.Contains("triangle"))
            {
                return BlockType.Triangle;
            }
            else if (name.Contains("rectangle") || name.Contains("rectangular"))
            {
                return BlockType.Rectangle;
            }
            else if (name.Contains("arch") || name.Contains("archway"))
            {
                return BlockType.Arch;
            }

            // Try to check prefab name if this is a prefab instance
            #if UNITY_EDITOR
            var prefabAsset = UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            if (prefabAsset != null)
            {
                string prefabName = prefabAsset.name.ToLower();
                
                if (prefabName.Contains("cube"))
                {
                    return BlockType.Cube;
                }
                else if (prefabName.Contains("cylinder"))
                {
                    return BlockType.Cylinder;
                }
                else if (prefabName.Contains("bigtriangle") || (prefabName.Contains("big") && prefabName.Contains("triangle")))
                {
                    return BlockType.BigTriangle;
                }
                else if (prefabName.Contains("triangle"))
                {
                    return BlockType.Triangle;
                }
                else if (prefabName.Contains("rectangle") || prefabName.Contains("rectangular"))
                {
                    return BlockType.Rectangle;
                }
                else if (prefabName.Contains("arch") || prefabName.Contains("archway"))
                {
                    return BlockType.Arch;
                }
            }
            #endif

            // Default to Cube if unable to determine - this is a problem case!
            Debug.LogWarning($"BlockReference: Could not determine block type for '{gameObject.name}' - defaulting to Cube");
            return BlockType.Cube;
        }

    }
}

