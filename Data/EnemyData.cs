using UnityEngine;

namespace KKN.Game.Data
{
    /// <summary>
    /// ScriptableObject defining ghost behavior parameters.
    /// Enables designers to tune enemy difficulty without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "KKN/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Personality")]
        [Tooltip("Hunter: aggressive chase. Stalker: observes from afar. Trickster: mimics sounds.")]
        public EnemyPersonality personality;

        [Header("Movement Speeds")]
        public float patrolSpeed = 3.5f;
        public float chaseSpeed  = 5.5f;
        public float stalkSpeed  = 2f;

        [Header("Sensing")]
        public float hearRadius      = 12f;
        public float sightRange      = 15f;
        public float sightAngle      = 70f;
        public float attackRange     = 1.8f;
        public float lightFearRadius = 7f;

        [Header("Timers")]
        public float searchTime   = 8f;
        public float cooldownTime = 5f;
        public float stalkDuration = 6f;

        [Header("Memory")]
        [Tooltip("How long ghost remembers player position after losing sight")]
        public float memoryDuration = 10f;

        [Header("Fake Retreat")]
        [Tooltip("Chance to fake retreat when player looks at ghost")]
        [Range(0f, 1f)]
        public float fakeRetreatChance = 0.3f;
        [Tooltip("How long ghost pretends to leave before returning")]
        public float fakeRetreatDuration = 4f;

        [Header("Light Combat")]
        [Tooltip("Seconds of flashlight exposure needed to make ghost flee")]
        public float lightExposureThreshold = 3f;
    }

    public enum EnemyPersonality
    {
        Hunter,    // Aggressive, direct chase, high speed
        Stalker,   // Observes from distance, follows silently
        Trickster  // Mimics sounds (footsteps, voices), lures player
    }
}

