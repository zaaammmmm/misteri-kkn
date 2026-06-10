using UnityEngine;
using System.Collections;
using KKN.Game.Core;
using KKN.Game.Enemy;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Advanced sanity system that alters reality as sanity decreases.
    /// Features hallucinations, fake ghosts, objective glitches, and breakdown sequence.
    /// </summary>
    public class SanitySystem : MonoBehaviour
    {
        public static SanitySystem Instance { get; private set; }

        public enum MentalState { Calm, Uneasy, Panic, Insane }

        [Header("Values")]
        [SerializeField] private float maxSanity = 100f;
        [SerializeField] private float currentSanity = 100f;

        [Header("Drain / Recover Rates")]
        [SerializeField] private float darkDrain = 5f;
        [SerializeField] private float lightRecover = 8f;
        [SerializeField] private float ghostSightDrain = 12f;
        [SerializeField] private float ghostNearDrain = 18f;

        [Header("Hallucination Settings")]
        [SerializeField] private float hallucinationChancePanic = 0.001f;
        [SerializeField] private float hallucinationChanceInsane = 0.005f;
        [SerializeField] private GameObject fakeGhostPrefab;
        [SerializeField] private AudioClip[] fakeSounds;

        [Header("Breakdown Sequence")]
        [SerializeField] private float breakdownDuration = 8f;
        [SerializeField] private AnimationCurve breakdownShakeCurve;

        [Header("References")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip heartbeatClip;
        [SerializeField] private AudioClip whisperClip;

        public MentalState currentState { get; private set; }
        public bool isInBreakdown { get; private set; }

        private Transform player;
        private Camera mainCam;
        private GhostAI[] cachedGhosts;
        private float whisperTimer;
        private bool isDead = false;
        private Coroutine breakdownRoutine;

        // Events
        public event System.Action<MentalState> OnMentalStateChanged;
        public event System.Action OnBreakdownStarted;
        public event System.Action OnBreakdownEnded;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            AutoAssign();
        }

        void Start()
        {
            currentSanity = maxSanity;

            var playerObj = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);
            if (playerObj != null)
                player = playerObj.transform;

            mainCam = Camera.main;
            cachedGhosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
        }

        void Update()
        {
            if (isDead || isInBreakdown) return;

            if (player == null) TryFindPlayer();
            if (mainCam == null) mainCam = Camera.main;

            HandleEnvironment();
            HandleGhostPressure();
            ClampSanity();
            UpdateMentalState();
            HandleEffects();
            HandleHallucinations();
            CheckDeath();
        }

        void AutoAssign()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        void TryFindPlayer()
        {
            var obj = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);
            if (obj != null) player = obj.transform;
        }

        // Core Logic

        void HandleEnvironment()
        {
            if (FlashlightSystem.Instance != null && FlashlightSystem.Instance.IsOn())
                Recover(lightRecover);
            else
                Drain(darkDrain);
        }

        void HandleGhostPressure()
        {
            if (player == null || cachedGhosts == null) return;

            foreach (var ghost in cachedGhosts)
            {
                if (ghost == null) continue;

                float dist = Vector3.Distance(player.position, ghost.transform.position);

                if (dist < 8f)
                    Drain(ghostNearDrain);
                else if (dist < 14f && CanSeeGhost(ghost.transform))
                    Drain(ghostSightDrain);
            }
        }

        bool CanSeeGhost(Transform ghost)
        {
            if (mainCam == null || ghost == null) return false;

            Vector3 dir = (ghost.position - mainCam.transform.position).normalized;

            if (Physics.Raycast(mainCam.transform.position, dir, out RaycastHit hit, 20f))
            {
                return hit.transform == ghost;
            }

            return false;
        }

        // States

        void UpdateMentalState()
        {
            MentalState previous = currentState;

            if (currentSanity >= maxSanity * GameConstants.SANITY_CALM)
                currentState = MentalState.Calm;
            else if (currentSanity >= maxSanity * GameConstants.SANITY_UNEASY)
                currentState = MentalState.Uneasy;
            else if (currentSanity >= maxSanity * GameConstants.SANITY_PANIC)
                currentState = MentalState.Panic;
            else
                currentState = MentalState.Insane;

            if (previous != currentState)
                OnMentalStateChanged?.Invoke(currentState);
        }

        // Effects

        void HandleEffects()
        {
            switch (currentState)
            {
                case MentalState.Calm:
                    StopHeartbeat();
                    break;

                case MentalState.Uneasy:
                    PlayHeartbeat(0.3f);
                    break;

                case MentalState.Panic:
                    PlayHeartbeat(0.6f);
                    WhisperRandom();
                    break;

                case MentalState.Insane:
                    PlayHeartbeat(1f);
                    WhisperRandom();
                    break;
            }
        }

        void PlayHeartbeat(float volume)
        {
            if (audioSource == null || heartbeatClip == null) return;

            if (audioSource.clip != heartbeatClip)
            {
                audioSource.clip = heartbeatClip;
                audioSource.loop = true;
                audioSource.Play();
            }

            audioSource.volume = volume;
        }

        void StopHeartbeat()
        {
            if (audioSource == null) return;
            if (audioSource.clip == heartbeatClip && audioSource.isPlaying)
                audioSource.Stop();
        }

        void WhisperRandom()
        {
            if (audioSource == null || whisperClip == null) return;

            whisperTimer -= Time.deltaTime;
            if (whisperTimer <= 0f)
            {
                audioSource.PlayOneShot(whisperClip, 0.6f);
                whisperTimer = Random.Range(4f, 8f);
            }
        }

        // Hallucinations

        void HandleHallucinations()
        {
            float chance = currentState switch
            {
                MentalState.Panic  => hallucinationChancePanic,
                MentalState.Insane => hallucinationChanceInsane,
                _ => 0f
            };

            if (Random.value < chance)
                TriggerHallucination();
        }

        void TriggerHallucination()
        {
            int type = Random.Range(0, 4);

            switch (type)
            {
                case 0:
                    SpawnFakeGhost();
                    break;
                case 1:
                    PlayFakeSound();
                    break;
                case 2:
                    GlitchObjective();
                    break;
                case 3:
                    break;
            }
        }

        void SpawnFakeGhost()
        {
            if (fakeGhostPrefab == null || player == null) return;

            Vector3 spawnPos = player.position + player.forward * 5f + Random.insideUnitSphere * 3f;
            spawnPos.y = player.position.y;

            GameObject fakeGhost = Instantiate(fakeGhostPrefab, spawnPos, Quaternion.LookRotation(player.position - spawnPos));
            Destroy(fakeGhost, 2f);

#if UNITY_EDITOR
            Debug.Log("[SanitySystem] Fake ghost hallucination triggered");
#endif
        }

        void PlayFakeSound()
        {
            if (fakeSounds == null || fakeSounds.Length == 0 || audioSource == null) return;

            AudioClip clip = fakeSounds[Random.Range(0, fakeSounds.Length)];
            audioSource.PlayOneShot(clip, 0.5f);
        }

        void GlitchObjective()
        {
            ObjectiveManager.Instance?.GlitchObjectiveText();
        }

        // Breakdown Sequence

        void CheckDeath()
        {
            if (isDead || currentSanity > 0f) return;
            StartBreakdownSequence();
        }

        void StartBreakdownSequence()
        {
            isInBreakdown = true;
            OnBreakdownStarted?.Invoke();

            if (breakdownRoutine != null)
                StopCoroutine(breakdownRoutine);

            breakdownRoutine = StartCoroutine(BreakdownRoutine());
        }

        IEnumerator BreakdownRoutine()
        {
            StopHeartbeat();

            float timer = 0f;
            while (timer < breakdownDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / breakdownDuration;
                float shakeIntensity = breakdownShakeCurve?.Evaluate(progress) ?? (1f - progress);
                yield return null;
            }

            if (Random.value < 0.5f)
            {
                currentSanity = maxSanity * 0.2f;
                isInBreakdown = false;
                OnBreakdownEnded?.Invoke();
            }
            else
            {
                isDead = true;
                UI.JumpscareManager.Instance?.TriggerSanityDeath();
            }
        }

        // Public Helpers

        public void Drain(float amount)
        {
            currentSanity -= amount * Time.deltaTime;
        }

        public void Recover(float amount)
        {
            currentSanity += amount * Time.deltaTime;
        }

        void ClampSanity()
        {
            currentSanity = Mathf.Clamp(currentSanity, 0f, maxSanity);
        }

        public float Percent() => currentSanity / maxSanity;

        public void ResetSanity()
        {
            isDead = false;
            isInBreakdown = false;
            currentSanity = maxSanity;

            if (breakdownRoutine != null)
            {
                StopCoroutine(breakdownRoutine);
                breakdownRoutine = null;
            }
        }

        public void RefreshGhostCache()
        {
            cachedGhosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
        }
    }
}

