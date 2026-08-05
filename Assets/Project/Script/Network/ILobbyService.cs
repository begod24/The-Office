using System;
using Office.Data;

namespace Office.Network
{
    /// <summary>
    /// A stable handle on the lobby for the UI.
    ///
    /// The roster and the director are components on a networked object that only exists while a
    /// session is running, and they live in the Boot scene while the UI lives in SCN_Lobby —
    /// a direct inspector reference across those two would be exactly the cross-scene coupling
    /// Technical Plan §3.3 forbids. This façade is registered once at boot and reports
    /// <see cref="IsAvailable"/> = false while offline, so the UI has one thing to hold and one
    /// thing to check.
    /// </summary>
    public interface ILobbyService
    {
        /// <summary>False while offline. Every other member returns a neutral value in that case.</summary>
        bool IsAvailable { get; }

        bool IsHost { get; }

        /// <summary>This client's NGO id, so the UI can tell which roster row is its own.</summary>
        ulong LocalClientId { get; }

        GameState Phase { get; }

        int PlayerCount { get; }

        bool TryGetSlot(int index, out PlayerSlot slot);

        bool LocalIsReady { get; }

        bool AllReady { get; }

        /// <summary>Raised when the roster, a ready flag, or the phase changes.</summary>
        event Action Changed;

        void SetReady(bool ready);

        /// <summary>Host only. Ignored by the server unless everyone is ready.</summary>
        void RequestStartRun();

        /// <summary>Host only. Ends the run and returns everyone to the lobby.</summary>
        void RequestEndRun();
    }
}
