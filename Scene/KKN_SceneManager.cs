using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Core singleton scene manager for Misteri KKN.
    /// Handles async scene loading with fade transitions.
    /// Persists across all scenes via DontDestroyOnLoad.
    /// </summary>
    public class KKN_SceneManager : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────
        public static KKN_SceneManager Instance { get; private set; }

        // ─── Scene Name Constants ─────────────────────────────────────────────
        public static class SceneNames
        {
            public const string Intro    = "Intro";
            public const string MainMenu = "MainMenu";
            public const string Chapter1 = "Chapter1";
            public const string Gameplay = "Gameplay";
            public const string Ending   = "Ending";
        }

        // ─── State ────────────────────────────────────────────────────────────
        public enum GameScene { Intro, MainMenu, Chapter1, Gameplay, Ending }

        public GameScene CurrentScene  { get; private set; } = GameScene.Intro;
        public GameScene PreviousScene { get; private set; } = GameScene.Intro;
        public bool      IsTransitioning { get; private set; } = false;

        // ─── Events ───────────────────────────────────────────────────────────
        public static event Action<GameScene> OnSceneLoadStarted;
        public static event Action<GameScene> OnSceneLoadCompleted;

        // ─── Config ───────────────────────────────────────────────────────────
        [Header("Transition Settings")]
        [SerializeField] private float defaultFadeDuration = 0.8f;

        // ─── References ───────────────────────────────────────────────────────
        private KKN_FadeController _fade;

        // ─────────────────────────────────────────────────────────────────────
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Determine initial scene from Unity's active scene
            SyncSceneState(SceneManager.GetActiveScene().name);

            SceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Load a scene with default fade transition.</summary>
        public void LoadScene(GameScene target)
        {
            LoadSceneWithFade(target, defaultFadeDuration);
        }

        /// <summary>Load a scene with a custom fade duration.</summary>
        public void LoadSceneWithFade(GameScene target, float fadeDuration)
        {
            if (IsTransitioning) return;
            StartCoroutine(TransitionRoutine(target, fadeDuration));
        }

        public void LoadSceneWithLoading(string targetScene)
        {
            // LoadingData.TargetScene = targetScene; // Set target scene for loading screen
            StartCoroutine(
                LoadSceneRoutine(targetScene)
            );
        }

        private IEnumerator LoadSceneRoutine(string targetScene)
        {
            Debug.Log("TARGET SCENE PARAM = " + targetScene);

            if (KKN_FadeController.Instance != null)
            {
                yield return StartCoroutine(
                    KKN_FadeController.Instance.FadeOut(defaultFadeDuration)
                );
            }

            LoadingData.TargetScene = targetScene;

            Debug.Log("TARGET AFTER SET = " + LoadingData.TargetScene);

            SceneManager.LoadScene("Loading");

            yield return null;
            
            if (KKN_FadeController.Instance != null)
            {
                yield return StartCoroutine(
                    KKN_FadeController.Instance.FadeOut(defaultFadeDuration)
                );
            }

            LoadingData.TargetScene = targetScene;

            SceneManager.LoadScene("Loading");
        }

        /// <summary>Reload the current scene.</summary>
        public void ReloadCurrentScene()
        {
            LoadScene(CurrentScene);
        }

        /// <summary>Navigate back to the previous scene.</summary>
        public void GoBack()
        {
            LoadScene(PreviousScene);
        }

        // ─── Internal ─────────────────────────────────────────────────────────

        private IEnumerator TransitionRoutine(GameScene target, float fadeDuration)
        {
            IsTransitioning = true;
            OnSceneLoadStarted?.Invoke(target);

            // Acquire fade controller (may be in current scene)
            _fade = FindFirstObjectByType<KKN_FadeController>();

            // Fade out
            if (_fade != null)
                yield return StartCoroutine(_fade.FadeOut(fadeDuration));
            else
                yield return new WaitForSecondsRealtime(fadeDuration);

            // Begin async load
            string sceneName = GameSceneToName(target);
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            // Wait until scene is ready (progress reaches 0.9)
            while (op.progress < 0.9f)
                yield return null;

            // Activate scene
            op.allowSceneActivation = true;

            // Wait one frame for the new scene to initialise
            yield return null;

            // Update state
            PreviousScene = CurrentScene;
            CurrentScene  = target;

            // Acquire fade controller from new scene
            _fade = FindFirstObjectByType<KKN_FadeController>();

            // Fade in
            if (_fade != null)
                yield return StartCoroutine(_fade.FadeIn(fadeDuration));

            IsTransitioning = false;
            OnSceneLoadCompleted?.Invoke(target);
        }

        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SyncSceneState(scene.name);
        }

        private void SyncSceneState(string sceneName)
        {
            switch (sceneName)
            {
                case SceneNames.Intro:    CurrentScene = GameScene.Intro;    break;
                case SceneNames.MainMenu: CurrentScene = GameScene.MainMenu; break;
                case SceneNames.Chapter1: CurrentScene = GameScene.Chapter1; break;
                case SceneNames.Gameplay: CurrentScene = GameScene.Gameplay; break;
                case SceneNames.Ending:   CurrentScene = GameScene.Ending;   break;
            }
        }

        private static string GameSceneToName(GameScene scene)
        {
            return scene switch
            {
                GameScene.Intro    => SceneNames.Intro,
                GameScene.MainMenu => SceneNames.MainMenu,
                GameScene.Chapter1 => SceneNames.Chapter1,
                GameScene.Gameplay => SceneNames.Gameplay,
                GameScene.Ending   => SceneNames.Ending,
                _                  => SceneNames.MainMenu
            };
        }

        // ─── Convenience shortcuts ────────────────────────────────────────────
        public void GoToMainMenu()  => LoadScene(GameScene.MainMenu);
        public void GoToChapter1()  => LoadScene(GameScene.Chapter1);
        public void GoToGameplay()  => LoadScene(GameScene.Gameplay);
        public void GoToEnding()    => LoadScene(GameScene.Ending);
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
