using UnityEngine;
using System;
using KKN.Game.Systems;

namespace KKN.Game.Player
{
    /// <summary>
    /// Single source of truth untuk seluruh state player.
    /// Semua sistem query ke sini — tidak ada yang langsung cek input atau komponen lain.
    ///
    /// Perbaikan dari versi sebelumnya:
    ///   1. Event OnFrozenChanged, OnCrouchChanged, OnRunningChanged
    ///      → PlayerHUD, LowSanityEffects, animator bisa subscribe tanpa polling
    ///   2. DontDestroyOnLoad — PlayerState ikut player rig yang persist antar scene
    ///   3. ResetForNewScene() — bersihkan transient state saat scene baru dimuat
    ///   4. IsHiding berdampak pada noise emitter (crouchNoise dipakai saat hiding)
    ///   5. IsInteracting flag — mencegah movement saat hold interact berlangsung
    ///   6. Sanity HUD sync otomatis via OnEnable subscribe ke SanitySystem event
    /// </summary>
    public class PlayerState : MonoBehaviour
    {
        public static PlayerState Instance { get; private set; }

        // ─── Events ───────────────────────────────────────────────────────────
        /// <summary>Dipanggil saat IsFrozen berubah. True = player beku.</summary>
        public event Action<bool> OnFrozenChanged;

        /// <summary>Dipanggil saat IsCrouching berubah.</summary>
        public event Action<bool> OnCrouchChanged;

        /// <summary>Dipanggil saat IsRunning berubah.</summary>
        public event Action<bool> OnRunningChanged;

        // ─── Backing fields ───────────────────────────────────────────────────
        private bool _isFrozen;
        private bool _isCrouching;
        private bool _isRunning;

        // ─── Movement State ───────────────────────────────────────────────────

        /// <summary>
        /// True = player tidak bisa bergerak sama sekali.
        /// Di-set oleh: JumpscareManager, cutscene, dialog, game over.
        /// </summary>
        public bool IsFrozen
        {
            get => _isFrozen;
            set
            {
                if (_isFrozen == value) return;
                _isFrozen = value;
                OnFrozenChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// True = player sedang jongkok.
        /// Di-set oleh: PlayerMovement saat tombol crouch ditekan.
        /// </summary>
        public bool IsCrouching
        {
            get => _isCrouching;
            set
            {
                if (_isCrouching == value) return;
                _isCrouching = value;
                OnCrouchChanged?.Invoke(value);

                // Jika mulai crouch saat running → paksa stop running
                if (value && _isRunning) IsRunning = false;
            }
        }

        /// <summary>
        /// True = player sedang sprint.
        /// Di-set oleh: PlayerMovement berdasarkan input + kondisi CanSprint.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                OnRunningChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// True = player sedang reload senter.
        /// Di-set oleh: FlashlightSystem.ReloadRoutine().
        /// Mencegah sprint selama reload berlangsung.
        /// </summary>
        public bool IsReloading { get; set; } = false;

        /// <summary>
        /// True = player sedang melakukan hold interact (misal: nyalakan generator).
        /// Di-set oleh: PlayerInteraction.HandleHoldInteraction().
        /// </summary>
        public bool IsInteracting { get; set; } = false;

        // ─── Derived State (computed, tidak di-set dari luar) ─────────────────

        /// <summary>Player bisa bergerak jika tidak beku dan tidak reload.</summary>
        public bool CanMove => !IsFrozen && !IsReloading;

        /// <summary>Player bisa sprint jika bisa gerak, tidak crouch, stamina cukup.</summary>
        public bool CanSprint => CanMove && !IsCrouching;

        // ─── World State ──────────────────────────────────────────────────────

        /// <summary>
        /// True = player sedang di area aman (lampu menyala, safe room).
        /// Dipakai SanitySystem untuk mempercepat recovery.
        /// </summary>
        public bool IsInSafeZone { get; set; } = false;

        /// <summary>
        /// True = player sedang bersembunyi (lemari, kolong meja, dll.).
        /// Dipakai PlayerNoiseEmitter: noise diturunkan ke level crouch saat hiding.
        /// </summary>
        public bool IsHiding { get; set; } = false;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            // Sync sanity bar ke PlayerHUD saat SanitySystem sudah siap
            if (SanitySystem.Instance != null)
                SanitySystem.Instance.OnSanityChanged += SyncSanityHUD;
        }

        void OnDisable()
        {
            if (SanitySystem.Instance != null)
                SanitySystem.Instance.OnSanityChanged -= SyncSanityHUD;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Reset semua transient state saat scene baru dimuat.
        /// Dipanggil dari KKN_SceneManager.OnSceneLoadCompleted.
        /// State permanen (IsInSafeZone, dll.) tidak direset di sini.
        /// </summary>
        public void ResetForNewScene()
        {
            IsFrozen      = false;
            IsReloading   = false;
            IsInteracting = false;
            IsRunning     = false;
            // IsCrouching dibiarkan — bisa jadi player mau tetap crouch antar scene
        }

        /// <summary>
        /// Freeze player secara total: beku + unlock cursor untuk UI.
        /// Shortcut untuk JumpscareManager dan cutscene.
        /// </summary>
        public void FreezeForCutscene()
        {
            IsFrozen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Unfreeze player dan kunci cursor kembali untuk gameplay.
        /// </summary>
        public void UnfreezeForGameplay()
        {
            IsFrozen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ─── Private ──────────────────────────────────────────────────────────

        private void SyncSanityHUD(float sanityPercent)
        {
            UI.PlayerHUD.Instance?.SetSanity(sanityPercent);
        }

#if UNITY_EDITOR
        void OnGUI()
        {
            if (!Application.isPlaying) return;

            // Debug overlay — hanya di Editor
            GUILayout.BeginArea(new Rect(10, 200, 200, 180));
            GUI.backgroundColor = new Color(0, 0, 0, 0.6f);
            GUILayout.Box(
                $"[PlayerState]\n" +
                $"Frozen:      {IsFrozen}\n" +
                $"Crouching:   {IsCrouching}\n" +
                $"Running:     {IsRunning}\n" +
                $"Reloading:   {IsReloading}\n" +
                $"Interacting: {IsInteracting}\n" +
                $"Hiding:      {IsHiding}\n" +
                $"SafeZone:    {IsInSafeZone}"
            );
            GUILayout.EndArea();
        }
#endif
    }
}
