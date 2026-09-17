using System;
using Office.Core;
using Office.Data;
using Office.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Office.Enemies
{
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

        private IEventBus bus;
        private Health target;
        private float lastSeenTime;
        private float nextScanTime;
        private float nextAttackTime;

        private float attackLandsAt = -1f;

        private Vector3 noisePoint;
        private bool hasNoise;
        private float noiseArrivedTime = -1f;

        private const float NoiseArriveDistance = 0.75f;

        public EnemyBehaviourState State => state.Value;

        public event Action<EnemyBehaviourState> StateChanged;

        public override void OnNetworkSpawn()
        {
            state.OnValueChanged += OnStateChanged;

            enabled = IsServer;

            if (IsServer)
            {
                state.Value = EnemyBehaviourState.Idle;
                target = null;
                attackLandsAt = -1f;
                nextAttackTime = 0f;
                nextScanTime = 0f;
                hasNoise = false;
                noiseArrivedTime = -1f;

                if (ServiceLocator.TryGet(out bus)) bus.Subscribe<NoiseRaised>(OnNoise);
            }

            StateChanged?.Invoke(state.Value);
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateChanged;
            target = null;

            bus?.Unsubscribe<NoiseRaised>(OnNoise);
            bus = null;
        }

        private void OnStateChanged(EnemyBehaviourState previous, EnemyBehaviourState current) =>
            StateChanged?.Invoke(current);

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            var definition = enemy != null ? enemy.Definition : null;
            if (definition == null) return;

            if (health != null && !health.IsAlive)
            {
                Enter(EnemyBehaviourState.Dead);
                target = null;
                attackLandsAt = -1f;
                return;
            }

            if (attackLandsAt >= 0f)
            {
                if (Time.time >= attackLandsAt) LandAttack(definition);

                FaceTarget(definition);
                return;
            }

            RefreshTarget(definition);

            if (target == null)
            {
                if (hasNoise) Investigate(definition);
                else Idle(definition);
                return;
            }

            hasNoise = false;

            var distance = Vector3.Distance(transform.position, target.transform.position);

            if (distance <= definition.AttackRange && Time.time >= nextAttackTime)
            {
                BeginAttack(definition);
                return;
            }

            Chase(definition);
        }

        private void Idle(EnemyDefinition definition)
        {
            Enter(EnemyBehaviourState.Idle);

            var agent = enemy.Agent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            agent.speed = definition.PatrolSpeed;

            if (agent.hasPath) agent.ResetPath();
        }

        private void Chase(EnemyDefinition definition)
        {
            Enter(EnemyBehaviourState.Chasing);

            var agent = enemy.Agent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            agent.speed = definition.ChaseSpeed;

            // A sprayer holds its distance; a biter closes all the way. Without this the ranged
            // enemies walk into the player they are shooting at and the attack reads as a shove.
            agent.stoppingDistance = definition.ChaseStopDistance;
            agent.isStopped = false;
            agent.SetDestination(target.transform.position);
        }

        private void Investigate(EnemyDefinition definition)
        {
            Enter(EnemyBehaviourState.Investigating);

            var agent = enemy.Agent;

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                hasNoise = false;
                return;
            }

            agent.speed = definition.ChaseSpeed;
            agent.stoppingDistance = 0f;
            agent.isStopped = false;
            agent.SetDestination(noisePoint);

            if (agent.pathPending) return;

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                hasNoise = false;
                return;
            }

            if (agent.remainingDistance > NoiseArriveDistance) return;

            if (noiseArrivedTime < 0f)
            {
                noiseArrivedTime = Time.time;
                return;
            }

            if (Time.time - noiseArrivedTime < definition.MemorySeconds) return;

            hasNoise = false;
            noiseArrivedTime = -1f;
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

        private void LandAttack(EnemyDefinition definition)
        {
            attackLandsAt = -1f;
            nextAttackTime = Time.time + definition.AttackCooldown;

            // Announced before anything is resolved, and whether or not it connects: the blast
            // the players see is the attack happening, not the damage arriving.
            if (enemy != null) enemy.ServerAnnounceAttack(LandingPoint(definition));

            if (!definition.AttackHitsArea)
            {
                if (target != null) Hit(definition, target, false);
                return;
            }

            var players = Health.SpawnedPlayerList;

            for (var i = players.Count - 1; i >= 0; i--) Hit(definition, players[i], true);
        }

        private Vector3 LandingPoint(EnemyDefinition definition)
        {
            if (target != null) return CombatGeometry.AimPoint(target.NetworkObject);

            return transform.position
                   + Vector3.up * EyeHeight(definition)
                   + transform.forward * definition.AttackRange;
        }

        private void Hit(EnemyDefinition definition, Health victim, bool area)
        {
            if (victim == null || !victim.IsSpawned || !victim.State.IsStanding) return;

            var point = area
                ? CombatGeometry.AimPoint(victim.NetworkObject)
                : victim.transform.position + Vector3.up * (definition.BodyHeight * 0.5f);

            var offset = point - transform.position;

            if (offset.sqrMagnitude > definition.AttackRange * definition.AttackRange) return;

            if (area)
            {
                var flat = new Vector3(offset.x, 0f, offset.z);

                if (flat.sqrMagnitude > 0.0001f &&
                    Vector3.Angle(transform.forward, flat) > definition.AttackConeAngle * 0.5f)
                    return;
            }

            if (CombatGeometry.IsOccluded(transform, point, EyeHeight(definition))) return;

            var direction = offset.normalized;

            victim.ApplyDamage(new DamageInfo(
                definition.AttackDamage, definition.AttackDamageType, DamageInfo.World,
                point, direction));

            if (definition.AttackKnockback <= 0f) return;
            if (!victim.TryGetComponent<PlayerMovement>(out var movement)) return;

            var shove = new Vector3(direction.x, 0f, direction.z).normalized *
                        definition.AttackKnockback;

            // A little of it upward, so the player is lifted off the floor rather than scraped
            // along it — the shove is readable only if it breaks their footing.
            shove.y = definition.AttackKnockback * 0.3f;

            movement.ApplyKnockbackRpc(shove);
        }

        private void Enter(EnemyBehaviourState next)
        {
            if (state.Value != next) state.Value = next;
        }

        private void OnNoise(NoiseRaised evt)
        {
            if (!IsSpawned || target != null) return;
            if (health != null && !health.IsAlive) return;

            var definition = enemy != null ? enemy.Definition : null;
            if (definition == null) return;

            var reach = Mathf.Min(definition.HearingRadius, evt.Radius);
            if ((evt.Position - transform.position).sqrMagnitude > reach * reach) return;

            hasNoise = true;
            noisePoint = evt.Position;
            noiseArrivedTime = -1f;
        }

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

            var clients = NetworkManager.ConnectedClientsList;

            for (var i = 0; i < clients.Count; i++)
            {
                var player = clients[i].PlayerObject;
                if (player == null || !player.IsSpawned) continue;

                var candidate = player.GetComponent<Health>();

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

            var flat = new Vector3(offset.x, 0f, offset.z);

            if (flat.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, flat) > definition.SightAngle * 0.5f)
                return false;

            return !CombatGeometry.IsOccluded(transform, point, eye);
        }

        private static float EyeHeight(EnemyDefinition definition) => definition.BodyHeight * 0.85f;

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
