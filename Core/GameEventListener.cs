using UnityEngine;
using UnityEngine.Events;

namespace KKN.Game.Core
{
    /// <summary>
    /// MonoBehaviour that listens to a GameEvent and invokes a UnityEvent response.
    /// Place this on GameObjects that need to react to global events.
    /// </summary>
    public class GameEventListener : MonoBehaviour
    {
        [Tooltip("The ScriptableObject event to listen to")]
        public GameEvent gameEvent;

        [Tooltip("Response invoked when the event is raised")]
        public UnityEvent response;

        void OnEnable()
        {
            if (gameEvent != null)
                gameEvent.RegisterListener(OnEventRaised);
        }

        void OnDisable()
        {
            if (gameEvent != null)
                gameEvent.UnregisterListener(OnEventRaised);
        }

        void OnEventRaised()
        {
            response?.Invoke();
        }
    }
}

