using UnityEngine;
using System;
using System.Collections;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Manages the horror intensity curve over time.
    /// Cycles through: Calm → Suspicion → Threat → Chaos → Relief → repeat.
    /// Ensures player gets breathing room for fear to regenerate.
    /// </summary>
    public class IntensityManager : MonoBehaviour
    {
        public static IntensityManager Instance { get; private set; }

        public enum IntensityPhase
        {
            Calm,       // Safe, exploration, narrative
            Suspicion,  // Subtle hints something is wrong
            Threat,     // Ghost is actively hunting
            Chaos,      // Multiple threats, low resources
            Relief      // Temporary safety, catch breath
        }

        [Header("Phase Settings")]
        [SerializeField] private float calmDuration     = 45f;
        [SerializeField] private float suspicionDuration= 20f;
        [SerializeField] private float threatDuration   = 30f;
        [SerializeField] private float chaosDuration    = 15f;
        [SerializeField] private float reliefDuration   = 10f;

        [Header("Current State")]
        [SerializeField] private IntensityPhase currentPhase = IntensityPhase.Calm;

        public IntensityPhase CurrentPhase => currentPhase;

        public event Action<IntensityPhase> OnPhaseChanged;

        private float phaseTimer;
        private Coroutine phaseRoutine;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            StartPhaseCycle();
        }

        void OnDisable()
        {
            if (phaseRoutine != null)
                StopCoroutine(phaseRoutine);
        }

        // ── Phase Cycle ───────────────────────────────────

        void StartPhaseCycle()
        {
            phaseRoutine = StartCoroutine(PhaseCycleRoutine());
        }

        IEnumerator PhaseCycleRoutine()
        {
            while (true)
            {
                yield return RunPhase(IntensityPhase.Calm,      calmDuration);
                yield return RunPhase(IntensityPhase.Suspicion, suspicionDuration);
                yield return RunPhase(IntensityPhase.Threat,    threatDuration);
                yield return RunPhase(IntensityPhase.Chaos,     chaosDuration);
                yield return RunPhase(IntensityPhase.Relief,    reliefDuration);
            }
        }

        IEnumerator RunPhase(IntensityPhase phase, float duration)
        {
            SetPhase(phase);
            phaseTimer = duration;

            while (phaseTimer > 0f)
            {
                phaseTimer -= Time.deltaTime;
                yield return null;
            }
        }

        void SetPhase(IntensityPhase phase)
        {
            if (currentPhase == phase) return;
            currentPhase = phase;
            OnPhaseChanged?.Invoke(phase);

#if UNITY_EDITOR
            Debug.Log($"[IntensityManager] Phase changed to: {phase}");
#endif
        }

        // ── Public Control ────────────────────────────────

        public void ForcePhase(IntensityPhase phase)
        {
            if (phaseRoutine != null)
                StopCoroutine(phaseRoutine);
            SetPhase(phase);
        }

        public void ResumeCycle()
        {
            StartPhaseCycle();
        }

        public float PhaseProgress => 1f - (phaseTimer / GetPhaseDuration(currentPhase));

        float GetPhaseDuration(IntensityPhase phase)
        {
            return phase switch
            {
                IntensityPhase.Calm       => calmDuration,
                IntensityPhase.Suspicion  => suspicionDuration,
                IntensityPhase.Threat     => threatDuration,
                IntensityPhase.Chaos      => chaosDuration,
                IntensityPhase.Relief     => reliefDuration,
                _ => 10f
            };
        }

        public bool IsThreatActive => currentPhase == IntensityPhase.Threat || currentPhase == IntensityPhase.Chaos;
    }
}

