using UnityEngine;
using System.IO;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Extension of your existing SaveSystem.
    /// Adds GetSavedScene() and SaveCurrentScene() for the Continue flow.
    ///
    /// MERGE INSTRUCTIONS
    /// ──────────────────
    /// This file is an EXTENSION — do NOT replace your existing SaveSystem.cs.
    /// Two options:
    ///   A) Copy the three new methods below into your existing SaveSystem class.
    ///   B) Keep this file as a companion partial class (rename both files to use
    ///      `public partial class SaveSystem`).
    ///
    /// If you use option B, add the `partial` keyword to your original SaveSystem.
    /// </summary>
    public partial class SaveSystem
    {
        // ─── Scene Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the scene name stored in the save file, or null if no save exists.
        /// Used by MainMenuController to decide where "Continue" navigates.
        /// </summary>
        public string GetSavedScene()
        {
            if (!HasSaveFile()) return null;

            try
            {
                string json = File.ReadAllText(SavePath);
                GameData data = JsonUtility.FromJson<GameData>(json);
                return string.IsNullOrEmpty(data.currentScene) ? null : data.currentScene;
            }
            catch
            {
                Debug.LogWarning("[SaveSystem] Failed to read scene from save file.");
                return null;
            }
        }

        /// <summary>
        /// Writes the current active Unity scene name into an existing save file.
        /// Call this at checkpoints in Gameplay even before a full SaveGame().
        /// </summary>
        public void SaveCurrentScene()
        {
            // If no save exists yet, create a blank one first
            if (!HasSaveFile()) SaveGame();

            try
            {
                string json     = File.ReadAllText(SavePath);
                GameData data   = JsonUtility.FromJson<GameData>(json);
                data.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch
            {
                Debug.LogWarning("[SaveSystem] Failed to update currentScene in save file.");
            }
        }

        /// <summary>
        /// Maps the saved scene name to a KKN_SceneManager.GameScene enum value.
        /// Returns GameScene.Gameplay as fallback for any unknown scene name.
        /// </summary>
        public KKN_SceneManager.GameScene GetSavedGameScene()
        {
            string saved = GetSavedScene();
            if (saved == null) return KKN_SceneManager.GameScene.Gameplay;

            return saved switch
            {
                KKN_SceneManager.SceneNames.Intro    => KKN_SceneManager.GameScene.Intro,
                KKN_SceneManager.SceneNames.MainMenu => KKN_SceneManager.GameScene.MainMenu,
                KKN_SceneManager.SceneNames.Chapter1 => KKN_SceneManager.GameScene.Chapter1,
                KKN_SceneManager.SceneNames.Gameplay => KKN_SceneManager.GameScene.Gameplay,
                KKN_SceneManager.SceneNames.Ending   => KKN_SceneManager.GameScene.Ending,
                _                                    => KKN_SceneManager.GameScene.Gameplay
            };
        }
    }
}
