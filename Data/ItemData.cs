using UnityEngine;

namespace KKN.Game.Data
{
    /// <summary>
    /// ScriptableObject defining an item in the game world.
    /// Used by InventorySystem and pickup objects.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemData", menuName = "KKN/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("Unique identifier for this item")]
        public string itemID;

        [Tooltip("Display name shown to the player")]
        public string displayName;

        [Tooltip("Description shown in journal/inventory")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Icon sprite for UI")]
        public Sprite icon;

        [Tooltip("Can this item be consumed/used?")]
        public bool isConsumable;

        [Tooltip("Maximum stack size (1 for keys, higher for consumables)")]
        public int maxStack = 1;

        [Tooltip("Kategori item untuk tab inventori. Isi: Key / Material / Other")]
        public string itemType = "Other";
    }
}

