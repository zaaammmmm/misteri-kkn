using UnityEngine;
using KKN.Game.Systems;

#if UNITY_POST_PROCESSING
using UnityEngine.Rendering.PostProcessing;
#endif

namespace KKN.Game.Player
{
    public class LowSanityEffects : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────
        //  INSPECTOR
        // ─────────────────────────────────────────────────────────────────

        [Header("Shake — Position")]
        [Tooltip("Amplitudo shake posisi maksimum saat sanity nol")]
        [SerializeField] private float maxShakeAmplitude = 0.06f;
        [Tooltip("Frekuensi Perlin noise. Lebih tinggi = shake lebih cepat / frenetik")]
        [SerializeField] private float shakeFrequency    = 8f;
        [Tooltip("Sanity threshold (0–1) saat shake mulai terasa")]
        [SerializeField] private float shakeThreshold    = 0.40f;

        [Header("Shake — Tilt (rotasi Z)")]
        [Tooltip("Max tilt kamera kiri-kanan dalam derajat")]
        [SerializeField] private float maxTiltDegrees    = 2.5f;
        [Tooltip("Frekuensi tilt Perlin — beda dari posisi agar tidak sinkron")]
        [SerializeField] private float tiltFrequency     = 3f;

        [Header("Transition")]
        [Tooltip("Kecepatan lerp shake masuk/keluar")]
        [SerializeField] private float transitionSpeed   = 6f;

        [Header("Breakdown")]
        [SerializeField] private float breakdownShakeMultiplier = 3.5f;
        [SerializeField] private float breakdownTiltMultiplier  = 4f;
        [Tooltip("Kurva shake selama breakdown (X: progress 0–1, Y: multiplier 0–1)")]
        [SerializeField] private AnimationCurve breakdownCurve;
        [Header("FOV Breathing")]
        [SerializeField] private Camera playerCamera;

        [SerializeField] private float fovAmplitude = 2f;
        [SerializeField] private float fovFrequency = 0.8f;

        [Header("Fade to Black (sanity kritis)")]
        [Tooltip("Sanity threshold (0–1) saat vignette mulai menutup layar")]
        [SerializeField] private float fadeThreshold = 0.10f;
        [SerializeField] private CanvasGroup fadeOverlay;  // Image hitam di atas Canvas

#if UNITY_POST_PROCESSING
        [Header("Post Processing")]
        [SerializeField] private PostProcessVolume postProcessVolume;

        private Vignette         _vignette;
        private ChromaticAberration _chromatic;
        private LensDistortion   _lensDistortion;
#endif

        // ─────────────────────────────────────────────────────────────────
        //  RUNTIME
        // ─────────────────────────────────────────────────────────────────

        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;

        // Sanity yang di-smooth untuk transisi mulus, tidak reaktif ke spike sesaat
        private float _smoothedSanity    = 1f;
        private float _currentShakePower = 0f;

        // Seed offset agar tiap instance berbeda
        private float _noiseOffsetX;
        private float _noiseOffsetY;
        private float _noiseOffsetTilt;

        // Breakdown state
        private bool  _isInBreakdown;
        private float _breakdownProgress;

        // Cached sanity dari event — thread-safe, tidak perlu query tiap frame
        private float _latestSanityPercent = 1f;
        private float baseFOV;
        // ─────────────────────────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────────────────────────

        void Start()
        {
            baseFOV = playerCamera.fieldOfView;

            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;

            // Random seed agar tiap sesi terasa berbeda
            _noiseOffsetX    = Random.Range(0f, 100f);
            _noiseOffsetY    = Random.Range(100f, 200f);
            _noiseOffsetTilt = Random.Range(200f, 300f);

            if (SanitySystem.Instance != null)
            {
                SanitySystem.Instance.OnBreakdownStarted += OnBreakdownStart;
                SanitySystem.Instance.OnBreakdownEnded   += OnBreakdownEnd;
                SanitySystem.Instance.OnSanityChanged    += OnSanityUpdated;

                _latestSanityPercent = SanitySystem.Instance.Percent();
                _smoothedSanity      = _latestSanityPercent;
            }

#if UNITY_POST_PROCESSING
            InitPostProcessing();
#endif
        }

        void OnDestroy()
        {
            if (SanitySystem.Instance != null)
            {
                SanitySystem.Instance.OnBreakdownStarted -= OnBreakdownStart;
                SanitySystem.Instance.OnBreakdownEnded   -= OnBreakdownEnd;
                SanitySystem.Instance.OnSanityChanged    -= OnSanityUpdated;
            }
        }

        void Update()
        {
            UpdateFOVBreathing();
            // Smooth sanity untuk mencegah efek reaktif ke perubahan sesaat
            _smoothedSanity = Mathf.Lerp(_smoothedSanity, _latestSanityPercent,
                                          Time.deltaTime * transitionSpeed);

            UpdateShake();
            UpdatePostProcessing();
            UpdateFadeOverlay();

            if (_isInBreakdown)
                _breakdownProgress = Mathf.Clamp01(_breakdownProgress + Time.deltaTime / 8f);
        }

        // ─────────────────────────────────────────────────────────────────
        //  SHAKE
        // ─────────────────────────────────────────────────────────────────
        void UpdateFOVBreathing()
        {
            if(playerCamera == null)
                return;

            float insanity = 1f - _smoothedSanity;

            float breathing =
                Mathf.Sin(Time.time * fovFrequency)
                * fovAmplitude
                * insanity;

            playerCamera.fieldOfView =
                baseFOV + breathing;
        }
        void UpdateShake()
        {
            float targetPower = CalculateShakePower();
            _currentShakePower = Mathf.Lerp(_currentShakePower, targetPower,
                                             Time.deltaTime * transitionSpeed);

            if (_currentShakePower < 0.0005f)
            {
                // Kembalikan ke posisi/rotasi asli secara halus
                transform.localPosition = Vector3.Lerp(transform.localPosition,
                                                        _baseLocalPos, Time.deltaTime * transitionSpeed);
                transform.localRotation = Quaternion.Slerp(transform.localRotation,
                                                             _baseLocalRot, Time.deltaTime * transitionSpeed);
                return;
            }

            float time = Time.time;

            // Perlin noise untuk posisi (X dan Y independen agar terasa alami)
            float px = (Mathf.PerlinNoise((time * shakeFrequency) + _noiseOffsetX, 0f) - 0.5f) * 2f;
            float py = (Mathf.PerlinNoise(0f, (time * shakeFrequency) + _noiseOffsetY) - 0.5f) * 2f;

            Vector3 shakeOffset = new Vector3(px, py, 0f) * _currentShakePower;
            transform.localPosition = _baseLocalPos + shakeOffset;

            // Tilt independen — frekuensi lebih rendah, terasa seperti disorientasi
            float tiltNoise = (Mathf.PerlinNoise((time * tiltFrequency) + _noiseOffsetTilt, 0f) - 0.5f) * 2f;
            float tiltPower = _isInBreakdown
                ? maxTiltDegrees * breakdownTiltMultiplier
                : maxTiltDegrees * (1f - (_smoothedSanity / shakeThreshold));
            tiltPower = Mathf.Max(0f, tiltPower);

            Quaternion tiltRot = Quaternion.Euler(0f, 0f, tiltNoise * tiltPower);
            transform.localRotation = _baseLocalRot * tiltRot;
        }

        float CalculateShakePower()
        {
            if (_isInBreakdown)
            {
                float curveValue = breakdownCurve != null
                    ? breakdownCurve.Evaluate(_breakdownProgress)
                    : (1f - _breakdownProgress);
                return maxShakeAmplitude * breakdownShakeMultiplier * curveValue;
            }

            if (_smoothedSanity < shakeThreshold)
            {
                // t = 0 saat sanity = shakeThreshold, t = 1 saat sanity = 0
                float t = 1f - (_smoothedSanity / shakeThreshold);
                return t * maxShakeAmplitude;
            }

            return 0f;
        }

        // ─────────────────────────────────────────────────────────────────
        //  POST PROCESSING
        // ─────────────────────────────────────────────────────────────────

#if UNITY_POST_PROCESSING
        void InitPostProcessing()
        {
            if (postProcessVolume == null) return;
            postProcessVolume.profile.TryGetSettings(out _vignette);
            postProcessVolume.profile.TryGetSettings(out _chromatic);
            postProcessVolume.profile.TryGetSettings(out _lensDistortion);
        }
#endif

        void UpdatePostProcessing()
        {
#if UNITY_POST_PROCESSING
            if (_vignette == null && _chromatic == null) return;

            float t = _isInBreakdown
                ? 1f
                : Mathf.Clamp01(1f - (_smoothedSanity / shakeThreshold));

            // Vignette — menggelapkan sudut layar, memberi rasa sempit / tertekan
            if (_vignette != null)
            {
                float targetIntensity = _isInBreakdown
                    ? Mathf.Lerp(0.45f, 0.75f, _breakdownProgress)
                    : Mathf.Lerp(0f, 0.45f, t);

                _vignette.intensity.value = Mathf.Lerp(
                    _vignette.intensity.value, targetIntensity, Time.deltaTime * transitionSpeed);
            }

            // Chromatic Aberration — efek RGB split saat sanity rendah
            if (_chromatic != null)
            {
                float targetCA = _isInBreakdown
                    ? Mathf.Lerp(0.5f, 1f, _breakdownProgress)
                    : Mathf.Lerp(0f, 0.5f, t);

                _chromatic.intensity.value = Mathf.Lerp(
                    _chromatic.intensity.value, targetCA, Time.deltaTime * transitionSpeed);
            }

            // Lens Distortion — dunia terasa bengkok saat Insane
            if (_lensDistortion != null)
            {
                float targetDist = _isInBreakdown
                    ? -35f
                    : Mathf.Lerp(0f, -15f, t);

                _lensDistortion.intensity.value = Mathf.Lerp(
                    _lensDistortion.intensity.value, targetDist, Time.deltaTime * transitionSpeed * 0.5f);
            }
#endif
        }

        // ─────────────────────────────────────────────────────────────────
        //  FADE OVERLAY
        // ─────────────────────────────────────────────────────────────────

        void UpdateFadeOverlay()
        {
            if (fadeOverlay == null) return;

            float targetAlpha = 0f;

            if (_isInBreakdown)
            {
                // Breakdown: layar menghitam di awal, fade ke terang menjelang akhir
                targetAlpha = breakdownCurve != null
                    ? breakdownCurve.Evaluate(_breakdownProgress) * 0.9f
                    : Mathf.Sin(_breakdownProgress * Mathf.PI) * 0.9f;
            }
            else if (_smoothedSanity < fadeThreshold)
            {
                // Sanity sangat kritis: vignette hitam mulai menutup layar
                float t = 1f - (_smoothedSanity / fadeThreshold);
                targetAlpha = t * 0.7f;
            }

            fadeOverlay.alpha = Mathf.Lerp(fadeOverlay.alpha, targetAlpha,
                                            Time.deltaTime * transitionSpeed);
        }

        // ─────────────────────────────────────────────────────────────────
        //  EVENT HANDLERS
        // ─────────────────────────────────────────────────────────────────

        void OnSanityUpdated(float percent)
        {
            _latestSanityPercent = percent;
        }

        void OnBreakdownStart()
        {
            _isInBreakdown    = true;
            _breakdownProgress = 0f;
        }

        void OnBreakdownEnd()
        {
            _isInBreakdown    = false;
            _breakdownProgress = 0f;
        }

        // ─────────────────────────────────────────────────────────────────
        //  DEBUG
        // ─────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"ShakePower: {_currentShakePower:F4}\n" +
                $"SmoothedSanity: {_smoothedSanity:F2}\n" +
                $"Breakdown: {_isInBreakdown} ({_breakdownProgress:F2})");
        }
#endif
    }
}