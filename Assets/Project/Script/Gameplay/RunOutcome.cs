using System.Collections.Generic;
using Office.Data;
using Office.Network;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RunOutcome : NetworkBehaviour
    {
        [SerializeField] private SessionDirector director;

        private readonly List<Health> tracked = new(4);

        private bool decided;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            Health.SpawnedPlayersChanged += Rebind;

            if (director != null) director.PhaseChanged += OnPhaseChanged;

            Rebind();
        }

        public override void OnNetworkDespawn()
        {
            Health.SpawnedPlayersChanged -= Rebind;

            if (director != null) director.PhaseChanged -= OnPhaseChanged;

            Untrack();
        }

        public bool ServerCompleteRun()
        {
            if (!IsServer || decided || director == null) return false;

            if (director.Phase != GameState.InRun) return false;

            decided = true;

            Debug.Log("[Run] Objective complete. Ending the shift.");

            return director.ServerEndRun(GameState.RunComplete);
        }

        private void OnPhaseChanged(GameState phase)
        {
            if (phase is GameState.Generating or GameState.InRun) decided = false;
        }

        private void Rebind()
        {
            if (!IsServer) return;

            Untrack();

            var players = Health.SpawnedPlayerList;

            for (var i = 0; i < players.Count; i++)
            {
                var health = players[i];
                if (health == null) continue;

                health.Changed += OnVitalsChanged;
                tracked.Add(health);
            }

            Evaluate();
        }

        private void Untrack()
        {
            foreach (var health in tracked)
                if (health != null)
                    health.Changed -= OnVitalsChanged;

            tracked.Clear();
        }

        private void OnVitalsChanged(VitalsState state) => Evaluate();

        private void Evaluate()
        {
            if (!IsServer || decided || director == null) return;
            if (director.Phase != GameState.InRun) return;

            var players = Health.SpawnedPlayerList;

            if (players.Count == 0) return;

            for (var i = 0; i < players.Count; i++)
            {
                var health = players[i];
                if (health == null) continue;

                if (health.State.IsStanding) return;
            }

            decided = true;

            Debug.Log("[Run] Every player is down or dead. Ending the shift.");

            director.ServerEndRun(GameState.RunFailed);
        }
    }
}
