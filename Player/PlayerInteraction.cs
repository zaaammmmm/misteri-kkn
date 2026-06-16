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
        private IHoldInteractable currentHoldTarget;
        private float holdTimer;

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

                    HandleInteraction(interactable);

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

        private void HandleInteraction(IInteractable interactable)
        {
            if (interactable is IHoldInteractable holdable)
            {
                HandleHoldInteraction(interactable, holdable);
                return;
            }

            if (InputManager.Instance != null && InputManager.Instance.GetInteractDown())
                interactable.Interact();
            else if (Input.GetKeyDown(GameConstants.KEY_INTERACT))
                interactable.Interact();
        }

        private void HandleHoldInteraction(
            IInteractable interactable,
            IHoldInteractable holdable)
        {
            if (!holdable.CanStartHold())
            {
                if (currentHoldTarget == holdable)
                {
                    holdable.OnHoldCancel();
                    currentHoldTarget = null;
                    holdTimer = 0f;
                }

                return;
            }
            bool holding =
                (InputManager.Instance != null &&
                InputManager.Instance.GetInteractHeld())
                || Input.GetKey(GameConstants.KEY_INTERACT);

            if (holding)
            {
                if (currentHoldTarget != holdable)
                {
                    currentHoldTarget = holdable;
                    holdTimer = 0f;

                    holdable.OnHoldStart();
                }

                holdTimer += Time.deltaTime;

                float progress =
                    Mathf.Clamp01(holdTimer / holdable.GetHoldDuration());

                SetHint(
                    $"{interactable.GetInteractText()}\n" +
                    $"Menyalakan Generator... {(int)(progress * 100)}%"
                );

                if (holdTimer >= holdable.GetHoldDuration())
                {
                    holdable.OnHoldComplete();
                    interactable.Interact();

                    // kunci di nilai maksimum
                    holdTimer = holdable.GetHoldDuration();
                }
            }
            else
            {
                if (currentHoldTarget == holdable)
                {
                    holdable.OnHoldCancel();

                    currentHoldTarget = null;
                    holdTimer = 0f;
                }
            }
        }
    }
}

