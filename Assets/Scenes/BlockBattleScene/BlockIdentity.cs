using UnityEngine;

namespace BlockBattle
{
    public class BlockIdentity : MonoBehaviour
    {
        [Header("Wand-Erkennung")]
        [Tooltip("Wähle hier den Typ für dieses Prefab aus")]
        public BlockType blockType;
    }
}