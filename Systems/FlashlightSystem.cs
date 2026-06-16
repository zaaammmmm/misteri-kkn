using UnityEngine;
using System.Collections;
using KKN.Game.Core;
using KKN.Game.Player;
using KKN.Game.Enemy;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Flashlight system with battery curve, tense reload ritual,
    /// and light combat mechanics. Core identity of the horror experience.
    ///
    /// Perbaikan dari versi lama:
    ///   C3 — ReloadEffects() dan CameraShake() dipanggil 2x di ReloadRoutine().
    ///         Versi baru hanya memanggil sekali di awal coroutine.
    ///   C6 — Tambah static property IsFlashlightOn untuk null-safe access
    ///         dari SanitySystem dan sistem lain tanpa duplikasi null-check.
    ///   C7 — cachedGhosts dihapus, diganti GhostRegistry.Active yang
    ///         selalu up-to-date tanpa perlu RefreshGhostCache().
    /// </summary>
    public class FlashlightSystem : MonoBehaviour
    {
        public static FlashlightSystem Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Light flashLight;

        [Tooltip("AudioSource lokal — HANYA dipakai untuk loop reloadLoopClip (butuh start/stop presisi). " +
                 "Untuk SFX one-shot (toggle, reload start) sekarang lewat AudioManager.")]
        [SerializeField] private AudioSource audioSource;

        [SerializeField] private AudioClip toggleClip;
        [SerializeField] private AudioClip reloadClip;
        [SerializeField] private AudioClip reloadLoopClip;
        [SerializeField] private Transform noiseOrigin;

        [Header("Battery")]
        [SerializeField] private float maxBattery     = 100f;
        [SerializeField] private float currentBattery = 100f;
        [SerializeField] private float drainRate      = 7f;

        [Header("Intensity Curve")]
        [Tooltip("Intensity at 100% battery")]
        [SerializeField] private float maxIntensity   = 3f;
        [Tooltip("Intensity at 40% battery")]
        [SerializeField] private float dimIntensity   = 1.2f;
        [Tooltip("Intensity at critical battery")]
        [SerializeField] private float minIntensity   = 0.3f;
        [Tooltip("Battery % where light starts dimming")]
        [SerializeField] private float dimThreshold      = 40f;
        [Tooltip("Battery % where flicker begins")]
        [SerializeField] private float criticalThreshold = 10f;

        [Header("Tense Reload")]
        [SerializeField] private float reloadDuration     = 2.2f;
        [SerializeField] private float reloadNoiseRadius  = 14f;
        [Tooltip("Player cannot sprint during reload")]
        [SerializeField] private bool restrictMovementDuringReload = true;

        [Header("Flicker")]
        [SerializeField] private float flickerSpeed      = 0.08f;
        [SerializeField] private float heavyFlickerSpeed = 0.04f;

        [Header("Light Combat")]
        [Tooltip("Beam exposure accumulates on ghost while aimed")]
        [SerializeField] private bool enableLightCombat = true;

        [SerializeField] private Light fillLight;

        // ─────────────────────────────────────────────────────────────────
        //  RUNTIME STATE
        // ─────────────────────────────────────────────────────────────────

        private bool      isOn        = true;
        private bool      isReloading = false;
        private float     flickerTimer;
        // private Coroutine reloadEffectRoutine;

        // C7 — cachedGhosts dihapus sepenuhnya.
        //      GhostRegistry.Active dipakai langsung di EmitNoise().

        // ─────────────────────────────────────────────────────────────────
        //  EVENTS
        // ─────────────────────────────────────────────────────────────────

        public event System.Action OnFlashlightToggled;
        public event System.Action OnReloadStarted;
        public event System.Action OnReloadCompleted;

        // ─────────────────────────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────────────────────────

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
            currentBattery = maxBattery;
            ApplyLightState();

            // C7 — Tidak ada FindObjectsByType di sini.
            //      GhostRegistry.Active selalu up-to-date via GhostAI.OnEnable/OnDisable.
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            HandleInput();
            DrainBattery();
            UpdateIntensityCurve();
            HandleCriticalFlicker();
        }

        void AutoAssign()
        {
            if (flashLight == null)
                flashLight = GetComponentInChildren<Light>();

            if (fillLight == null)
            {
                var lights = GetComponentsInChildren<Light>();

                if (lights.Length > 1)
                    fillLight = lights[1];
            }
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            // audioSource khusus untuk reloadLoopClip — pastikan tidak auto-play
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake  = false;
            audioSource.loop         = false;

            if (noiseOrigin == null) noiseOrigin = transform;

            if (flashLight == null)
                Debug.LogWarning("[FlashlightSystem] Light not found. Assign in Inspector.");
        }

        // ─────────────────────────────────────────────────────────────────
        //  INPUT
        // ─────────────────────────────────────────────────────────────────

        void HandleInput()
        {
            if (PlayerState.Instance != null && PlayerState.Instance.IsFrozen) return;
            if (InputManager.Instance == null) return;

            if (InputManager.Instance.GetFlashlightDown() && !isReloading)
                ToggleFlashlight();

            if (InputManager.Instance.GetReloadDown() && !isReloading)
                StartCoroutine(ReloadRoutine());
        }

        void ToggleFlashlight()
        {
            if (currentBattery <= 0f) return;

            isOn = !isOn;
            ApplyLightState();

            // SFX 2D — feedback UI, tidak dipengaruhi posisi pemain
            AudioManager.Instance?.PlaySFX2D(toggleClip, volume: 0.9f);

            OnFlashlightToggled?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────
        //  BATTERY
        // ─────────────────────────────────────────────────────────────────

        void DrainBattery()
        {
            if (!isOn || isReloading) return;

            currentBattery -= drainRate * Time.deltaTime;

            if (currentBattery <= 0f)
            {
                currentBattery = 0f;
                isOn = false;
                ApplyLightState();
            }
        }

        void UpdateIntensityCurve()
        {
            if (flashLight == null || !flashLight.enabled) return;

            float percent = currentBattery / maxBattery * 100f;
            float targetIntensity;

            if (percent > dimThreshold)
            {
                float t = (percent - dimThreshold) / (100f - dimThreshold);
                targetIntensity = Mathf.Lerp(dimIntensity, maxIntensity, t);
            }
            else if (percent > criticalThreshold)
            {
                float t = (percent - criticalThreshold) / (dimThreshold - criticalThreshold);
                targetIntensity = Mathf.Lerp(minIntensity, dimIntensity, t);
            }
            else
            {
                targetIntensity = minIntensity;
            }

            flashLight.intensity = Mathf.Lerp(flashLight.intensity, targetIntensity, Time.deltaTime * 5f);
        }

        // ─────────────────────────────────────────────────────────────────
        //  RELOAD
        // ─────────────────────────────────────────────────────────────────

        IEnumerator ReloadRoutine()
        {
            isReloading = true;

            isOn = false;
            ApplyLightState();

            OnReloadStarted?.Invoke();

            AudioManager.Instance?.PlaySFX(
                reloadClip,
                noiseOrigin.position,
                1f
            );

            EmitNoise(reloadNoiseRadius);

            yield return new WaitForSeconds(reloadDuration);

            currentBattery = maxBattery;

            isOn = true;
            isReloading = false;

            ApplyLightState();

            OnReloadCompleted?.Invoke();
        }

        // IEnumerator ReloadEffects()
        // {
        //     if (flashLight == null) yield break;

        //     float originalIntensity = flashLight.intensity;
        //     flashLight.enabled = true;

        //     float timer = 0f;
        //     while (timer < reloadDuration * 0.8f)
        //     {
        //         timer += Time.deltaTime;
        //         flashLight.intensity = Random.Range(0.05f, originalIntensity);
        //         yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
        //     }

        //     flashLight.intensity = 0f;
        //     flashLight.enabled   = false;
        // }

        // IEnumerator CameraShake()
        // {
        //     Camera cam = Camera.main;
        //     if (cam == null) yield break;

        //     Vector3 originalPos = cam.transform.localPosition;
        //     float   duration    = 0.35f;
        //     float   strength    = 0.015f;
        //     float   timer       = 0f;

        //     while (timer < duration)
        //     {
        //         timer += Time.deltaTime;
        //         cam.transform.localPosition = originalPos + new Vector3(
        //             Random.Range(-strength, strength),
        //             Random.Range(-strength, strength),
        //             0f
        //         );
        //         yield return null;
        //     }

        //     cam.transform.localPosition = originalPos;
        // }

        // ─────────────────────────────────────────────────────────────────
        //  FLICKER
        // ─────────────────────────────────────────────────────────────────

        void HandleCriticalFlicker()
        {
            if (!isOn || isReloading || flashLight == null) return;
            if (currentBattery / maxBattery * 100f > criticalThreshold) return;

            float speed = currentBattery / maxBattery * 100f < 5f
                ? heavyFlickerSpeed
                : flickerSpeed;

            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flashLight.enabled = !flashLight.enabled;
                flickerTimer = speed + Random.Range(0f, 0.05f);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  NOISE EMISSION
        // ─────────────────────────────────────────────────────────────────

        void EmitNoise(float radius)
        {
            if (noiseOrigin == null) return;

            // C7 — GhostRegistry.Active menggantikan cachedGhosts.
            //      Tidak perlu RefreshGhostCache() — registry selalu current.
            foreach (var ghost in GhostRegistry.Active)
            {
                if (ghost != null)
                    ghost.HearSound(noiseOrigin.position, radius);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  LIGHT STATE
        // ─────────────────────────────────────────────────────────────────

        void ApplyLightState()
        {
            bool enabledState =
                isOn &&
                !isReloading &&
                currentBattery > 0f;

            if (flashLight != null)
                flashLight.enabled = enabledState;

            if (fillLight != null)
                fillLight.enabled = enabledState;
        }

        // ─────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// C6 — Static null-safe property untuk dipakai SanitySystem dan sistem lain.
        /// Menggantikan pola berulang "Instance != null &amp;&amp; Instance.IsOn()"
        /// yang tidak mengecek baterai habis.
        /// </summary>
        public static bool IsFlashlightOn
            => Instance != null && Instance.isOn && Instance.currentBattery > 0f;

        public bool IsOn()             => isOn;
        public bool IsReloading()      => isReloading;
        public float BatteryPercent()  => currentBattery / maxBattery;
        public float BatteryValue()    => currentBattery;

        public void ForceTurnOff()
        {
            isOn = false;
            ApplyLightState();
        }

        // C7 — RefreshGhostCache() dihapus. GhostRegistry menanganinya otomatis.
    }
}
