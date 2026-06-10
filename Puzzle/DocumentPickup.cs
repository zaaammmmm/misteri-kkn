using UnityEngine;
using KKN.Game.Systems;
using KKN.Game.Inventory;

namespace KKN.Game.Puzzle
{
    /// <summary>
    /// Letakkan script ini pada GameObject dokumen di scene.
    /// Pastikan ada Collider (IsTrigger tidak perlu) untuk interaksi.
    /// </summary>
    public class DocumentPickup : MonoBehaviour, Core.IInteractable
    {
        [Header("Document Data")]
        [SerializeField] private DocumentData documentData;

        [Header("UI")]
        [SerializeField] private string pickupText = "[E] Ambil Dokumen";

        [Header("Optional")]
        [SerializeField] private bool destroyAfterPickup = true;
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

            // Tambah ke inventory
            InventorySystem.Instance.AddDocument(documentData);

            Debug.Log($"[DocumentPickup] Diambil: {documentData.title}");

            // Efek pickup
            if (pickupEffect != null)
                Instantiate(pickupEffect, transform.position, Quaternion.identity);

            if (pickupSound != null)
            {
                pickupSound.transform.SetParent(null);
                pickupSound.Play();
                Destroy(pickupSound.gameObject, pickupSound.clip != null ? pickupSound.clip.length + 0.1f : 1f);
            }

            if (destroyAfterPickup)
                Destroy(gameObject);
            else
                // Nonaktifkan interaksi tapi objek tetap ada di scene
                gameObject.layer = LayerMask.NameToLayer("Default");
        }

        public string GetInteractText()
        {
            return hasBeenPickedUp ? "" : pickupText;
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
    }
}