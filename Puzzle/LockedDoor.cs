using UnityEngine;
using KKN.Game.Systems;
using KKN.Game.Core;

namespace KKN.Game.Puzzle
{
    /// <summary>
    /// Door that requires a key to open. Supports animation and objective progression.
    /// </summary>
    public class LockedDoor : MonoBehaviour, IInteractable
    {
        [Header("Key Requirement")]
        [SerializeField] private string requiredKey = GameConstants.ITEM_MAIN_KEY;

        [Header("Door Settings")]
        [SerializeField] private bool opened = false;
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 2f;

        [Header("Auto Progress")]
        [SerializeField] private bool completeObjectiveWhenOpened = true;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip unlockClip;
        [SerializeField] private AudioClip lockedClip;

        private Quaternion startRotation;
        private Quaternion targetRotation;
        private bool isOpening = false;

        void Start()
        {
            startRotation = transform.rotation;
            targetRotation = Quaternion.Euler(
                transform.eulerAngles.x,
                transform.eulerAngles.y + openAngle,
                transform.eulerAngles.z
            );
        }

        void Update()
        {
            if (isOpening)
            {
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
        }

        public void Interact()
        {
            if (opened || isOpening) return;

            if (InventorySystem.Instance != null && InventorySystem.Instance.HasItem(requiredKey))
            {
                opened = true;
                isOpening = true;

                if (unlockClip != null && audioSource != null)
                    audioSource.PlayOneShot(unlockClip);

                if (completeObjectiveWhenOpened)
                    ObjectiveManager.Instance?.NextStep();
            }
            else
            {
                if (lockedClip != null && audioSource != null)
                    audioSource.PlayOneShot(lockedClip);
            }
        }

        public string GetInteractText()
        {
            if (opened) return "";
            if (InventorySystem.Instance != null && InventorySystem.Instance.HasItem(requiredKey))
                return "[E] Buka Pintu";
            return "[E] Pintu Terkunci";
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
    }
}

