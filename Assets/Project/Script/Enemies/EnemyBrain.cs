using System;
using Office.Data;
using Office.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace Office.Enemies
{
    /// <summary>
    /// What one enemy decides to do. Runs on the server and nowhere else.
    /// </summary>
    /// <remarks>
    /// <b>Server only, deliberately and completely.</b> The component disables itself on every
    /// other machine in <see cref="OnNetworkSpawn"/>. An AI that also thought on the client
    /// would reach its own conclusions from its own copy of the world, and the two would
    /// disagree the first time a door closed on one machine a frame before the other — which
    /// arrives as an enemy that attacks thin air for exactly one player.
    /// <para>
    /// What leaves this machine is one <see cref="EnemyBehaviourState"/>, and the position the
    /// <c>NetworkTransform</c> already sends. That is the whole contract: everything a client
    /// draws — including the procedural animation this state feeds — is derived from those two,
    /// so a swarm costs no more to replicate than a swarm of moving boxes.
    /// </para>
    /// <para>
    /// The senses here are sight alone. Hearing is the other half, and the number it needs is
    /// already authored on both sides — <see cref="EnemyDefinition.HearingRadius"/> and the
    /// <c>NoiseRadius</c> every <c>WeaponProfile</c> resolves — waiting for something to publish
    /// a noise. That is what raises <see cref="EnemyBehaviourState.Investigating"/>, which nothing does
    /// yet.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyBrain : NetworkBehaviour
    {
        [SerializeField] private Enemy enemy;
        [SerializeField] private Health health;

        [Tooltip("Seconds between sight scans. Not every frame on purpose: a scan is a raycast " +
                 "per player and GDD §9.1 is built on swarms, so the cost is multiplied by the " +
                 "worst case rather than the average one. Low enough that a player cannot walk " +
                 "through a cone between two scans.")]
        [Min(0.02f)]
        [SerializeField] private float scanInterval = 0.15f;

        private readonly NetworkVariable<EnemyBehaviourState> state = new(
            EnemyBehaviourState.Idle, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Server only, all of it.
        private Health target;
        private float lastSeenTime;
        private float nextScanTime;
        private float nextAttackTime;

        // Negative means no swing is in the air.
        private float attackLandsAt = -1f;

        /// <summary>What this enemy is doing, on every machine.</summary>
        public EnemyBehaviourState State => state.Value;

        /// <summary>
        /// Raised on every machine when the state changes. The seam a view animates from, so
        /// that nothing drawing an enemy has to poll or to ask the server anything.
        /// </summary>
        public event Action<EnemyBehaviourState> StateChanged;

        public override void OnNetworkSpawn()
        {
            state.OnValueChanged += OnStateChanged;

            // Assigned rather than only disabled: this component is pooled with its object and
            // a returning instance carries whatever the last life left here.
            enabled = IsServer;

            if (IsServer)
            {
                state.Value = EnemyBehaviourState.Idle;
                target = null;
                attackLandsAt = -1f;
                nextAttackTime = 0f;
                nextScanTime = 0f;
            }

            StateChanged?.Invoke(state.Value);
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateChanged;
            target = null;
        }

        private void OnStateChanged(EnemyBehaviourState previous, EnemyBehaviourState current) =>
            StateChanged?.Invoke(current);

        // ------------------------------------------------------------------ the loop

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            var definition = enemy != null ? enemy.Definition : null;
            if (definition == null) return;

            if (health != null && !health.IsAlive)
            {
                // Nothing else runs after this. The carrier has already taken the agent down
                // and is holding the body for its corpse timer.
                Enter(EnemyBehaviourState.Dead);
                target = null;
                attackLandsAt = -1f;
                return;
            }

            // A swing already in the air lands whatever else changed. Interrupting it on the
            // frame the player stepped out of range would make the wind-up a lie — the tell
            // has to be worth reading, or it is only a delay.
            if (attackLandsAt >= 0f)
            {
                if (Time.time >= attackLandsAt) LandAttack(definition);

                FaceTarget(definition);
                return;
            }

            RefreshTarget(definition);

            if (target == null)
            {
                Idle(definition);
                return;
            }

            var distance = Vector3.Distance(transform.position, target.transform.position);

            if (distance <= definition.AttackRange && Time.time >= nextAttackTime)
            {
                BeginAttack(definition);
                return;
            }

            Chase(definition);
        }

        // ------------------------------------------------------------------ states

        private void Idle(EnemyDefinition definition)
        {
            Enter(EnemyBehaviourState.Idle);

            var agent = enemy.Agent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            agent.speed = definition.PatrolSpeed;

            // Standing still rather than wandering. A patrol route is level design's answer and
            // there is no level yet; what matters now is that an idle enemy does not keep
            // walking the path it was given while it still had a target.
            if (agent.hasPath) agent.ResetPath();
        }

        private void Chase(EnemyDefinition definition)
        {
            Enter(EnemyBehaviourState.Chasing);

            var agent = enemy.Agent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            agent.speed = definition.ChaseSpeed;
            agent.isStopped = false;
            agent.SetDestination(target.transform.position);
        }

        private void BeginAttack(EnemyDefinition definition)
        {
            Enter(EnemyBehaviourState.Attacking);

            var agent = enemy.Agent;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                if (agent.hasPath) agent.ResetPath();
            }

            attackLandsAt = Time.time + definition.AttackWindup;
        }

        /// <remarks>
        /// Range and line of sight are re-checked here rather than at <see cref="BeginAttack"/>
        /// alone, so backing away during the wind-up works. That is the counterplay the tell
        /// exists to offer.
        /// </remarks>
        private void LandAttack(EnemyDefinition definition)
        {
            attackLandsAt = -1f;
            nextAttackTime = Time.time + definition.AttackCooldown;

            if (target == null || !target.State.IsStanding) return;

            var point = target.transform.position + Vector3.up * (definition.BodyHeight * 0.5f);
            var offset = point - transform.position;

            if (offset.sqrMagnitude > definition.AttackRange * definition.AttackRange) return;
            if (CombatGeometry.IsOccluded(transform, point, EyeHeight(definition))) return;

            // Not a client. An enemy is the office doing something to you, and DamageInfo
            // already has the word for that.
            target.ApplyDamage(new DamageInfo(
                definition.AttackDamage, definition.AttackDamageType, DamageInfo.World,
                point, offset.normalized));
        }

        private void Enter(EnemyBehaviourState next)
        {
            if (state.Value != next) state.Value = next;
        }

        // ------------------------------------------------------------------ sight

        /// <summary>
        /// Keeps, drops or picks a target. Scans on a timer; forgetting is checked every frame
        /// because a chase that outlives its memory by a fifth of a second is a chase that
        /// followed you through a wall.
        /// </summary>
        private void RefreshTarget(EnemyDefinition definition)
        {
            if (target != null)
            {
                if (!target.IsSpawned || !target.State.IsStanding) target = null;
                else if (CanSee(definition, target)) lastSeenTime = Time.time;
                else if (Time.time - lastSeenTime > definition.MemorySeconds) target = null;
            }

            if (target != null || Time.time < nextScanTime) return;

            nextScanTime = Time.time + scanInterval;

            var found = FindVisiblePlayer(definition);
            if (found == null) return;

            target = found;
            lastSeenTime = Time.time;
        }

        private Health FindVisiblePlayer(EnemyDefinition definition)
        {
            Health nearest = null;
            var nearestDistance = float.PositiveInfinity;

            // Server only, which this method is. The list throws on a client.
            var clients = NetworkManager.ConnectedClientsList;

            for (var i = 0; i < clients.Count; i++)
            {
                var player = clients[i].PlayerObject;
                if (player == null || !player.IsSpawned) continue;

                var candidate = player.GetComponent<Health>();

                // A downed player is not hunted. Architecture §10.2 already makes damage to one
                // do nothing, and an enemy standing over a body is exactly the pressure GDD §15
                // says the revive window must not have.
                if (candidate == null || !candidate.State.IsStanding) continue;

                var distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance >= nearestDistance) continue;

                if (!CanSee(definition, candidate)) continue;

                nearest = candidate;
                nearestDistance = distance;
            }

            return nearest;
        }

        private bool CanSee(EnemyDefinition definition, Health candidate)
        {
            var eye = EyeHeight(definition);
            var origin = transform.position + Vector3.up * eye;
            var point = CombatGeometry.AimPoint(candidate.NetworkObject);

            var offset = point - origin;
            if (offset.sqrMagnitude > definition.SightRadius * definition.SightRadius) return false;

            // The cone is measured flat. An enemy that loses you by standing on a desk is not a
            // rule any player would work out.
            var flat = new Vector3(offset.x, 0f, offset.z);

            if (flat.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, flat) > definition.SightAngle * 0.5f)
                return false;

            return !CombatGeometry.IsOccluded(transform, point, eye);
        }

        // Near the top of the body. Sight from the floor is blocked by every desk in the
        // office, which is the same reason CombatGeometry casts from a player's shoulders.
        private static float EyeHeight(EnemyDefinition definition) => definition.BodyHeight * 0.85f;

        // The agent turns while it is moving; this is for the frames it is not, which is all of
        // the wind-up. An enemy that swings without facing you looks like it hit you by mistake.
        private void FaceTarget(EnemyDefinition definition)
        {
            if (target == null) return;

            var offset = target.transform.position - transform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(offset),
                definition.TurnSpeed * Time.deltaTime);
        }
    }
}
