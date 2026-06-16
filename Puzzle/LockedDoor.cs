using UnityEngine;
using KKN.Game.Systems;
using KKN.Game.Core;
using KKN.Game.Data;

namespace KKN.Game.Puzzle
{
    /// <summary>
    /// Pintu terkunci yang membutuhkan key tertentu.
    /// Sekarang mendukung objective text (before/after) via Inspector.
    /// </summary>
    public class LockedDoor : MonoBehaviour, IInteractable
    {
        [Header("Key Requirement")]
        [SerializeField] private ItemData requiredKeyItem;

        [Header("Door Settings")]
        [SerializeField] private bool  opened    = false;
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 2f;

        [Header("Objective Text")]
        [Tooltip("Muncul saat player hover TAPI belum punya kunci")]
        [TextArea(2, 3)]
        [SerializeField] private string objectiveTextLocked = "Temukan kunci untuk membuka pintu ini.";

        [Tooltip("Muncul setelah pintu berhasil dibuka")]
        [TextArea(2, 3)]
        [SerializeField] private string objectiveTextOpened = "";

        [Header("Auto Progress")]
        [SerializeField] private bool completeObjectiveWhenOpened = true;

        [Header("Audio")]
        [Tooltip("SFX 3D di posisi pintu saat berhasil dibuka")]
        [SerializeField] private AudioClip   unlockClip;
        [Tooltip("SFX 3D di posisi pintu saat dicoba tanpa kunci")]
        [SerializeField] private AudioClip   lockedClip;

        private Quaternion startRotation;
        private Quaternion targetRotation;
        private bool       isOpening = false;

        void Start()
        {
            startRotation  = transform.rotation;
            targetRotation = Quaternion.Euler(
                transform.eulerAngles.x,
                transform.eulerAngles.y + openAngle,
                transform.eulerAngles.z
            );
        }

        void Update()
        {
            if (!isOpening) return;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                openSpeed * Time.deltaTime
            );

            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
            {
                transform.rotation = targetRotation;
                isOpening = false;
            }
        }

        private bool HasRequiredKey()
        {
            if (requiredKeyItem == null)
                return false;

            return InventorySystem.Instance != null &&
                InventorySystem.Instance.HasItem(requiredKeyItem.itemID);
        }

        public void Interact()
        {
            if (opened || isOpening) return;

            if (requiredKeyItem == null)
            {
                Debug.LogWarning(
                    $"[LockedDoor] Required Key Item NULL pada {gameObject.name}");
                return;
            }

            bool hasKey = HasRequiredKey();

            if (hasKey)
            {
                opened    = true;
                isOpening = true;

                AudioManager.Instance?.PlaySFX(unlockClip, transform.position, 1f);

                if (!string.IsNullOrEmpty(objectiveTextOpened))
                    ObjectiveManager.Instance?.SetObjective(objectiveTextOpened);

                if (completeObjectiveWhenOpened)
                    ObjectiveManager.Instance?.NextStep();
            }
            else
            {
                AudioManager.Instance?.PlaySFX(lockedClip, transform.position, 0.8f);

                if (!string.IsNullOrEmpty(objectiveTextLocked))
                    ObjectiveManager.Instance?.SetObjective(objectiveTextLocked);
            }
        }

        public string GetInteractText()
        {
            bool hasKey = HasRequiredKey();

            if (hasKey)
                return "[E] Buka Pintu";

            if (requiredKeyItem != null)
                return $"[E] Membutuhkan {requiredKeyItem.displayName}";

            return "[E] Pintu Terkunci";
        }

        public void OnHoverEnter()
        {
            if (!opened && !string.IsNullOrEmpty(objectiveTextLocked))
            {
                bool hasKey = HasRequiredKey();
                if (!hasKey)
                    ObjectiveManager.Instance?.SetObjective(objectiveTextLocked);
            }
        }

        public void OnHoverExit() { }
    }
}