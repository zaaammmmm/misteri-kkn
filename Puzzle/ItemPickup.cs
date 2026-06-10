using UnityEngine;
using KKN.Game.Systems;

namespace KKN.Game.Puzzle
{
    public class ItemPickup : MonoBehaviour, Core.IInteractable
    {
        [Header("Item")]
        public string itemID = "Gasoline";

        [Header("UI")]
        public string pickupText = "[E] Ambil Item";

        [Header("Optional")]
        public bool destroyOnPickup = true;
        public GameObject pickupEffect;

        public void Interact()
        {
            InventorySystem.Instance?.AddItem(itemID);

            if (pickupEffect != null)
                Instantiate(pickupEffect, transform.position, Quaternion.identity);

            if (destroyOnPickup)
                Destroy(gameObject);
        }

        public string GetInteractText()
        {
            return pickupText;
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
    }
}