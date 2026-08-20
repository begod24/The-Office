using System.Collections.Generic;
using Office.Data;
using Office.Network;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The server-side judge of how a run stops: everyone on the floor ends it in failure,
    /// the objective being finished ends it in success. GDD §15.
    /// </summary>
    /// <remarks>
    /// Lives on <c>PF_Session</c> beside the spawners, because it has to outlive the run scene
    /// and it is a decision rather than a view. Nothing here is authoritative on its own —
    /// it reads replicated vitals and asks <see cref="SessionDirector.ServerEndRun"/>, which
    /// owns the phase and the ordering.
    /// <para>
    /// <b>Down counts, not just dead.</b> A squad where the last player standing goes down has
    /// already lost: there is nobody left who can revive anyone, and the sixty seconds would
    /// be spent watching a timer with no possible outcome. Ending it at that moment is the
    /// difference between a loss and a wait.
    /// </para>
    /// </remarks>
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

        /// <summary>
        /// Server only. The run's objective is finished — end it as a win.
        /// </summary>
        /// <remarks>
        /// Public because the thing that completes an objective is a prop in the level, not
        /// this component: the switch knows it was thrown, and this knows what that means for
        /// the run.
        /// </remarks>
        public bool ServerCompleteRun()
        {
            if (!IsServer || decided || director == null) return false;

            if (director.Phase != GameState.InRun) return false;

            decided = true;

            Debug.Log("[Run] Objective complete. Ending the shift.");

            return director.ServerEndRun(GameState.RunComplete);
        }

        // A fresh run is undecided again — the flag exists to stop a wipe and a completion
        // racing each other inside one run, not to end the session.
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

            // A player leaving is as much a reason to re-check as one falling over: the last
            // standing teammate disconnecting leaves exactly the same room.
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

            // No bodies at all is the half-second before the first one spawns, not a wipe.
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
