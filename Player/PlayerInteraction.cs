using UnityEngine;
using TMPro;
using KKN.Game.Core;

namespace KKN.Game.Player
{
    /// <summary>
    /// Handles raycast-based interaction with IInteractable objects.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private LayerMask interactLayer;

        [Header("UI Reference")]
        [SerializeField] private TMP_Text hintText;

        private Camera cam;
        private IInteractable currentTarget;

        void Start()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (PlayerState.Instance != null && PlayerState.Instance.IsFrozen)
            {
                ClearHint();
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                ClearHint();
                return;
            }

            CheckInteract();
        }

        void CheckInteract()
        {
            if (cam == null) { cam = Camera.main; return; }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();

                if (interactable != null)
                {
                    if (currentTarget != interactable)
                    {
                        currentTarget?.OnHoverExit();
                        currentTarget = interactable;
                        currentTarget?.OnHoverEnter();
                    }

                    SetHint(interactable.GetInteractText());

                    if (InputManager.Instance != null && InputManager.Instance.GetInteractDown())
                        interactable.Interact();
                    else if (Input.GetKeyDown(GameConstants.KEY_INTERACT))
                        interactable.Interact();

                    return;
                }
            }

            if (currentTarget != null)
            {
                currentTarget.OnHoverExit();
                currentTarget = null;
            }

            ClearHint();
        }

        void SetHint(string text)
        {
            if (hintText != null)
                hintText.text = text;
            else if (UI.PlayerHUD.Instance != null)
                UI.PlayerHUD.Instance.ShowPrompt(text);
        }

        void ClearHint()
        {
            if (hintText != null)
                hintText.text = string.Empty;
            else if (UI.PlayerHUD.Instance != null)
                UI.PlayerHUD.Instance.HidePrompt();
        }
    }
}

