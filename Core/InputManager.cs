using UnityEngine;

namespace KKN.Game.Core
{
    /// <summary>
    /// Centralized input abstraction.
    /// All player input flows through here, enabling easy key rebinding.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Key Bindings (can be rebound at runtime)")]
        public KeyCode interactKey   = GameConstants.KEY_INTERACT;
        public KeyCode flashlightKey = GameConstants.KEY_FLASHLIGHT;
        public KeyCode reloadKey     = GameConstants.KEY_RELOAD;
        public KeyCode crouchKey     = GameConstants.KEY_CROUCH;
        public KeyCode runKey        = GameConstants.KEY_RUN;
        public KeyCode tabKey        = GameConstants.KEY_TAB;
        public KeyCode escapeKey     = GameConstants.KEY_ESCAPE;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject); // Tambahkan ini
        }

        // ── Movement ──────────────────────────────────────
        public Vector2 GetMovementInput()
        {
            return new Vector2(
                Input.GetAxisRaw(GameConstants.AXIS_HORIZONTAL),
                Input.GetAxisRaw(GameConstants.AXIS_VERTICAL)
            ).normalized;
        }

        public Vector2 GetLookInput()
        {
            return new Vector2(
                Input.GetAxis(GameConstants.AXIS_MOUSE_X),
                Input.GetAxis(GameConstants.AXIS_MOUSE_Y)
            );
        }

        // ─- Actions ───────────────────────────────────────
        public bool GetInteractDown()    => Input.GetKeyDown(interactKey);
        public bool GetInteractHeld()    => Input.GetKey(interactKey);

        public bool GetFlashlightDown()  => Input.GetKeyDown(flashlightKey);
        public bool GetReloadDown()      => Input.GetKeyDown(reloadKey);
        public bool GetCrouchDown()      => Input.GetKeyDown(crouchKey);
        public bool GetTabDown()         => Input.GetKeyDown(tabKey);
        public bool GetEscapeDown()      => Input.GetKeyDown(escapeKey);

        public bool GetRunHeld()         => Input.GetKey(runKey);
        public bool GetMouseLeftDown()   => Input.GetMouseButtonDown(0);

        // ─- Cursor ────────────────────────────────────────
        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }
}

