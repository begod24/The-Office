using System;
using Office.Data;

namespace Office.Core
{
    public interface IGameStateService
    {
        GameState Current { get; }

        event Action<GameState, GameState> Changed;

        bool TryChange(GameState next);

        void SetFromAuthority(GameState next);
    }
}
