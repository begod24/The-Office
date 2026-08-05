using System;
using System.Collections.Generic;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// The authoritative session phase. Technical Plan §7.1 — server-authoritative, replicated
    /// as a <see cref="NetworkVariable{T}"/>, every system subscribes and reacts, no system
    /// infers the phase from anything else.
    ///
    /// The local <see cref="IGameStateService"/> on every client is a mirror, not a second
    /// source of truth: the server validates a transition against the shared table and writes
    /// the variable, and every client applies what arrives through
    /// <see cref="IGameStateService.SetFromAuthority"/>. One decision point, one code path.
    /// </summary>
    public sealed class SessionDirector : NetworkBehaviour
    {
        [SerializeField] private LobbyRoster roster;

        private readonly NetworkVariable<GameState> phase = new(GameState.Lobby);

        /// <summary>Clients that have finished loading the run scene. Server-side only.</summary>
        private readonly HashSet<ulong> sceneReady = new();

        private IGameStateService gameState;
        private LobbyService lobbyService;

        public GameState Phase => phase.Value;

        public bool IsHostClient => IsServer;

        public event Action<GameState> PhaseChanged;

        public override void OnNetworkSpawn()
        {
            ServiceLocator.TryGet(out gameState);

            phase.OnValueChanged += OnPhaseReplicated;

            if (IsServer)
            {
                phase.Value = GameState.Lobby;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }

            // The initial value arrives with the spawn message and never fires OnValueChanged,
            // so a late joiner would otherwise sit in whatever phase it booted into.
            ApplyPhaseLocally(phase.Value);

            if (ServiceLocator.TryGet<ILobbyService>(out var service) &&
                service is LobbyService concrete)
            {
                lobbyService = concrete;
                lobbyService.Bind(this, roster);
            }
        }

        public override void OnNetworkDespawn()
        {
            phase.OnValueChanged -= OnPhaseReplicated;

            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                sceneReady.Clear();
            }

            lobbyService?.Unbind();
            lobbyService = null;

            // Back to a local, offline phase so the menus behave after leaving a session.
            gameState?.SetFromAuthority(GameState.Lobby);
        }

        // --------------------------------------------------------------- client requests

        /// <summary>
        /// Host asks to start the run. Validated on the server: only the host may start, and
        /// only when everyone has pressed ready — a client that fakes this RPC changes nothing.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestStartRunRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning("[Session] A non-host client asked to start the run. Ignored.");
                return;
            }

            if (phase.Value != GameState.Lobby) return;

            if (roster == null || !roster.AllReady)
            {
                Debug.Log("[Session] Start refused: not everyone is ready.");
                return;
            }

            sceneReady.Clear();
            TrySetPhase(GameState.Generating);
        }

        /// <summary>
        /// A client finished loading the run scene. The run only starts once every connected
        /// client reports in, otherwise players spawn into a scene that does not exist on their
        /// machine yet.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ReportRunSceneReadyRpc(RpcParams rpcParams = default)
        {
            sceneReady.Add(rpcParams.Receive.SenderClientId);

            if (phase.Value != GameState.Generating) return;
            if (sceneReady.Count < NetworkManager.ConnectedClientsIds.Count) return;

            TrySetPhase(GameState.InRun);
        }

        /// <summary>Host ends the run and sends everyone back to the lobby.</summary>
        [Rpc(SendTo.Server)]
        public void RequestEndRunRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            if (phase.Value is not (GameState.InRun or GameState.Generating)) return;

            // GameState has no direct InRun -> Lobby edge, and adding one would let a run end
            // without ever passing through a terminal state. Aborting is a failed run.
            if (phase.Value == GameState.InRun) TrySetPhase(GameState.RunFailed);

            sceneReady.Clear();
            roster?.ClearReadyFlags();
            TrySetPhase(GameState.Lobby);
        }

        // --------------------------------------------------------------- server internals

        private bool TrySetPhase(GameState next)
        {
            if (!IsServer) return false;
            if (next == phase.Value) return true;

            if (!GameStateMachine.IsLegal(phase.Value, next))
            {
                Debug.LogError($"[Session] Illegal transition {phase.Value} -> {next}. Ignored.");
                return false;
            }

            phase.Value = next;
            return true;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            sceneReady.Remove(clientId);

            // The last client to load may have been the one that left, which would otherwise
            // leave everyone stuck in Generating forever.
            if (phase.Value == GameState.Generating &&
                sceneReady.Count >= NetworkManager.ConnectedClientsIds.Count &&
                sceneReady.Count > 0)
                TrySetPhase(GameState.InRun);
        }

        private void OnPhaseReplicated(GameState previous, GameState current) =>
            ApplyPhaseLocally(current);

        private void ApplyPhaseLocally(GameState current)
        {
            gameState?.SetFromAuthority(current);
            PhaseChanged?.Invoke(current);
        }
    }
}
