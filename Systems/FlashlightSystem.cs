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
    /// </summary>
    public class FlashlightSystem : MonoBehaviour
    {
        public static FlashlightSystem Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Light flashLight;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip toggleClip;
        [SerializeField] private AudioClip reloadClip;
        [SerializeField] private AudioClip reloadLoopClip;
        [SerializeField] private Transform noiseOrigin;

        [Header("Battery")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float currentBattery = 100f;
        [SerializeField] private float drainRate = 7f;

        [Header("Intensity Curve")]
        [Tooltip("Intensity at 100% battery")]
        [SerializeField] private float maxIntensity = 3f;
        [Tooltip("Intensity at 40% battery")]
        [SerializeField] private float dimIntensity = 1.2f;
        [Tooltip("Intensity at critical battery")]
        [SerializeField] private float minIntensity = 0.3f;
        [Tooltip("Battery % where light starts dimming")]
        [SerializeField] private float dimThreshold = 40f;
        [Tooltip("Battery % where flicker begins")]
        [SerializeField] private float criticalThreshold = 10f;

        [Header("Tense Reload")]
        [SerializeField] private float reloadDuration = 2.2f;
        [SerializeField] private float reloadNoiseRadius = 14f;
        [Tooltip("Player cannot sprint during reload")]
        [SerializeField] private bool restrictMovementDuringReload = true;

        [Header("Flicker")]
        [SerializeField] private float flickerSpeed = 0.08f;
        [SerializeField] private float heavyFlickerSpeed = 0.04f;

        [Header("Light Combat")]
        [Tooltip("Beam exposure accumulates on ghost while aimed")]
        [SerializeField] private bool enableLightCombat = true;

        private bool isOn = true;
        private bool isReloading = false;
        private float flickerTimer;
        private GhostAI[] cachedGhosts;

        // Events
        public event System.Action OnFlashlightToggled;
        public event System.Action OnReloadStarted;
        public event System.Action OnReloadCompleted;

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
            cachedGhosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
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
            if (flashLight == null) flashLight = GetComponentInChildren<Light>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            if (noiseOrigin == null) noiseOrigin = transform;

            if (flashLight == null)
                Debug.LogWarning("[FlashlightSystem] Light not found assign in Inspector.");
        }

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

            if (toggleClip != null && audioSource != null)
                audioSource.PlayOneShot(toggleClip);

            OnFlashlightToggled?.Invoke();
        }

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

        IEnumerator ReloadRoutine()
        {
            isReloading = true;
            isOn = false;
            ApplyLightState();

            OnReloadStarted?.Invoke();

            if (restrictMovementDuringReload && PlayerState.Instance != null)
                PlayerState.Instance.IsReloading = true;

            if (reloadClip != null && audioSource != null)
                audioSource.PlayOneShot(reloadClip);

            if (reloadLoopClip != null && audioSource != null)
            {
                audioSource.clip = reloadLoopClip;
                audioSource.loop = true;
                audioSource.Play();
            }

            EmitNoise(reloadNoiseRadius);

            float timer = 0f;
            while (timer < reloadDuration)
            {
                timer += Time.deltaTime;

                if (timer > reloadDuration * 0.3f && timer < reloadDuration * 0.7f)
                {
                    EmitNoise(reloadNoiseRadius * 0.5f);
                }

                yield return null;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }

            currentBattery = maxBattery;
            isOn = true;
            isReloading = false;
            ApplyLightState();

            if (restrictMovementDuringReload && PlayerState.Instance != null)
                PlayerState.Instance.IsReloading = false;

            OnReloadCompleted?.Invoke();
        }

        void HandleCriticalFlicker()
        {
            if (!isOn || isReloading || flashLight == null) return;
            if (currentBattery / maxBattery * 100f > criticalThreshold) return;

            float speed = currentBattery / maxBattery * 100f < 5f ? heavyFlickerSpeed : flickerSpeed;

            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flashLight.enabled = !flashLight.enabled;
                flickerTimer = speed + Random.Range(0f, 0.05f);
            }
        }

        void EmitNoise(float radius)
        {
            if (cachedGhosts == null || noiseOrigin == null) return;

            foreach (var ghost in cachedGhosts)
            {
                if (ghost != null)
                    ghost.HearSound(noiseOrigin.position, radius);
            }
        }

        public void RefreshGhostCache()
        {
            cachedGhosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
        }

        void ApplyLightState()
        {
            if (flashLight == null) return;
            flashLight.enabled = isOn && !isReloading && currentBattery > 0f;
        }

        public bool IsOn() => isOn;
        public bool IsReloading() => isReloading;
        public float BatteryPercent() => currentBattery / maxBattery;
        public float BatteryValue() => currentBattery;

        public void ForceTurnOff()
        {
            isOn = false;
            ApplyLightState();
        }
    }
}

