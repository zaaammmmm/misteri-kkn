using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Controller for the Intro (splash) scene.
    ///
    /// Sequence
    /// ────────
    ///   Black screen → Studio logo fades in → holds → fades out
    ///   → Game title fades in → tagline appears (typewriter) → holds
    ///   → fade to black → load MainMenu
    ///
    /// Any key / click skips to MainMenu immediately.
    ///
    /// Expected Hierarchy
    /// ──────────────────
    /// [Canvas]
    ///   ├── FadeOverlay     (Image + CanvasGroup) → KKN_FadeController
    ///   ├── StudioLogo      (Image, CanvasGroup)
    ///   ├── TitleGroup      (CanvasGroup)
    ///   │     ├── GameTitle (TMP)   "MISTERI KKN"
    ///   │     └── Tagline   (TMP)   tagline text
    ///   └── SkipHint        (TMP)   "Tekan tombol apa saja untuk lewati"
    /// </summary>
    public class KKN_IntroController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup studioLogoGroup;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private TMP_Text    taglineText;
        [SerializeField] private CanvasGroup skipHintGroup;

        [Header("Timing (seconds)")]
        [SerializeField] private float fadeInDuration   = 1.2f;
        [SerializeField] private float logoHoldDuration = 2.0f;
        [SerializeField] private float fadeOutDuration  = 0.8f;
        [SerializeField] private float titleHoldDuration = 3.0f;
        [SerializeField] private float typewriterSpeed   = 0.045f; // sec per character

        [Header("Content")]
        [SerializeField]
        [TextArea]
        private string tagline =
            "Jumat, 13 Juni 2026.\n" +
            "Desa yang seharusnya tenang…\n" +
            "kini hanya menyisakan kegelapan.";

        // ─── State ────────────────────────────────────────────────────────────
        private bool _skipped = false;

        // ─────────────────────────────────────────────────────────────────────
        void Start()
        {
            // Hide everything initially
            SetAlpha(studioLogoGroup, 0f);
            SetAlpha(titleGroup,      0f);
            SetAlpha(skipHintGroup,   0f);

            if (taglineText) taglineText.text = "";

        StartCoroutine(SceneStartRoutine());
        }

        private IEnumerator SceneStartRoutine()
        {
            yield return null;

            if (KKN_FadeController.Instance != null)
                yield return KKN_FadeController.Instance.FadeIn(1f);

            yield return IntroSequence();
        }

        void Update()
        {
            // Any input skips the intro
            if (!_skipped && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
                Skip();
        }

        // ─── Sequence ─────────────────────────────────────────────────────────
        private IEnumerator IntroSequence()
        {
            // ── Show skip hint after half a second ──
            yield return new WaitForSecondsRealtime(0.5f);
            yield return FadeGroup(skipHintGroup, 0f, 0.5f, 0.6f);

            // ── Studio logo ──
            yield return FadeGroup(studioLogoGroup, 0f, 1f, fadeInDuration);
            yield return WaitOrSkip(logoHoldDuration);
            yield return FadeGroup(studioLogoGroup, 1f, 0f, fadeOutDuration);

            if (_skipped) yield break;

            yield return new WaitForSecondsRealtime(0.3f);

            // ── Game title ──
            yield return FadeGroup(titleGroup, 0f, 1f, fadeInDuration);
            if (taglineText != null)
                yield return TypewriterEffect(tagline, taglineText);

            yield return WaitOrSkip(titleHoldDuration);

            GoToMainMenu();
        }

        private void Skip()
        {
            if (_skipped) return;
            _skipped = true;
            StopAllCoroutines();
            GoToMainMenu();
        }

        private void GoToMainMenu()
        {
            KKN_SceneManager.Instance?.LoadScene(KKN_SceneManager.GameScene.MainMenu);
        }

        // ─── Effects ──────────────────────────────────────────────────────────

        private IEnumerator TypewriterEffect(string fullText, TMP_Text target)
        {
            target.text = "";
            foreach (char c in fullText)
            {
                if (_skipped) { target.text = fullText; yield break; }
                target.text += c;
                yield return new WaitForSecondsRealtime(typewriterSpeed);
            }
        }

        private IEnumerator WaitOrSkip(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !_skipped)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator FadeGroup(CanvasGroup cg, float from, float to, float dur)
        {
            if (cg == null) yield break;
            float elapsed = 0f;
            while (elapsed < dur && !_skipped)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / dur));
                yield return null;
            }
            cg.alpha = _skipped ? to : to;
        }

        private void SetAlpha(CanvasGroup cg, float a)
        {
            if (cg != null) cg.alpha = a;
        }
    }
}
