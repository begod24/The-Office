using System;
using Office.Data;

namespace Office.Network
{
    /// <inheritdoc cref="ILobbyService"/>
    public sealed class LobbyService : ILobbyService
    {
        private SessionDirector director;
        private LobbyRoster roster;

        public bool IsAvailable => director != null && director.IsSpawned && roster != null;

        public bool IsHost => IsAvailable && director.IsHostClient;

        public ulong LocalClientId => IsAvailable ? director.NetworkManager.LocalClientId : 0;

        public GameState Phase => IsAvailable ? director.Phase : GameState.Lobby;

        public int PlayerCount => IsAvailable ? roster.Count : 0;

        public bool AllReady => IsAvailable && roster.AllReady;

        public bool LocalIsReady =>
            IsAvailable && roster.IsReady(director.NetworkManager.LocalClientId);

        public event Action Changed;

        public bool TryGetSlot(int index, out PlayerSlot slot)
        {
            if (!IsAvailable || index < 0 || index >= roster.Count)
            {
                slot = default;
                return false;
            }

            slot = roster[index];
            return true;
        }

        public void SetReady(bool ready)
        {
            if (!IsAvailable) return;

            roster.SetReadyRpc(ready);
        }

        public void RequestStartRun()
        {
            if (!IsAvailable) return;

            director.RequestStartRunRpc();
        }

        public void RequestEndRun()
        {
            if (!IsAvailable) return;

            director.RequestEndRunRpc();
        }

        /// <summary>Called by <see cref="SessionDirector"/> when the session object spawns.</summary>
        internal void Bind(SessionDirector newDirector, LobbyRoster newRoster)
        {
            Unbind();

            director = newDirector;
            roster = newRoster;

            if (roster != null) roster.Changed += RaiseChanged;
            if (director != null) director.PhaseChanged += OnPhaseChanged;

            RaiseChanged();
        }

        internal void Unbind()
        {
            if (roster != null) roster.Changed -= RaiseChanged;
            if (director != null) director.PhaseChanged -= OnPhaseChanged;

            director = null;
            roster = null;

            RaiseChanged();
        }

        private void OnPhaseChanged(GameState phase) => RaiseChanged();

        private void RaiseChanged() => Changed?.Invoke();
    }
}
