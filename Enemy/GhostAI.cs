// ================================================================
//  GhostAI.cs  — v2.0  KKN: Desa Terkutuk
//  Perbaikan dari analisis scene:
//  • obstacleMask default dibenahi (tidak ALL layers)
//  • eyePoint auto-assign jika null (tidak NullRefException)
//  • Chase ke player lebih agresif dan smooth
//  • Light fear rebalanced
//  • Spawn ulang setelah cooldown via NavMesh random
//  • Audio untuk atmosphere (napas, footstep alert)
// ================================================================
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using KKN.Game.Core;
using KKN.Game.Data;
using KKN.Game.Systems;

namespace KKN.Game.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class GhostAI : MonoBehaviour
    {
        public enum State
        {
            Patrol,
            Investigate,
            Chase,
            Search,
            Stalk,
            FakeRetreat,
            Cooldown,
            Attacking
        }

        // ── Inspector ──────────────────────────────────────
        [Header("Data")]
        public EnemyData enemyData;

        [Header("References")]
        public Transform[] patrolPoints;

        [Tooltip("Model visual ghost")]
        [SerializeField] private Transform modelTransform;

        [Tooltip("Offset rotasi model visual")]
        [SerializeField] private Vector3 modelRotationOffset;

        [Tooltip("Layer yang dianggap penghalang penglihatan (jangan masukkan layer Player!)")]
        public LayerMask obstacleMask = (1 << 0) | (1 << 3);
        public Transform   eyePoint;
        public Animator    animator;

        [Header("Player Reference")]
        public FlashlightSystem playerFlashlight;

        [Header("Ghost Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   breathClip;
        [SerializeField] private AudioClip   alertClip;
        [SerializeField] private AudioClip   footstepClip;

        [Header("Aggression Tuning")]
        [Tooltip("Jarak minimum agar ghost langsung chase meski tidak lihat player")]
        [SerializeField] private float proximityChaseRange = 3f;
        [Tooltip("Delay sebelum ghost menyerang setelah dalam range")]
        [SerializeField] private float attackDelay = 0.6f;

        // ── Runtime State ──────────────────────────────────
        public State currentState { get; private set; }

        private NavMeshAgent agent;
        private Transform    player;
        private int          patrolIndex;
        private float        timer;
        private Vector3      lastKnownPos;
        private bool         hasAttacked;

        // Memory
        private float memoryTimer;
        private bool  hasMemoryOfPlayer;

        // Stalking
        private float stalkTimer;

        // Fake Retreat
        private Vector3 retreatDestination;

        // Light Combat
        private float lightExposureTimer;

        // Audio
        private float breathTimer;
        private float footTimer;

        // ── Lifecycle ──────────────────────────────────────
        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            AutoAssign();
        }

        void Start()
        {
            player = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER)?.transform;

            if (player != null && playerFlashlight == null)
                playerFlashlight = player.GetComponentInChildren<FlashlightSystem>();

            if (enemyData == null)
            {
                Debug.LogError($"[GhostAI] '{gameObject.name}': enemyData tidak di-assign! " +
                               "Buat EnemyData ScriptableObject via Create > KKN > Enemy Data.");
                enabled = false;
                return;
            }

            ApplyAgentSettings();
            currentState = State.Patrol;
            GoNextPatrol();
        }

        void AutoAssign()
        {
            // eyePoint: auto-create child jika belum ada
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
                    Debug.Log($"[GhostAI] '{gameObject.name}': EyePoint auto-created di (0, 1.6, 0.2).");
                }
            }

            // audioSource
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }

        void ApplyAgentSettings()
        {
            if (agent == null || enemyData == null) return;
            agent.speed             = enemyData.patrolSpeed;
            agent.stoppingDistance  = 1.2f;
            agent.angularSpeed      = 240f;
            agent.acceleration      = 10f;
        }

        // ── Update ────────────────────────────────────────
        void Update()
        {
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER)?.transform;
                return;
            }

            UpdateMemory();
            RunState();

            if (currentState != State.Attacking)
            {
                CheckProximityChase();
                CheckVision();
                CheckLightFear();
                CheckLightCombat();
                CheckAttack();
            }

            UpdateAnimator();
            UpdateAudio();
        }

        void LateUpdate()
        {
            ApplyModelRotationOffset();
        }

        // ══════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════
        void RunState()
        {
            switch (currentState)
            {
                case State.Patrol:       PatrolUpdate();       break;
                case State.Investigate:  InvestigateUpdate();  break;
                case State.Chase:        ChaseUpdate();        break;
                case State.Search:       SearchUpdate();       break;
                case State.Stalk:        StalkUpdate();        break;
                case State.FakeRetreat:  FakeRetreatUpdate();  break;
                case State.Cooldown:     CooldownUpdate();     break;
            }
        }

        void PatrolUpdate()
        {
            agent.speed = enemyData.patrolSpeed;

            if (!agent.pathPending && agent.remainingDistance < 1f)
                GoNextPatrol();

            // Stalker personality
            if (enemyData.personality == EnemyPersonality.Stalker)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (dist < enemyData.sightRange * 0.7f && !CanSeePlayer())
                    EnterStalkMode();
            }
        }

        void InvestigateUpdate()
        {
            agent.speed = enemyData.patrolSpeed * 1.2f;

            if (!agent.pathPending && agent.remainingDistance < 1f)
                SetState(State.Search, enemyData.searchTime);
        }

        void ChaseUpdate()
        {
            agent.speed = enemyData.chaseSpeed;

            if (player != null)
            {
                agent.SetDestination(player.position);
                lastKnownPos      = player.position;
                hasMemoryOfPlayer = true;
                memoryTimer       = enemyData.memoryDuration;
            }

            if (!CanSeePlayer() && !hasMemoryOfPlayer)
            {
                if (enemyData.personality == EnemyPersonality.Trickster &&
                    Random.value < enemyData.fakeRetreatChance)
                {
                    EnterFakeRetreat();
                    return;
                }
                agent.SetDestination(lastKnownPos);
                SetState(State.Search, enemyData.searchTime);
            }
        }

        void SearchUpdate()
        {
            timer -= Time.deltaTime;
            agent.speed = enemyData.patrolSpeed * 1.1f;

            if (!agent.pathPending && agent.remainingDistance < 1f)
            {
                Vector3 rnd = lastKnownPos + Random.insideUnitSphere * 6f;
                rnd.y = lastKnownPos.y;
                if (NavMesh.SamplePosition(rnd, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
            }

            if (timer <= 0f)
                SetState(State.Cooldown, enemyData.cooldownTime);
        }

        void StalkUpdate()
        {
            stalkTimer -= Time.deltaTime;
            agent.speed = enemyData.stalkSpeed;

            if (CanSeePlayer())
            {
                Vector3 dir = (player.position - transform.position).normalized;
                Vector3 stalkPos = player.position - dir * (enemyData.sightRange * 0.45f);
                agent.SetDestination(stalkPos);
            }

            float distToPlayer = Vector3.Distance(transform.position, player.position);
            if (stalkTimer <= 0f || distToPlayer < enemyData.attackRange * 2.5f)
                currentState = CanSeePlayer() ? State.Chase : State.Cooldown;
        }

        void FakeRetreatUpdate()
        {
            timer -= Time.deltaTime;
            agent.speed = enemyData.chaseSpeed * 0.8f;
            agent.SetDestination(retreatDestination);

            if (timer <= 0f)
                EnterStalkMode();
        }

        void CooldownUpdate()
        {
            timer -= Time.deltaTime;
            agent.speed = enemyData.patrolSpeed * 0.5f;

            if (timer <= 0f)
            {
                currentState = State.Patrol;
                agent.speed  = enemyData.patrolSpeed;
                GoNextPatrol();
            }
        }

        // ══════════════════════════════════════════════════
        //  SENSING
        // ══════════════════════════════════════════════════

        /// <summary>
        /// Dipanggil oleh FlashlightSystem dan PlayerNoiseEmitter saat ada suara.
        /// </summary>
        public void HearSound(Vector3 pos, float radius)
        {
            if (currentState == State.Attacking) return;

            float dist = Vector3.Distance(transform.position, pos);
            float effectiveRadius = Mathf.Min(radius, enemyData.hearRadius);

            if (dist > effectiveRadius) return;

            PlayAlert();

            if (enemyData.personality == EnemyPersonality.Trickster && Random.value < 0.35f)
            {
                // Trickster: datang dari sudut berbeda
                Vector3 offset = Random.insideUnitSphere * 4f;
                offset.y = 0;
                if (NavMesh.SamplePosition(pos + offset, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    currentState = State.Investigate;
                    agent.SetDestination(hit.position);
                    return;
                }
            }

            currentState = State.Investigate;
            agent.SetDestination(pos);
        }

        void CheckVision()
        {
            if (currentState == State.Stalk || currentState == State.FakeRetreat) return;
            if (CanSeePlayer()) currentState = State.Chase;
        }

        /// <summary>
        /// Jika player sangat dekat, ghost langsung chase tanpa harus bisa "melihat".
        /// Mencegah ghost terlalu pasif saat player ada di depannya.
        /// </summary>
        void CheckProximityChase()
        {
            if (currentState == State.Cooldown || currentState == State.Attacking) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= proximityChaseRange && currentState != State.Chase)
            {
                lastKnownPos = player.position;
                currentState = State.Chase;
            }
        }

        bool CanSeePlayer()
        {
            if (eyePoint == null || player == null) return false;

            Vector3 dir  = (player.position - eyePoint.position).normalized;
            float   dist = Vector3.Distance(eyePoint.position, player.position);

            if (dist > enemyData.sightRange)    return false;

            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > enemyData.sightAngle * 0.5f) return false;

            // Raycast dengan obstacleMask yang BENAR (bukan ALL layers)
            if (Physics.Raycast(eyePoint.position, dir, out RaycastHit hit, enemyData.sightRange, obstacleMask))
                return false; // blocked by obstacle

            // Jika tidak terblok obstacle, cek apakah player ada di sana
            if (Physics.Raycast(eyePoint.position, dir, out RaycastHit playerHit, enemyData.sightRange))
                return playerHit.transform.CompareTag(GameConstants.TAG_PLAYER);

            return false;
        }

        // ══════════════════════════════════════════════════
        //  ATTACK
        // ══════════════════════════════════════════════════
        void CheckAttack()
        {
            if (hasAttacked) return;
            if (currentState != State.Chase) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= enemyData.attackRange)
                StartCoroutine(AttackSequence());
        }

        IEnumerator AttackSequence()
        {
            hasAttacked       = true;
            currentState      = State.Attacking;
            agent.isStopped   = true;

            // Jeda singkat sebelum jumpscare (build tension)
            yield return new WaitForSeconds(attackDelay);

            UI.JumpscareManager.Instance?.StartJumpscare(transform);
        }

        public void ResetAttack()
        {
            hasAttacked     = false;
            agent.isStopped = false;
            currentState    = State.Patrol;
            agent.speed     = enemyData.patrolSpeed;
            GoNextPatrol();
        }

        // ══════════════════════════════════════════════════
        //  LIGHT MECHANICS
        // ══════════════════════════════════════════════════
        void CheckLightFear()
        {
            if (playerFlashlight == null || !playerFlashlight.IsOn()) return;
            if (currentState == State.Chase || currentState == State.Attacking) return;

            float dist = Vector3.Distance(transform.position, playerFlashlight.transform.position);
            if (dist < enemyData.lightFearRadius)
            {
                // Flee away from light source
                Vector3 dir = (transform.position - playerFlashlight.transform.position).normalized;
                Vector3 flee = transform.position + dir * 5f;

                if (NavMesh.SamplePosition(flee, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    agent.speed = enemyData.chaseSpeed;
                    agent.SetDestination(hit.position);
                }
            }
        }

        void CheckLightCombat()
        {
            if (playerFlashlight == null || !playerFlashlight.IsOn())
            {
                lightExposureTimer = Mathf.Max(0f, lightExposureTimer - Time.deltaTime);
                return;
            }

            float dist = Vector3.Distance(transform.position, playerFlashlight.transform.position);
            if (dist < enemyData.lightFearRadius)
            {
                lightExposureTimer += Time.deltaTime;

                if (lightExposureTimer >= enemyData.lightExposureThreshold)
                {
                    // Ghost retreat setelah terlalu lama kena cahaya
                    currentState       = State.Cooldown;
                    timer              = enemyData.cooldownTime * 1.5f;
                    lightExposureTimer = 0f;

                    Vector3 fleeDir = (transform.position - player.position).normalized;
                    if (NavMesh.SamplePosition(transform.position + fleeDir * 10f, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                        agent.SetDestination(hit.position);

                    Debug.Log($"[GhostAI] '{gameObject.name}' retreat karena senter!");
                }
            }
            else
            {
                lightExposureTimer = Mathf.Max(0f, lightExposureTimer - Time.deltaTime * 1.5f);
            }
        }

        // ══════════════════════════════════════════════════
        //  MEMORY
        // ══════════════════════════════════════════════════
        void UpdateMemory()
        {
            if (!hasMemoryOfPlayer) return;
            memoryTimer -= Time.deltaTime;
            if (memoryTimer <= 0f) hasMemoryOfPlayer = false;
        }

        // ══════════════════════════════════════════════════
        //  PATROL
        // ══════════════════════════════════════════════════
        void GoNextPatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                // Fallback: wander randomly pada NavMesh jika tidak ada patrol points
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

        // ══════════════════════════════════════════════════
        //  STALK / FAKE RETREAT
        // ══════════════════════════════════════════════════
        void EnterStalkMode()
        {
            currentState = State.Stalk;
            stalkTimer   = enemyData.stalkDuration;
        }

        void EnterFakeRetreat()
        {
            currentState = State.FakeRetreat;
            timer        = enemyData.fakeRetreatDuration;

            Vector3 away = (transform.position - player.position).normalized;
            Vector3 dest = transform.position + away * 12f;
            if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                retreatDestination = hit.position;
            else
                retreatDestination = transform.position;
        }

        // ══════════════════════════════════════════════════
        //  ANIMATOR & AUDIO
        // ══════════════════════════════════════════════════
        void UpdateAnimator()
        {
            if (animator == null) return;

            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed",       speed);
            animator.SetBool("IsChasing",    currentState == State.Chase);
            animator.SetBool("IsSearching",  currentState == State.Search);
            animator.SetBool("IsStalking",   currentState == State.Stalk);
            animator.SetBool("IsAttacking",  currentState == State.Attacking);
        }

        void UpdateAudio()
        {
            if (audioSource == null) return;

            bool isChasing = currentState == State.Chase || currentState == State.Attacking;

            // Suara napas ghost
            if (breathClip != null)
            {
                breathTimer -= Time.deltaTime;
                if (breathTimer <= 0f && isChasing)
                {
                    audioSource.PlayOneShot(breathClip, 0.4f);
                    breathTimer = Random.Range(1.5f, 3f);
                }
            }

            // Suara footstep alert (ketika investigate)
            if (footstepClip != null && currentState == State.Investigate)
            {
                footTimer -= Time.deltaTime;
                if (footTimer <= 0f)
                {
                    audioSource.PlayOneShot(footstepClip, 0.25f);
                    footTimer = 0.5f;
                }
            }
        }

        void PlayAlert()
        {
            if (audioSource != null && alertClip != null)
                audioSource.PlayOneShot(alertClip, 0.6f);
        }

        void ApplyModelRotationOffset()
        {
            if (modelTransform == null) return;

            modelTransform.localRotation =
                Quaternion.Euler(modelRotationOffset);
        }

        // ── Helpers ───────────────────────────────────────
        void SetState(State s, float duration = 0f)
        {
            currentState = s;
            timer        = duration;
        }

        // ══════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════
        public void ForceChase()
        {
            currentState = State.Chase;
            agent.speed  = enemyData != null ? enemyData.chaseSpeed : 5f;
        }

        public bool IsChasing    => currentState == State.Chase || currentState == State.Attacking;
        public bool IsAware      => currentState != State.Patrol && currentState != State.Cooldown;

        // ══════════════════════════════════════════════════
        //  GIZMOS
        // ══════════════════════════════════════════════════
        void OnDrawGizmosSelected()
        {
            if (enemyData == null) return;

            // Hear radius — yellow
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, enemyData.hearRadius);

            // Sight range — red cone approximation
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyData.sightRange);
            Vector3 fwd    = transform.forward * enemyData.sightRange;
            float   half   = enemyData.sightAngle * 0.5f * Mathf.Deg2Rad;
            Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, enemyData.sightAngle * 0.5f, 0) * fwd);
            Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, -enemyData.sightAngle * 0.5f, 0) * fwd);

            // Light fear radius — cyan
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, enemyData.lightFearRadius);

            // Attack range — magenta
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);

            // Proximity chase — orange
            Gizmos.color = new Color(1f, .5f, 0f);
            Gizmos.DrawWireSphere(transform.position, proximityChaseRange);

            // Memory line
            if (hasMemoryOfPlayer)
            {
                Gizmos.color = new Color(1f, 0f, 1f, .6f);
                Gizmos.DrawLine(transform.position, lastKnownPos);
                Gizmos.DrawWireSphere(lastKnownPos, 0.4f);
            }

            // State label
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f,
                $"[{currentState}]  hp:{lightExposureTimer:F1}s");
#endif
        }
    }
}
