using Office.Data;
using UnityEngine;

namespace Office.Enemies
{
    [DisallowMultipleComponent]
    public sealed class WaterCoolerView : EnemyView
    {
        [Header("Bones")]
        [SerializeField] private Transform nozzle;

        [SerializeField] private Transform jug;

        [Header("Effects")]
        [Tooltip("The jet. One non-looping burst per attack, aimed where the server says the " +
                 "attack landed.")]
        [SerializeField] private ParticleSystem jet;

        [Tooltip("Optional drip that runs while the nozzle is pressurised — the tell before the " +
                 "jet.")]
        [SerializeField] private ParticleSystem drip;

        [Header("Aim")]
        [Range(0f, 180f)]
        [SerializeField] private float yawLimit = 75f;

        [Range(0f, 90f)]
        [SerializeField] private float pitchLimit = 45f;

        [Min(1f)]
        [SerializeField] private float aimSpeed = 160f;

        [Tooltip("Degrees the jet is aimed above the target to pay for the arc the water falls " +
                 "through on the way. Zero sprays everyone's shins.")]
        [Range(0f, 30f)]
        [SerializeField] private float arcCompensation = 6f;

        [Tooltip("Degrees the nozzle sweeps when there is nothing to point at.")]
        [Range(0f, 90f)]
        [SerializeField] private float idleSway = 15f;

        [Min(0.01f)]
        [SerializeField] private float idleSwaySpeed = 0.2f;

        [Header("Jug")]
        [Tooltip("Degrees the bottle leans per metre per second squared of the body's own " +
                 "acceleration. It is full of water and it is the top of a tall thing: it " +
                 "should never look bolted down.")]
        [Min(0f)]
        [SerializeField] private float swayPerAcceleration = 2.5f;

        [Range(0f, 45f)]
        [SerializeField] private float maxSway = 14f;

        [Tooltip("How fast the bottle chases its lean. Low values wallow, which is the point.")]
        [Min(0.5f)]
        [SerializeField] private float swayFollowSpeed = 6f;

        [Tooltip("Degrees of shudder while the pump is charging.")]
        [Range(0f, 20f)]
        [SerializeField] private float windupShake = 5f;

        [Header("Recoil")]
        [Min(0f)]
        [SerializeField] private float recoilPush = 0.1f;

        [Range(0f, 45f)]
        [SerializeField] private float recoilTilt = 7f;

        [Min(0f)]
        [SerializeField] private float windupCrouch = 0.08f;

        private Quaternion restNozzle;
        private Quaternion restJug;

        private float yaw;
        private float pitch;
        private float recoil;
        private float swayPhase;

        private Vector2 sway;

        protected override void Awake()
        {
            base.Awake();

            if (nozzle != null) restNozzle = nozzle.localRotation;
            if (jug != null) restJug = jug.localRotation;

            swayPhase = Random.value * 100f;
        }

        protected override void Animate(float deltaTime)
        {
            UpdateNozzle(deltaTime);
            UpdateJug(deltaTime);
            UpdateBody(deltaTime);
        }

        private void UpdateNozzle(float deltaTime)
        {
            if (nozzle == null) return;

            nozzle.localRotation = restNozzle;

            var desiredYaw = 0f;
            var desiredPitch = 0f;

            if (IsHunting && HasAim)
            {
                var angles = AimAngles(AimDirection(nozzle.position));

                desiredYaw = Mathf.Clamp(angles.x, -yawLimit, yawLimit);
                desiredPitch = Mathf.Clamp(angles.y - arcCompensation, -pitchLimit, pitchLimit);
            }
            else if (!IsDead)
            {
                desiredYaw = Mathf.Sin((Time.time + swayPhase) * idleSwaySpeed * Mathf.PI * 2f) *
                             idleSway;
            }
            else
            {
                desiredPitch = pitchLimit;
            }

            var speed = aimSpeed * deltaTime;

            yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, speed);
            pitch = Mathf.MoveTowardsAngle(pitch, desiredPitch, speed);

            ApplyAim(nozzle, yaw, pitch);

            if (drip == null) return;

            var emission = drip.emission;
            emission.enabled = State == EnemyBehaviourState.Attacking && !IsDead;
        }

        private void UpdateJug(float deltaTime)
        {
            if (jug == null) return;

            var target = Vector2.zero;

            if (Walker != null && !IsDead)
            {
                var local = Reference.InverseTransformDirection(Walker.Acceleration);

                // Negated on purpose: the water inside lags the bottle, so accelerating forward
                // leans it back.
                target = new Vector2(
                    Mathf.Clamp(-local.z * swayPerAcceleration, -maxSway, maxSway),
                    Mathf.Clamp(local.x * swayPerAcceleration, -maxSway, maxSway));
            }

            if (IsDead) target = new Vector2(maxSway, 0f);

            sway = Vector2.Lerp(sway, target, 1f - Mathf.Exp(-swayFollowSpeed * deltaTime));

            var shake = windupShake * WindupProgress * WindupProgress;

            var shakeX = shake * (Mathf.PerlinNoise(Time.time * 43f, 0f) - 0.5f) * 2f;
            var shakeZ = shake * (Mathf.PerlinNoise(0f, Time.time * 47f) - 0.5f) * 2f;

            jug.localRotation = restJug * Quaternion.Euler(sway.x + shakeX, 0f, sway.y + shakeZ);
        }

        private void UpdateBody(float deltaTime)
        {
            if (Walker == null) return;

            recoil = Mathf.MoveTowards(recoil, 0f, deltaTime / 0.3f);

            Walker.BodyOffset = -transform.forward * (recoil * recoilPush) +
                                Vector3.down * (windupCrouch * WindupProgress);

            Walker.ExtraPitch = -recoil * recoilTilt;
        }

        protected override void Fire(Vector3 point)
        {
            recoil = 1f;

            if (jet == null) return;

            var look = Quaternion.LookRotation(AimDirection(jet.transform.position), Vector3.up);

            // Aimed above the target by the same angle the nozzle holds, because the jet is
            // thrown rather than traced: gravity takes the rest of it back down.
            jet.transform.rotation = look * Quaternion.Euler(-arcCompensation, 0f, 0f);

            jet.Play(true);
        }
    }
}
