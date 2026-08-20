namespace Office.Gameplay
{
    public interface IInteractable
    {
        string Prompt { get; }

        bool IsAvailable { get; }

        void Interact(ulong clientId);
    }
}
