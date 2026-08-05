using System;
using Office.Data;

namespace Office.Network
{
    public interface ILobbyService
    {
        bool IsAvailable { get; }

        bool IsHost { get; }

        ulong LocalClientId { get; }

        GameState Phase { get; }

        int PlayerCount { get; }

        bool TryGetSlot(int index, out PlayerSlot slot);

        bool LocalIsReady { get; }

        bool AllReady { get; }

        event Action Changed;

        void SetReady(bool ready);

        void RequestStartRun();

        void RequestEndRun();
    }
}
