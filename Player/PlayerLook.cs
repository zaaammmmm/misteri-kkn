using UnityEngine;
using KKN.Game.Core;

namespace KKN.Game.Player
{
    /// <summary>
    /// First-person camera look with adjustable sensitivity and smooth clamping.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Transform playerBody;
        [SerializeField] private float sensitivity = 150f;
        [SerializeField] private float minVerticalAngle = -80f;
        [SerializeField] private float maxVerticalAngle = 80f;

        [Header("Smoothing")]
        [SerializeField] private bool smoothLook = true;
        [SerializeField] private float smoothSpeed = 15f;

        private float xRotation; // Vertical (pitch)
        private float yRotation; // Horizontal (yaw)
        private float currentXRotation;

        void Start()
        {
            // Auto-assign playerBody if not set
            if (playerBody == null)
            {
                playerBody = transform.parent;
            }

            if (playerBody == null)
            {
                Debug.LogWarning(
                    "[PlayerLook] playerBody not assigned. " +
                    "Camera will rotate standalone. " +
                    "For best results, assign the player root transform in Inspector.");
            }
            else if (playerBody == transform)
            {
                Debug.LogWarning(
                    "[PlayerLook] playerBody is set to Camera itself. " +
                    "This will cause look issues. Assign the player root transform instead.");
                playerBody = null;
            }

            // Lock cursor
            if (InputManager.Instance != null)
                InputManager.Instance.LockCursor();
            else
                LockCursor();
        }

        void Update()
        {
            // if (InputManager.Instance != null && InputManager.Instance.GetEscapeDown())
            //     InputManager.Instance.UnlockCursor();

            // if (InputManager.Instance != null && InputManager.Instance.GetMouseLeftDown())
            //     InputManager.Instance.LockCursor();

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            if (PlayerState.Instance != null && PlayerState.Instance.IsFrozen)
                return;

            Vector2 lookInput = InputManager.Instance != null
                ? InputManager.Instance.GetLookInput()
                : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

            // Fallback to direct Input if InputManager returns zero
            if (Mathf.Approximately(lookInput.x, 0f))
                lookInput.x = Input.GetAxis("Mouse X");
            if (Mathf.Approximately(lookInput.y, 0f))
                lookInput.y = Input.GetAxis("Mouse Y");

            float mouseX = lookInput.x * sensitivity * Time.deltaTime;
            float mouseY = lookInput.y * sensitivity * Time.deltaTime;

            // Accumulate rotations
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);

            yRotation += mouseX;

            // Apply rotations
            if (smoothLook)
            {
                currentXRotation = Mathf.Lerp(currentXRotation, xRotation, Time.deltaTime * smoothSpeed);
            }
            else
            {
                currentXRotation = xRotation;
            }

            if (playerBody != null)
            {
                // Standard FPS: rotate camera pitch, rotate body yaw
                transform.localRotation = Quaternion.Euler(currentXRotation, 0f, 0f);
                playerBody.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }
            else
            {
                // Standalone camera: rotate both pitch and yaw on same transform
                transform.rotation = Quaternion.Euler(currentXRotation, yRotation, 0f);
            }
        }

        void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SetPlayerBody(Transform body)
        {
            playerBody = body;
        }
    }
}

