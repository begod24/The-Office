using System;
using System.Threading.Tasks;
using Office.Data;

namespace Office.Network
{
    /// <summary>
    /// Creating and joining the online session. Technical Plan §2.1 — client-hosted listen
    /// server, no dedicated infrastructure.
    ///
    /// Gameplay and UI depend on this interface, never on the Unity Services types behind it.
    /// That is not an abstraction "in case we switch backends" (§2.2 forbids those); it exists
    /// so the session can be faked in a PlayMode test without a network.
    /// </summary>
    public interface ISessionService
    {
        SessionPhase Phase { get; }

        /// <summary>The code a friend types to join. Empty unless <see cref="Phase"/> is InSession.</summary>
        string JoinCode { get; }

        /// <summary>Last failure message, for display. Empty when there has been no failure.</summary>
        string LastError { get; }

        bool IsHost { get; }

        int PlayerCount { get; }

        int MaxPlayers { get; }

        event Action<SessionPhase> PhaseChanged;

        /// <summary>Creates a Relay-backed session and starts the local host. Returns false on failure.</summary>
        Task<bool> HostAsync(int maxPlayers, string sessionName);

        /// <summary>Joins an existing session by its code. Returns false on failure.</summary>
        Task<bool> JoinAsync(string joinCode);

        /// <summary>Leaves cleanly. Safe to call when already offline.</summary>
        Task LeaveAsync();
    }
}
