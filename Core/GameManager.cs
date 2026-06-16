using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

namespace KKN.Game.Core
{
    /// <summary>
    /// Central game state manager with horror intensity curve.
    /// Controls game flow: Menu → Playing → Paused → GameOver.
    ///
    /// Perbaikan dari versi lama:
    ///   G1 — Scene reset logic via SceneManager events + GhostRegistry.ClearAll()
    ///   G2 — Time.timeScale dikembalikan dengan benar saat Cutscene masuk dari Inventory
    ///   G3 — Guard matrix transisi state ilegal
    ///   C7 — Integrasi GhostRegistry.PurgeDestroyed() berkala
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Menu, Playing, Paused, GameOver, Cutscene, Inventory }

        // ─────────────────────────────────────────────────────────────────
        //  G3 — LEGAL TRANSITION MATRIX
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Mendefinisikan transisi state yang diizinkan.
        /// Transisi di luar tabel ini diblokir dengan log warning.
        ///
        /// Cara baca: kunci = state asal, value = set state tujuan yang valid.
        /// </summary>
        private static readonly Dictionary<GameState, HashSet<GameState>> _validTransitions
            = new Dictionary<GameState, HashSet<GameState>>
        {
            { GameState.Menu,      new HashSet<GameState> { GameState.Playing, GameState.Cutscene } },
            { GameState.Playing,   new HashSet<GameState> { GameState.Paused, GameState.GameOver,
                                                             GameState.Cutscene, GameState.Inventory } },
            { GameState.Paused,    new HashSet<GameState> { GameState.Playing, GameState.GameOver,
                                                             GameState.Menu } },
            { GameState.GameOver,  new HashSet<GameState> { GameState.Menu, GameState.Playing } },
            { GameState.Cutscene,  new HashSet<GameState> { GameState.Playing, GameState.GameOver } },
            { GameState.Inventory, new HashSet<GameState> { GameState.Playing, GameState.GameOver } },
        };

        // ─────────────────────────────────────────────────────────────────
        //  INSPECTOR
        // ─────────────────────────────────────────────────────────────────

        [Header("Current State")]
        [SerializeField] private GameState currentState = GameState.Playing;

        [Header("Events")]
        public GameEvent onGameStateChanged;
        public GameEvent onGamePaused;
        public GameEvent onGameResumed;
        public GameEvent onGameOver;

        // ─────────────────────────────────────────────────────────────────
        //  PROPERTIES & EVENTS
        // ─────────────────────────────────────────────────────────────────

        public GameState CurrentState => currentState;

        public event Action<GameState> OnStateChanged;

        public bool IsPlaying  => currentState == GameState.Playing;
        public bool IsPaused   => currentState == GameState.Paused;
        public bool IsGameOver => currentState == GameState.GameOver;

        // ─────────────────────────────────────────────────────────────────
        //  C7 — GHOST REGISTRY PURGE TIMER
        // ─────────────────────────────────────────────────────────────────

        private float _registryPurgeTimer;
        private const float REGISTRY_PURGE_INTERVAL = 5f;   // detik

        // ─────────────────────────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // G1 — Subscribe ke scene events untuk handle reload dengan benar
            SceneManager.sceneLoaded   += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDestroy()
        {
            // Bersihkan subscription agar tidak leak saat Instance di-replace
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void Update()
        {
            HandleEscapeInput();
            TickRegistryPurge();
        }

        // ─────────────────────────────────────────────────────────────────
        //  G1 — SCENE MANAGEMENT HOOKS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Dipanggil saat scene baru selesai load.
        /// Reset state ke Playing (atau Menu jika index 0).
        /// </summary>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Additive load (misalnya UI overlay) tidak reset state
            if (mode == LoadSceneMode.Additive) return;

            bool isMainMenu = scene.buildIndex == 0;

            // Paksa timeScale kembali ke 1 — bisa saja scene reload dari GameOver/Pause
            Time.timeScale = 1f;

            // Set state tanpa validasi (ForceSetState), karena ini hard reset
            ForceSetState(isMainMenu ? GameState.Menu : GameState.Playing);

            if (isMainMenu)
                InputManager.Instance?.UnlockCursor();
            else
                InputManager.Instance?.LockCursor();

#if UNITY_EDITOR
            Debug.Log($"[GameManager] Scene loaded '{scene.name}' → state: {currentState}");
#endif
        }

        /// <summary>
        /// Dipanggil sesaat sebelum scene di-unload.
        /// Bersihkan GhostRegistry agar tidak ada referensi stale.
        /// </summary>
        void OnSceneUnloaded(Scene scene)
        {
            GhostRegistry.ClearAll();
        }

        // ─────────────────────────────────────────────────────────────────
        //  C7 — PERIODIC REGISTRY PURGE
        // ─────────────────────────────────────────────────────────────────

        void TickRegistryPurge()
        {
            if (!IsPlaying) return;

            _registryPurgeTimer -= Time.deltaTime;
            if (_registryPurgeTimer <= 0f)
            {
                GhostRegistry.PurgeDestroyed();
                _registryPurgeTimer = REGISTRY_PURGE_INTERVAL;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  INPUT
        // ─────────────────────────────────────────────────────────────────

        void HandleEscapeInput()
        {
            if (InputManager.Instance == null || !InputManager.Instance.GetEscapeDown()) return;

            switch (currentState)
            {
                case GameState.Playing:
                    PauseGame();
                    break;

                case GameState.Paused:
                    ResumeGame();
                    break;

                // ESC saat Inventory → TabInventoryUI yang menangani via HandleInput()
                // ESC saat Cutscene → tidak interuptible dari GameManager
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  STATE TRANSITIONS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Transisi state dengan validasi matrix (G3).
        /// Gunakan ini untuk semua transisi normal dari gameplay.
        /// </summary>
        public void SetState(GameState newState)
        {
            if (currentState == newState) return;

            // G3 — Blokir transisi ilegal
            if (!IsValidTransition(currentState, newState))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[GameManager] Transisi ilegal: {currentState} → {newState}. Diabaikan.");
#endif
                return;
            }

            ForceSetState(newState);
        }

        /// <summary>
        /// Set state tanpa validasi matrix.
        /// Dipakai untuk hard reset (scene load, debug).
        /// Jangan pakai ini dari gameplay code biasa.
        /// </summary>
        private void ForceSetState(GameState newState)
        {
            // G2 — Simpan timeScale sebelum Inventory/Pause
            // agar Cutscene yang masuk dari state pause tahu harus kembalikan ke berapa
            GameState prev = currentState;
            currentState   = newState;

            ApplyStateEffects(newState, prev);

            onGameStateChanged?.Raise();
            OnStateChanged?.Invoke(newState);
        }

        /// <summary>
        /// Terapkan efek samping tiap state.
        /// Dipisah dari ForceSetState agar mudah di-extend tanpa menyentuh logika transisi.
        /// </summary>
        private void ApplyStateEffects(GameState newState, GameState prevState)
        {
            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    InputManager.Instance?.LockCursor();
                    onGameResumed?.Raise();
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    InputManager.Instance?.UnlockCursor();
                    onGamePaused?.Raise();
                    break;

                case GameState.GameOver:
                    // G2 — Pastikan time berjalan normal meski dari Inventory/Pause (timeScale=0)
                    Time.timeScale = 1f;
                    InputManager.Instance?.UnlockCursor();
                    onGameOver?.Raise();
                    break;

                case GameState.Cutscene:
                    // G2 — Perbaiki: Cutscene HARUS set timeScale = 1f agar animasi berjalan.
                    // Sebelumnya tidak di-set, sehingga masuk cutscene dari Inventory
                    // (timeScale=0) membuat cutscene beku.
                    Time.timeScale = 1f;
                    InputManager.Instance?.LockCursor();
                    break;

                case GameState.Inventory:
                    Time.timeScale = 0f;
                    InputManager.Instance?.UnlockCursor();
                    break;

                case GameState.Menu:
                    Time.timeScale = 1f;
                    InputManager.Instance?.UnlockCursor();
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  G3 — TRANSITION VALIDATION
        // ─────────────────────────────────────────────────────────────────

        private bool IsValidTransition(GameState from, GameState to)
        {
            return _validTransitions.TryGetValue(from, out var allowed)
                && allowed.Contains(to);
        }

        /// <summary>
        /// Cek apakah transisi tertentu valid.
        /// Berguna untuk UI button (misalnya disable tombol Pause saat Cutscene).
        /// </summary>
        public bool CanTransitionTo(GameState target)
            => IsValidTransition(currentState, target);

        // ─────────────────────────────────────────────────────────────────
        //  PUBLIC SHORTCUTS
        // ─────────────────────────────────────────────────────────────────

        public void PauseGame()      => SetState(GameState.Paused);
        public void ResumeGame()     => SetState(GameState.Playing);
        public void GameOver()       => SetState(GameState.GameOver);
        public void StartCutscene()  => SetState(GameState.Cutscene);
        public void EndCutscene()    => SetState(GameState.Playing);
        public void OpenInventory()  => SetState(GameState.Inventory);
        public void CloseInventory() => SetState(GameState.Playing);

        // ─────────────────────────────────────────────────────────────────
        //  DEBUG
        // ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Debug: Print State Info")]
        void DebugPrintState()
        {
            Debug.Log($"[GameManager] State: {currentState} | " +
                      $"timeScale: {Time.timeScale} | " +
                      $"Ghosts active: {GhostRegistry.Count}");
        }
#endif
    }
}
