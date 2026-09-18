using Office.Data;
using Office.Gameplay;
using UnityEngine;

namespace Office.Enemies
{
    // Runs after ProceduralWalker so the parts mounted on the body — a fan housing, a nozzle, a
    // camera head — are aimed from where the body actually ended up this frame.
    [DefaultExecutionOrder(50)]
    public abstract class EnemyView : MonoBehaviour
    {
        [SerializeField] protected ProceduralWalker Walker;

        [Tooltip("Seconds between scans for something to point at. This is presentation only — " +
                 "the server's brain decides what is actually attacked — so it can be lazy.")]
        [Min(0.02f)]
        [SerializeField] private float aimScanInterval = 0.2f;

        [Tooltip("Degrees per second the aimed parts turn. Slower reads as heavier and gives the " +
                 "player the tell GDD §9 asks every enemy to have.")]
        [Min(1f)]
        [SerializeField] private float aimTurnSpeed = 180f;

        private Enemy enemy;
        private EnemyBrain brain;
        private Health health;

        private Health aimTarget;
        private float nextScanTime;

        private EnemyBehaviourState state = EnemyBehaviourState.Idle;
        private float stateEnteredAt;

        private float collapse;

        protected EnemyDefinition Definition { get; private set; }

        protected EnemyBehaviourState State => state;

        protected float StateTime => Time.time - stateEnteredAt;

        protected bool IsDead => state == EnemyBehaviourState.Dead ||
                                 (health != null && !health.IsAlive);

        protected bool IsHunting => state == EnemyBehaviourState.Chasing ||
                                    state == EnemyBehaviourState.Attacking;

        // Where the enemy is pointing. Every machine picks it independently out of the players
        // it already has — nothing about the target crosses the wire, which is the whole reason
        // this animation is free (Architecture §13).
        protected Vector3 AimPoint { get; private set; }

        protected bool HasAim { get; private set; }

        protected float SightRadius => Definition != null ? Definition.SightRadius : 12f;

        protected float AttackWindup => Definition != null ? Definition.AttackWindup : 0.6f;

        // 0 while nothing is being charged, 1 at the moment the attack lands.
        protected float WindupProgress => state == EnemyBehaviourState.Attacking
            ? Mathf.Clamp01(StateTime / Mathf.Max(0.05f, AttackWindup))
            : 0f;

        protected virtual void Awake()
        {
            if (Walker == null) Walker = GetComponent<ProceduralWalker>();

            enemy = GetComponentInParent<Enemy>();
            brain = GetComponentInParent<EnemyBrain>();

            if (enemy != null)
            {
                health = enemy.Health;
                Definition = enemy.Definition;
                enemy.AttackLanded += OnAttackLanded;
            }

            AimPoint = transform.position + transform.forward * 2f + Vector3.up;

            // Staggered for the same reason the brain's scan is: a swarm spawns together, and a
            // schedule they all share turns a steady trickle into a spike every interval.
            nextScanTime = Time.time + UnityEngine.Random.Range(0f, aimScanInterval);
        }

        protected virtual void OnDestroy()
        {
            if (enemy != null) enemy.AttackLanded -= OnAttackLanded;
        }

        // Late, and after the walker: the body has already been placed for this frame, so a part
        // bolted to it is aimed from where it really is rather than from where it was.
        private void LateUpdate()
        {
            var deltaTime = Time.deltaTime;

            ReadState();
            ScanForAim(deltaTime);
            UpdateCollapse(deltaTime);

            Animate(deltaTime);
        }

        private void ReadState()
        {
            var next = brain != null ? brain.State : EnemyBehaviourState.Idle;

            if (health != null && !health.IsAlive) next = EnemyBehaviourState.Dead;

            if (next == state) return;

            state = next;
            stateEnteredAt = Time.time;

            OnStateChanged(next);
        }

        private void ScanForAim(float deltaTime)
        {
            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + aimScanInterval;
                aimTarget = IsDead ? null : FindTarget();
            }

            if (aimTarget != null && (!aimTarget.IsSpawned || !aimTarget.State.IsStanding))
                aimTarget = null;

            HasAim = aimTarget != null;

            if (!HasAim) return;

            var point = CombatGeometry.AimPoint(aimTarget);

            // The point is followed rather than snapped to, so a head that was looking elsewhere
            // swings across instead of teleporting onto the player.
            var distance = Vector3.Distance(AimPoint, transform.position);
            var speed = Mathf.Max(1f, distance) * aimTurnSpeed * Mathf.Deg2Rad;

            AimPoint = Vector3.MoveTowards(AimPoint, point, speed * deltaTime);
        }

        private Health FindTarget()
        {
            var players = Health.SpawnedPlayerList;

            Health nearest = null;
            var nearestDistance = SightRadius * SightRadius;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || !player.IsSpawned || !player.State.IsStanding) continue;

                var distance = (player.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;

                nearest = player;
                nearestDistance = distance;
            }

            return nearest;
        }

        private void UpdateCollapse(float deltaTime)
        {
            var target = IsDead ? 1f : 0f;

            collapse = Mathf.MoveTowards(collapse, target, deltaTime / 0.6f);

            if (Walker != null) Walker.Collapse = collapse;
        }

        protected float CollapseAmount => collapse;

        // Direction the aimed parts should point, flattened against nothing: a fan housing is
        // allowed to point up a staircase.
        protected Vector3 AimDirection(Vector3 from)
        {
            var offset = AimPoint - from;

            return offset.sqrMagnitude > 1e-6f ? offset.normalized : transform.forward;
        }

        // The frame the body is measured against: it is the body bone, so a housing bolted to a
        // pitching body pitches with it and only has to carry the difference.
        protected Transform Reference => Walker != null && Walker.Body != null
            ? Walker.Body
            : transform;

        // x is yaw, y is pitch, both in degrees and both relative to Reference. Pitch is
        // positive downward, which is the direction a rotation about the right axis goes.
        protected Vector2 AimAngles(Vector3 direction)
        {
            var local = Reference.InverseTransformDirection(direction);

            return new Vector2(
                Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg,
                -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg);
        }

        // Expects the bone to have been put back on its rest pose first, so nothing accumulates.
        protected void ApplyAim(Transform bone, float yaw, float pitch)
        {
            if (bone == null) return;

            var yawRotation = Quaternion.AngleAxis(yaw, Reference.up);

            bone.rotation = Quaternion.AngleAxis(pitch, yawRotation * Reference.right) *
                            yawRotation * bone.rotation;
        }

        // For the second half of a split head: the bone below a yawed one has already inherited
        // that yaw, so it must only add the pitch — but around the axis the yaw left behind.
        protected void ApplyPitch(Transform bone, float yaw, float pitch)
        {
            if (bone == null) return;

            var right = Quaternion.AngleAxis(yaw, Reference.up) * Reference.right;

            bone.rotation = Quaternion.AngleAxis(pitch, right) * bone.rotation;
        }

        private void OnAttackLanded(Vector3 point)
        {
            if (IsDead) return;

            AimPoint = point;
            Fire(point);
        }

        protected virtual void OnStateChanged(EnemyBehaviourState next)
        {
        }

        // The attack has landed on the server and this machine has been told where. Everything
        // the player sees of an attack happens here.
        protected virtual void Fire(Vector3 point)
        {
        }

        protected abstract void Animate(float deltaTime);
    }
}
