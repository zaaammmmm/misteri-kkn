using System.Collections.Generic;
using UnityEngine;
using KKN.Game.Enemy;

namespace KKN.Game.Core
{
    /// <summary>
    /// Static registry untuk semua GhostAI yang aktif di scene.
    ///
    /// Menggantikan pola FindObjectsByType<GhostAI> yang stale di:
    ///   - SanitySystem.cachedGhosts
    ///   - FlashlightSystem.cachedGhosts
    ///
    /// Ghost mendaftarkan diri via OnEnable/OnDisable — otomatis
    /// menangani spawn dinamis, despawn, dan scene reload.
    ///
    /// Cara pakai:
    ///   foreach (var ghost in GhostRegistry.Active) { ... }
    ///
    /// CATATAN: Ini static class murni — tidak butuh MonoBehaviour,
    /// tidak butuh GameObject di scene, tidak ada DontDestroyOnLoad.
    /// Dibersihkan otomatis via ClearAll() saat scene unload
    /// (dipanggil dari GameManager.OnSceneUnloaded).
    /// </summary>
    public static class GhostRegistry
    {
        // ─────────────────────────────────────────────────────────────────
        //  INTERNAL STATE
        // ─────────────────────────────────────────────────────────────────

        private static readonly List<GhostAI> _active = new List<GhostAI>(8);

        // Read-only view — caller tidak bisa modify list langsung.
        // Gunakan foreach di atas ini, bukan index loop, karena list
        // bisa berubah jika ghost die/spawn saat iterasi (lihat SafeActive).
        public static IReadOnlyList<GhostAI> Active => _active;

        // ─────────────────────────────────────────────────────────────────
        //  EVENTS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Dipanggil saat ghost baru terdaftar (spawn / enable).</summary>
        public static event System.Action<GhostAI> OnGhostRegistered;

        /// <summary>Dipanggil saat ghost dihapus dari registry (destroy / disable).</summary>
        public static event System.Action<GhostAI> OnGhostUnregistered;

        /// <summary>Jumlah ghost aktif saat ini.</summary>
        public static int Count => _active.Count;

        // ─────────────────────────────────────────────────────────────────
        //  REGISTRATION
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Daftarkan ghost ke registry.
        /// Dipanggil dari GhostAI.OnEnable().
        /// </summary>
        public static void Register(GhostAI ghost)
        {
            if (ghost == null || _active.Contains(ghost)) return;

            _active.Add(ghost);
            OnGhostRegistered?.Invoke(ghost);

#if UNITY_EDITOR
            Debug.Log($"[GhostRegistry] Registered '{ghost.gameObject.name}'. Total: {_active.Count}");
#endif
        }

        /// <summary>
        /// Hapus ghost dari registry.
        /// Dipanggil dari GhostAI.OnDisable().
        /// </summary>
        public static void Unregister(GhostAI ghost)
        {
            if (ghost == null || !_active.Contains(ghost)) return;

            _active.Remove(ghost);
            OnGhostUnregistered?.Invoke(ghost);

#if UNITY_EDITOR
            Debug.Log($"[GhostRegistry] Unregistered '{ghost.gameObject.name}'. Total: {_active.Count}");
#endif
        }

        // ─────────────────────────────────────────────────────────────────
        //  UTILITIES
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Copy snapshot list yang aman untuk iterasi ketika ada kemungkinan
        /// ghost berubah di tengah loop (misalnya OnGhostRegistered trigger
        /// spawn lagi). Gunakan ini HANYA jika benar-benar butuh — lebih berat
        /// dari iterasi langsung atas Active.
        /// </summary>
        public static List<GhostAI> GetSnapshot()
        {
            // Bersihkan entri null dulu (ghost di-destroy tanpa OnDisable)
            _active.RemoveAll(g => g == null);
            return new List<GhostAI>(_active);
        }

        /// <summary>
        /// Hapus semua entri, termasuk yang null.
        /// Dipanggil oleh GameManager saat scene unload untuk menghindari
        /// referensi stale ke object yang sudah destroyed.
        /// </summary>
        public static void ClearAll()
        {
            _active.Clear();
#if UNITY_EDITOR
            Debug.Log("[GhostRegistry] Cleared — scene unloaded.");
#endif
        }

        /// <summary>
        /// Bersihkan entri null yang terjadi jika Destroy() dipanggil
        /// tanpa melewati OnDisable (edge case saat game over / scene reload).
        /// Dipanggil secara berkala dari GameManager.
        /// </summary>
        public static void PurgeDestroyed()
        {
            int before = _active.Count;
            _active.RemoveAll(g => g == null);
            int removed = before - _active.Count;

#if UNITY_EDITOR
            if (removed > 0)
                Debug.Log($"[GhostRegistry] Purged {removed} destroyed ghost(s).");
#endif
        }

        /// <summary>
        /// Cek apakah ghost tertentu sudah terdaftar.
        /// Berguna untuk guard di sistem lain.
        /// </summary>
        public static bool Contains(GhostAI ghost) => ghost != null && _active.Contains(ghost);

        /// <summary>
        /// Cari ghost terdekat dari posisi tertentu.
        /// Shortcut untuk SanitySystem / objective system.
        /// </summary>
        public static GhostAI GetNearest(Vector3 position)
        {
            GhostAI nearest = null;
            float   minDist = float.MaxValue;

            foreach (var ghost in _active)
            {
                if (ghost == null) continue;
                float d = Vector3.Distance(position, ghost.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = ghost;
                }
            }

            return nearest;
        }
    }
}
