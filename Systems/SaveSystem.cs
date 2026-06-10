using UnityEngine;
using System.IO;

namespace KKN.Game.Systems
{
    /// <summary>
    /// JSON-based save/load system for game progress.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        [System.Serializable]
        public class GameData
        {
            public int currentObjectiveStep;
            public float sanity;
            public float battery;
            public float stamina;
            public string[] inventoryItems;
            public string currentScene;
        }

        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

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

        public void SaveGame()
        {
            var data = new GameData
            {
                currentObjectiveStep = ObjectiveManager.Instance?.currentStep ?? 0,
                sanity = SanitySystem.Instance?.Percent() ?? 1f,
                battery = FlashlightSystem.Instance?.BatteryPercent() ?? 1f,
                stamina = FindFirstObjectByType<Player.PlayerMovement>()?.GetStaminaPercent() ?? 1f,
                currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);

#if UNITY_EDITOR
            Debug.Log($"[SaveSystem] Saved to: {SavePath}");
#endif
        }

        public void LoadGame()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("[SaveSystem] No save file found.");
                return;
            }

            string json = File.ReadAllText(SavePath);
            GameData data = JsonUtility.FromJson<GameData>(json);

            // Apply loaded data to systems
            if (ObjectiveManager.Instance != null)
            {
                // Need to advance to correct step
                while (ObjectiveManager.Instance.currentStep < data.currentObjectiveStep)
                    ObjectiveManager.Instance.NextStep();
            }

            if (SanitySystem.Instance != null)
            {
                SanitySystem.Instance.Recover(data.sanity * 100f);
            }

#if UNITY_EDITOR
            Debug.Log($"[SaveSystem] Loaded from: {SavePath}");
#endif
        }

        public bool HasSaveFile()
        {
            return File.Exists(SavePath);
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
    }
}

