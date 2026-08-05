using System;
using System.Threading.Tasks;
using Office.Data;

namespace Office.Network
{
    public interface ISessionService
    {
        SessionPhase Phase { get; }

        string JoinCode { get; }

        string LastError { get; }

        bool IsHost { get; }

        int PlayerCount { get; }

        int MaxPlayers { get; }

        event Action<SessionPhase> PhaseChanged;

        Task<bool> HostAsync(int maxPlayers, string sessionName);

        Task<bool> JoinAsync(string joinCode);

        Task LeaveAsync();
    }
}
