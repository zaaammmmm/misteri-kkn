using UnityEngine;
using KKN.Game.Systems;

namespace KKN.Game.Player
{
    /// <summary>
    /// Smooth camera shake and visual distortion based on sanity level.
    /// Responds to breakdown events from SanitySystem.
    /// </summary>
    public class LowSanityEffects : MonoBehaviour
    {
        [Header("Shake Settings")]
        [Tooltip("Max shake strength at zero sanity")]
        [SerializeField] private float maxShakePower = 0.06f;
        [Tooltip("Smoothing speed for shake transitions")]
        [SerializeField] private float lerpSpeed = 8f;
        [Tooltip("Sanity threshold where shake begins (0..1)")]
        [SerializeField] private float shakeThreshold = 0.35f;

        [Header("Breakdown")]
        [SerializeField] private float breakdownShakeMultiplier = 3f;

        private Vector3 baseLocalPos;
        private Vector3 targetOffset;
        private float currentShakePower;
        private bool isInBreakdown;

        void Start()
        {
            baseLocalPos = transform.localPosition;

            if (SanitySystem.Instance != null)
            {
                SanitySystem.Instance.OnBreakdownStarted += OnBreakdownStart;
                SanitySystem.Instance.OnBreakdownEnded += OnBreakdownEnd;
            }
        }

        void OnDestroy()
        {
            if (SanitySystem.Instance != null)
            {
                SanitySystem.Instance.OnBreakdownStarted -= OnBreakdownStart;
                SanitySystem.Instance.OnBreakdownEnded -= OnBreakdownEnd;
            }
        }

        void Update()
        {
            if (SanitySystem.Instance == null)
            {
                ResetPosition();
                return;
            }

            float sanity = SanitySystem.Instance.Percent();
            float power = 0f;

            if (isInBreakdown)
            {
                power = maxShakePower * breakdownShakeMultiplier;
                targetOffset = Random.insideUnitSphere * power;
            }
            else if (sanity < shakeThreshold)
            {
                float t = 1f - (sanity / shakeThreshold);
                power = t * maxShakePower;
                targetOffset = Random.insideUnitSphere * power;
            }
            else
            {
                targetOffset = Vector3.zero;
            }

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                baseLocalPos + targetOffset,
                Time.deltaTime * lerpSpeed
            );
        }

        void ResetPosition()
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                baseLocalPos,
                Time.deltaTime * lerpSpeed
            );
        }

        void OnBreakdownStart()
        {
            isInBreakdown = true;
        }

        void OnBreakdownEnd()
        {
            isInBreakdown = false;
        }
    }
}

