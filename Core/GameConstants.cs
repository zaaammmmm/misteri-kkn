using UnityEngine;

namespace KKN.Game.Core
{
    /// <summary>
    /// Centralized constants for the entire game.
    /// Eliminates magic numbers and hardcoded strings.
    /// </summary>
    public static class GameConstants
    {
        // ── Input ─────────────────────────────────────────
        public const string AXIS_HORIZONTAL = "Horizontal";
        public const string AXIS_VERTICAL   = "Vertical";
        public const string AXIS_MOUSE_X    = "Mouse X";
        public const string AXIS_MOUSE_Y    = "Mouse Y";

        public const KeyCode KEY_INTERACT   = KeyCode.E;
        public const KeyCode KEY_FLASHLIGHT = KeyCode.F;
        public const KeyCode KEY_RELOAD     = KeyCode.R;
        public const KeyCode KEY_CROUCH     = KeyCode.LeftControl;
        public const KeyCode KEY_RUN        = KeyCode.LeftShift;
        public const KeyCode KEY_TAB        = KeyCode.Tab;
        public const KeyCode KEY_ESCAPE     = KeyCode.Escape;

        // ── Tags ──────────────────────────────────────────
        public const string TAG_PLAYER = "Player";

        // ── Layers ────────────────────────────────────────
        public const string LAYER_INTERACT = "Interactable";

        // ── Sanity Thresholds ─────────────────────────────
        public const float SANITY_CALM   = 0.70f;
        public const float SANITY_UNEASY = 0.40f;
        public const float SANITY_PANIC  = 0.15f;
        public const float SANITY_SHAKE  = 0.35f;

        // ── Ghost AI ──────────────────────────────────────
        public const float GHOST_HEAR_RADIUS      = 12f;
        public const float GHOST_SIGHT_RANGE      = 15f;
        public const float GHOST_SIGHT_ANGLE      = 70f;
        public const float GHOST_ATTACK_RANGE     = 1.8f;
        public const float GHOST_LIGHT_FEAR_RADIUS= 7f;
        public const float GHOST_SEARCH_TIME      = 8f;
        public const float GHOST_COOLDOWN_TIME    = 5f;

        // ── Noise ─────────────────────────────────────────
        public const float NOISE_WALK   = 4f;
        public const float NOISE_RUN    = 9f;
        public const float NOISE_CROUCH = 1.5f;

        // ── Flashlight ────────────────────────────────────
        public const float FLASHLIGHT_DRAIN      = 7f;
        public const float FLASHLIGHT_CRITICAL   = 20f;
        public const float FLASHLIGHT_RELOAD_TIME= 2.2f;
        public const float FLASHLIGHT_RELOAD_NOISE= 14f;

        // ── Stamina ───────────────────────────────────────
        public const float STAMINA_MAX       = 100f;
        public const float STAMINA_DRAIN     = 22f;
        public const float STAMINA_RECOVER   = 18f;
        public const float STAMINA_RUN_MIN   = 5f;

        // ── Movement ──────────────────────────────────────
        public const float WALK_SPEED    = 3.5f;
        public const float RUN_SPEED     = 6f;
        public const float CROUCH_SPEED  = 2f;
        public const float GRAVITY       = -9.81f;
        public const float NORMAL_HEIGHT = 2f;
        public const float CROUCH_HEIGHT = 1.2f;

        // ── Interaction ───────────────────────────────────
        public const float INTERACT_DISTANCE = 3f;

        // ── UI ────────────────────────────────────────────
        public const float UI_LERP_SPEED     = 8f;
        public const float UI_FADE_SPEED     = 2f;
        public const float OBJECTIVE_SHOW_TIME = 3f;

        // ── Item IDs ──────────────────────────────────────
        public const string ITEM_INTRO_KEY      = "IntroKey";
        public const string ITEM_MAIN_KEY       = "MainKey";
        public const string ITEM_EXIT_KEY       = "ExitKey";
        public const string ITEM_GENERATOR_KEY  = "GeneratorKey";
        public const string ITEM_GASOLINE       = "Gasoline";
        public const string ITEM_OIL            = "Oil";
    }
}

