using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Gameplay state controller for Misteri KKN.
    ///
    /// Responsibilities
    /// ────────────────
    /// • Monitors generator win condition
    /// • Pause / resume with full-screen overlay
    /// • Connects "Back to Main Menu" from pause
    /// • Auto-saves at the start of gameplay
    ///
    /// Win Condition
    /// ─────────────
    /// Call SetGeneratorOn() from your existing generator script when
    /// the generator is activated, OR set isGeneratorOn = true via
    /// the existing system and this controller will poll it.
    ///
    /// Expected Hierarchy
    /// ──────────────────
    /// [Canvas]
    ///   ├── FadeOverlay      (CanvasGroup) → KKN_FadeController
    ///   └── PauseMenuPanel   (CanvasGroup)
    ///         ├── BtnResume  (Button)
    ///         ├── BtnMainMenu (Button)
    ///         └── BtnQuit    (Button)
    /// </summary>
    public class KKN_GameplayController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("Win Condition")]
        [Tooltip("Set true (or call SetGeneratorOn()) when generator activates.")]
        public bool isGeneratorOn = false;

        [Header("Pause Menu")]
        [SerializeField] private CanvasGroup pauseMenuPanel;
        [SerializeField] private Button btnResume;
        [SerializeField] private Button btnMainMenu;
        [SerializeField] private Button btnQuit;

        [Header("Checkpoint")]
        [Tooltip("Save game automatically this many seconds after scene loads.")]
        [SerializeField] private float autoSaveDelay = 2f;

        // ─── State ────────────────────────────────────────────────────────────
        private bool _isPaused        = false;
        private bool _winTriggered    = false;

        // ─────────────────────────────────────────────────────────────────────
        void Start()
        {
            // Hide pause menu
            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.alpha          = 0f;
                pauseMenuPanel.interactable   = false;
                pauseMenuPanel.blocksRaycasts = false;
            }

            // Wire pause buttons
            btnResume?.onClick.AddListener(Resume);
            btnMainMenu?.onClick.AddListener(GoToMainMenu);
            btnQuit?.onClick.AddListener(() => KKN_SceneManager.Instance?.QuitGame());

            StartCoroutine(SceneStartRoutine());
        }

        private IEnumerator SceneStartRoutine()
        {
            yield return null;

            if (KKN_FadeController.Instance != null)
                yield return KKN_FadeController.Instance.FadeIn(1f);

            yield return AutoSaveRoutine();
        }

        void Update()
        {
            // Toggle pause with Escape / Start
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isPaused) Resume();
                else           Pause();
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Dipanggil oleh GeneratorPuzzle.PowerOnGenerator() saat generator berhasil dinyalakan.
        /// Menangani SaveGame dan sinkronisasi state — transisi scene ditangani oleh
        /// GeneratorPuzzle.EndSequence() via KKN_SceneManager.GoToEnding().
        /// </summary>
        public void NotifyGeneratorOn()
        {
            if (_winTriggered) return;
            _winTriggered  = true;
            isGeneratorOn  = true;

            // Simpan progress sebelum pindah scene
            SaveSystem.Instance?.SaveGame();

            Debug.Log("[KKN_GameplayController] NotifyGeneratorOn — game saved, menunggu EndSequence.");
        }

        /// <summary>
        /// Alternatif langsung: set flag + trigger win sekaligus dalam satu call.
        /// Gunakan ini jika kamu tidak butuh delay dari GeneratorPuzzle.EndSequence().
        /// </summary>
        public void SetGeneratorOn()
        {
            isGeneratorOn = true;
            TriggerWin();
        }

        /// <summary>Trigger game-over / fail state and return to Main Menu.</summary>
        public void TriggerGameOver()
        {
            if (_winTriggered) return;
            StartCoroutine(GameOverRoutine());
        }

        // ─── Pause ────────────────────────────────────────────────────────────

        private void Pause()
        {
            _isPaused = true;
            Time.timeScale = 0f;
            ShowPanel(pauseMenuPanel, true);
        }

        private void Resume()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            ShowPanel(pauseMenuPanel, false);
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            KKN_SceneManager.Instance?.GoToMainMenu();
        }

        // ─── Win Condition ────────────────────────────────────────────────────

        private void TriggerWin()
        {
            if (_winTriggered) return;
            _winTriggered = true;

            SaveSystem.Instance?.SaveGame();
            StartCoroutine(WinRoutine());
        }

        private IEnumerator WinRoutine()
        {
            // Small dramatic pause before transitioning
            yield return new WaitForSeconds(1.5f);
            KKN_SceneManager.Instance?.GoToEnding();
        }

        private IEnumerator GameOverRoutine()
        {
            _winTriggered = true; // prevent double-trigger
            yield return new WaitForSeconds(0.5f);
            KKN_SceneManager.Instance?.GoToMainMenu();
        }

        // ─── Auto-Save ────────────────────────────────────────────────────────

        private IEnumerator AutoSaveRoutine()
        {
            yield return new WaitForSeconds(autoSaveDelay);
            SaveSystem.Instance?.SaveCurrentScene();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void ShowPanel(CanvasGroup cg, bool show)
        {
            if (cg == null) return;
            cg.alpha          = show ? 1f : 0f;
            cg.interactable   = show;
            cg.blocksRaycasts = show;
        }
    }
}