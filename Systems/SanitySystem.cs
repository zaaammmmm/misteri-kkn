using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KKN.Game.Core;
using KKN.Game.Enemy;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Sanity system profesional untuk KKN: Desa Terkutuk.
    ///
    /// Perbaikan dari versi lama:
    ///   1. Audio dipisah — heartbeatSource (loop) vs sfxSource (one-shot) vs AudioManager
    ///   2. Hallucination pakai timer + cooldown, bukan raw Random per frame
    ///   3. isDead / isInBreakdown guard diperbaiki — tidak bisa race condition
    ///   4. Drain-rate berdasarkan GhostAI.State, bukan hanya jarak
    ///   5. CanSeeGhost memperhitungkan FOV kamera dan di-cache per frame
    ///   6. Pitch modulation heartbeat lewat AudioManager.SetSanityPitch()
    ///   7. Post-breakdown cooldown mencegah sanity langsung drain habis lagi
    ///   8. GlitchObjective punya cooldown sendiri
    ///   9. Sanity buff/debuff dari sistem luar bisa diapply via modifier stack
    ///       — dipisah menjadi _externalDrainPerSec (berkelanjutan) dan
    ///         _externalDrainFlat (satu kali/instan) [C5]
    ///  10. Event OnSanityChanged(float percent) untuk UI / shader lerp
    ///  11. [C1] CanSeeGhost() cache ganti ke Dictionary<GhostAI,(frame,bool)>
    ///          agar multi-ghost dicache dengan benar dalam satu frame
    ///  12. [C2] isInBreakdown di-set atomik di CheckDeath() sebelum coroutine
    ///  13. [C7] cachedGhosts dihapus — diganti GhostRegistry.Active yang
    ///          selalu up-to-date termasuk ghost spawn dinamis
    /// </summary>
    public class SanitySystem : MonoBehaviour
    {
        public static SanitySystem Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────
        //  STATE ENUM
        // ─────────────────────────────────────────────────────────────────
        public enum MentalState { Calm, Uneasy, Panic, Insane }

        // ─────────────────────────────────────────────────────────────────
        //  INSPECTOR
        // ─────────────────────────────────────────────────────────────────

        [Header("Sanity Values")]
        [SerializeField] private float maxSanity     = 100f;
        [SerializeField] private float currentSanity = 100f;

        [Header("Drain Rates (per detik)")]
        [SerializeField] private float darkDrain         = 5f;
        [SerializeField] private float lightRecover      = 8f;
        [SerializeField] private float ghostNearDrain    = 18f;  // <8u, state apapun
        [SerializeField] private float ghostSightDrain   = 12f;  // 8–14u, dalam FOV
        // Drain tambahan berbasis ghost state
        [SerializeField] private float ghostHauntBonus   = 8f;   // state Haunt / Lurk
        [SerializeField] private float ghostChaseBonus   = 6f;   // state Chase / Ambush
        [SerializeField] private float ghostObserveBonus = 4f;   // state Observe / Stalk

        [Header("State Thresholds (0–1)")]
        [Tooltip("Di atas ini = Calm")]
        [SerializeField] private float thresholdCalm   = 0.75f;
        [Tooltip("Di atas ini = Uneasy")]
        [SerializeField] private float thresholdUneasy = 0.40f;
        [Tooltip("Di atas ini = Panic")]
        [SerializeField] private float thresholdPanic  = 0.15f;
        // Di bawah thresholdPanic = Insane

        [Header("Hallucination")]
        [SerializeField] private float hallucinationIntervalPanic  = 18f;
        [SerializeField] private float hallucinationIntervalInsane = 7f;
        [SerializeField] private float hallucinationCooldown       = 4f;
        [SerializeField] private GameObject fakeGhostPrefab;
        [SerializeField] private AudioClip[] fakeSounds;
        [SerializeField] private float fakeGhostLifetime = 2.5f;

        [Header("Breakdown Sequence")]
        [SerializeField] private float breakdownDuration          = 8f;
        [SerializeField] private float postBreakdownRecovery      = 25f;
        [SerializeField] private float postBreakdownImmunityTime  = 15f;
        [SerializeField] private AnimationCurve breakdownShakeCurve;

        [Header("Audio — Heartbeat (loop)")]
        [Tooltip("AudioSource KHUSUS heartbeat loop. Jangan share dengan SFX.")]
        [SerializeField] private AudioSource heartbeatSource;
        [SerializeField] private AudioClip   heartbeatClip;
        [Tooltip("Pitch heartbeat di sanity 0 (lebih cepat = lebih panik)")]
        [SerializeField] private float heartbeatMaxPitch = 1.6f;

        [Header("Audio — Sanity SFX (one-shot)")]
        [Tooltip("AudioSource terpisah untuk whisper & fake sounds.")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip   whisperClip;
        [SerializeField] private float       whisperIntervalMin = 4f;
        [SerializeField] private float       whisperIntervalMax = 9f;
        [SerializeField] private AudioSource breathingSource;

        [SerializeField] private AudioClip calmBreath;
        [SerializeField] private AudioClip uneasyBreath;
        [SerializeField] private AudioClip panicBreath;
        [SerializeField] private AudioClip insaneBreath;
        [SerializeField] AudioSource tinnitusSource;
        [SerializeField] AudioClip tinnitusClip;

        [Header("References")]
        [SerializeField] private Camera overrideCamera;

        // ─────────────────────────────────────────────────────────────────
        //  RUNTIME STATE
        // ─────────────────────────────────────────────────────────────────

        public MentalState currentState { get; private set; }
        public bool isInBreakdown       { get; private set; }
        public bool IsDead              { get; private set; }
        public bool IsCriticalSanity()
        {
            return currentSanity <= 0f;
        }

        // C5 — Pisah modifier menjadi dua dengan semantik eksplisit:
        // _externalDrainPerSec : efek berkelanjutan, dikali Time.deltaTime tiap frame
        // _externalDrainFlat   : efek instan, dikonsumsi sekali lalu direset
        private float _externalDrainPerSec;
        private float _externalDrainFlat;

        // Timers
        private float hallucinationTimer;
        private float hallucinationCooldownTimer;
        private float whisperTimer;
        private float glitchCooldownTimer;
        private float postBreakdownImmunityTimer;
        // private ColorGrading _colorGrading;
        // private DepthOfField _dof;

        // C1 — Cache per-ghost menggunakan Dictionary, valid hanya 1 frame.
        // Menggantikan (_cachedCanSeeGhost, _lastSeeFrame, _lastSeenGhost) yang
        // hanya menyimpan satu ghost sehingga iterasi multi-ghost salah.
        private readonly Dictionary<GhostAI, (int frame, bool result)> _seeCache
            = new Dictionary<GhostAI, (int, bool)>();

        // References
        private Transform  player;
        private Camera     mainCam;
        private Coroutine  breakdownRoutine;

        // Konstanta
        private const float GHOST_NEAR_RADIUS  = 8f;
        private const float GHOST_SIGHT_RADIUS = 14f;
        private const float GHOST_FOV_ANGLE    = 65f;
        private const float GLITCH_COOLDOWN    = 6f;

        // ─────────────────────────────────────────────────────────────────
        //  EVENTS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Dipanggil tiap kali MentalState berubah.</summary>
        public event System.Action<MentalState> OnMentalStateChanged;

        /// <summary>Dipanggil tiap frame dengan nilai 0–1. Berguna untuk UI shader/lerp.</summary>
        public event System.Action<float> OnSanityChanged;

        public event System.Action OnBreakdownStarted;
        public event System.Action OnBreakdownEnded;

        // ─────────────────────────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            AutoAssignAudioSources();
        }

        void Start()
        {
            currentSanity = maxSanity;

            var playerObj = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);
            if (playerObj != null) player = playerObj.transform;

            mainCam = overrideCamera != null ? overrideCamera : Camera.main;

            // C7 — Tidak ada FindObjectsByType di sini.
            //      GhostRegistry.Active selalu up-to-date via GhostAI.OnEnable/OnDisable.

            ResetHallucinationTimer();
            if (tinnitusSource != null)
            {
                tinnitusSource.clip = tinnitusClip;
                tinnitusSource.loop = true;
                tinnitusSource.volume = 0f;
            }
        }

        void Update()
        {
            if (IsDead || isInBreakdown) return;

            if (player == null) TryFindPlayer();
            if (mainCam == null) mainCam = Camera.main;

            if (postBreakdownImmunityTimer > 0f)
            {
                postBreakdownImmunityTimer -= Time.deltaTime;
            }
            else
            {
                HandleEnvironmentDrain();
                HandleGhostPressure();
            }

            ClampSanity();
            UpdateMentalState();
            HandleAudioFeedback();
            HandleHallucinations();
            TickTimers();
            CheckDeath();

            OnSanityChanged?.Invoke(Percent());
        }

        void AutoAssignAudioSources()
        {
            if (heartbeatSource == null)
            {
                var go = new GameObject("SanityHeartbeatSource");
                go.transform.SetParent(transform);
                heartbeatSource              = go.AddComponent<AudioSource>();
                heartbeatSource.spatialBlend = 0f;
                heartbeatSource.loop         = true;
                heartbeatSource.playOnAwake  = false;
            }

            if (sfxSource == null)
            {
                var go = new GameObject("SanitySFXSource");
                go.transform.SetParent(transform);
                sfxSource              = go.AddComponent<AudioSource>();
                sfxSource.spatialBlend = 0f;
                sfxSource.loop         = false;
                sfxSource.playOnAwake  = false;
            }
        }

        void TryFindPlayer()
        {
            var obj = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);
            if (obj != null) player = obj.transform;
        }

        // ─────────────────────────────────────────────────────────────────
        //  DRAIN / RECOVER
        // ─────────────────────────────────────────────────────────────────

        void HandleEnvironmentDrain()
        {
            // C6 — Static helper property, null-safe
            bool flashOn = FlashlightSystem.IsFlashlightOn;

            if (flashOn)
                Recover(lightRecover);
            else
                Drain(darkDrain);

            // C5 — Modifier berkelanjutan (per detik), dikali Time.deltaTime via Drain()
            if (_externalDrainPerSec != 0f)
                Drain(_externalDrainPerSec);

            // C5 — Modifier instan (flat), dikonsumsi sekali langsung
            if (_externalDrainFlat != 0f)
            {
                DrainInstant(_externalDrainFlat);
                _externalDrainFlat = 0f;
            }
        }

        void HandleGhostPressure()
        {
            if (player == null) return;

            // C7 — GhostRegistry.Active menggantikan cachedGhosts.
            //      Otomatis menangani ghost spawn/despawn dinamis.
            foreach (var ghost in GhostRegistry.Active)
            {
                if (ghost == null) continue;

                float dist = Vector3.Distance(player.position, ghost.transform.position);

                if (dist < GHOST_NEAR_RADIUS)
                {
                    float bonus = GetGhostStateDrainBonus(ghost);
                    Drain(ghostNearDrain + bonus);
                }
                else if (dist < GHOST_SIGHT_RADIUS && CanSeeGhost(ghost))
                {
                    float bonus = GetGhostStateDrainBonus(ghost);
                    Drain(ghostSightDrain + bonus);
                }
            }
        }

        float GetGhostStateDrainBonus(GhostAI ghost)
        {
            return ghost.currentState switch
            {
                GhostAI.State.Haunt   => ghostHauntBonus,
                GhostAI.State.Lurk    => ghostHauntBonus,
                GhostAI.State.Chase   => ghostChaseBonus,
                GhostAI.State.Ambush  => ghostChaseBonus,
                GhostAI.State.Observe => ghostObserveBonus,
                GhostAI.State.Stalk   => ghostObserveBonus,
                _ => 0f
            };
        }

        // ─────────────────────────────────────────────────────────────────
        //  PERCEPTION — CanSeeGhost
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cek apakah player bisa melihat ghost spesifik.
        /// C1 — Di-cache per ghost per frame menggunakan Dictionary,
        ///      menggantikan cache tunggal (_lastSeenGhost) yang salah
        ///      saat ada lebih dari satu ghost di scene.
        /// </summary>
        bool CanSeeGhost(GhostAI ghost)
        {
            if (mainCam == null || ghost == null) return false;

            // C1 — Cek cache per ghost
            if (_seeCache.TryGetValue(ghost, out var cached) && cached.frame == Time.frameCount)
                return cached.result;

            Vector3 origin  = mainCam.transform.position;
            Vector3 toGhost = ghost.transform.position - origin;
            float   dist    = toGhost.magnitude;

            // Cek FOV kamera
            float angle = Vector3.Angle(mainCam.transform.forward, toGhost.normalized);
            if (angle > GHOST_FOV_ANGLE)
            {
                _seeCache[ghost] = (Time.frameCount, false);
                return false;
            }

            // Raycast — cek obstacle
            bool canSee;
            if (Physics.Raycast(origin, toGhost.normalized, out RaycastHit hit, dist))
            {
                canSee = hit.transform.IsChildOf(ghost.transform)
                      || hit.transform == ghost.transform;
            }
            else
            {
                canSee = true;
            }

            _seeCache[ghost] = (Time.frameCount, canSee);
            return canSee;
        }

        // ─────────────────────────────────────────────────────────────────
        //  MENTAL STATE
        // ─────────────────────────────────────────────────────────────────

        void UpdateMentalState()
        {
            MentalState previous = currentState;
            float pct = Percent();

            if      (pct >= thresholdCalm)   currentState = MentalState.Calm;
            else if (pct >= thresholdUneasy)  currentState = MentalState.Uneasy;
            else if (pct >= thresholdPanic)   currentState = MentalState.Panic;
            else                              currentState = MentalState.Insane;

            if (previous != currentState)
                OnMentalStateChanged?.Invoke(currentState);
        }

        // ─────────────────────────────────────────────────────────────────
        //  AUDIO FEEDBACK
        // ─────────────────────────────────────────────────────────────────

        void HandleAudioFeedback()
        {
            UpdateBreathing();
            UpdateTinnitus();

            switch (currentState)
            {
                case MentalState.Calm:
                    StopHeartbeat();
                    break;

                case MentalState.Uneasy:
                    PlayHeartbeat(0.25f);
                    break;

                case MentalState.Panic:
                    PlayHeartbeat(0.55f);
                    TryPlayWhisper();
                    break;

                case MentalState.Insane:
                    PlayHeartbeat(1f);
                    TryPlayWhisper();
                    break;
            }

            float sanityPitch = Mathf.Lerp(1f, heartbeatMaxPitch, 1f - Percent());
            if (heartbeatSource != null && heartbeatSource.isPlaying)
                heartbeatSource.pitch = sanityPitch;

            AudioManager.Instance?.SetSanityPitch(sanityPitch);
        }

        void UpdateBreathing()
        {
            if (breathingSource == null) return;

            switch (currentState)
            {
                case MentalState.Calm:
                    FadeBreath(0f, 1f);
                    break;

                case MentalState.Uneasy:
                    PlayBreath(uneasyBreath, 0.25f, 1.0f);
                    break;

                case MentalState.Panic:
                    PlayBreath(panicBreath, 0.6f, 1.2f);
                    break;

                case MentalState.Insane:
                    PlayBreath(insaneBreath, 1f, 1.4f);
                    break;
            }
        }

        void PlayBreath(AudioClip clip, float volume, float pitch)
        {
            if (breathingSource == null || clip == null) return;

            if (breathingSource.clip != clip)
            {
                breathingSource.clip = clip;
                breathingSource.loop = true;
                breathingSource.Play();
            }

            breathingSource.volume = Mathf.Lerp(breathingSource.volume, volume, Time.deltaTime * 2f);
            breathingSource.pitch  = Mathf.Lerp(breathingSource.pitch,  pitch,  Time.deltaTime * 2f);
        }

        void FadeBreath(float volume, float pitch)
        {
            if (breathingSource == null) return;

            breathingSource.volume = Mathf.Lerp(breathingSource.volume, volume, Time.deltaTime * 2f);
            breathingSource.pitch  = Mathf.Lerp(breathingSource.pitch,  pitch,  Time.deltaTime * 2f);
        }

        void PlayHeartbeat(float targetVolume)
        {
            if (heartbeatSource == null || heartbeatClip == null) return;

            if (!heartbeatSource.isPlaying || heartbeatSource.clip != heartbeatClip)
            {
                heartbeatSource.clip = heartbeatClip;
                heartbeatSource.loop = true;
                heartbeatSource.Play();
            }

            heartbeatSource.volume = Mathf.Lerp(heartbeatSource.volume, targetVolume, Time.deltaTime * 3f);
        }

        void StopHeartbeat()
        {
            if (heartbeatSource == null || !heartbeatSource.isPlaying) return;

            heartbeatSource.volume = Mathf.Lerp(heartbeatSource.volume, 0f, Time.deltaTime * 3f);
            if (heartbeatSource.volume < 0.01f)
            {
                heartbeatSource.Stop();
                heartbeatSource.volume = 0f;
            }
        }

        void TryPlayWhisper()
        {
            if (sfxSource == null || whisperClip == null) return;
            if (whisperTimer > 0f) return;

            sfxSource.PlayOneShot(whisperClip, 0.6f);
            whisperTimer = Random.Range(whisperIntervalMin, whisperIntervalMax);
        }

        // ─────────────────────────────────────────────────────────────────
        //  HALLUCINATIONS
        // ─────────────────────────────────────────────────────────────────

        void HandleHallucinations()
        {
            if (currentState != MentalState.Panic && currentState != MentalState.Insane) return;
            if (hallucinationCooldownTimer > 0f) return;

            hallucinationTimer -= Time.deltaTime;
            if (hallucinationTimer > 0f) return;

            TriggerHallucination();
            ResetHallucinationTimer();
            hallucinationCooldownTimer = hallucinationCooldown;
        }

        void TriggerHallucination()
        {
            float ghostWeight = currentState == MentalState.Insane ? 0.45f : 0.25f;
            float roll = Random.value;

            if (roll < ghostWeight)
                SpawnFakeGhost();
            else if (roll < ghostWeight + 0.35f)
                PlayFakeSound();
            else if (roll < ghostWeight + 0.55f)
                TryGlitchObjective();

#if UNITY_EDITOR
            Debug.Log($"[SanitySystem] Hallucination triggered: roll={roll:F2}, state={currentState}");
#endif
        }

        void SpawnFakeGhost()
        {
            if (fakeGhostPrefab == null || player == null) return;

            Vector3 dir = (
                -player.forward +
                Random.insideUnitSphere * 0.5f
            ).normalized;

            Vector3 spawnPos = player.position + dir * Random.Range(4f, 8f);
            spawnPos.y = player.position.y;

            var fakeGhost = Instantiate(fakeGhostPrefab, spawnPos,
                                         Quaternion.LookRotation(player.position - spawnPos));
            Destroy(fakeGhost, fakeGhostLifetime);
        }

        void PlayFakeSound()
        {
            if (fakeSounds == null || fakeSounds.Length == 0) return;

            if (AudioManager.Instance != null && player != null)
            {
                var clip    = fakeSounds[Random.Range(0, fakeSounds.Length)];
                Vector3 pos = player.position + Random.insideUnitSphere * 6f;
                pos.y       = player.position.y;
                AudioManager.Instance.PlaySFX(clip, pos, 0.5f);
            }
            else if (sfxSource != null && fakeSounds.Length > 0)
            {
                sfxSource.PlayOneShot(fakeSounds[Random.Range(0, fakeSounds.Length)], 0.5f);
            }
        }

        void TryGlitchObjective()
        {
            if (glitchCooldownTimer > 0f) return;

            ObjectiveManager.Instance?.GlitchObjectiveText();
            glitchCooldownTimer = GLITCH_COOLDOWN;
        }

        void ResetHallucinationTimer()
        {
            float interval = currentState == MentalState.Insane
                ? hallucinationIntervalInsane
                : hallucinationIntervalPanic;

            hallucinationTimer = Random.Range(interval * 0.8f, interval * 1.3f);
        }

        void UpdateTinnitus()
        {
            if (tinnitusSource == null || tinnitusClip == null)
                return;

            float insanity = 1f - Percent();

            // Mulai muncul saat sanity < 40%
            float targetVolume =
                Mathf.InverseLerp(0.6f, 1f, insanity);

            tinnitusSource.volume = Mathf.Lerp(
                tinnitusSource.volume,
                targetVolume,
                Time.deltaTime * 2f);

            // Aktif hanya saat memang diperlukan
            if (targetVolume > 0.01f)
            {
                if (!tinnitusSource.isPlaying)
                {
                    tinnitusSource.clip = tinnitusClip;
                    tinnitusSource.loop = true;
                    tinnitusSource.Play();
                }
            }
            else
            {
                if (tinnitusSource.isPlaying)
                    tinnitusSource.Stop();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  TIMERS
        // ─────────────────────────────────────────────────────────────────

        void TickTimers()
        {
            if (whisperTimer             > 0f) whisperTimer             -= Time.deltaTime;
            if (hallucinationCooldownTimer > 0f) hallucinationCooldownTimer -= Time.deltaTime;
            if (glitchCooldownTimer      > 0f) glitchCooldownTimer      -= Time.deltaTime;
        }

        // ─────────────────────────────────────────────────────────────────
        //  BREAKDOWN
        // ─────────────────────────────────────────────────────────────────

        void CheckDeath()
        {
            if (IsDead || isInBreakdown || currentSanity > 0f) return;

            // C2 — Set flag SEBELUM StartBreakdownSequence untuk menutup
            //      pintu re-entry secara atomik. Versi lama men-set flag
            //      di dalam StartBreakdownSequence, membuka window beberapa
            //      frame di mana CheckDeath bisa masuk lagi.
            isInBreakdown = true;
            StartBreakdownSequence();
        }

        void StartBreakdownSequence()
        {
            // C2 — isInBreakdown sudah true dari CheckDeath, tidak set lagi
            OnBreakdownStarted?.Invoke();

            if (breakdownRoutine != null)
                StopCoroutine(breakdownRoutine);

            breakdownRoutine = StartCoroutine(BreakdownRoutine());
        }

        IEnumerator BreakdownRoutine()
        {
            StopHeartbeat();
            if (heartbeatSource != null) heartbeatSource.Stop();

            float timer = 0f;
            while (timer < breakdownDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // C8 — Survival chance berbasis konteks gameplay
            if (Random.value < SurvivalChance())
            {
                currentSanity              = maxSanity * (postBreakdownRecovery / 100f);
                postBreakdownImmunityTimer = postBreakdownImmunityTime;
                isInBreakdown              = false;
                breakdownRoutine           = null;
                OnBreakdownEnded?.Invoke();
            }
            else
            {
                if (IsGhostThreatening())
                {
                    IsDead = true;

                    UI.JumpscareManager.Instance
                        ?.StartJumpscare(GetNearestGhost()?.transform);
                }
                else
                {
                    IsDead = true;

                    UI.JumpscareManager.Instance
                        ?.TriggerSanityDeath();
                }
            }
        }

        /// <summary>
        /// C8 — Survival chance saat breakdown bukan lagi flat 50%.
        /// Dipengaruhi progress objective dan kedekatan ghost.
        /// Clamp antara 10%–85% agar selalu ada ketegangan.
        /// </summary>
        float SurvivalChance()
        {
            float chance = 0.75f;

            if (player != null)
            {
                foreach (var ghost in GhostRegistry.Active)
                {
                    if (ghost == null)
                        continue;

                    float dist =
                        Vector3.Distance(player.position,
                                        ghost.transform.position);

                    if (dist < 8f)
                    {
                        chance -= 0.35f;
                    }

                    if (ghost.currentState == GhostAI.State.Chase ||
                        ghost.currentState == GhostAI.State.Attacking)
                    {
                        chance -= 0.45f;
                    }
                }
            }

            return Mathf.Clamp(chance, 0.1f, 0.85f);
        }

        bool IsGhostThreatening()
        {
            if (player == null)
                return false;

            foreach (var ghost in GhostRegistry.Active)
            {
                if (ghost == null)
                    continue;

                float dist =
                    Vector3.Distance(player.position,
                                    ghost.transform.position);

                if (dist < 8f)
                    return true;

                if (ghost.currentState == GhostAI.State.Chase ||
                    ghost.currentState == GhostAI.State.Attacking)
                    return true;
            }

            return false;
        }
        // ─────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────
        public void Drain(float amount)
        {
            if (IsDead || isInBreakdown) return;
            currentSanity -= amount * Time.deltaTime;
        }

        public void Recover(float amount)
        {
            if (IsDead) return;
            currentSanity += amount * Time.deltaTime;
        }

        /// <summary>
        /// Drain/recover instan (tidak dikali Time.deltaTime).
        /// Untuk item, event, jumpscare langsung.
        /// Positive = drain, Negative = recover.
        /// </summary>
        public void DrainInstant(float amount)
        {
            if (IsDead) return;
            currentSanity -= amount;
            ClampSanity();
        }

        /// <summary>
        /// C5 — Set drain berkelanjutan dari sistem luar (area kutukan, debuff, dll).
        /// Positive = extra drain per detik, Negative = extra recover per detik.
        /// Set ke 0 untuk menghapus efek.
        /// Menggantikan SetExternalDrainModifier() yang semantiknya ambigu.
        /// </summary>
        public void SetExternalDrainPerSec(float value) => _externalDrainPerSec = value;

        /// <summary>
        /// C5 — Tambah drain satu kali (flat, tidak dikali Time.deltaTime).
        /// Positive = drain, Negative = recover.
        /// Dikonsumsi di frame berikutnya lalu direset otomatis.
        /// Menggantikan pola DrainInstant() dari caller eksternal yang
        /// sebelumnya ambigu dengan modifier stack.
        /// </summary>
        public void AddExternalDrainFlat(float amount) => _externalDrainFlat += amount;

        public void ResetSanity()
        {
            IsDead                     = false;
            isInBreakdown              = false;
            currentSanity              = maxSanity;
            postBreakdownImmunityTimer = 0f;
            _externalDrainPerSec       = 0f;   // C5
            _externalDrainFlat         = 0f;   // C5
            _seeCache.Clear();                 // C1 — bersihkan cache saat reset

            if (breakdownRoutine != null)
            {
                StopCoroutine(breakdownRoutine);
                breakdownRoutine = null;
            }

            StopHeartbeat();
        }

        void ClampSanity()
        {
            currentSanity = Mathf.Clamp(currentSanity, 0f, maxSanity);
        }

        /// <summary>Sanity saat ini sebagai nilai 0–1.</summary>
        public float Percent() => currentSanity / maxSanity;

        /// <summary>Nilai mentah 0–maxSanity.</summary>
        public float Value() => currentSanity;

        GhostAI GetNearestGhost()
        {
            GhostAI nearest = null;
            float closest = float.MaxValue;

            foreach (var ghost in GhostRegistry.Active)
            {
                if (ghost == null)
                    continue;

                float dist =
                    Vector3.Distance(player.position,
                                    ghost.transform.position);

                if (dist < closest)
                {
                    closest = dist;
                    nearest = ghost;
                }
            }

            return nearest;
        }

        // ─────────────────────────────────────────────────────────────────
        //  GIZMOS
        // ─────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (player == null) return;

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(player.position, GHOST_NEAR_RADIUS);

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(player.position, GHOST_SIGHT_RADIUS);

            UnityEditor.Handles.Label(player.position + Vector3.up * 2.8f,
                $"Sanity: {currentSanity:F0}/{maxSanity:F0}  [{currentState}]\n" +
                $"Immunity: {postBreakdownImmunityTimer:F1}s  " +
                $"Hallucination: {hallucinationTimer:F1}s\n" +
                $"DrainPerSec: {_externalDrainPerSec:F1}  " +
                $"DrainFlat pending: {_externalDrainFlat:F1}\n" +
                $"Ghosts tracked: {GhostRegistry.Count}");
        }
#endif
    }
}
