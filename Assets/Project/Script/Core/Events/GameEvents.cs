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
