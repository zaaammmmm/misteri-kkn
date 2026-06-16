using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using KKN.Game.Core;
using KKN.Game.Data;
using KKN.Game.Systems;

namespace KKN.Game.Enemy
{
    // ══════════════════════════════════════════════════
    //  SUPPORTING DATA STRUCTURES  (TODO #10)
    // ══════════════════════════════════════════════════

    /// <summary>
    /// TODO #10 — Titik menarik di map yang diketahui ghost (jalan utama,
    /// gang sempit, objective, dll.). Assign via Inspector atau GhostMapKnowledge.
    /// </summary>
    public enum GhostZoneType { OpenArea, Alley, House, Objective, HauntArea }

    [System.Serializable]
    public class GhostInterestPoint
    {
        public Transform  point;
        public GhostZoneType zoneType;
        [Range(0f, 1f)] public float weight = 1f;   // preferensi ghost memilih titik ini
    }

    // ══════════════════════════════════════════════════
    //  MAIN CLASS
    // ══════════════════════════════════════════════════

    [RequireComponent(typeof(NavMeshAgent))]
    public class GhostAI : MonoBehaviour
    {
        // ────────────────────────────────────────────
        //  STATE ENUM  (TODO #8: tambah Observe / Haunt / Lurk)
        // ────────────────────────────────────────────
        public enum State
        {
            Patrol,
            Investigate,
            Chase,
            Search,
            Stalk,
            FakeRetreat,
            Ambush,
            Cooldown,
            Attacking,
            // TODO #8 — Mind Game states
            Observe,    // mengikuti dari jauh tanpa diketahui
            Haunt,      // berdiri diam / bergerak lambat untuk menakut-nakuti
            Lurk        // muncul-hilang di sekitar player
        }

        // ── Inspector ──────────────────────────────
        [Header("Data")]
        public EnemyData enemyData;

        [Header("References")]
        public Transform[] patrolPoints;

        [Tooltip("Model visual ghost")]
        [SerializeField] private Transform modelTransform;

        [Tooltip("Offset rotasi model visual")]
        [SerializeField] private Vector3 modelRotationOffset;

        [Tooltip("Layer penghalang penglihatan — jangan masukkan layer Player!")]
        public LayerMask obstacleMask = (1 << 0) | (1 << 3);

        public Transform eyePoint;
        public Animator  animator;

        [Header("Player Reference")]
        public FlashlightSystem playerFlashlight;

        [Header("Paranormal Settings")]
        [SerializeField] private float teleportMinDistance   = 15f;
        [SerializeField] private float teleportMaxDistance   = 30f;
        [SerializeField] private float respawnBehindChance   = 0.3f;
        [SerializeField] private float disappearDuration     = 5f;
        [SerializeField] private float manifestationChance   = 0.003f;

        // ── Ghost Audio ─────────────────────────────
        // AudioSource utama di-create runtime via AudioManager.
        // Semua clip di-assign via Inspector.
        [Header("Ghost Audio Clips")]
        [SerializeField] private AudioClip   ghostBreathClip;
        [SerializeField] private AudioClip   ghostAlertClip;
        [SerializeField] private AudioClip[] ghostFootstepClips;
        [SerializeField] private AudioClip   ghostDoorKnockClip;
        [SerializeField] private AudioClip   ghostHauntAmbientClip;
        [SerializeField] private AudioClip   ghostDistantClip;

        // Decoy / fake footstep — diputar di posisi berbeda via AudioManager.PlaySFX
        [SerializeField] private AudioClip   ghostFakeFootstepClip;

        // Runtime AudioSource yang disetup oleh AudioManager
        private AudioSource _ghostAudioSource;

        // State breath looping
        private bool _breathPlaying;

        [Header("Aggression Tuning")]
        [Tooltip("Jarak minimum agar ghost langsung chase meski tidak lihat player")]
        [SerializeField] private float proximityChaseRange = 3f;
        [Tooltip("Delay sebelum ghost menyerang setelah dalam range")]
        [SerializeField] private float attackDelay = 0.6f;

        // ── TODO #5: Frustration System ────────────
        [Header("Frustration System")]
        [SerializeField] private float maxFrustration             = 100f;
        [SerializeField] private float frustrationPerEscape       = 15f;
        [SerializeField] private float frustrationPerFailedAttack = 10f;
        [SerializeField] private float frustrationDecayRate       = 2f;   // per detik saat tenang
        [SerializeField] private float frustrationSpeedBonus      = 1.5f; // max tambahan speed
        [SerializeField] private float frustrationTeleportMult    = 3f;   // multiplier teleport chance
        [SerializeField] private float frustrationAmbushMult      = 2f;

        // ── TODO #6: Adaptive Difficulty ───────────
        [Header("Adaptive Difficulty")]
        [SerializeField] private bool  adaptiveDifficultyEnabled = true;
        [SerializeField] private float adaptiveDifficultyRate    = 0.05f; // laju penyesuaian

        // ── TODO #9: Tension System (Manifestation) ─
        [Header("Tension / Manifestation")]
        [SerializeField] private float tensionDecayRate    = 1f;   // per detik tanpa event
        [SerializeField] private float tensionBuildRate    = 5f;   // per detik saat player dekat
        [SerializeField] private float tensionMax          = 100f;
        [SerializeField] private float manifestTensionBase = 0.001f; // chance per detik saat tension rendah

        // ── TODO #10: Map Knowledge ─────────────────
        [Header("Map Knowledge (TODO #10)")]
        [SerializeField] private GhostInterestPoint[] interestPoints;
        [SerializeField] private float zoneAwarenessRadius = 40f;

        // ── TODO #4: Valid Teleport ──────────────────
        [Header("Teleport Validation")]
        [SerializeField] private LayerMask invalidTeleportMask;
        [SerializeField] private float     teleportMinDistanceFromObjective = 5f;
        [SerializeField] private Transform[] objectiveTransforms;

        // ── Runtime State ───────────────────────────
        public State currentState { get; private set; }

        private NavMeshAgent agent;

        // Frustration (TODO #5)
        private float frustrationLevel;

        // Adaptive Difficulty modifiers (TODO #6)
        private float adaptiveSightMod    = 0f;
        private float adaptiveHearMod     = 0f;
        private int   totalJumpscareFails = 0;
        private int   totalEscapes        = 0;
        private float totalSurvivalTime   = 0f;

        // Tension (TODO #9)
        private float tensionLevel;

        // Smoothed velocity (TODO #3)
        private const int   VELOCITY_HISTORY = 8;
        private Vector3[]   velocityHistory   = new Vector3[VELOCITY_HISTORY];
        private int         velHistoryIdx     = 0;
        private Vector3     smoothedVelocity;
        private Vector3     _lastPlayerPos;
        private float       _predictionTime   = 0.6f;

        // Audio decoy (TODO #7)
        private float audioDecoyTimer;

        // Mind Game states (TODO #8)
        private float observeTimer;
        private float hauntTimer;
        private float lurkTimer;
        private bool  lurkVisible = true;

        // Footstep timer — dipicu dari Update, bukan Animation Event
        private float _footTimer;

        // State Priority
        private int GetStatePriority(State s)
        {
            return s switch
            {
                State.Attacking    => 100,
                State.Chase        => 90,
                State.Ambush       => 80,
                State.Stalk        => 70,
                State.Lurk         => 65,
                State.Investigate  => 60,
                State.Haunt        => 55,
                State.Search       => 50,
                State.Observe      => 45,
                State.FakeRetreat  => 40,
                State.Patrol       => 10,
                State.Cooldown     => 0,
                _                  => 0
            };
        }

        private void TrySetState(State next, float duration = 0f)
        {
            if (next == currentState) return;
            int curP  = GetStatePriority(currentState);
            int nextP = GetStatePriority(next);
            if (nextP <= curP) return;
            ForceSetState(next, duration);
        }

        private static readonly HashSet<State> _uninterruptibleStates
            = new HashSet<State>
        {
            State.Attacking,
            State.Chase
        };

        private void ForceSetState(
            State next,
            float duration = 0f,
            bool respectUninterruptible = false)
        {
            if (respectUninterruptible &&
                _uninterruptibleStates.Contains(currentState))
            {
        #if UNITY_EDITOR
                if (debugMode)
                {
                    Debug.Log(
                        $"[GhostAI] ForceSetState({next}) ditolak - " +
                        $"{currentState} tidak interruptible.");
                }
        #endif
                return;
            }

            var prev = currentState;
            currentState = next;
            timer = duration;

            OnStateChanged?.Invoke(prev, currentState);
        }

        private Transform player;
        private int       patrolIndex;
        private float     timer;
        private Vector3   lastKnownPos;
        private bool      hasAttacked;
        private bool      isTeleporting;

        // Memory
        private float memoryTimer;
        private bool  hasMemoryOfPlayer;
        private float manifestationCooldown;

        // Stalking
        private float stalkTimer;

        // Fake Retreat
        private Vector3 retreatDestination;

        // Light Combat
        private float lightExposureTimer;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private bool _cachedCanSeePlayer;
        private int  _lastSeeFrame = -1;

        private Coroutine manifestCoroutine;
        private float     teleportCooldown;
        [SerializeField] private float teleportCooldownDuration = 8f;

        void OnEnable()
        {
            GhostRegistry.Register(this);
        }

        // ── Lifecycle ──────────────────────────────
        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            AutoAssign();
        }

        void OnDisable()
        {
            GhostRegistry.Unregister(this);

            StopAllCoroutines();
            manifestCoroutine = null;
            isTeleporting = false;

            StopAttackCoroutinesAndReset();

            if (_ghostAudioSource != null)
            {
                _ghostAudioSource.Stop();
                _breathPlaying = false;
            }
        }
        void Start()
        {
            player = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER)?.transform;

            if (player != null && playerFlashlight == null)
                playerFlashlight = player.GetComponentInChildren<FlashlightSystem>();

            if (enemyData == null)
            {
                Debug.LogError($"[GhostAI] '{gameObject.name}': enemyData tidak di-assign!");
                enabled = false;
                return;
            }

            // ── Setup 3D AudioSource via AudioManager ──
            // AudioManager membuat AudioSource yang ter-route ke sfxGroup mixer
            // dengan spatial blend 3D, sehingga volume mengecil sesuai jarak player
            if (AudioManager.Instance != null)
            {
                _ghostAudioSource = AudioManager.Instance.CreateGhostAudioSource(
                    gameObject, minDist: 1f, maxDist: 30f
                );
            }
            else
            {
                // Fallback jika AudioManager belum ada di scene
                _ghostAudioSource = gameObject.AddComponent<AudioSource>();
                _ghostAudioSource.spatialBlend = 1f;
                _ghostAudioSource.playOnAwake  = false;
                Debug.LogWarning($"[GhostAI] '{gameObject.name}': AudioManager.Instance null, fallback AudioSource digunakan.");
            }

            ApplyAgentSettings();
            ForceSetState(State.Patrol);
            GoNextPatrol();

            if (player != null) _lastPlayerPos = player.position;
        }

        void AutoAssign()
        {
            if (eyePoint == null)
            {
                var ep = transform.Find("EyePoint");
                if (ep != null)
                {
                    eyePoint = ep;
                }
                else
                {
                    var go = new GameObject("EyePoint");
                    go.transform.SetParent(transform);
                    go.transform.localPosition = new Vector3(0, 1.6f, 0.2f);
                    eyePoint = go.transform;
                    Debug.Log($"[GhostAI] '{gameObject.name}': EyePoint auto-created.");
                }
            }
        }

        void ApplyAgentSettings()
        {
            if (agent == null || enemyData == null) return;
            agent.stoppingDistance = 1.2f;
            agent.angularSpeed     = 240f;
            agent.acceleration     = 10f;
            SetAgentSpeed(enemyData.patrolSpeed);
        }

        void SetAgentSpeed(float speed)
        {
            float bonus = Mathf.Lerp(0f, frustrationSpeedBonus,
                                     frustrationLevel / Mathf.Max(1f, maxFrustration));
            if (agent != null) agent.speed = speed + bonus;
        }

        // ── Update ─────────────────────────────────
        void Update()
        {
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER)?.transform;
                return;
            }

            totalSurvivalTime += Time.deltaTime;
            ApplyAdaptiveDifficulty();

            UpdateMemory();
            UpdatePerception();
            UpdateFrustration();
            UpdateTension();
            RunState();

            if (currentState != State.Attacking)
            {
                CheckProximityChase();
                if (currentState == State.Stalk)
                    CheckVisionStalk();
                else
                    CheckVision();

                CheckLightFear();
                CheckLightCombat();
                CheckAttack();
                UpdateAudioDecoy();
            }

            UpdateAnimator();
            UpdateGhostAudio();         // ← ganti nama dari UpdateAudio() agar tidak ambigu
            manifestationCooldown -= Time.deltaTime;
            teleportCooldown = Mathf.Max(0f, teleportCooldown - Time.deltaTime);
        }

        void LateUpdate()
        {
            ApplyModelRotationOffset();
        }

        // ══════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════
        void RunState()
        {
            switch (currentState)
            {
                case State.Patrol:      PatrolUpdate();      break;
                case State.Investigate: InvestigateUpdate(); break;
                case State.Chase:       ChaseUpdate();       break;
                case State.Search:      SearchUpdate();      break;
                case State.Stalk:       StalkUpdate();       break;
                case State.FakeRetreat: FakeRetreatUpdate(); break;
                case State.Ambush:      AmbushUpdate();      break;
                case State.Cooldown:    CooldownUpdate();    break;
                case State.Observe:     ObserveUpdate();     break;
                case State.Haunt:       HauntUpdate();       break;
                case State.Lurk:        LurkUpdate();        break;
                case State.Attacking:   break;
            }
        }

        void PatrolUpdate()
        {
            SetAgentSpeed(enemyData.patrolSpeed);

            if (!agent.pathPending && agent.remainingDistance < 1f)
                GoNextPatrol();

            if (enemyData.personality == EnemyPersonality.Stalker)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (dist < enemyData.sightRange * 0.7f && !CanSeePlayer())
                    EnterStalkMode();
            }

            UpdateManifestationByTension();
        }

        void UpdateManifestationByTension()
        {
            if (manifestationCooldown > 0f || isTeleporting) return;

            float tensionFactor = Mathf.Lerp(manifestTensionBase,
                                             manifestTensionBase * 10f,
                                             tensionLevel / Mathf.Max(1f, tensionMax));

            if (Random.value < tensionFactor * Time.deltaTime)
            {
                manifestationCooldown = 25f;
                tensionLevel = 0f;
                EnterManifestation();
            }
        }

        void UpdateTension()
        {
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.position);

            if (dist < enemyData.sightRange * 1.5f)
                tensionLevel = Mathf.Min(tensionMax, tensionLevel + tensionBuildRate * Time.deltaTime);
            else
                tensionLevel = Mathf.Max(0f, tensionLevel - tensionDecayRate * Time.deltaTime);
        }

        void EnterManifestation()
        {
            if (isTeleporting) return;

            if (Random.value < respawnBehindChance)
                TeleportBehindPlayer();
            else
                TeleportRandom();

            agent.isStopped = true;
            ForceSetState(
                State.Stalk,
                0f,
                respectUninterruptible: true);

            stalkTimer = 3f;

            if (manifestCoroutine != null) StopCoroutine(manifestCoroutine);
            manifestCoroutine = StartCoroutine(ManifestRoutine());
        }

        void InvestigateUpdate()
        {
            SetAgentSpeed(enemyData.patrolSpeed * 1.2f);

            if (!agent.pathPending && agent.remainingDistance < 1f)
                TrySetState(State.Search, enemyData.searchTime);
        }

        void ChaseUpdate()
        {
            SetAgentSpeed(enemyData.chaseSpeed);

            bool    canSee       = CanSeePlayer();
            Vector3 predictedPos = GetPredictedPlayerPosition();

            if (canSee)
            {
                agent.SetDestination(predictedPos);
                lastKnownPos      = player.position;
                hasMemoryOfPlayer = true;
                memoryTimer       = enemyData.memoryDuration;
            }
            else
            {
                float ambushChance = 0.12f * Mathf.Lerp(1f, frustrationAmbushMult,
                                                          frustrationLevel / Mathf.Max(1f, maxFrustration));

                if (hasMemoryOfPlayer && Random.value < ambushChance
                    && enemyData.personality != EnemyPersonality.Stalker)
                {
                    EnterAmbush();
                    return;
                }

                if (hasMemoryOfPlayer)
                {
                    agent.SetDestination(lastKnownPos);
                    return;
                }

                if (enemyData.personality == EnemyPersonality.Trickster
                    && Random.value < enemyData.fakeRetreatChance)
                {
                    EnterFakeRetreat();
                    return;
                }

                agent.SetDestination(lastKnownPos);
                TrySetState(State.Search, enemyData.searchTime);
            }

            float teleChance = 0.0002f * Mathf.Lerp(1f, frustrationTeleportMult,
                                                      frustrationLevel / Mathf.Max(1f, maxFrustration));

            if (currentState == State.Chase && !hasAttacked && Random.value < teleChance)
            {
                TryTeleportBehindPlayer();
                EnterStalkMode();
            }

            if (enemyData.personality == EnemyPersonality.Trickster && Random.value < 0.0005f)
                TryDisappearAndTeleport();
        }

        void SearchUpdate()
        {
            timer -= Time.deltaTime;
            SetAgentSpeed(enemyData.patrolSpeed * 1.1f);

            if (!agent.pathPending && agent.remainingDistance < 1f)
            {
                Vector3 searchTarget = GetSearchTargetNearInterestPoint();
                agent.SetDestination(searchTarget);
            }

            if (timer <= 0f)
                TrySetState(State.Cooldown, enemyData.cooldownTime);
        }

        void StalkUpdate()
        {
            stalkTimer -= Time.deltaTime;
            SetAgentSpeed(enemyData.stalkSpeed);

            bool canSee = CanSeePlayer();

            if (canSee)
            {
                Vector3 dir      = (player.position - transform.position).normalized;
                Vector3 stalkPos = player.position - dir * (enemyData.sightRange * 0.45f);
                agent.SetDestination(stalkPos);
                lastKnownPos = player.position;
            }

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist < 5f)
            {
                Vector3 lookPos = player.position;
                lookPos.y = transform.position.y;
                transform.LookAt(lookPos);
            }

            if (stalkTimer <= 0f)
            {
                if (!canSee && Random.value < 0.2f)
                {
                    EnterAmbush();
                    return;
                }

                TrySetState(
                    canSee ? State.Chase : State.Cooldown,
                    canSee ? 0f : enemyData.cooldownTime);
            }
        }

        void FakeRetreatUpdate()
        {
            timer -= Time.deltaTime;
            SetAgentSpeed(enemyData.chaseSpeed * 0.8f);
            agent.SetDestination(retreatDestination);

            if (timer <= 0f)
            {
                bool playerStillFacingGhost = player != null
                    && Vector3.Angle(player.forward,
                                     (transform.position - player.position).normalized) <= 60f;

                if (!playerStillFacingGhost)
                {
                    if (Random.value < 0.2f) EnterAmbush();
                    else EnterStalkMode();
                    return;
                }

                if (Random.value < 0.2f) EnterAmbush();
                else EnterStalkMode();
            }
        }

        void CooldownUpdate()
        {
            timer -= Time.deltaTime;
            SetAgentSpeed(enemyData.patrolSpeed * 0.5f);

            if (timer <= 0f)
            {
                TrySetState(State.Patrol);
                GoNextPatrol();
            }
        }

        // ══════════════════════════════════════════
        //  TODO #8 — MIND GAME STATES
        // ══════════════════════════════════════════

        void ObserveUpdate()
        {
            observeTimer -= Time.deltaTime;
            SetAgentSpeed(enemyData.stalkSpeed * 0.7f);

            if (player == null) { TrySetState(State.Patrol); return; }

            Vector3 dir       = (transform.position - player.position).normalized;
            float   orbitDist = enemyData.sightRange * 0.8f;
            Vector3 orbitPos  = player.position + dir * orbitDist;

            if (NavMesh.SamplePosition(orbitPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);

            // Suara jauh saat Observe — ghost "terdengar" tapi tidak terlihat
            if (Random.value < 0.0005f)
                PlayDistantGhostAudio();

            if (CanSeePlayer())
            {
                float roll = Random.value;
                if (roll < 0.4f)      { TrySetState(State.Chase); return; }
                else if (roll < 0.7f) { EnterStalkMode(); return; }
                else                  { EnterAmbush(); return; }
            }

            if (observeTimer <= 0f)
            {
                float roll = Random.value;
                if (roll < 0.35f)      EnterLurkMode();
                else if (roll < 0.65f) EnterHauntMode();
                else                   EnterStalkMode();
            }
        }

        void HauntUpdate()
        {
            hauntTimer -= Time.deltaTime;

            agent.isStopped = true;

            if (player != null)
            {
                Vector3 lookPos = player.position;
                lookPos.y = transform.position.y;
                transform.LookAt(lookPos);
            }

            // ── Haunt ambient via AudioManager ──
            // PlayAmbience() crossfade — aman dipanggil berulang karena AudioManager
            // mengecek apakah clip sama sebelum memutar ulang
            if (hauntTimer > 0f)
                StartHauntAmbient();

            if (hauntTimer <= 0f)
            {
                agent.isStopped = false;
                StopHauntAmbient();

                if (Random.value < 0.5f)
                    TryDisappearAndTeleport();
                else
                    TrySetState(State.Chase);
            }
        }

        void LurkUpdate()
        {
            lurkTimer -= Time.deltaTime;
            SetAgentSpeed(enemyData.stalkSpeed * 1.1f);

            if (lurkTimer <= 0f)
            {
                lurkVisible = !lurkVisible;
                SetRenderersEnabled(lurkVisible);

                if (lurkVisible)
                {
                    Vector3 lurkPos = player.position + Random.insideUnitSphere * 8f;
                    lurkPos.y = player.position.y;
                    if (NavMesh.SamplePosition(lurkPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                        agent.Warp(hit.position);

                    lurkTimer = Random.Range(1f, 3f);
                }
                else
                {
                    lurkTimer = Random.Range(0.5f, 2f);
                }

                if (Random.value < 0.25f)
                {
                    SetRenderersEnabled(true);
                    TrySetState(State.Chase);
                }
            }

            if (lurkVisible && CanSeePlayer() && Random.value < 0.3f)
            {
                SetRenderersEnabled(true);
                TrySetState(State.Chase);
            }
        }

        void EnterObserveMode()
        {
            ForceSetState(State.Observe);
            observeTimer = Random.Range(6f, 12f);
        }

        void EnterHauntMode()
        {
            if (player != null)
            {
                Vector3 hauntPos = player.position + player.forward * Random.Range(8f, 15f);
                if (NavMesh.SamplePosition(hauntPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }
            ForceSetState(
                State.Haunt,
                0f,
                respectUninterruptible: true);
            hauntTimer = Random.Range(3f, 6f);
        }

        void EnterLurkMode()
        {
            ForceSetState(
                State.Lurk,
                0f,
                respectUninterruptible: true);
            lurkTimer   = Random.Range(1f, 2f);
            lurkVisible = true;
        }

        // ══════════════════════════════════════════
        //  TODO #3 — SMOOTHED VELOCITY PREDICTION
        // ══════════════════════════════════════════
        void UpdatePerception()
        {
            Vector3 rawVelocity = (player.position - _lastPlayerPos)
                                  / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPlayerPos = player.position;

            velocityHistory[velHistoryIdx % VELOCITY_HISTORY] = rawVelocity;
            velHistoryIdx++;

            Vector3 sum = Vector3.zero;
            int     cnt = Mathf.Min(velHistoryIdx, VELOCITY_HISTORY);
            for (int i = 0; i < cnt; i++) sum += velocityHistory[i];

            smoothedVelocity = sum / Mathf.Max(1, cnt);
        }

        Vector3 GetPredictedPlayerPosition()
        {
            float   predTime  = Mathf.Clamp(_predictionTime, 0.2f, 1.2f);
            Vector3 predicted = player.position + smoothedVelocity * predTime;

            if (NavMesh.SamplePosition(predicted, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                return hit.position;

            return player.position;
        }

        // ══════════════════════════════════════════
        //  TODO #5 — FRUSTRATION SYSTEM
        // ══════════════════════════════════════════
        void UpdateFrustration()
        {
            if (currentState != State.Chase && currentState != State.Ambush)
                frustrationLevel = Mathf.Max(0f, frustrationLevel - frustrationDecayRate * Time.deltaTime);
        }

        void AddFrustration(float amount)
        {
            frustrationLevel = Mathf.Min(maxFrustration, frustrationLevel + amount);
            if (debugMode)
                Debug.Log($"[GhostAI] Frustration: {frustrationLevel:F1}/{maxFrustration}");
        }

        // ══════════════════════════════════════════
        //  TODO #6 — ADAPTIVE DIFFICULTY
        // ══════════════════════════════════════════
        void ApplyAdaptiveDifficulty()
        {
            if (!adaptiveDifficultyEnabled || enemyData == null) return;

            float difficultyScore = (totalEscapes * 0.3f) + (totalSurvivalTime / 120f);
            float targetMod       = Mathf.Clamp(difficultyScore * adaptiveDifficultyRate, 0f, 5f);

            adaptiveSightMod = Mathf.Lerp(adaptiveSightMod, targetMod,       Time.deltaTime * 0.1f);
            adaptiveHearMod  = Mathf.Lerp(adaptiveHearMod,  targetMod * 2f,  Time.deltaTime * 0.1f);
        }

        float GetEffectiveSightRange()  => enemyData.sightRange  + adaptiveSightMod;
        float GetEffectiveHearRadius()  => enemyData.hearRadius   + adaptiveHearMod;

        // ══════════════════════════════════════════
        //  TODO #7 — AUDIO SEBAGAI GAMEPLAY (Decoy)
        // ══════════════════════════════════════════
        void UpdateAudioDecoy()
        {
            audioDecoyTimer -= Time.deltaTime;
            if (audioDecoyTimer > 0f) return;

            if (currentState != State.Patrol &&
                currentState != State.Stalk  &&
                currentState != State.Observe) return;

            float roll = Random.value;

            if (roll < 0.3f)
            {
                // Knock decoy — diputar di posisi interest point, bukan posisi ghost
                // sehingga player mendengar suara dari arah yang "salah"
                PlayFakeDoorKnock(GetDecoyPosition());
                audioDecoyTimer = Random.Range(20f, 45f);
            }
            else if (roll < 0.6f)
            {
                // Fake footstep decoy
                PlayFakeFootstep(GetDecoyPosition());
                audioDecoyTimer = Random.Range(10f, 25f);
            }
            else
            {
                audioDecoyTimer = Random.Range(15f, 30f);
            }
        }

        Vector3 GetDecoyPosition()
        {
            if (interestPoints != null && interestPoints.Length > 0)
            {
                var pt = interestPoints[Random.Range(0, interestPoints.Length)];
                if (pt.point != null) return pt.point.position;
            }

            Vector3 rnd = transform.position + Random.insideUnitSphere * 15f;
            rnd.y = transform.position.y;
            return rnd;
        }

        // ══════════════════════════════════════════
        //  TODO #10 — MAP KNOWLEDGE
        // ══════════════════════════════════════════

        GhostInterestPoint GetWeightedInterestPoint(GhostZoneType preferredZone = GhostZoneType.OpenArea)
        {
            if (interestPoints == null || interestPoints.Length == 0) return null;

            float totalWeight = 0f;
            foreach (var pt in interestPoints)
            {
                if (pt.point == null) continue;
                float dist = Vector3.Distance(transform.position, pt.point.position);
                if (dist > zoneAwarenessRadius) continue;

                float w = pt.weight;
                if (pt.zoneType == preferredZone) w *= 2f;
                totalWeight += w;
            }

            float rnd = Random.value * totalWeight;
            float acc = 0f;

            foreach (var pt in interestPoints)
            {
                if (pt.point == null) continue;
                float dist = Vector3.Distance(transform.position, pt.point.position);
                if (dist > zoneAwarenessRadius) continue;

                float w = pt.weight;
                if (pt.zoneType == preferredZone) w *= 2f;
                acc += w;

                if (rnd <= acc) return pt;
            }

            return interestPoints[Random.Range(0, interestPoints.Length)];
        }

        Vector3 GetSearchTargetNearInterestPoint()
        {
            if (interestPoints == null || interestPoints.Length == 0)
            {
                Vector3 rnd = lastKnownPos + Random.insideUnitSphere * 6f;
                rnd.y = lastKnownPos.y;
                if (NavMesh.SamplePosition(rnd, out NavMeshHit h, 6f, NavMesh.AllAreas))
                    return h.position;
                return lastKnownPos;
            }

            GhostInterestPoint closest = null;
            float minDist = float.MaxValue;

            foreach (var pt in interestPoints)
            {
                if (pt.point == null) continue;
                float d = Vector3.Distance(lastKnownPos, pt.point.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = pt;
                }
            }

            if (closest != null && minDist < 20f)
                return closest.point.position;

            Vector3 fallback = lastKnownPos + Random.insideUnitSphere * 6f;
            fallback.y = lastKnownPos.y;
            if (NavMesh.SamplePosition(fallback, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                return hit.position;
            return lastKnownPos;
        }

        // ══════════════════════════════════════════
        //  SENSING
        // ══════════════════════════════════════════
        public void HearSound(Vector3 pos, float radius)
        {
            if (currentState == State.Attacking) return;

            float dist            = Vector3.Distance(transform.position, pos);
            float effectiveRadius = Mathf.Min(radius, GetEffectiveHearRadius());

            if (dist > effectiveRadius) return;

            PlayGhostAlert();

            if (enemyData.personality == EnemyPersonality.Trickster && Random.value < 0.35f)
            {
                Vector3 offset = Random.insideUnitSphere * 4f;
                offset.y = 0;
                if (NavMesh.SamplePosition(pos + offset, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    TrySetState(State.Investigate);
                    agent.SetDestination(hit.position);
                    return;
                }
            }

            TrySetState(State.Investigate);
            agent.SetDestination(pos);
        }

        void CheckVisionStalk()
        {
            if (currentState != State.Stalk) return;
            if (!CanSeePlayer()) return;

            float roll = Random.value;

            if (roll < 0.55f)
            {
                TrySetState(State.Chase);
                return;
            }
            if (roll < 0.8f)
            {
                stalkTimer = Mathf.Max(stalkTimer, 1.5f);
            }
            else
            {
                if (Random.value < 0.4f) EnterAmbush();
                else { TryTeleportBehindPlayer(); EnterStalkMode(); }
            }
        }

        void CheckVision()
        {
            if (currentState != State.Patrol    &&
                currentState != State.Investigate &&
                currentState != State.Search)
                return;

            if (!CanSeePlayer()) return;

            // Alert saat pertama kali mendeteksi player
            PlayGhostAlert();

            float roll = Random.value;

            if (roll < 0.25f)
            {
                EnterObserveMode();
            }
            else if (roll < 0.45f)
            {
                TrySetState(State.Chase);
            }
            else if (roll < 0.6f)
            {
                EnterStalkMode();
            }
            else if (roll < 0.75f)
            {
                EnterAmbush();
            }
            else if (roll < 0.85f)
            {
                EnterHauntMode();
            }
            else if (roll < 0.92f)
            {
                TryTeleportBehindPlayer();
                EnterStalkMode();
            }
            else
            {
                TryDisappearAndTeleport();
            }
        }

        void CheckProximityChase()
        {
            if (currentState == State.Cooldown   ||
                currentState == State.Attacking  ||
                currentState == State.FakeRetreat) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= proximityChaseRange && currentState != State.Chase)
            {
                lastKnownPos      = player.position;
                hasMemoryOfPlayer = true;
                memoryTimer       = enemyData.memoryDuration;
                TrySetState(State.Chase);
            }
        }

        bool CanSeePlayer()
        {
            if (Time.frameCount == _lastSeeFrame) return _cachedCanSeePlayer;
            _lastSeeFrame = Time.frameCount;

            if (eyePoint == null || player == null)
            {
                _cachedCanSeePlayer = false;
                return false;
            }

            Vector3 toPlayer = player.position - eyePoint.position;
            float   dist     = toPlayer.magnitude;

            if (dist > GetEffectiveSightRange())
            {
                _cachedCanSeePlayer = false;
                return false;
            }

            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > enemyData.sightAngle * 0.5f)
            {
                _cachedCanSeePlayer = false;
                return false;
            }

            if (Physics.Raycast(eyePoint.position, toPlayer.normalized, out RaycastHit hit,
                                 dist, obstacleMask))
            {
                _cachedCanSeePlayer = false;
                return false;
            }

            _cachedCanSeePlayer = true;
            return true;
        }

        // ══════════════════════════════════════════
        //  TODO #2 — IMPROVED AMBUSH
        // ══════════════════════════════════════════

        void AmbushUpdate()
        {
            timer -= Time.deltaTime;
            SetAgentSpeed(enemyData.chaseSpeed * 0.95f);

            Vector3 interceptTarget = GetAmbushInterceptPoint();

            if (CanSeePlayer())
            {
                lastKnownPos      = player.position;
                hasMemoryOfPlayer = true;
                memoryTimer       = enemyData.memoryDuration;
                agent.SetDestination(interceptTarget);
            }
            else
            {
                if (!agent.pathPending && agent.remainingDistance < 1f)
                {
                    Vector3 t = interceptTarget;
                    t.y = transform.position.y;
                    if (NavMesh.SamplePosition(t, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                        agent.SetDestination(hit.position);
                }
            }

            if (timer <= 0f) ExitAmbush();
        }

        Vector3 GetAmbushInterceptPoint()
        {
            if (player == null) return lastKnownPos;

            Vector3 predicted = GetPredictedPlayerPosition();

            Vector3 bestPatrolPt  = Vector3.zero;
            float   bestPatrolDot = -1f;
            bool    foundPatrol   = false;

            if (patrolPoints != null)
            {
                Vector3 playerDir = smoothedVelocity.sqrMagnitude > 0.01f
                    ? smoothedVelocity.normalized
                    : player.forward;

                foreach (var pt in patrolPoints)
                {
                    if (pt == null) continue;
                    Vector3 toNode = (pt.position - player.position).normalized;
                    float   dot    = Vector3.Dot(playerDir, toNode);
                    float   dist   = Vector3.Distance(player.position, pt.position);

                    if (dot > 0.3f && dist > 4f && dist < 25f && dot > bestPatrolDot)
                    {
                        bestPatrolDot = dot;
                        bestPatrolPt  = pt.position;
                        foundPatrol   = true;
                    }
                }
            }

            if (foundPatrol) return bestPatrolPt;

            if (interestPoints != null)
            {
                Vector3 playerDir = smoothedVelocity.sqrMagnitude > 0.01f
                    ? smoothedVelocity.normalized
                    : player.forward;

                GhostInterestPoint bestIP  = null;
                float              bestDot = -1f;

                foreach (var ip in interestPoints)
                {
                    if (ip.point == null) continue;
                    Vector3 toIP = (ip.point.position - player.position).normalized;
                    float   dot  = Vector3.Dot(playerDir, toIP);
                    float   dist = Vector3.Distance(player.position, ip.point.position);

                    if (dot > 0.4f && dist > 5f && dist < 30f && dot > bestDot)
                    {
                        bestDot = dot;
                        bestIP  = ip;
                    }
                }

                if (bestIP != null) return bestIP.point.position;
            }

            return predicted;
        }

        // ══════════════════════════════════════════
        //  ATTACK
        // ══════════════════════════════════════════
        void CheckAttack()
        {
            if (hasAttacked) return;
            if (currentState != State.Chase) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= enemyData.attackRange && CanSeePlayer())
            {
                hasAttacked = true;
                StartCoroutine(AttackSequence());
            }
        }

        IEnumerator AttackSequence()
        {
            ForceSetState(State.Attacking);
            agent.isStopped = true;

            yield return new WaitForSeconds(attackDelay);

            if (player != null &&
                Vector3.Distance(transform.position, player.position)
                <= enemyData.attackRange * 2f)
            {
                if (CanPerformFatalAttack())
                {
                    UI.JumpscareManager.Instance?.StartJumpscare(transform);
                }
                else
                {
                    SanitySystem.Instance?.DrainInstant(35f);

                    ResetAttack();

                    EnterStalkMode();
                }
            }
            else
            {
                AddFrustration(frustrationPerFailedAttack);
                totalJumpscareFails++;
                ResetAttack();
            }
        }

        IEnumerator DisappearAndTeleport()
        {
            if (isTeleporting) yield break;

            isTeleporting = true;
            ForceSetState(State.Cooldown);
            agent.isStopped = true;

            SetRenderersEnabled(false);

            yield return new WaitForSeconds(disappearDuration);

            TeleportRandom();

            SetRenderersEnabled(true);
            agent.isStopped = false;
            EnterStalkMode();

            isTeleporting = false;
        }

        IEnumerator ManifestRoutine()
        {
            if (currentState == State.Chase || currentState == State.Attacking)
                yield break;

            yield return new WaitForSeconds(6f);

            if (currentState == State.Chase || currentState == State.Attacking || isTeleporting)
            {
                manifestCoroutine = null;
                yield break;
            }

            agent.isStopped = false;
            TryDisappearAndTeleport();
            manifestCoroutine = null;
        }

        public void ResetAttack()
        {
            hasAttacked     = false;
            agent.isStopped = false;
            TrySetState(State.Patrol);
            GoNextPatrol();
        }

        // ══════════════════════════════════════════
        //  LIGHT MECHANICS
        // ══════════════════════════════════════════
        void CheckLightFear()
        {
            if (playerFlashlight == null || !playerFlashlight.IsOn()) return;
            if (currentState == State.Chase || currentState == State.Attacking) return;

            float dist = Vector3.Distance(transform.position, playerFlashlight.transform.position);
            if (dist < enemyData.lightFearRadius)
            {
                Vector3 dir  = (transform.position - playerFlashlight.transform.position).normalized;
                Vector3 flee = transform.position + dir * 5f;

                if (NavMesh.SamplePosition(flee, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    SetAgentSpeed(enemyData.chaseSpeed);
                    agent.SetDestination(hit.position);
                }
            }
        }

        void CheckLightCombat()
        {
            bool flashlightOn = playerFlashlight != null && playerFlashlight.IsOn();

            if (!flashlightOn)
            {
                lightExposureTimer = Mathf.Max(0f, lightExposureTimer - Time.deltaTime * 1.5f);
                return;
            }

            float dist = Vector3.Distance(transform.position, playerFlashlight.transform.position);
            if (dist < enemyData.lightFearRadius)
            {
                lightExposureTimer += Time.deltaTime;

                if (lightExposureTimer >= enemyData.lightExposureThreshold)
                {
                    if (!CanTeleport()) return;

                    lightExposureTimer = 0f;
                    TryDisappearAndTeleport();
                    return;
                }
            }
            else
            {
                lightExposureTimer = Mathf.Max(0f, lightExposureTimer - Time.deltaTime * 1.5f);
            }
        }

        // ══════════════════════════════════════════
        //  MEMORY
        // ══════════════════════════════════════════
        void UpdateMemory()
        {
            if (!hasMemoryOfPlayer) return;
            if (currentState == State.Chase || currentState == State.Attacking) return;

            memoryTimer -= Time.deltaTime;

            if (memoryTimer <= 0f)
            {
                hasMemoryOfPlayer = false;
                AddFrustration(frustrationPerEscape);
                totalEscapes++;
            }
        }

        // ══════════════════════════════════════════
        //  PATROL
        // ══════════════════════════════════════════
        void GoNextPatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                var pt = GetWeightedInterestPoint();
                if (pt != null && pt.point != null)
                    agent.SetDestination(pt.point.position);
                else
                    WanderRandom();
                return;
            }
            agent.SetDestination(patrolPoints[patrolIndex].position);
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }

        void WanderRandom()
        {
            Vector3 rnd = transform.position + Random.insideUnitSphere * 8f;
            rnd.y = transform.position.y;
            if (NavMesh.SamplePosition(rnd, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        // ══════════════════════════════════════════
        //  STALK / FAKE RETREAT / AMBUSH HELPERS
        // ══════════════════════════════════════════
        void EnterStalkMode()
        {
            ForceSetState(State.Stalk);
            stalkTimer = enemyData.stalkDuration;
        }

        void EnterAmbush()
        {
            ForceSetState(State.Ambush);
            timer = Mathf.Max(1.5f, enemyData.searchTime);

            Vector3 interceptPt = GetAmbushInterceptPoint();
            interceptPt.y = transform.position.y;

            if (NavMesh.SamplePosition(interceptPt, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                agent.SetDestination(hasMemoryOfPlayer ? lastKnownPos : player.position);

            if (player != null)
            {
                Vector3 lookPos = player.position;
                lookPos.y = transform.position.y;
                transform.LookAt(lookPos);
            }
        }

        void ExitAmbush()
        {
            bool canSee = CanSeePlayer();
            SetAgentSpeed(enemyData.chaseSpeed);
            TrySetState(canSee ? State.Chase : State.Search,
                        canSee ? 0f         : enemyData.searchTime);
        }

        void EnterFakeRetreat()
        {
            ForceSetState(State.FakeRetreat);
            timer = enemyData.fakeRetreatDuration;

            Vector3 away = (transform.position - player.position).normalized;
            Vector3 dest = transform.position + away * 12f;
            retreatDestination = NavMesh.SamplePosition(dest, out NavMeshHit hit, 12f, NavMesh.AllAreas)
                ? hit.position
                : transform.position;
        }

        // ══════════════════════════════════════════
        //  PARANORMAL ABILITIES
        // ══════════════════════════════════════════
        bool PlayerCanSee(Vector3 worldPos)
        {
            if (player == null) return false;

            Vector3 dir   = worldPos - player.position;
            float   angle = Vector3.Angle(player.forward, dir);

            if (angle > 60f) return false;

            if (Physics.Raycast(player.position + Vector3.up * 1.6f,
                                 dir.normalized, out RaycastHit hit,
                                 dir.magnitude, obstacleMask))
                return false;

            return true;
        }

        bool IsValidTeleportLocation(Vector3 pos)
        {
            if (invalidTeleportMask != 0)
            {
                if (Physics.CheckSphere(pos, 1f, invalidTeleportMask))
                    return false;
            }

            if (objectiveTransforms != null)
            {
                foreach (var obj in objectiveTransforms)
                {
                    if (obj == null) continue;
                    if (Vector3.Distance(pos, obj.position) < teleportMinDistanceFromObjective)
                        return false;
                }
            }

            if (PlayerCanSee(pos)) return false;

            return true;
        }

        void TeleportRandom()
        {
            if (player == null) return;

            for (int i = 0; i < 25; i++)
            {
                Vector3 randomPos = player.position
                    + Random.insideUnitSphere
                    * Random.Range(teleportMinDistance, teleportMaxDistance);
                randomPos.y = player.position.y;

                if (!NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                    continue;

                if (!IsValidTeleportLocation(hit.position))
                    continue;

                agent.Warp(hit.position);
                lastKnownPos = hit.position;

                if (debugMode) Debug.Log("[Ghost] Teleported (validated)");
                return;
            }

            Vector3 fallbackPos = player.position - player.forward * 15f;
            if (NavMesh.SamplePosition(fallbackPos, out NavMeshHit fallback, 20f, NavMesh.AllAreas))
                agent.Warp(fallback.position);
        }

        void TeleportBehindPlayer()
        {
            if (player == null) return;

            Vector3 behindPos = player.position - player.forward * Random.Range(6f, 12f);

            if (NavMesh.SamplePosition(behindPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                if (!IsValidTeleportLocation(hit.position))
                {
                    TeleportRandom();
                    return;
                }

                agent.Warp(hit.position);
                if (debugMode) Debug.Log($"[GhostAI:{gameObject.name}] Spawn Behind Player");
            }
        }

        bool CanTeleport() => !isTeleporting && teleportCooldown <= 0f;

        void StopAttackCoroutinesAndReset()
        {
            hasAttacked = false;
        }

        void TryTeleportRandom()
        {
            if (!CanTeleport()) return;
            teleportCooldown = teleportCooldownDuration;
            TeleportRandom();
        }

        void TryTeleportBehindPlayer()
        {
            if (!CanTeleport()) return;
            teleportCooldown = teleportCooldownDuration;
            TeleportBehindPlayer();
        }

        void TryDisappearAndTeleport()
        {
            if (!CanTeleport()) return;
            teleportCooldown = teleportCooldownDuration;
            StartCoroutine(DisappearAndTeleport());
        }

        void SetRenderersEnabled(bool enabled)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = enabled;
        }

        // ══════════════════════════════════════════
        //  GHOST AUDIO — terintegrasi AudioManager
        // ══════════════════════════════════════════

        /// <summary>
        /// Dipanggil tiap frame dari Update().
        /// Mengurus breath loop dan footstep berdasarkan state.
        /// </summary>
        void UpdateGhostAudio()
        {
            if (_ghostAudioSource == null) return;

            bool isChasing = currentState == State.Chase || currentState == State.Attacking;



            // ── Breath: loop saat chasing, stop saat tidak ──────────────────
            // Menggunakan pola dari AudioCallExamples: set .clip, .loop, .Play()
            // hanya jika belum aktif, untuk menghindari restart terus-menerus.
            if (isChasing)
            {
                PlayGhostBreathLoop();
            }
            else
            {
                StopGhostBreathLoop();
            }

            // ── Footstep: saat Investigate dan Chase ────────────────────────
            // Dipanggil dari kode (bukan Animation Event) sebagai fallback.
            // Jika project menggunakan Animation Event, hapus blok ini dan
            // panggil PlayGhostFootstep() langsung dari event tersebut.
            bool shouldPlayFootstep = currentState == State.Investigate
                                   || currentState == State.Chase;

            if (shouldPlayFootstep && ghostFootstepClips != null && ghostFootstepClips.Length > 0)
            {
                _footTimer -= Time.deltaTime;
                if (_footTimer <= 0f)
                {
                    PlayGhostFootstep();
                    // Interval lebih pendek saat chase
                    _footTimer = isChasing ? Random.Range(0.3f, 0.5f)
                                           : Random.Range(0.5f, 0.8f);
                }
            }
        }

        // ── Breath helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Mulai breath loop jika belum berjalan.
        /// Pattern dari AudioCallExamples: cek clip + isPlaying sebelum Play().
        /// </summary>
        private void PlayGhostBreathLoop()
        {
            if (_ghostAudioSource == null || ghostBreathClip == null) return;
            if (_breathPlaying && _ghostAudioSource.isPlaying
                && _ghostAudioSource.clip == ghostBreathClip) return;

            _ghostAudioSource.clip   = ghostBreathClip;
            _ghostAudioSource.loop   = true;
            _ghostAudioSource.volume = 0.6f;
            _ghostAudioSource.Play();
            _breathPlaying = true;
        }

        private void StopGhostBreathLoop()
        {
            if (!_breathPlaying) return;
            if (_ghostAudioSource == null) return;

            // Jangan stop jika sedang main clip lain (alert, footstep via PlayOneShot)
            // PlayOneShot tidak mengubah .clip, jadi cek apakah clip-nya masih breath
            if (_ghostAudioSource.clip == ghostBreathClip && _ghostAudioSource.loop)
            {
                _ghostAudioSource.Stop();
                _ghostAudioSource.loop = false;
            }
            _breathPlaying = false;
        }

        // ── Alert ──────────────────────────────────────────────────────────

        /// <summary>
        /// Dipanggil saat ghost pertama kali mendeteksi player (CheckVision, HearSound).
        /// PlayOneShot agar tidak memotong breath loop.
        /// </summary>
        private void PlayGhostAlert()
        {
            if (_ghostAudioSource == null || ghostAlertClip == null) return;
            // PlayOneShot melapisi clip yang sedang berjalan, cocok untuk one-shot di atas loop
            _ghostAudioSource.PlayOneShot(ghostAlertClip, 1f);
        }

        // ── Footstep ───────────────────────────────────────────────────────

        /// <summary>
        /// Dipanggil dari UpdateGhostAudio() atau bisa dipanggil dari Animation Event.
        /// </summary>
        public void PlayGhostFootstep()
        {
            if (_ghostAudioSource == null || ghostFootstepClips == null
                || ghostFootstepClips.Length == 0) return;

            var clip = ghostFootstepClips[Random.Range(0, ghostFootstepClips.Length)];
            _ghostAudioSource.PlayOneShot(clip, 0.9f);
        }

        // ── Decoy audio (TODO #7) ──────────────────────────────────────────

        /// <summary>
        /// Fake door knock — diputar di posisi decoy (bukan posisi ghost)
        /// via AudioManager.PlaySFX() agar ter-spatialized di lokasi yang tepat.
        /// </summary>
        private void PlayFakeDoorKnock(Vector3 worldPos)
        {
            if (ghostDoorKnockClip == null) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(ghostDoorKnockClip, worldPos, 1f);
            else if (_ghostAudioSource != null)
                _ghostAudioSource.PlayOneShot(ghostDoorKnockClip, 0.5f);
        }

        /// <summary>
        /// Fake footstep decoy — sama dengan knock, diputar di posisi berbeda dari ghost.
        /// </summary>
        private void PlayFakeFootstep(Vector3 worldPos)
        {
            if (ghostFakeFootstepClip == null) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(ghostFakeFootstepClip, worldPos, 0.7f);
            else if (_ghostAudioSource != null)
                _ghostAudioSource.PlayOneShot(ghostFakeFootstepClip, 0.35f);
        }

        /// <summary>
        /// Haunt ambient — crossfade via AudioManager.PlayAmbience().
        /// Dipanggil saat masuk/selama state Haunt.
        /// </summary>
        private void StartHauntAmbient()
        {
            if (ghostHauntAmbientClip == null) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAmbience(ghostHauntAmbientClip, fadeDuration: 1f);
        }

        private void StopHauntAmbient()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopAmbience(fadeDuration: 1.5f);
        }

        /// <summary>
        /// Suara ghost jauh — diputar di posisi ghost via AudioManager.PlaySFX().
        /// Terdengar samar karena 3D falloff, memberikan sinyal posisi ghost ke player.
        /// Dipanggil dari ObserveUpdate() secara berkala.
        /// </summary>
        private void PlayDistantGhostAudio()
        {
            if (ghostDistantClip == null) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(ghostDistantClip, transform.position, 0.5f);
            else if (_ghostAudioSource != null)
                _ghostAudioSource.PlayOneShot(ghostDistantClip, 0.5f);
        }

        // ══════════════════════════════════════════
        //  ANIMATOR
        // ══════════════════════════════════════════
        void UpdateAnimator()
        {
            if (animator == null) return;

            float normalizedSpeed = enemyData != null
                ? agent.velocity.magnitude / Mathf.Max(0.01f, enemyData.chaseSpeed)
                : agent.velocity.magnitude;

            animator.SetFloat("Speed",       normalizedSpeed);
            animator.SetBool("IsChasing",    currentState == State.Chase);
            animator.SetBool("IsSearching",  currentState == State.Search);
            animator.SetBool("IsStalking",   currentState == State.Stalk);
            animator.SetBool("IsAttacking",  currentState == State.Attacking);
            animator.SetBool("IsHaunting",   currentState == State.Haunt);
            animator.SetBool("IsObserving",  currentState == State.Observe);
        }

        void ApplyModelRotationOffset()
        {
            if (modelTransform == null) return;
            modelTransform.localRotation = Quaternion.Euler(modelRotationOffset);
        }

        // ── State Event ────────────────────────────
        public System.Action<State, State> OnStateChanged;

        // ══════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════
        public void ForceChase()
        {
            ForceSetState(State.Chase);
            SetAgentSpeed(enemyData != null ? enemyData.chaseSpeed : 5f);
        }

        public bool IsChasing => currentState == State.Chase || currentState == State.Attacking;
        public bool CanPerformFatalAttack()
        {
            if (SanitySystem.Instance == null)
                return false;

            return SanitySystem.Instance.IsCriticalSanity();
        }
        public bool IsAware   => currentState != State.Patrol && currentState != State.Cooldown;

        /// <summary>Dipanggil dari JumpscareManager saat jumpscare berhasil.</summary>
        public void OnJumpscareSuccess()
        {
            frustrationLevel = 0f;
            tensionLevel     = 0f;
        }

        // ══════════════════════════════════════════
        //  GIZMOS
        // ══════════════════════════════════════════
        void OnDrawGizmosSelected()
        {
            if (enemyData == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, enemyData.hearRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyData.sightRange);
            Vector3 fwd = transform.forward * enemyData.sightRange;
            Gizmos.DrawLine(transform.position, transform.position +
                Quaternion.Euler(0,  enemyData.sightAngle * 0.5f, 0) * fwd);
            Gizmos.DrawLine(transform.position, transform.position +
                Quaternion.Euler(0, -enemyData.sightAngle * 0.5f, 0) * fwd);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, enemyData.lightFearRadius);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);

            Gizmos.color = new Color(1f, .5f, 0f);
            Gizmos.DrawWireSphere(transform.position, proximityChaseRange);

            if (hasMemoryOfPlayer)
            {
                Gizmos.color = new Color(1f, 0f, 1f, .6f);
                Gizmos.DrawLine(transform.position, lastKnownPos);
                Gizmos.DrawWireSphere(lastKnownPos, 0.4f);
            }

            if (interestPoints != null)
            {
                foreach (var ip in interestPoints)
                {
                    if (ip.point == null) continue;
                    Gizmos.color = ip.zoneType == GhostZoneType.Objective
                        ? Color.red
                        : new Color(0.2f, 0.8f, 1f, 0.5f);
                    Gizmos.DrawWireSphere(ip.point.position, 1f);
                    Gizmos.DrawLine(transform.position, ip.point.position);
                }
            }

            Gizmos.color = Color.Lerp(Color.green, Color.red,
                                       frustrationLevel / Mathf.Max(1f, maxFrustration));
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.3f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f,
                $"[{currentState}]  lightExp:{lightExposureTimer:F1}s\n" +
                $"frustration:{frustrationLevel:F0}  tension:{tensionLevel:F0}\n" +
                $"sightMod:+{adaptiveSightMod:F1}  escapes:{totalEscapes}");
#endif
        }
    }
}
