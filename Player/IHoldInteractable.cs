namespace KKN.Game.Core
{
    public interface IHoldInteractable
    {
        float GetHoldDuration();

        bool CanStartHold();

        void OnHoldStart();
        void OnHoldComplete();
        void OnHoldCancel();
    }
}