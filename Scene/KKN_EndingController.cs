using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Controller for Scene Ending.
    /// Displays a slow atmospheric text reveal then offers "Selesai" to return to MainMenu.
    ///
    /// Expected Hierarchy
    /// ──────────────────
    /// [Canvas]
    ///   ├── FadeOverlay       (Image + CanvasGroup) → KKN_FadeController
    ///   ├── BackgroundImage   (Image, fullscreen)
    ///   ├── EndingTextGroup   (CanvasGroup)
    ///   │     └── EndingText  (TMP)
    ///   ├── ButtonGroup       (CanvasGroup)
    ///   │     └── BtnFinish   (Button + TMP)  "Selesai"
    ///   └── CreditText        (TMP, optional)
    /// </summary>
    public class KKN_EndingController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup endingTextGroup;
        [SerializeField] private TMP_Text    endingText;
        [SerializeField] private CanvasGroup buttonGroup;
        [SerializeField] private Button      btnFinish;
        [SerializeField] private TMP_Text    creditText;

        [Header("Timing")]
        [SerializeField] private float lineRevealInterval = 2.2f;
        [SerializeField] private float typewriterSpeed    = 0.04f;
        [SerializeField] private float endingHoldDuration = 3.0f;

        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip endingMusic;
        [SerializeField] private AudioSource typingSource;
        [SerializeField] private AudioClip typingClip;

        // private float lastTypingSoundTime;

        // [SerializeField]
        // private float typingSoundInterval = 0.20f;

        // ─── Ending Lines (atmospheric reveal) ───────────────────────────────
        [Header("Ending Narrative")]
        [SerializeField]
        private List<string> endingLines = new List<string>
        {
            "Generator menyala.",
            "Cahaya meledak memenuhi desa.",
            "Dan sesuatu itu... menghilang ke dalam kegelapan yang tersisa.",
            " ",
            "Azzam berdiri di antara lampu-lampu yang kembali hidup,\nmengatur nafasnya yang masih tersengal.",
            " ",
            "Teman-temannya tiba saat subuh.\nMereka tidak percaya dengan apa yang diceritakan Azzam.",
            " ",
            "Tidak ada yang percaya.",
            " ",
            "Tapi Azzam tahu.",
            "Desa itu bukan sekadar sunyi malam itu.",
            "Sesuatu memang ada.",
            " ",
            "Dan mungkin… masih ada.",
        };

        // ─────────────────────────────────────────────────────────────────────
        void Start()
        {
            SetAlpha(endingTextGroup, 0f);
            SetAlpha(buttonGroup, 0f);

            btnFinish?.onClick.AddListener(OnFinish);

            PlayEndingMusic();

            StartCoroutine(SceneStartRoutine());
        }

        private void PlayEndingMusic()
        {
            if (musicSource == null || endingMusic == null)
                return;

            musicSource.clip = endingMusic;
            musicSource.loop = true;
            musicSource.Play();
        }

        private IEnumerator SceneStartRoutine()
        {
            yield return null;

            if (KKN_FadeController.Instance != null)
                yield return KKN_FadeController.Instance.FadeIn(1f);

            yield return EndingSequence();
        }

        // ─── Sequence ─────────────────────────────────────────────────────────
        private IEnumerator EndingSequence()
        {
            yield return new WaitForSecondsRealtime(1.0f);

            yield return FadeGroup(
                endingTextGroup,
                0f,
                1f,
                1.2f
            );

            string accumulated = "";

            foreach (string line in endingLines)
            {
                if (line == " ")
                {
                    accumulated += "\n";

                    if (endingText)
                        endingText.text = accumulated;

                    yield return new WaitForSecondsRealtime(
                        lineRevealInterval * 0.5f
                    );

                    continue;
                }

                string fullLine = accumulated;

                foreach (char c in line)
                {
                    fullLine += c;

                    if (endingText)
                        endingText.text = fullLine;

                    if (!char.IsWhiteSpace(c))
                        PlayTypingSound();

                    yield return new WaitForSecondsRealtime(
                        typewriterSpeed
                    );
                }

                if (typingSource != null)
                    typingSource.Stop();

                accumulated = fullLine + "\n";

                yield return new WaitForSecondsRealtime(
                    lineRevealInterval
                );
            }

            // SEMUA NARASI SUDAH SELESAI
            yield return new WaitForSecondsRealtime(
                endingHoldDuration
            );

            if (typingSource != null)
                typingSource.Stop();

            // Baru tampilkan tombol selesai
            yield return FadeGroup(
                buttonGroup,
                0f,
                1f,
                1.0f
            );
        }

        private void OnFinish()
        {
            StartCoroutine(FinishRoutine());
        }

        private IEnumerator FinishRoutine()
        {
            yield return FadeOutMusic(2f);

            KKN_SceneManager.Instance?.GoToMainMenu();
        }
        private IEnumerator FadeOutMusic(float duration)
        {
            if (musicSource == null)
                yield break;

            float startVolume = musicSource.volume;
            float time = 0f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, time / duration);
                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = startVolume;
        }
        // ─── Helpers ──────────────────────────────────────────────────────────

        private IEnumerator FadeGroup(CanvasGroup cg, float from, float to, float dur)
        {
            if (cg == null) yield break;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / dur));
                yield return null;
            }
            cg.alpha = to;
        }

        private void SetAlpha(CanvasGroup cg, float a)
        {
            if (cg != null) cg.alpha = a;
        }

        private void PlayTypingSound()
        {
            if (typingSource == null || typingClip == null)
                return;

            if (typingSource.isPlaying)
                return;

            typingSource.clip = typingClip;
            typingSource.Play();
        }
    }
}
