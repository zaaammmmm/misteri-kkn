using UnityEngine;
using System;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Mengelola objective text yang ditampilkan ke player.
    /// Dipanggil oleh interactable objects dan puzzle scripts.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        // Event agar UI bisa subscribe
        public event Action<string> OnObjectiveChanged;

        [Header("Current Objective")]
        [SerializeField] private string currentObjective = "";

        [Header("Objective Steps (opsional, untuk linear progression)")]
        [SerializeField] private string[] objectiveSteps;
        private int stepIndex = 0;

        // Kompatibilitas SaveSystem — expose step index
        public int currentStep => stepIndex;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (objectiveSteps != null && objectiveSteps.Length > 0)
                SetObjective(objectiveSteps[0]);
        }

        public void SetObjective(string text)
        {
            currentObjective = text;
            OnObjectiveChanged?.Invoke(text);
            Debug.Log($"[ObjectiveManager] Objective: {text}");
        }

        public void NextStep()
        {
            Debug.Log(
                $"NextStep | stepIndex={stepIndex} | length={(objectiveSteps == null ? 0 : objectiveSteps.Length)}"
            );
            
            if (objectiveSteps == null || objectiveSteps.Length == 0)
            {
                Debug.LogWarning("[ObjectiveManager] Objective Steps kosong.");
                return;
            }

            stepIndex = Mathf.Clamp(
                stepIndex + 1,
                0,
                objectiveSteps.Length - 1
            );

            SetObjective(objectiveSteps[stepIndex]);
        }

        public void ClearObjective() => SetObjective("");

        public string GetCurrentObjective() => currentObjective;

        // ── Kompatibilitas SaveSystem ──────────────────────
        /// <summary>
        /// Restore step index saat load save game.
        /// Dipanggil oleh SaveSystem.
        /// </summary>
        public void LoadStep(int step)
        {
            if (objectiveSteps == null || objectiveSteps.Length == 0) return;
            stepIndex = Mathf.Clamp(step, 0, objectiveSteps.Length - 1);
            SetObjective(objectiveSteps[stepIndex]);
        }

        // ── Kompatibilitas SanitySystem ────────────────────
        /// <summary>
        /// Efek sanity rendah: objective text berkedip/terdistorsi sementara.
        /// Dipanggil oleh SanitySystem saat sanity sangat rendah.
        /// </summary>
        public void GlitchObjectiveText(float duration = 2f)
        {
            if (gameObject.activeInHierarchy)
                StartCoroutine(GlitchRoutine(duration));
        }

        private System.Collections.IEnumerator GlitchRoutine(float duration)
        {
            string original = currentObjective;
            string[] glitchVariants =
            {
                "̷̢͔̈́͠?̷̘͑?̸͚̾?̴͖̚?̵͙̑?",
                "ERROR // TIDAK BISA DIBACA",
                ".. . . ??. . .",
                original.Length > 0 ? Scramble(original) : "???"
            };

            float elapsed = 0f;
            while (elapsed < duration)
            {
                string glitch = glitchVariants[UnityEngine.Random.Range(0, glitchVariants.Length)];
                OnObjectiveChanged?.Invoke(glitch);
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.08f, 0.2f));
                elapsed += 0.15f;
            }

            // Kembalikan teks asli
            OnObjectiveChanged?.Invoke(original);
        }

        private string Scramble(string input)
        {
            var chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (UnityEngine.Random.value > 0.5f) chars[i] = (char)UnityEngine.Random.Range(63, 90);
            return new string(chars);
        }
    }
}
