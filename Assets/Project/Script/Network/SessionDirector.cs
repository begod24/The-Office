using System;
using System.Collections.Generic;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    public sealed class SessionDirector : NetworkBehaviour
    {
        [SerializeField] private LobbyRoster roster;

        private readonly NetworkVariable<GameState> phase = new(GameState.Lobby);

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

            gameState?.SetFromAuthority(GameState.Lobby);
        }

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

        [Rpc(SendTo.Server)]
        public void ReportRunSceneReadyRpc(RpcParams rpcParams = default)
        {
            sceneReady.Add(rpcParams.Receive.SenderClientId);

            if (phase.Value != GameState.Generating) return;
            if (sceneReady.Count < NetworkManager.ConnectedClientsIds.Count) return;

            TrySetPhase(GameState.InRun);
        }

        [Rpc(SendTo.Server)]
        public void RequestEndRunRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            if (phase.Value is not (GameState.InRun or GameState.Generating)) return;

            if (phase.Value == GameState.InRun) TrySetPhase(GameState.RunFailed);

            sceneReady.Clear();
            roster?.ClearReadyFlags();
            TrySetPhase(GameState.Lobby);
        }

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
