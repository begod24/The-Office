using Office.Data;

namespace Office.Core
{
    public readonly struct GameStateChanged
    {
        public readonly GameState Previous;
        public readonly GameState Current;

        public GameStateChanged(GameState previous, GameState current)
        {
            Previous = previous;
            Current = current;
        }
    }

    public readonly struct PowerStateChanged
    {
        public readonly int ZoneId;
        public readonly bool IsPowered;

        public PowerStateChanged(int zoneId, bool isPowered)
        {
            ZoneId = zoneId;
            IsPowered = isPowered;
        }
    }

    public readonly struct LocalPlayerSpawned
    {
        public readonly ulong ClientId;

        public LocalPlayerSpawned(ulong clientId) => ClientId = clientId;
    }

    public readonly struct LocalPauseChanged
    {
        public readonly bool IsPaused;

        public LocalPauseChanged(bool isPaused) => IsPaused = isPaused;
    }

    /// <summary>
    /// What the local player is currently looking at, as a line of HUD text. An empty
    /// prompt means "nothing in reach" and hides the label.
    /// </summary>
    /// <remarks>
    /// Published only when the text actually changes, not every frame — the interactor
    /// probes continuously and the bus is not a polling channel.
    /// </remarks>
    public readonly struct InteractionPromptChanged
    {
        public readonly string Prompt;

        public InteractionPromptChanged(string prompt) => Prompt = prompt ?? string.Empty;

        public bool HasPrompt => !string.IsNullOrEmpty(Prompt);
    }

    public readonly struct PlayerConnectionChanged
    {
        public readonly ulong ClientId;
        public readonly bool Connected;

        public PlayerConnectionChanged(ulong clientId, bool connected)
        {
            ClientId = clientId;
            Connected = connected;
        }
    }
}
