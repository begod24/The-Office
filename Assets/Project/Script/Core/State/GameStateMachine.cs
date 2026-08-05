using System;
using Office.Data;
using UnityEngine;

namespace Office.Core
{
    /// <summary>
    /// Local, non-networked phase machine used for Boot, MainMenu and Lobby.
    /// Replaced by the server-authoritative implementation once a session is running.
    ///
    /// Transitions are validated against an explicit table. An illegal transition is a bug in
    /// the caller, and it is far cheaper to catch it here than to debug a run that silently
    /// entered <see cref="GameState.InRun"/> without generating a floor.
    /// </summary>
    public sealed class GameStateMachine : IGameStateService
    {
        private readonly IEventBus bus;

        public GameState Current { get; private set; } = GameState.Boot;

        public event Action<GameState, GameState> Changed;

        public GameStateMachine(IEventBus bus) => this.bus = bus;

        public bool TryChange(GameState next)
        {
            if (next == Current) return true;

            if (!IsLegal(Current, next))
            {
                Debug.LogError($"[GameState] Illegal transition {Current} -> {next}. Ignored.");
                return false;
            }

            Apply(next);
            return true;
        }

        /// <inheritdoc />
        public void SetFromAuthority(GameState next)
        {
            if (next == Current) return;

            Apply(next);
        }

        private void Apply(GameState next)
        {
            var previous = Current;
            Current = next;

            Changed?.Invoke(previous, next);
            bus?.Publish(new GameStateChanged(previous, next));
        }

        /// <summary>
        /// Transition table for Technical Plan §7.1:
        /// Boot -> MainMenu -> Lobby -> Generating -> InRun -> FloorTransition
        ///      -> RunComplete / RunFailed -> Lobby
        /// </summary>
        public static bool IsLegal(GameState from, GameState to) => from switch
        {
            GameState.Boot => to is GameState.MainMenu,
            GameState.MainMenu => to is GameState.Lobby,
            GameState.Lobby => to is GameState.Generating or GameState.MainMenu,
            GameState.Generating => to is GameState.InRun or GameState.RunFailed or GameState.Lobby,
            GameState.InRun => to is GameState.FloorTransition or GameState.RunComplete
                or GameState.RunFailed,
            GameState.FloorTransition => to is GameState.Generating or GameState.InRun
                or GameState.RunFailed,
            GameState.RunComplete => to is GameState.Lobby,
            GameState.RunFailed => to is GameState.Lobby,
            _ => false
        };
    }
}
