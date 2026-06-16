using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Centralized audio manager with mixer groups, volume control,
    /// 3D spatial SFX, ambience looping, music fading, and ghost audio support.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        // ─── Mixer ────────────────────────────────────────────────────────────
        [Header("Mixer")]
        [SerializeField] private AudioMixer audioMixer;

        [Header("Mixer Groups")]
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        [Header("Volume Parameters")]
        [SerializeField] private string masterParam    = "MasterVolume";
        [SerializeField] private string sfxParam       = "SFXVolume";
        [SerializeField] private string musicParam     = "MusicVolume";
        [SerializeField] private string ambienceParam  = "AmbienceVolume";

        // ─── Internal AudioSources ────────────────────────────────────────────
        [Header("Dedicated Sources")]
        [SerializeField] private AudioSource musicSource;       // 2D, loop music
        [SerializeField] private AudioSource ambienceSource;    // 2D, loop ambience
        [SerializeField] private AudioSource ambienceSource2;   // crossfade target
        [SerializeField] private AudioSource sanitySource;      // 2D, loop sanity drone

        // ─── Runtime ──────────────────────────────────────────────────────────
        private Coroutine _musicFade;
        private Coroutine _ambienceFade;

        // ─────────────────────────────────────────────────────────────────────
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ValidateSources();
        }

        void ValidateSources()
        {
            musicSource    = EnsureSource(musicSource,    "Music_Source",    musicGroup,    false);
            ambienceSource = EnsureSource(ambienceSource, "Ambience_Source", ambienceGroup, false);
            ambienceSource2= EnsureSource(ambienceSource2,"Ambience_Source2",ambienceGroup, false);
            sanitySource   = EnsureSource(sanitySource,   "Sanity_Source",   sfxGroup,      false);
        }

        AudioSource EnsureSource(AudioSource existing, string goName,
                                  AudioMixerGroup group, bool is3D)
        {
            if (existing != null) return existing;
            var go = new GameObject(goName);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = group;
            src.spatialBlend = is3D ? 1f : 0f;
            src.playOnAwake = false;
            return src;
        }

        // ═════════════════════════════════════════════════════════════════════
        // SFX — 3D (world position), pakai sfxGroup
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play one-shot SFX di posisi dunia (3D spatialized).
        /// Untuk: footstep, jump, generator, pickup, door, dll.
        /// </summary>
        public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var go  = new GameObject("TempSFX_" + clip.name);
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip                  = clip;
            src.volume                = volume;
            src.pitch                 = pitch;
            src.spatialBlend          = 1f;        // 3D
            src.rolloffMode           = AudioRolloffMode.Logarithmic;
            src.minDistance           = 1f;
            src.maxDistance           = 20f;
            src.outputAudioMixerGroup = sfxGroup;
            src.Play();
            Destroy(go, clip.length / Mathf.Abs(pitch) + 0.1f);
        }

        /// <summary>
        /// Varian dengan random pitch — cocok untuk footstep variation.
        /// </summary>
        public void PlaySFXRandom(AudioClip[] clips, Vector3 position,
                                   float volume = 1f, float pitchMin = 0.9f, float pitchMax = 1.1f)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            PlaySFX(clip, position, volume, Random.Range(pitchMin, pitchMax));
        }

        // ─── 2D SFX (UI, non-positional) ─────────────────────────────────────

        /// <summary>
        /// Play 2D SFX tanpa posisi — untuk UI: flashlight click, hover, pickup chime, dll.
        /// </summary>
        public void PlaySFX2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var go  = new GameObject("TempSFX2D_" + clip.name);
            var src = go.AddComponent<AudioSource>();
            src.clip                  = clip;
            src.volume                = volume;
            src.pitch                 = pitch;
            src.spatialBlend          = 0f;        // 2D
            src.outputAudioMixerGroup = sfxGroup;
            src.Play();
            Destroy(go, clip.length / Mathf.Abs(pitch) + 0.1f);
        }

        // ─── Ghost 3D AudioSource (attach ke GameObject ghost) ───────────────

        /// <summary>
        /// Setup AudioSource di GameObject ghost untuk spatial audio.
        /// Panggil ini sekali di GhostAI.Start().
        /// </summary>
        public AudioSource CreateGhostAudioSource(GameObject ghostGO,
                                                   float minDist = 1f, float maxDist = 30f)
        {
            var src = ghostGO.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = sfxGroup;
            src.spatialBlend          = 1f;
            src.rolloffMode           = AudioRolloffMode.Logarithmic;
            src.minDistance           = minDist;
            src.maxDistance           = maxDist;
            src.playOnAwake           = false;
            return src;
        }

        // ═════════════════════════════════════════════════════════════════════
        // MUSIC
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play music dengan fade. Jika ada music yang playing, crossfade ke track baru.
        /// </summary>
        public void PlayMusic(AudioClip clip, bool loop = true, float fadeDuration = 1.5f)
        {
            if (clip == null) return;
            if (_musicFade != null) StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(CrossfadeMusic(clip, loop, fadeDuration));
        }

        public void StopMusic(float fadeDuration = 1.5f)
        {
            if (_musicFade != null) StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(FadeOutSource(musicSource, fadeDuration));
        }

        IEnumerator CrossfadeMusic(AudioClip clip, bool loop, float duration)
        {
            // Fade out current
            float startVol = musicSource.volume;
            if (musicSource.isPlaying)
            {
                float t = 0f;
                while (t < duration * 0.5f)
                {
                    t += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(startVol, 0f, t / (duration * 0.5f));
                    yield return null;
                }
                musicSource.Stop();
            }

            // Fade in new
            musicSource.clip   = clip;
            musicSource.loop   = loop;
            musicSource.volume = 0f;
            musicSource.Play();

            float t2 = 0f;
            while (t2 < duration * 0.5f)
            {
                t2 += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(0f, 1f, t2 / (duration * 0.5f));
                yield return null;
            }
            musicSource.volume = 1f;
        }

        // ═════════════════════════════════════════════════════════════════════
        // AMBIENCE
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play ambient loop dengan crossfade. Untuk: ambient_forest, wind_loop, haunt_ambient.
        /// </summary>
        public void PlayAmbience(AudioClip clip, float fadeDuration = 2f)
        {
            if (clip == null) return;
            if (_ambienceFade != null) StopCoroutine(_ambienceFade);
            _ambienceFade = StartCoroutine(CrossfadeAmbience(clip, fadeDuration));
        }

        public void StopAmbience(float fadeDuration = 2f)
        {
            if (_ambienceFade != null) StopCoroutine(_ambienceFade);
            _ambienceFade = StartCoroutine(FadeOutSource(ambienceSource, fadeDuration));
        }

        IEnumerator CrossfadeAmbience(AudioClip newClip, float duration)
        {
            // Mulai source2 dengan clip baru
            ambienceSource2.clip   = newClip;
            ambienceSource2.loop   = true;
            ambienceSource2.volume = 0f;
            ambienceSource2.Play();

            float t = 0f;
            float oldVol = ambienceSource.volume;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float ratio = t / duration;
                ambienceSource.volume  = Mathf.Lerp(oldVol, 0f, ratio);
                ambienceSource2.volume = Mathf.Lerp(0f, 1f, ratio);
                yield return null;
            }

            ambienceSource.Stop();
            // Swap references
            (ambienceSource, ambienceSource2) = (ambienceSource2, ambienceSource);
            ambienceSource2.volume = 0f;
        }

        // ═════════════════════════════════════════════════════════════════════
        // SANITY AUDIO (distortion drone, whispers)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play / stop sanity audio (loop, 2D, pitch bisa dimodulasi dari luar).
        /// </summary>
        public void PlaySanityAudio(AudioClip clip, float volume = 0.6f)
        {
            if (clip == null) return;
            if (sanitySource.isPlaying && sanitySource.clip == clip) return;
            sanitySource.clip   = clip;
            sanitySource.loop   = true;
            sanitySource.volume = volume;
            sanitySource.Play();
        }

        public void StopSanityAudio(float fadeDuration = 1f)
        {
            if (_ambienceFade != null) StopCoroutine(_ambienceFade);
            StartCoroutine(FadeOutSource(sanitySource, fadeDuration));
        }

        /// <summary>
        /// Modulasi pitch sanity audio secara realtime dari SanitySystem.
        /// </summary>
        public void SetSanityPitch(float pitch) => sanitySource.pitch = pitch;

        // ═════════════════════════════════════════════════════════════════════
        // VOLUME CONTROL
        // ═════════════════════════════════════════════════════════════════════

        public void SetMasterVolume(float volume)   => SetVolume(masterParam, volume);
        public void SetSFXVolume(float volume)      => SetVolume(sfxParam, volume);
        public void SetMusicVolume(float volume)    => SetVolume(musicParam, volume);
        public void SetAmbienceVolume(float volume) => SetVolume(ambienceParam, volume);

        void SetVolume(string param, float volume)
        {
            if (audioMixer == null) return;
            float db = volume > 0.001f ? 20f * Mathf.Log10(volume) : -80f;
            audioMixer.SetFloat(param, db);
        }

        // ═════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator FadeOutSource(AudioSource source, float duration)
        {
            float startVol = source.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            source.Stop();
            source.volume = startVol;
        }

    }
}