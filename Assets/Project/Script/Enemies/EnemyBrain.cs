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

        [Tooltip("Metres the thing being chased may drift before the path is asked for again. " +
                 "Re-planning is the most expensive thing navigation does, and GDD §9.1 has the " +
                 "host doing it for the whole swarm at once: a path to where someone stood a few " +
                 "centimetres ago arrives at the same place.")]
        [Min(0f)]
        [SerializeField] private float repathDistance = 0.4f;

        [Tooltip("Metres a drawn patrol point may be off the navigation mesh before it is thrown " +
                 "away and another is drawn. The point comes out of a circle that knows nothing " +
                 "about where the walls are, so most draws land inside geometry and this sample " +
                 "is what rescues them. Too small and a corridor enemy finds nothing to walk to; " +
                 "too large and it teleports its intent through a wall into the next room.")]
        [Min(0.1f)]
        [SerializeField] private float patrolSampleRadius = 2f;

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

        private Vector3 destination;
        private bool hasDestination;

        private Vector3 anchor;
        private bool hasAnchor;
        private bool hasPatrolPoint;
        private float patrolResumeTime;
        private int patrolFailures;

        private const float NoiseArriveDistance = 0.75f;

        // Draws per pick. Six is enough that a point in open floor is found on the first or
        // second try and a boxed-in marker gives up inside one frame instead of stalling.
        private const int PatrolDraws = 6;

        private const float PatrolArriveDistance = 0.5f;

        // A point at its own feet is a patrol that never moves. Anything closer than this is
        // redrawn, unless the enemy is already outside its circle and any point is progress home.
        private const float PatrolMinStep = 1.5f;

        // How long a failed pick waits before trying again. Without it a marker with no mesh
        // around it samples six times a frame forever.
        private const float PatrolBackoffSeconds = 1f;

        private const int PatrolFailuresBeforeWarning = 3;

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
                hasNoise = false;
                noiseArrivedTime = -1f;
                hasDestination = false;

                // The anchor is not taken here. Enemy snaps the transform onto the navigation
                // mesh in its own spawn, and nothing orders the two, so a position read now can
                // be the marker's rather than the one the agent actually stands on. It is taken
                // on the first frame the agent reports itself on the mesh instead.
                hasAnchor = false;
                hasPatrolPoint = false;
                patrolResumeTime = 0f;
                patrolFailures = 0;

                // Spread across the interval rather than landing on the same frame as everything
                // else that spawned with it. A scan is a raycast per player, so a schedule shared
                // by a whole swarm is a spike every interval instead of a steady trickle.
                nextScanTime = Time.time + UnityEngine.Random.Range(0f, scanInterval);

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
                else Patrol(definition);
                return;
            }

            hasNoise = false;

            var reach = definition.AttackRange;
            var offset = target.transform.position - transform.position;

            if (offset.sqrMagnitude <= reach * reach && Time.time >= nextAttackTime)
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

            if (!agent.hasPath) return;

            agent.ResetPath();
            hasDestination = false;
        }

        // Nothing seen, nothing heard. The enemy walks a circle around where it spawned, pausing
        // at each point it reaches. This is the state it is in for most of a run, so it is also
        // the one the player learns the building's threat from: a thing that only ever moves once
        // it has already noticed you gives them nothing to avoid.
        private void Patrol(EnemyDefinition definition)
        {
            var agent = enemy.Agent;

            // A marker off the mesh, or a definition that holds station. Both stand still, and
            // Idle is what standing still is called.
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || !definition.Patrols)
            {
                Idle(definition);
                return;
            }

            if (!hasAnchor)
            {
                anchor = transform.position;
                hasAnchor = true;
            }

            // Waiting out the pause at the point it just reached.
            if (Time.time < patrolResumeTime)
            {
                Idle(definition);
                return;
            }

            Enter(EnemyBehaviourState.Patrolling);

            agent.speed = definition.PatrolSpeed;
            agent.stoppingDistance = 0f;
            agent.isStopped = false;

            if (hasPatrolPoint && !agent.pathPending)
            {
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    hasPatrolPoint = false;
                }
                else if (agent.remainingDistance <= PatrolArriveDistance)
                {
                    hasPatrolPoint = false;
                    patrolResumeTime = Time.time + PauseFor(definition);
                    return;
                }
            }

            if (hasPatrolPoint) return;

            if (TryDrawPatrolPoint(agent, definition, out var point))
            {
                patrolFailures = 0;
                hasPatrolPoint = true;

                // Issued directly rather than through Steer. Steer exists to swallow re-paths at
                // a target that has only drifted a few centimetres; a patrol point is drawn once
                // and never moves, so the only thing that dedup could ever do here is swallow the
                // one call that matters and leave the enemy standing on its last destination.
                agent.SetDestination(point);

                destination = point;
                hasDestination = true;
                return;
            }

            // Nothing on the mesh inside the circle. Back off rather than redrawing every frame,
            // and say so once: a marker sealed in a cupboard is a level bug whose only other
            // symptom is an enemy that stands there, which is exactly what a working idle looks
            // like.
            patrolResumeTime = Time.time + PatrolBackoffSeconds;

            if (++patrolFailures == PatrolFailuresBeforeWarning)
                Debug.LogWarning(
                    $"[Enemy] '{definition.name}' found nowhere to patrol within " +
                    $"{definition.PatrolRadius:0.#}m of where it spawned. Check the marker is " +
                    "not boxed in by geometry, widen its PatrolRadius, or set it to zero if the " +
                    "enemy is meant to hold station.", this);

            Idle(definition);
        }

        // Jittered half either side so two of the same enemy spawned in one room fall out of step
        // with each other within a couple of points.
        private static float PauseFor(EnemyDefinition definition) =>
            definition.PatrolPause * UnityEngine.Random.Range(0.5f, 1.5f);

        private bool TryDrawPatrolPoint(NavMeshAgent agent, EnemyDefinition definition,
            out Vector3 point)
        {
            var radius = definition.PatrolRadius;

            // Outside its own circle — it gave up a chase somewhere else — every point on the
            // mesh is progress homeward, so the short-step rule is dropped for the walk back.
            var strayed = (transform.position - anchor).sqrMagnitude > radius * radius;

            for (var draw = 0; draw < PatrolDraws; draw++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * radius;
                var candidate = anchor + new Vector3(offset.x, 0f, offset.y);

                // The agent's own mask rather than every area: an enemy barred from a door area
                // must not patrol through it either.
                if (!NavMesh.SamplePosition(candidate, out var hit, patrolSampleRadius,
                        agent.areaMask))
                    continue;

                if (!strayed &&
                    (hit.position - transform.position).sqrMagnitude < PatrolMinStep * PatrolMinStep)
                    continue;

                point = hit.position;
                return true;
            }

            point = default;
            return false;
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

            Steer(agent, target.transform.position);
        }

        // A path is only asked for when the destination has actually moved somewhere else. The
        // agent keeps walking its current one in between, which is what it would have done with
        // a freshly planned identical path anyway.
        private void Steer(NavMeshAgent agent, Vector3 point)
        {
            if (hasDestination &&
                (point - destination).sqrMagnitude <= repathDistance * repathDistance)
                return;

            agent.SetDestination(point);

            destination = point;
            hasDestination = true;
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

            Steer(agent, noisePoint);

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

                hasDestination = false;
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
            if (target != null) return CombatGeometry.AimPoint(target);

            return transform.position
                   + Vector3.up * EyeHeight(definition)
                   + transform.forward * definition.AttackRange;
        }

        private void Hit(EnemyDefinition definition, Health victim, bool area)
        {
            if (victim == null || !victim.IsSpawned || !victim.State.IsStanding) return;

            var point = area
                ? CombatGeometry.AimPoint(victim)
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
            if (state.Value == next) return;

            // Leaving patrol throws the current point away. Whatever interrupted it will have
            // carried the enemy somewhere else, and a point chosen from the old position is no
            // longer the one it would pick from the new one. Coming back draws fresh.
            if (state.Value == EnemyBehaviourState.Patrolling) hasPatrolPoint = false;

            state.Value = next;
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

                var distance = (candidate.transform.position - transform.position).sqrMagnitude;
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
            var point = CombatGeometry.AimPoint(candidate);

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
