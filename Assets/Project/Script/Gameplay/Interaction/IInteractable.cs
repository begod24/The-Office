namespace Office.Gameplay
{
    /// <summary>
    /// Anything the player can put the crosshair on and press Interact.
    /// </summary>
    /// <remarks>
    /// Implementations live on the root of a spawned NetworkObject — colliders may sit
    /// anywhere below it, but the component itself must be on the root, because that is
    /// where the server looks it up from the reference the client sent.
    /// The owning client only ever
    /// *asks*: <see cref="PlayerInteractor"/> sends the request to the server, the server
    /// re-checks reach and availability, and only the server calls <see cref="Interact"/>.
    /// Reading <see cref="Prompt"/> and <see cref="IsAvailable"/> on a client is fine —
    /// they drive the HUD and nothing else.
    /// </remarks>
    public interface IInteractable
    {
        /// <summary>Line shown under the crosshair. Empty hides the label.</summary>
        string Prompt { get; }

        /// <summary>False while locked, busy, already taken or not yet spawned.</summary>
        bool IsAvailable { get; }

        /// <summary>Server only. <paramref name="clientId"/> is the player that asked.</summary>
        void Interact(ulong clientId);
    }
}
