using UnityEngine;
using KKN.Game.Systems;
using KKN.Game.Inventory;

namespace KKN.Game.Puzzle
{
    /// <summary>
    /// Pickup untuk dokumen. Setelah diambil, dokumen masuk ke tab Dokumen di inventory.
    /// </summary>
    public class DocumentPickup : MonoBehaviour, Core.IInteractable
    {
        [Header("Document Data")]
        [SerializeField] private DocumentData documentData;

        [Header("Interact Text")]
        [SerializeField] private string pickupText = "[E] Ambil Dokumen";

        [Header("Objective Text")]
        [Tooltip("Objective sebelum dokumen diambil")]
        [TextArea(2, 3)]
        [SerializeField] private string objectiveTextBefore = "";

        [Tooltip("Objective setelah dokumen diambil")]
        [TextArea(2, 3)]
        [SerializeField] private string objectiveTextAfter  = "";

        [Header("Optional")]
        [SerializeField] private bool  destroyAfterPickup = true;
        [SerializeField] private GameObject pickupEffect;
        [SerializeField] private AudioSource pickupSound;

        private bool hasBeenPickedUp = false;

        public void Interact()
        {
            if (hasBeenPickedUp) return;

            if (documentData == null)
            {
                Debug.LogWarning($"[DocumentPickup] DocumentData NULL pada {gameObject.name}");
                return;
            }

            hasBeenPickedUp = true;

            InventorySystem.Instance?.AddDocument(documentData);

            if (!string.IsNullOrEmpty(objectiveTextAfter))
                ObjectiveManager.Instance?.SetObjective(objectiveTextAfter);

            if (pickupEffect != null)
                Instantiate(pickupEffect, transform.position, Quaternion.identity);

            if (pickupSound != null)
            {
                pickupSound.transform.SetParent(null);
                pickupSound.Play();
                Destroy(pickupSound.gameObject,
                    pickupSound.clip != null ? pickupSound.clip.length + 0.1f : 1f);
            }

            if (destroyAfterPickup)
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
            if (!hasBeenPickedUp && !string.IsNullOrEmpty(objectiveTextBefore))
                ObjectiveManager.Instance?.SetObjective(objectiveTextBefore);
        }

        public void OnHoverExit() { }
    }
}
