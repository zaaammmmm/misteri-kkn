using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Controller for Scene Chapter1.
    /// Plays a sequential cinematic dialog sequence based on GDD narrative,
    /// then transitions to Gameplay.
    ///
    /// Expected Hierarchy
    /// ──────────────────
    /// [Canvas]
    ///   ├── FadeOverlay        (Image + CanvasGroup) → KKN_FadeController
    ///   ├── BackgroundImage    (Image, fullscreen — set via script or inspector)
    ///   ├── DialogPanel        (CanvasGroup)
    ///   │     ├── SpeakerName  (TMP)
    ///   │     ├── DialogText   (TMP)
    ///   │     └── ContinueHint (TMP) "▶  Klik untuk lanjut"
    ///   ├── ChapterTitle       (CanvasGroup)
    ///   │     └── TitleText    (TMP)  "Chapter 1 — Sebelum Kegelapan"
    ///   └── SkipButton         (Button)  → skips entire sequence
    /// </summary>
    public class KKN_Chapter1Controller : MonoBehaviour
    {
        // ─── Data ─────────────────────────────────────────────────────────────
        [System.Serializable]
        public class DialogEntry
        {
            public string speaker;

            [TextArea(2, 5)]
            public string text;

            [Tooltip("Optional background sprite for this dialog line")]
            public Sprite backgroundSprite;

            [Tooltip("Pause in seconds before this line appears")]
            public float preDelay = 0f;
        }

        // ─── Inspector ────────────────────────────────────────────────────────
        [Header("UI References")]
        [SerializeField] private CanvasGroup chapterTitleGroup;
        [SerializeField] private TMP_Text    chapterTitleText;
        [SerializeField] private CanvasGroup dialogPanel;
        [SerializeField] private TMP_Text    speakerNameText;
        [SerializeField] private TMP_Text    dialogText;
        [SerializeField] private CanvasGroup continueHintGroup;
        [SerializeField] private Button      skipButton;
        [SerializeField] private Image       backgroundImage;

        [Header("Typewriter")]
        [SerializeField] private float typewriterSpeed = 0.035f;

        [Header("Dialog Data — Auto-filled from GDD")]
        [SerializeField] private List<DialogEntry> dialogs = new List<DialogEntry>();
        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource typingSource;

        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip typingClip;
        private float lastTypingSoundTime;
        [SerializeField] private float typingSoundInterval = 0.20f;
        private bool _waitingForContinue = false;

        // ─── State ────────────────────────────────────────────────────────────
        // private int  _currentIndex = 0;
        private bool _isTyping     = false;
        private bool _skipAll      = false;
        private bool _clickedNext  = false;

        // ─────────────────────────────────────────────────────────────────────
        void Awake()
        {
            PopulateDefaultDialogs();
        }

        void Start()
        {
            SetAlpha(chapterTitleGroup, 0f);
            SetAlpha(dialogPanel, 0f);
            SetAlpha(continueHintGroup, 0f);

            skipButton?.onClick.AddListener(SkipAll);

            PlayBackgroundMusic();

            StartCoroutine(SceneStartRoutine());
        }

        private IEnumerator SceneStartRoutine()
        {
            yield return null;

            if (KKN_FadeController.Instance != null)
                yield return KKN_FadeController.Instance.FadeIn(1f);

            yield return ChapterSequence();
        }
        
        void Update()
        {
            if (Input.GetMouseButtonDown(0) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return))
            {
                if (_isTyping)
                {
                    FinishTypingInstantly();
                }
                else if (_waitingForContinue)
                {
                    _clickedNext = true;
                }
            }
        }

        // ─── Sequence ─────────────────────────────────────────────────────────

        private IEnumerator ChapterSequence()
        {
            // ── Chapter Title Card ──
            if (chapterTitleText != null)
                chapterTitleText.text = "CHAPTER 1\n<size=60%>Sebelum Kegelapan</size>";

            yield return FadeGroup(chapterTitleGroup, 0f, 1f, 1.5f);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return FadeGroup(chapterTitleGroup, 1f, 0f, 0.8f);

            yield return new WaitForSecondsRealtime(0.4f);

            // ── Dialog Panel ──
            yield return FadeGroup(dialogPanel, 0f, 1f, 0.6f);

            foreach (DialogEntry entry in dialogs)
            {
                if (_skipAll) break;

                if (entry.preDelay > 0f)
                    yield return new WaitForSecondsRealtime(entry.preDelay);

                // Swap background if provided
                if (backgroundImage != null && entry.backgroundSprite != null)
                    backgroundImage.sprite = entry.backgroundSprite;

                yield return PlayDialog(entry);
            }

            // ── Transition to Gameplay ──
            yield return FadeGroup(dialogPanel, 1f, 0f, 0.5f);
            yield return new WaitForSecondsRealtime(0.3f);

            if (musicSource != null)
                musicSource.Stop();

            KKN_SceneManager.Instance
                ?.LoadSceneWithLoading("Gameplay");
        }

        private IEnumerator PlayDialog(DialogEntry entry)
        {
            _clickedNext = false;

            SetAlpha(continueHintGroup, 0f);

            if (speakerNameText != null)
                speakerNameText.text = entry.speaker;

            if (dialogText != null)
                dialogText.text = "";

            _isTyping = true;

            foreach (char c in entry.text)
            {
                if (_skipAll)
                {
                    dialogText.text = entry.text;
                    break;
                }

                if (!_isTyping)
                {
                    dialogText.text = entry.text;
                    break;
                }

                dialogText.text += c;

                if (!char.IsWhiteSpace(c))
                    PlayTypingSound();

                yield return new WaitForSecondsRealtime(typewriterSpeed);
            }

            _isTyping = false;

            dialogText.text = entry.text;

            if (typingSource != null)
                typingSource.Stop();

            yield return FadeGroup(continueHintGroup, 0f, 1f, 0.4f);

            // Buang klik yang mungkin masih tersisa
            yield return null;

            _clickedNext = false;

            _waitingForContinue = true;

            while (!_clickedNext && !_skipAll)
                yield return null;

            _waitingForContinue = false;

            yield return FadeGroup(continueHintGroup, 1f, 0f, 0.2f);
        }

        private void FinishTypingInstantly()
        {
            _isTyping = false;

            if (typingSource != null)
                typingSource.Stop();
        }

        private void SkipAll()
        {
            _skipAll = true;
        }

        // ─── Default Dialog from GDD ──────────────────────────────────────────
        // Narrative: Siang normal di desa KKN → menjelang Magrib →
        //            teman-teman pergi ke desa seberang →
        //            Azzam sakit, tinggal sendirian → bangun → desa kosong.

        private void PopulateDefaultDialogs()
        {
            if (dialogs != null && dialogs.Count > 0) return; // respect inspector data

            dialogs = new List<DialogEntry>
            {
                // datanya sudah di inspector, jadi biar gampang editnya langsung di sana aja
            };
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

        private void PlayBackgroundMusic()
        {
            if (musicSource == null || backgroundMusic == null)
                return;

            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            musicSource.Play();
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
