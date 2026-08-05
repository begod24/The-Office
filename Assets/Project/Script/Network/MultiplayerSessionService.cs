using System;
using System.Threading.Tasks;
using Office.Data;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Office.Network
{
    public sealed class MultiplayerSessionService : ISessionService
    {
        private const string PlayerNameProperty = "playerName";

        private ISession session;
        private SessionPhase phase = SessionPhase.Offline;

        public SessionPhase Phase
        {
            get => phase;
            private set
            {
                if (phase == value) return;
                phase = value;
                PhaseChanged?.Invoke(value);
            }
        }

        public string JoinCode => session?.Code ?? string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public bool IsHost => session?.IsHost ?? false;
        public int PlayerCount => session?.PlayerCount ?? 0;
        public int MaxPlayers => session?.MaxPlayers ?? 0;

        public event Action<SessionPhase> PhaseChanged;

        public async Task<bool> HostAsync(int maxPlayers, string sessionName)
        {
            if (session != null)
            {
                LastError = "Already in a session.";
                return false;
            }

            if (!await EnsureSignedInAsync()) return false;

            Phase = SessionPhase.Creating;

            try
            {
                var options = new SessionOptions
                {
                    Name = string.IsNullOrWhiteSpace(sessionName) ? "Office" : sessionName,
                    MaxPlayers = Mathf.Clamp(maxPlayers, 1, 4),
                    IsPrivate = true
                }.WithRelayNetwork();

                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                Bind(session);

                Phase = SessionPhase.InSession;
                Debug.Log($"[Session] Hosting. Join code: {session.Code}");
                return true;
            }
            catch (Exception e)
            {
                Fail("Could not create the session.", e);
                return false;
            }
        }

        public async Task<bool> JoinAsync(string joinCode)
        {
            if (session != null)
            {
                LastError = "Already in a session.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                LastError = "Enter a join code.";
                Phase = SessionPhase.Failed;
                return false;
            }

            if (!await EnsureSignedInAsync()) return false;

            Phase = SessionPhase.Joining;

            try
            {
                session = await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(joinCode.Trim().ToUpperInvariant());
                Bind(session);

                Phase = SessionPhase.InSession;
                Debug.Log($"[Session] Joined {session.Id}.");
                return true;
            }
            catch (Exception e)
            {
                Fail("Could not join that session. Check the code.", e);
                return false;
            }
        }

        public async Task LeaveAsync()
        {
            if (session == null)
            {
                Phase = SessionPhase.Offline;
                return;
            }

            Phase = SessionPhase.Leaving;

            try
            {
                Unbind(session);
                await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Session] Leave reported an error, continuing: {e.Message}");
            }
            finally
            {
                session = null;
                Phase = SessionPhase.Offline;
            }
        }

        private async Task<bool> EnsureSignedInAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    options.SetProfile(ResolveProfileName());
                    await UnityServices.InitializeAsync(options);
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Phase = SessionPhase.Initialising;
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                return true;
            }
            catch (Exception e)
            {
                Fail("Could not reach Unity Gaming Services. Check the project link and network.", e);
                return false;
            }
        }

        private static string ResolveProfileName()
        {
#if UNITY_EDITOR
            var id = System.Diagnostics.Process.GetCurrentProcess().Id;
            return $"editor{id}";
#else
            return "player";
#endif
        }

        private void Bind(ISession target)
        {
            target.RemovedFromSession += OnRemovedFromSession;
            target.Deleted += OnSessionDeleted;
        }

        private void Unbind(ISession target)
        {
            target.RemovedFromSession -= OnRemovedFromSession;
            target.Deleted -= OnSessionDeleted;
        }

        private void OnRemovedFromSession()
        {
            LastError = "You were removed from the session.";
            ClearSession();
        }

        private void OnSessionDeleted()
        {
            LastError = "The host closed the session.";
            ClearSession();
        }

        private void ClearSession()
        {
            if (session != null) Unbind(session);
            session = null;
            Phase = SessionPhase.Offline;
        }

        private void Fail(string userMessage, Exception e)
        {
            LastError = userMessage;
            Phase = SessionPhase.Failed;
            Debug.LogError($"[Session] {userMessage}\n{e}");
        }
    }
}
