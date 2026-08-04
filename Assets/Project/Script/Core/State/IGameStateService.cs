using System;
using Office.Data;

namespace Office.Core
{
    /// <summary>
    /// Reads and drives the session phase. Technical Plan §7.1.
    ///
    /// Gameplay depends on this interface, never on the implementation. In the Boot and menu
    /// flow it is backed by a local machine; from the moment a session starts it is backed by
    /// a server-authoritative NetworkVariable in <c>Office.Network</c>. Callers cannot tell the
    /// difference, which is the entire point.
    /// </summary>
    public interface IGameStateService
    {
        GameState Current { get; }

        /// <summary>Previous state, then current state.</summary>
        event Action<GameState, GameState> Changed;

        /// <summary>
        /// Requests a transition. Returns false and logs if the transition is not legal,
        /// rather than silently corrupting the phase.
        /// </summary>
        bool TryChange(GameState next);
    }
}
