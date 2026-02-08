using UnityEngine;

namespace BlockBattle
{
    /// <summary>
    /// Enumeration of available block colors in BlockBattle.
    /// </summary>
    public enum BlockColor
    {
        Natural,    // Natural wood (brown)
        Red,
        Green,
        Yellow,
        Blue,
        Orange,
        DarkGreen,  // Dark green for base blocks
        White
    }

    /// <summary>
    /// Utility class for block colors.
    /// </summary>
    public static class BlockColorUtility
    {
        /// <summary>
        /// Gets the Unity Color for a BlockColor enum value.
        /// </summary>
        /// <param name="blockColor">The block color enum value</param>
        /// <returns>The corresponding Unity Color</returns>
        public static Color GetColor(BlockColor blockColor)
        {
            switch (blockColor)
            {
                case BlockColor.Natural:
                    return new Color(0.6f, 0.4f, 0.2f, 1f); // Brown wooden color
                case BlockColor.Red:
                    return new Color(0.8f, 0.2f, 0.2f, 1f);
                case BlockColor.Green:
                    return new Color(0.2f, 0.7f, 0.3f, 1f);
                case BlockColor.Yellow:
                    return new Color(1f, 0.8f, 0.2f, 1f);
                case BlockColor.Blue:
                    return new Color(0.2f, 0.4f, 0.9f, 1f);
                case BlockColor.Orange:
                    return new Color(1f, 0.5f, 0.1f, 1f);
                case BlockColor.DarkGreen:
                    return new Color(0.1f, 0.4f, 0.2f, 1f);
                case BlockColor.White:
                    return Color.white;
                default:
                    return new Color(0.6f, 0.4f, 0.2f, 1f); // Default to natural
            }
        }

        /// <summary>
        /// Gets the material name for a BlockColor enum value.
        /// </summary>
        /// <param name="blockColor">The block color enum value</param>
        /// <returns>The material name (e.g., "BlockMaterial_Red")</returns>
        public static string GetMaterialName(BlockColor blockColor)
        {
            return $"BlockMaterial_{blockColor}";
        }
    }
}

