using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Full-screen fade overlay using CanvasGroup alpha.
    /// Place one instance in every scene on a persistent Canvas.
    /// Uses pure Coroutines — no third-party tweening required.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class KKN_FadeController : MonoBehaviour
    {
        // ─── Singleton (scene-local) ──────────────────────────────────────────
        public static KKN_FadeController Instance { get; private set; }

        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("Fade Settings")]
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private bool  startFadedIn = false;   // true for Intro scene

        // ─── References ───────────────────────────────────────────────────────
        private CanvasGroup _cg;
        private Image       _bg;

        // ─────────────────────────────────────────────────────────────────────
        void Awake()
        {
            Instance = this;

            _cg = GetComponent<CanvasGroup>();
            _bg = GetComponentInChildren<Image>();

            if (_bg != null) _bg.color = fadeColor;

            // Block raycasts during fade; don't interact otherwise
            _cg.blocksRaycasts  = false;
            _cg.interactable    = false;

            _cg.alpha = startFadedIn ? 1f : 0f;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Fade from transparent to opaque (black).</summary>
        public IEnumerator FadeOut(float duration)
        {
            yield return Fade(0f, 1f, duration);
        }

        /// <summary>Fade from opaque (black) to transparent.</summary>
        public IEnumerator FadeIn(float duration)
        {
            yield return Fade(1f, 0f, duration);
        }

        /// <summary>Fade to black, invoke callback, then fade back.</summary>
        public IEnumerator FadeOutAndIn(float halfDuration, System.Action onMidpoint)
        {
            yield return FadeOut(halfDuration);
            onMidpoint?.Invoke();
            yield return new WaitForSecondsRealtime(0.1f);
            yield return FadeIn(halfDuration);
        }

        /// <summary>Set alpha instantly with no animation.</summary>
        public void SetAlpha(float alpha)
        {
            _cg.alpha = Mathf.Clamp01(alpha);
            _cg.blocksRaycasts = alpha > 0.01f;
        }

        // ─── Core ─────────────────────────────────────────────────────────────

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                _cg.alpha = to;
                _cg.blocksRaycasts = to > 0.01f;
                yield break;
            }

            _cg.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth-step for a cinematic feel
                _cg.alpha = Mathf.SmoothStep(from, to, t);
                yield return null;
            }

            _cg.alpha = to;
            _cg.blocksRaycasts = to > 0.01f;
        }
    }
}
