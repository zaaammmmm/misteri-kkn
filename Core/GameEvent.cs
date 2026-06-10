using UnityEngine;

namespace KKN.Game.Core
{
    /// <summary>
    /// ScriptableObject-based event for decoupled communication.
    /// Any system can raise this event; any listener can respond.
    /// </summary>
    [CreateAssetMenu(fileName = "GameEvent", menuName = "KKN/Game Event")]
    public class GameEvent : ScriptableObject
    {
        private event System.Action listeners;

        /// <summary>Raise the event, notifying all listeners.</summary>
        public void Raise()
        {
            listeners?.Invoke();
        }

        /// <summary>Subscribe a listener to this event.</summary>
        public void RegisterListener(System.Action listener)
        {
            listeners += listener;
        }

        /// <summary>Unsubscribe a listener from this event.</summary>
        public void UnregisterListener(System.Action listener)
        {
            listeners -= listener;
        }
    }
}

