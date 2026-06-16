using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KKN.Game.Systems;
using KKN.Game.Core;
using KKN.Game.Player;

namespace KKN.Game.UI
{
    /// <summary>
    /// Settings menu with save/load, key rebinding placeholders, and volume controls.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject pausePanel;

        [Header("Volume Sliders")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider musicSlider;

        [Header("Sensitivity")]
        [SerializeField] private Slider mouseSlider;
        [SerializeField] private TMP_Text sensitivityValue;

        [Header("Other")]
        [SerializeField] private Toggle subtitleToggle;

        private PlayerLook playerLook;
        private bool isPaused = false;

        void Start()
        {
            playerLook = FindFirstObjectByType<PlayerLook>();

            if (masterSlider != null)
                masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(OnSFXChanged);
            if (musicSlider != null)
                musicSlider.onValueChanged.AddListener(OnMusicChanged);
            if (mouseSlider != null)
                mouseSlider.onValueChanged.AddListener(OnSensitivityChanged);

            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnGameStateChanged;
                
            LoadSettings();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        void OnGameStateChanged(GameManager.GameState newState)
        {
            switch (newState)
            {
                case GameManager.GameState.Paused:
                    isPaused = true;
                    pausePanel?.SetActive(true);
                    break;

                case GameManager.GameState.Playing:
                    isPaused = false;
                    pausePanel?.SetActive(false);
                    settingsPanel?.SetActive(false);
                    break;

                case GameManager.GameState.Inventory:
                    // Pastikan pause panel tersembunyi saat inventory dibuka
                    isPaused = false;
                    pausePanel?.SetActive(false);
                    settingsPanel?.SetActive(false);
                    break;
            }
        }

        // void Update()
        // {
        //     if (InputManager.Instance != null && InputManager.Instance.GetEscapeDown())
        //     {
        //         if (!isPaused && GameManager.Instance != null && GameManager.Instance.IsPlaying)
        //             PauseGame();
        //         else if (isPaused)
        //             ResumeGame();
        //     }
        // }

        public void PauseGame()
        {
            GameManager.Instance?.PauseGame();
            // GameManager.SetState(Paused) sudah handle UnlockCursor + timeScale = 0.
            // Eksplisit di sini sebagai fallback jika GameManager null.
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale   = 0f;
        }

        public void ResumeGame()
        {
            Debug.Log("BUTTON CLICKED");
            GameManager.Instance?.ResumeGame();
            pausePanel?.SetActive(false);
            settingsPanel?.SetActive(false);
            // GameManager.SetState(Playing) sudah handle LockCursor + timeScale = 1.
            // Eksplisit di sini sebagai fallback jika GameManager null.
            Cursor.visible   = false;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale   = 1f;
        }

        public void OpenSettings()
        {
            pausePanel?.SetActive(false);
            settingsPanel?.SetActive(true);
        }

        public void CloseSettings()
        {
            settingsPanel?.SetActive(false);
            pausePanel?.SetActive(true);
        }

        public void SaveGame()
        {
            SaveSystem.Instance?.SaveGame();
        }

        public void LoadGame()
        {
            SaveSystem.Instance?.LoadGame();
            ResumeGame();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnMasterChanged(float value)
        {
            AudioManager.Instance?.SetMasterVolume(value);
            PlayerPrefs.SetFloat("MasterVolume", value);
        }

        void OnSFXChanged(float value)
        {
            AudioManager.Instance?.SetSFXVolume(value);
            PlayerPrefs.SetFloat("SFXVolume", value);
        }

        void OnMusicChanged(float value)
        {
            AudioManager.Instance?.SetMusicVolume(value);
            PlayerPrefs.SetFloat("MusicVolume", value);
        }

        void OnSensitivityChanged(float value)
        {
            if (playerLook != null)
                playerLook.GetType().GetField("sensitivity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(playerLook, value * 100f + 50f);

            if (sensitivityValue != null)
                sensitivityValue.text = value.ToString("F1");

            PlayerPrefs.SetFloat("MouseSensitivity", value);
        }

        void LoadSettings()
        {
            if (masterSlider != null)
                masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            if (sfxSlider != null)
                sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
            if (musicSlider != null)
                musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            if (mouseSlider != null)
                mouseSlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
        }
    }
}

