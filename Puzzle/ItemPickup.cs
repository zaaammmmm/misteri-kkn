using UnityEngine;
using KKN.Game.Systems;
using KKN.Game.Data;

namespace KKN.Game.Puzzle
{
    /// <summary>
    /// Pickup untuk semua item (Key / Material / Other).
    /// Kategori ditentukan oleh ItemData.itemType.
    ///
    /// Fitur:
    ///   • objectiveTextBefore — teks objective sebelum bisa diambil
    ///   • objectiveTextAfter  — teks objective setelah item diambil
    ///   • Kedua field bisa dikosongkan jika tidak diperlukan
    ///   • pickupClip — SFX 2D (chime) saat item diambil, lewat AudioManager
    /// </summary>
    public class ItemPickup : MonoBehaviour, Core.IInteractable
    {
        [Header("Item Data")]
        [SerializeField] private ItemData itemData;

        [Header("Interact Text")]
        [Tooltip("Teks yang muncul di HUD saat player mengarahkan crosshair")]
        public string pickupText = "[E] Ambil Item";

        [Header("Objective Text")]
        [Tooltip("Objective yang ditampilkan SEBELUM item diambil (kosongkan jika tidak perlu)")]
        [TextArea(2, 3)]
        public string objectiveTextBefore = "";

        [Tooltip("Objective yang ditampilkan SETELAH item diambil (kosongkan jika tidak perlu)")]
        [TextArea(2, 3)]
        public string objectiveTextAfter  = "";

        [Header("Audio")]
        [Tooltip("SFX 2D saat item diambil (chime pickup) — diputar lewat AudioManager.PlaySFX2D")]
        [SerializeField] private AudioClip pickupClip;

        [Header("Optional")]
        public bool destroyOnPickup = true;
        public GameObject pickupEffect;

        private bool hasBeenPickedUp = false;

        public void Interact()
        {
            if (hasBeenPickedUp) return;

            if (itemData == null)
            {
                Debug.LogWarning($"[ItemPickup] ItemData NULL pada {gameObject.name}");
                return;
            }

            hasBeenPickedUp = true;

            InventorySystem.Instance?.AddItem(itemData.itemID);

            // Objective setelah pickup
            if (!string.IsNullOrEmpty(objectiveTextAfter))
                ObjectiveManager.Instance?.SetObjective(objectiveTextAfter);

            // SFX 2D — chime pickup, tidak terpengaruh posisi
            AudioManager.Instance?.PlaySFX2D(pickupClip, 0.9f);

            // Efek
            if (pickupEffect != null)
                Instantiate(pickupEffect, transform.position, Quaternion.identity);

            if (destroyOnPickup)
                Destroy(gameObject);
            else
                gameObject.layer = LayerMask.NameToLayer("Default");
        }

        public string GetInteractText()
        {
            return hasBeenPickedUp ? "" : pickupText;
        }

        public void OnHoverEnter()
        {
            // Tampilkan objective "sebelum" saat player hover
            if (!hasBeenPickedUp && !string.IsNullOrEmpty(objectiveTextBefore))
                ObjectiveManager.Instance?.SetObjective(objectiveTextBefore);
        }

        public void OnHoverExit() { }
    }
}