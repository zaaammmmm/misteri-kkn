using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Controller for the MainMenu scene.
    ///
    /// Expected Hierarchy
    /// ──────────────────
    /// [Canvas]
    ///   ├── FadeOverlay          (Image + CanvasGroup)  → KKN_FadeController
    ///   ├── Background           (Image, fullscreen)
    ///   ├── TitleGroup           (CanvasGroup)
    ///   │     ├── TitleText      (TMP)   "MISTERI KKN"
    ///   │     └── SubtitleText   (TMP)   "DESA TERKUTUK"
    ///   └── ButtonGroup          (CanvasGroup)
    ///         ├── BtnNewGame     (Button + TMP)
    ///         ├── BtnContinue    (Button + TMP)
    ///         └── BtnQuit        (Button + TMP)
    /// </summary>
    public class KKN_MainMenuController : MonoBehaviour
    {
        // ─── Inspector References ─────────────────────────────────────────────
        [Header("Groups")]
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private CanvasGroup buttonGroup;

        [Header("Buttons")]
        [SerializeField] private Button btnNewGame;
        [SerializeField] private Button btnContinue;
        [SerializeField] private Button btnQuit;

        [Header("Continue Lock Visuals")]
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private Color    disabledTextColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color    enabledTextColor  = new Color(0.85f, 0.78f, 0.60f, 1f);

        [Header("Timing")]
        [SerializeField] private float titleFadeDuration   = 1.4f;
        [SerializeField] private float buttonStaggerDelay  = 0.22f;
        [SerializeField] private float buttonFadeDuration  = 0.7f;
        [SerializeField] private float initialPause        = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip mainMenuMusic;
        // ─────────────────────────────────────────────────────────────────────
        void Start()
        {
            SetGroupAlpha(titleGroup, 0f);
            SetGroupAlpha(buttonGroup, 0f);

            btnNewGame.onClick.AddListener(OnNewGame);
            btnContinue.onClick.AddListener(OnContinue);
            btnQuit.onClick.AddListener(OnQuit);

            bool hasSave =
                SaveSystem.Instance != null &&
                SaveSystem.Instance.HasSaveFile();

            SetContinueState(hasSave);

            PlayMainMenuMusic();

            StartCoroutine(SceneStartRoutine());
        }

        private void PlayMainMenuMusic()
        {
            if (musicSource == null || mainMenuMusic == null)
                return;

            musicSource.clip = mainMenuMusic;
            musicSource.loop = true;
            musicSource.Play();
        }

        private IEnumerator FadeOutMusic(float duration)
        {
            if (musicSource == null)
                yield break;

            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                musicSource.volume = Mathf.Lerp(
                    startVolume,
                    0f,
                    elapsed / duration);

                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = startVolume;
        }

        private IEnumerator SceneStartRoutine()
        {
            yield return null;

            if (KKN_FadeController.Instance != null)
                yield return KKN_FadeController.Instance.FadeIn(1f);

            yield return RevealSequence();
        }

        // ─── Button Handlers ──────────────────────────────────────────────────

        private void OnNewGame()
        {
            StartCoroutine(NewGameRoutine());
        }

        private IEnumerator NewGameRoutine()
        {
            yield return FadeOutMusic(1.5f);

            SaveSystem.Instance?.DeleteSave();
            KKN_SceneManager.Instance?.GoToChapter1();
        }

        private void OnContinue()
        {
            StartCoroutine(ContinueRoutine());
        }

        private IEnumerator ContinueRoutine()
        {
            if (SaveSystem.Instance == null)
                yield break;

            KKN_SceneManager.GameScene target =
                SaveSystem.Instance.GetSavedGameScene();

            yield return FadeOutMusic(1.5f);

            KKN_SceneManager.Instance
                ?.LoadSceneWithLoading(target.ToString());
        }

        private void OnQuit()
        {
            StartCoroutine(QuitRoutine());
        }

        private IEnumerator QuitRoutine()
        {
            yield return FadeOutMusic(1f);

            KKN_SceneManager.Instance?.QuitGame();
        }

        // ─── Reveal Sequence ──────────────────────────────────────────────────
        private IEnumerator RevealSequence()
        {
            yield return new WaitForSeconds(initialPause);

            // 1. Fade in title slowly
            yield return StartCoroutine(FadeGroup(titleGroup, 0f, 1f, titleFadeDuration));

            yield return new WaitForSeconds(0.3f);

            // 2. Reveal buttons one by one (staggered — horror ambiance)
            Button[] buttons = { btnNewGame, btnContinue, btnQuit };
            foreach (Button btn in buttons)
            {
                CanvasGroup cg = btn.GetComponent<CanvasGroup>();
                if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }

            SetGroupAlpha(buttonGroup, 1f); // parent visible, children handle their own alpha

            foreach (Button btn in buttons)
            {
                CanvasGroup cg = btn.GetComponent<CanvasGroup>();
                StartCoroutine(FadeGroup(cg, 0f, 1f, buttonFadeDuration));
                yield return new WaitForSeconds(buttonStaggerDelay);
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void SetContinueState(bool enabled)
        {
            btnContinue.interactable = enabled;
            if (continueLabel != null)
                continueLabel.color = enabled ? enabledTextColor : disabledTextColor;
        }

        private void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg == null) return;
            cg.alpha = alpha;
        }

        private IEnumerator FadeGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            cg.alpha = to;
        }
    }
}
