using UnityEngine;
using KKN.Game.Core;
using KKN.Game.Enemy;

namespace KKN.Game.Player
{
    /// <summary>
    /// Emits noise that ghosts can hear based on player movement state.
    /// Only emits when player is actually moving.
    /// </summary>
    public class PlayerNoiseEmitter : MonoBehaviour
    {
        [Header("Noise Radius")]
        [SerializeField] private float walkNoise = 4f;
        [SerializeField] private float runNoise = 9f;
        [SerializeField] private float crouchNoise = 1.5f;

        [Header("Emit Interval")]
        [SerializeField] private float walkInterval = 0.6f;
        [SerializeField] private float runInterval = 0.4f;
        [SerializeField] private float crouchInterval = 0.9f;

        [Header("Settings")]
        [SerializeField] private float moveThreshold = 0.15f;

        private float timer;
        private CharacterController cc;
        private GhostAI[] cachedGhosts;

        /// <summary>
        /// Noise level saat ini dalam rentang 0–1, untuk dibaca PlayerHUD.
        /// 0 = diam, 0.3 = jalan, 0.7 = jongkok bergerak, 1.0 = berlari.
        /// </summary>
        public float CurrentNoiseLevel { get; private set; }

        void Start()
        {
            cc = GetComponent<CharacterController>();
            cachedGhosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;

            float speed = cc != null
                ? new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude
                : 0f;

            bool isMoving = speed > moveThreshold;

            if (!isMoving)
            {
                CurrentNoiseLevel = 0f;
                timer = walkInterval;
                return;
            }

            if (PlayerState.Instance != null && PlayerState.Instance.IsRunning)
            {
                CurrentNoiseLevel = 1.0f;
                Emit(runNoise);
                timer = runInterval;
            }
            else if (PlayerState.Instance != null && PlayerState.Instance.IsCrouching)
            {
                CurrentNoiseLevel = 0.3f;
                Emit(crouchNoise);
                timer = crouchInterval;
            }
            else
            {
                CurrentNoiseLevel = 0.6f;
                Emit(walkNoise);
                timer = walkInterval;
            }
        }

        void Emit(float radius)
        {
            if (cachedGhosts == null) return;

            foreach (var g in cachedGhosts)
            {
                if (g == null) continue;

                float dist = Vector3.Distance(transform.position, g.transform.position);
                if (dist <= radius)
                    g.HearSound(transform.position, radius);
            }
        }

        public void RefreshGhostCache()
        {
            cachedGhosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
        }
    }
}

