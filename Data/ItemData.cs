using UnityEngine;

namespace KKN.Game.Data
{
    /// <summary>
    /// ScriptableObject mendefinisikan item di dunia game.
    /// Digunakan oleh InventorySystem dan semua pickup objects.
    ///
    /// itemType WAJIB diisi dengan tepat (case-insensitive):
    ///   "Key"      — kunci pintu / area
    ///   "Material" — bahan generator (Gas, Oli, Kabel, Gear, dst)
    ///   "Other"    — item lain-lain
    /// </summary>
    [CreateAssetMenu(fileName = "ItemData", menuName = "KKN/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("ID unik item, harus sama persis dengan yang dipakai di ItemPickup.itemID")]
        public string itemID;

        [Tooltip("Nama tampilan di UI inventory")]
        public string displayName;

        [Tooltip("Deskripsi singkat di panel detail inventory")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Ikon sprite untuk slot inventory")]
        public Sprite icon;

        [Tooltip("Bisa dikonsumsi/dipakai?")]
        public bool isConsumable;

        [Tooltip("Ukuran stack maksimum (1 untuk kunci)")]
        public int maxStack = 1;

        [Tooltip("Kategori tab inventory: Key / Material / Other")]
        public string itemType = "Other";
    }
}
