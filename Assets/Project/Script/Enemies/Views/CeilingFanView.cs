using Office.Data;
using UnityEngine;

namespace Office.Enemies
{
    [DisallowMultipleComponent]
    public sealed class CeilingFanView : EnemyView
    {
        [Header("Bones")]
        [SerializeField] private Transform housing;

        [SerializeField] private Transform rotor;

        [Header("Effects")]
        [Tooltip("The blast itself. Fired once, where the server says the attack landed.")]
        [SerializeField] private ParticleSystem gust;

        [Tooltip("Optional loop that runs while the blades are up to speed.")]
        [SerializeField] private ParticleSystem draft;

        [Header("Blades")]
        [Tooltip("Degrees per second at rest, while hunting, and at the top of a wind-up. The " +
                 "ramp between the last two is the tell: the player hears and sees the blades " +
                 "spin up before anything is thrown at them.")]
        [SerializeField] private float idleSpin = 240f;

        [SerializeField] private float huntSpin = 900f;

        [SerializeField] private float blastSpin = 3200f;

        [Min(1f)]
        [SerializeField] private float spinAcceleration = 2400f;

        [Tooltip("Degrees per second above which the draft loop emits.")]
        [Min(0f)]
        [SerializeField] private float draftThreshold = 800f;

        [Header("Aim")]
        [Range(0f, 180f)]
        [SerializeField] private float yawLimit = 65f;

        [Range(0f, 90f)]
        [SerializeField] private float pitchLimit = 35f;

        [Min(1f)]
        [SerializeField] private float aimSpeed = 90f;

        [Tooltip("Degrees the housing drifts side to side when there is nothing to point at.")]
        [Range(0f, 90f)]
        [SerializeField] private float idleSway = 22f;

        [Min(0.01f)]
        [SerializeField] private float idleSwaySpeed = 0.25f;

        [Tooltip("Degrees of judder at the top of a wind-up.")]
        [Range(0f, 20f)]
        [SerializeField] private float windupShake = 4f;

        [Header("Recoil")]
        [Tooltip("Metres the body is shoved back by its own blast, and degrees it rears up. A " +
                 "thing that throws air this hard should feel it.")]
        [Min(0f)]
        [SerializeField] private float recoilPush = 0.22f;

        [Range(0f, 45f)]
        [SerializeField] private float recoilTilt = 12f;

        [Tooltip("Metres the body braces down into while winding up.")]
        [Min(0f)]
        [SerializeField] private float windupCrouch = 0.12f;

        private Quaternion restHousing;
        private Quaternion restRotor;

        private float spin;
        private float spinAngle;
        private float yaw;
        private float pitch;
        private float recoil;
        private float swayPhase;

        protected override void Awake()
        {
            base.Awake();

            if (housing != null) restHousing = housing.localRotation;
            if (rotor != null) restRotor = rotor.localRotation;

            spin = idleSpin;
            swayPhase = Random.value * 100f;
        }

        protected override void Animate(float deltaTime)
        {
            UpdateBlades(deltaTime);
            UpdateHousing(deltaTime);
            UpdateBody(deltaTime);
        }

        private void UpdateBlades(float deltaTime)
        {
            var target = idleSpin;

            if (IsDead) target = 0f;
            else if (State == EnemyBehaviourState.Attacking)
                target = Mathf.Lerp(huntSpin, blastSpin, WindupProgress);
            else if (IsHunting) target = huntSpin;

            spin = Mathf.MoveTowards(spin, target, spinAcceleration * deltaTime);
            spinAngle = Mathf.Repeat(spinAngle + spin * deltaTime, 360f);

            if (rotor != null)
                rotor.localRotation = restRotor * Quaternion.Euler(0f, spinAngle, 0f);

            if (draft == null) return;

            var emission = draft.emission;
            emission.enabled = spin > draftThreshold && !IsDead;
        }

        private void UpdateHousing(float deltaTime)
        {
            if (housing == null) return;

            housing.localRotation = restHousing;

            var desiredYaw = 0f;
            var desiredPitch = 0f;

            if (IsHunting && HasAim)
            {
                var angles = AimAngles(AimDirection(housing.position));

                desiredYaw = Mathf.Clamp(angles.x, -yawLimit, yawLimit);
                desiredPitch = Mathf.Clamp(angles.y, -pitchLimit, pitchLimit);
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

            var shake = windupShake * WindupProgress * WindupProgress;

            var shakeYaw = shake * (Mathf.PerlinNoise(Time.time * 37f, 0f) - 0.5f) * 2f;
            var shakePitch = shake * (Mathf.PerlinNoise(0f, Time.time * 41f) - 0.5f) * 2f;

            ApplyAim(housing, yaw + shakeYaw, pitch + shakePitch);
        }

        private void UpdateBody(float deltaTime)
        {
            if (Walker == null) return;

            recoil = Mathf.MoveTowards(recoil, 0f, deltaTime / 0.35f);

            var crouch = windupCrouch * WindupProgress;

            Walker.BodyOffset = -transform.forward * (recoil * recoilPush) +
                                Vector3.down * crouch;

            Walker.ExtraPitch = -recoil * recoilTilt;
        }

        protected override void Fire(Vector3 point)
        {
            recoil = 1f;
            spin = blastSpin;

            if (gust != null)
            {
                gust.transform.rotation = Quaternion.LookRotation(
                    AimDirection(gust.transform.position), Vector3.up);

                gust.Play(true);
            }
        }
    }
}
