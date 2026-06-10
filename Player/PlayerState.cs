using UnityEngine;

namespace KKN.Game.Player
{
    /// <summary>
    /// Single source of truth for player state.
    /// All systems query this instead of directly checking input or other scripts.
    /// </summary>
    public class PlayerState : MonoBehaviour
    {
        public static PlayerState Instance { get; private set; }

        [Header("Movement State")]
        public bool IsFrozen { get; set; } = false;
        public bool IsCrouching { get; set; } = false;
        public bool IsRunning { get; set; } = false;
        public bool IsReloading { get; set; } = false;
        public bool CanMove => !IsFrozen && !IsReloading;
        public bool CanSprint => !IsFrozen && !IsReloading && !IsCrouching;

        [Header("World State")]
        public bool IsInSafeZone { get; set; } = false;
        public bool IsHiding { get; set; } = false;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
    }
}
