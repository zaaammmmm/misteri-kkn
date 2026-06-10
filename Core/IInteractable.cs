namespace KKN.Game.Core
{
    /// <summary>
    /// Interface for objects the player can interact with via raycast.
    /// </summary>
    public interface IInteractable
    {
        void Interact();
        string GetInteractText();
        void OnHoverEnter();
        void OnHoverExit();
    }
}

