using UnityEngine;
using UnityEngine.Audio;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Centralized audio manager with mixer groups and volume control.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer")]
        [SerializeField] private AudioMixer audioMixer;

        [Header("Mixer Groups")]
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        [Header("Volume Parameters")]
        [SerializeField] private string masterParam = "MasterVolume";
        [SerializeField] private string sfxParam = "SFXVolume";
        [SerializeField] private string musicParam = "MusicVolume";
        [SerializeField] private string ambienceParam = "AmbienceVolume";

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

        public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            GameObject tempGO = new GameObject("TempAudio");
            tempGO.transform.position = position;
            AudioSource source = tempGO.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.outputAudioMixerGroup = sfxGroup;
            source.Play();
            Destroy(tempGO, clip.length);
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            // Implementation depends on music player setup
        }

        public void SetMasterVolume(float volume)
        {
            SetVolume(masterParam, volume);
        }

        public void SetSFXVolume(float volume)
        {
            SetVolume(sfxParam, volume);
        }

        public void SetMusicVolume(float volume)
        {
            SetVolume(musicParam, volume);
        }

        public void SetAmbienceVolume(float volume)
        {
            SetVolume(ambienceParam, volume);
        }

        void SetVolume(string param, float volume)
        {
            if (audioMixer == null) return;
            float db = volume > 0.001f ? 20f * Mathf.Log10(volume) : -80f;
            audioMixer.SetFloat(param, db);
        }
    }
}

