using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using KKN.Game.Systems;
using KKN.Game.Core;
using KKN.Game.Data;
using KKN.Game.Enemy;

namespace KKN.Game.Puzzle
{
    /// <summary>
    /// Generator puzzle — harus diisi semua bahan (itemType == "Material")
    /// yang didaftarkan di requiredMaterials sebelum bisa dinyalakan.
    ///
    /// ══════════════════════════════════════════════════════════════════════
    ///  PERUBAHAN v2 — Integrasi KKN Scene System
    /// ══════════════════════════════════════════════════════════════════════
    ///  DIHAPUS  : EndingManager.Instance?.TriggerEnding()
    ///  DIGANTI  : KKN_SceneManager.Instance?.GoToEnding()
    ///             + KKN_GameplayController.Instance?.NotifyGeneratorOn()
    ///
    ///  Alasan   : EndingManager tidak lagi diperlukan sebagai perantara.
    ///             KKN_SceneManager adalah satu-satunya authority untuk
    ///             navigasi antar scene. KKN_GameplayController menerima
    ///             notifikasi agar SaveSystem dan state-nya tetap sinkron.
    ///
    /// ══════════════════════════════════════════════════════════════════════
    ///  PERUBAHAN v3 — Integrasi AudioManager
    /// ══════════════════════════════════════════════════════════════════════
    ///  - failClip / startupClip   : sekarang lewat AudioManager.PlaySFX (3D,
    ///                                di posisi generator) — tidak lagi
    ///                                audioSource.PlayOneShot lokal.
    ///  - humLoop                  : sekarang lewat AudioManager.PlayAmbience
    ///                                (crossfade dari ambience desa ke hum
    ///                                generator), dipanggil setelah startupClip
    ///                                selesai via coroutine delay.
    ///  - generatorStartingClip    : TETAP pakai audioSource lokal karena
    ///                                butuh start/cancel/complete presisi
    ///                                selama hold interaction.
    ///
    /// Cara setup di Inspector (tidak berubah):
    ///   1. Tambahkan script ini pada prefab Generator.
    ///   2. Isi requiredMaterials dengan ItemData bahan (Gasoline, Oil, Cable, Gear, dst).
    ///   3. Isi objective text sesuai kebutuhan.
    ///   4. Assign animasi, suara, dan efek partikel.
    ///   5. Pastikan Collider ada dan Layer = "Interactable".
    ///
    /// Flow:
    ///   Hover    → tampilkan bahan yang masih kurang (via ObjectiveManager)
    ///   Interact → cek semua bahan
    ///              → jika lengkap  : mulai hold timer
    ///              → jika kurang   : tampilkan objective "kurang bahan" + SFX failClip
    ///   HoldComplete → PowerOnGenerator()
    ///                  → SFX startupClip (3D) + Ambience humLoop (delayed)
    ///                  → KKN_GameplayController.NotifyGeneratorOn()  [save + state]
    ///                  → KKN_SceneManager.GoToEnding()               [scene transition]
    /// </summary>
    public class GeneratorPuzzle : MonoBehaviour, IInteractable, IHoldInteractable
    {
        // ══════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════

        [Header("Required Materials")]
        [Tooltip("Daftarkan semua ItemData dengan itemType 'Material' yang dibutuhkan")]
        [SerializeField] private List<ItemData> requiredMaterials = new();

        [Tooltip("Apakah bahan dikonsumsi (dihapus dari inventory) setelah generator menyala?")]
        [SerializeField] private bool consumeMaterials = true;

        [Header("Interact Texts")]
        [SerializeField] private string interactTextReady   = "[HOLD E] Nyalakan Generator";
        [SerializeField] private string interactTextMissing = "[E] Generator (bahan kurang)";
        [SerializeField] private string interactTextDone    = "Generator Sudah Menyala";

        [Header("Objective Texts")]
        [TextArea(2, 3)]
        [SerializeField] private string objectiveTextIncomplete =
            "Kumpulkan semua bahan untuk menyalakan generator.";

        [TextArea(2, 3)]
        [SerializeField] private string objectiveTextComplete =
            "Generator berhasil dinyalakan! Cari jalan keluar.";

        [Header("Visual & Audio")]
        [SerializeField] private Animator    generatorAnimator;

        [Tooltip("AudioSource lokal — HANYA dipakai untuk loop generatorStartingClip saat hold. " +
                 "startupClip/failClip/humLoop sekarang lewat AudioManager.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("SFX 3D di posisi generator saat berhasil menyala")]
        [SerializeField] private AudioClip   startupClip;

        [Tooltip("SFX 3D di posisi generator saat interact gagal (bahan kurang)")]
        [SerializeField] private AudioClip   failClip;

        [Tooltip("Ambience hum generator pasca menyala — crossfade dari ambience desa via AudioManager.PlayAmbience")]
        [SerializeField] private AudioClip   humLoop;

        [SerializeField] private GameObject  powerOnEffect;
        [SerializeField] private Light[]     lightsToEnable;

        [Header("Generator Start Sound")]
        [Tooltip("Loop lokal selama hold-to-start — TETAP pakai audioSource (butuh start/cancel/complete presisi)")]
        [SerializeField] private AudioClip generatorStartingClip;

        [Header("Auto Objective on Start")]
        [SerializeField] private bool showObjectiveOnStart = true;

        [Header("Hold Interaction")]
        [SerializeField] private float holdDuration = 5f;

        [Header("Ghost Detection")]
        [SerializeField] private float generatorNoiseRadius = 100f;

        // ─── Scene Transition Timing ───────────────────────────────────
        [Header("Scene Transition (KKN Scene System)")]
        [Tooltip(
            "Jeda dalam detik antara generator menyala dan fade ke Scene Ending.\n" +
            "Beri waktu cukup untuk animasi & audio generator selesai.\n" +
            "Default: 4 detik."
        )]
        [SerializeField] private float endingTransitionDelay = 4f;

        // ══════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════

        private bool _isPowered       = false;
        private bool _isHolding       = false;
        private bool _hasAlertedGhosts = false;

        // ══════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════

        void Start()
        {
            foreach (var light in lightsToEnable)
                if (light != null) light.enabled = false;

            if (showObjectiveOnStart && !_isPowered)
                ObjectiveManager.Instance?.SetObjective(objectiveTextIncomplete);
        }

        // ══════════════════════════════════════════════════
        //  IHoldInteractable
        // ══════════════════════════════════════════════════

        public float GetHoldDuration() => holdDuration;

        public bool CanStartHold()
        {
            if (_isPowered) return false;
            return GetMissingMaterials().Count == 0;
        }

        public void OnHoldStart()
        {
            if (_isPowered || _isHolding) return;
            if (GetMissingMaterials().Count > 0) return;

            _isHolding = true;

            // Alert ghosts sekali saat hold dimulai
            if (!_hasAlertedGhosts)
            {
                _hasAlertedGhosts = true;
                AlertGhosts();
            }

            // Loop "starting" lokal — butuh kontrol start/cancel/complete presisi
            if (audioSource != null && generatorStartingClip != null)
            {
                audioSource.clip = generatorStartingClip;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        public void OnHoldCancel()
        {
            if (_isPowered) return;
            _isHolding = false;
            audioSource?.Stop();
        }

        public void OnHoldComplete()
        {
            if (_isPowered) return;
            _isHolding = false;
            audioSource?.Stop();
            PowerOnGenerator();
        }

        // ══════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════

        public void Interact()
        {
            if (_isPowered) return;

            var missing = GetMissingMaterials();
            if (missing.Count > 0)
            {
                ObjectiveManager.Instance?.SetObjective(BuildMissingText(missing));

                // SFX 3D di posisi generator — "gagal" saat bahan kurang
                AudioManager.Instance?.PlaySFX(failClip, transform.position, 1f);
            }
            // Jika bahan lengkap, sistem hold yang menangani — tidak ada aksi instan di sini.
        }

        public string GetInteractText()
        {
            if (_isPowered)                          return interactTextDone;
            if (GetMissingMaterials().Count == 0)    return interactTextReady;
            return interactTextMissing;
        }

        public void OnHoverEnter()
        {
            if (_isPowered) return;
            var missing = GetMissingMaterials();
            if (missing.Count > 0)
                ObjectiveManager.Instance?.SetObjective(BuildMissingText(missing));
        }

        public void OnHoverExit() { }

        // ══════════════════════════════════════════════════
        //  MATERIAL CHECK
        // ══════════════════════════════════════════════════

        private List<ItemData> GetMissingMaterials()
        {
            var missing = new List<ItemData>();
            if (InventorySystem.Instance == null) return missing;

            foreach (var mat in requiredMaterials)
            {
                if (mat == null) continue;
                if (!InventorySystem.Instance.HasItem(mat.itemID))
                    missing.Add(mat);
            }
            return missing;
        }

        private string BuildMissingText(List<ItemData> missing)
        {
            if (missing.Count == 0) return objectiveTextIncomplete;

            var names = new List<string>();
            foreach (var item in missing)
                if (item != null) names.Add(item.displayName);

            return $"Generator membutuhkan:\n[{string.Join(" | ", names)}]";
        }

        // ══════════════════════════════════════════════════
        //  POWER ON
        // ══════════════════════════════════════════════════

        private void PowerOnGenerator()
        {
            _isPowered = true;

            // 1. Konsumsi bahan dari inventory
            if (consumeMaterials && InventorySystem.Instance != null)
                foreach (var mat in requiredMaterials)
                    if (mat != null) InventorySystem.Instance.RemoveItem(mat.itemID);

            // 2. Animasi generator
            if (generatorAnimator != null)
                generatorAnimator.SetTrigger("PowerOn");
            else
                Debug.LogWarning("[GeneratorPuzzle] Generator Animator belum di-assign.");

            // 3. SFX startup — 3D di posisi generator
            AudioManager.Instance?.PlaySFX(startupClip, transform.position, 1f);

            // 4. Ambience hum generator — crossfade dari ambience desa ke hum generator,
            //    dimulai setelah startupClip selesai bermain.
            if (humLoop != null)
            {
                float delay = startupClip != null ? startupClip.length : 0f;
                StartCoroutine(PlayHumAmbienceDelayed(delay));
            }

            // 5. Efek visual
            if (powerOnEffect != null) powerOnEffect.SetActive(true);

            // 6. Nyalakan lampu
            foreach (var light in lightsToEnable)
                if (light != null) light.enabled = true;

            // 7. Update objective
            ObjectiveManager.Instance?.SetObjective(objectiveTextComplete);
            ObjectiveManager.Instance?.NextStep();

            // ─── [CHANGED] ────────────────────────────────────────────────────
            // Lama : EndingManager.Instance?.TriggerEnding()
            // Baru : Notifikasi ke KKN_GameplayController (untuk SaveGame & state)
            //        lalu KKN_SceneManager menangani transisi scene.
            // ──────────────────────────────────────────────────────────────────
            KKN_GameplayController gameplay = FindFirstObjectByType<KKN_GameplayController>();
            gameplay?.NotifyGeneratorOn();

            Debug.Log("[GeneratorPuzzle] Generator menyala — memulai EndSequence.");
            StartCoroutine(EndSequence());
        }

        /// <summary>
        /// Tunggu sampai SFX startup selesai, lalu mulai ambience hum generator
        /// (crossfade menggantikan ambience desa yang sedang main).
        /// </summary>
        private IEnumerator PlayHumAmbienceDelayed(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            AudioManager.Instance?.PlayAmbience(humLoop, fadeDuration: 3f);
        }

        // ══════════════════════════════════════════════════
        //  END SEQUENCE
        // ══════════════════════════════════════════════════

        private IEnumerator EndSequence()
        {
            yield return new WaitForSeconds(endingTransitionDelay);

            if (KKN_SceneManager.Instance != null)
            {
                KKN_SceneManager.Instance.LoadSceneWithLoading("Ending");
            }
            else
            {
                // Fallback jika singleton belum ada (tidak seharusnya terjadi di production)
                Debug.LogError(
                    "[GeneratorPuzzle] KKN_SceneManager.Instance adalah null! " +
                    "Pastikan KKN_Bootstrap ada di scene Gameplay."
                );
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    KKN_SceneManager.SceneNames.Ending
                );
            }
        }

        // ══════════════════════════════════════════════════
        //  GHOST ALERT
        // ══════════════════════════════════════════════════

        private void AlertGhosts()
        {
            GhostAI[] ghosts = FindObjectsOfType<GhostAI>();
            foreach (GhostAI ghost in ghosts)
                ghost?.HearSound(transform.position, generatorNoiseRadius);
        }

        // ══════════════════════════════════════════════════
        //  GIZMO
        // ══════════════════════════════════════════════════

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = _isPowered ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(0.8f, 0.8f, 0.8f));

            // Visualisasikan radius suara generator
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, generatorNoiseRadius);
        }
#endif
    }
}