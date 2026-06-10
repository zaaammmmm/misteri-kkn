using UnityEngine;
using System;

namespace KKN.Game.Core
{
    /// <summary>
    /// Central game state manager with horror intensity curve.
    /// Controls game flow: Menu → Playing → Paused → GameOver.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Menu, Playing, Paused, GameOver, Cutscene, Inventory }


        [Header("Current State")]
        [SerializeField] private GameState currentState = GameState.Playing;

        [Header("Events")]
        public GameEvent onGameStateChanged;
        public GameEvent onGamePaused;
        public GameEvent onGameResumed;
        public GameEvent onGameOver;

        public GameState CurrentState => currentState;

        public event Action<GameState> OnStateChanged;

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

        void Update()
        {
            if (InputManager.Instance == null || !InputManager.Instance.GetEscapeDown()) return;

            switch (currentState)
            {
                case GameState.Playing:
                    PauseGame();
                    break;
                case GameState.Paused:
                    ResumeGame();
                    break;
                // ESC saat Inventory → tutup inventory, kembali Playing
                // TabInventoryUI yang menangani via HandleInput(),
                // jadi GameManager tidak perlu duplikasi. Cukup biarkan kosong.
            }
        }

        // ── State Transitions ─────────────────────────────

        public void SetState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;

            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    InputManager.Instance?.LockCursor();
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    InputManager.Instance?.UnlockCursor();
                    onGamePaused?.Raise();
                    break;

                case GameState.GameOver:
                    Time.timeScale = 1f; // Keep time running for animations
                    InputManager.Instance?.UnlockCursor();
                    onGameOver?.Raise();
                    break;

                case GameState.Cutscene:
                    InputManager.Instance?.LockCursor();
                    break;

                case GameState.Inventory:
                    Time.timeScale = 0f;
                    InputManager.Instance?.UnlockCursor();
                    break;
            }

            onGameStateChanged?.Raise();
            OnStateChanged?.Invoke(newState);
        }

        public void PauseGame()  => SetState(GameState.Paused);
        public void ResumeGame() => SetState(GameState.Playing);
        public void GameOver()   => SetState(GameState.GameOver);
        public void StartCutscene() => SetState(GameState.Cutscene);
        public void EndCutscene()   => SetState(GameState.Playing);

        public bool IsPlaying => currentState == GameState.Playing;
        public bool IsPaused  => currentState == GameState.Paused;
        public bool IsGameOver=> currentState == GameState.GameOver;
    }
}

