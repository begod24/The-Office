using Office.Data;
using UnityEngine;

namespace Office.Enemies
{
    [DisallowMultipleComponent]
    public sealed class ProjectorView : EnemyView
    {
        [Header("Bones")]
        [SerializeField] private Transform headPan;

        [SerializeField] private Transform headTilt;

        [Header("Lamp")]
        [SerializeField] private Light lamp;

        [Tooltip("The renderer carrying the lens material, and which of its materials that is. " +
                 "The colour is pushed through a property block so one material asset can serve " +
                 "every projector in the room and still show a different mood on each.")]
        [SerializeField] private Renderer lensRenderer;

        [Min(0)]
        [SerializeField] private int lensMaterial = 1;

        [Header("Colour")]
        [Tooltip("Searching, hunting, and the moment it fires. README: the lens goes blue to red.")]
        [ColorUsage(true, true)]
        [SerializeField] private Color idleColour = new(0.15f, 0.5f, 1f, 1f);

        [ColorUsage(true, true)]
        [SerializeField] private Color huntColour = new(1f, 0.12f, 0.08f, 1f);

        [ColorUsage(true, true)]
        [SerializeField] private Color flashColour = new(1f, 0.85f, 0.7f, 1f);

        [Min(0f)]
        [SerializeField] private float idleIntensity = 4f;

        [Min(0f)]
        [SerializeField] private float huntIntensity = 7f;

        [Min(0f)]
        [SerializeField] private float flashIntensity = 45f;

        [Tooltip("Seconds the flash takes to fade. Short: this is a camera, not a firework.")]
        [Min(0.05f)]
        [SerializeField] private float flashFade = 0.3f;

        [Header("Head")]
        [Tooltip("Degrees the head sweeps either side while it has found nothing, and how many " +
                 "sweeps a second. The sweep is the enemy's approach sound made visible — you " +
                 "see the beam before it sees you.")]
        [Range(0f, 170f)]
        [SerializeField] private float scanYaw = 55f;

        [Min(0.01f)]
        [SerializeField] private float scanSpeed = 0.18f;

        [Range(0f, 45f)]
        [SerializeField] private float scanPitch = 8f;

        [Range(0f, 180f)]
        [SerializeField] private float yawLimit = 110f;

        [Range(0f, 90f)]
        [SerializeField] private float pitchLimit = 45f;

        [Min(1f)]
        [SerializeField] private float aimSpeed = 260f;

        [Tooltip("Degrees the head droops once it is dead.")]
        [Range(0f, 90f)]
        [SerializeField] private float deadDroop = 40f;

        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;

        private Quaternion restPan;
        private Quaternion restTilt;

        private float yaw;
        private float pitch;
        private float flash;
        private float recoil;
        private float scanPhase;

        protected override void Awake()
        {
            base.Awake();

            if (headPan != null) restPan = headPan.localRotation;
            if (headTilt != null) restTilt = headTilt.localRotation;

            block = new MaterialPropertyBlock();
            scanPhase = Random.value * 100f;
        }

        protected override void Animate(float deltaTime)
        {
            flash = Mathf.MoveTowards(flash, 0f, deltaTime / flashFade);
            recoil = Mathf.MoveTowards(recoil, 0f, deltaTime / 0.25f);

            if (Walker != null) Walker.ExtraPitch = -recoil * 5f;

            UpdateHead(deltaTime);
            UpdateLamp();
        }

        private void UpdateHead(float deltaTime)
        {
            var desiredYaw = yaw;
            var desiredPitch = pitch;

            if (IsDead)
            {
                desiredPitch = deadDroop;
            }
            else if (IsHunting && HasAim)
            {
                var origin = headTilt != null ? headTilt.position : transform.position;
                var angles = AimAngles(AimDirection(origin));

                desiredYaw = Mathf.Clamp(angles.x, -yawLimit, yawLimit);
                desiredPitch = Mathf.Clamp(angles.y, -pitchLimit, pitchLimit);
            }
            else
            {
                // A searching head sweeps faster than a bored one, so an investigating projector
                // reads as agitated from across the room.
                var speed = State == EnemyBehaviourState.Investigating ? scanSpeed * 2.5f : scanSpeed;
                var phase = (Time.time + scanPhase) * speed * Mathf.PI * 2f;

                desiredYaw = Mathf.Sin(phase) * scanYaw;
                desiredPitch = Mathf.Cos(phase * 0.5f) * scanPitch;
            }

            var step = aimSpeed * deltaTime;

            yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, step);
            pitch = Mathf.MoveTowardsAngle(pitch, desiredPitch, step);

            if (headPan != null)
            {
                headPan.localRotation = restPan;
                ApplyAim(headPan, yaw, 0f);
            }

            if (headTilt == null) return;

            headTilt.localRotation = restTilt;
            ApplyPitch(headTilt, yaw, pitch);
        }

        private void UpdateLamp()
        {
            var colour = idleColour;
            var intensity = idleIntensity;

            if (IsDead)
            {
                colour = Color.black;
                intensity = 0f;
            }
            else if (IsHunting)
            {
                colour = huntColour;
                intensity = huntIntensity;

                // The flicker is the wind-up made readable: by the time it is steady again, the
                // attack has already landed.
                if (State == EnemyBehaviourState.Attacking)
                {
                    var flicker = 1f + Mathf.Sin(Time.time * 45f) * 0.35f * WindupProgress;
                    intensity *= flicker;
                }
            }

            if (flash > 0f)
            {
                colour = Color.Lerp(colour, flashColour, flash);
                intensity = Mathf.Lerp(intensity, flashIntensity, flash);
            }

            if (lamp != null)
            {
                lamp.color = colour;
                lamp.intensity = intensity;
                lamp.enabled = intensity > 0.01f;
            }

            if (lensRenderer == null) return;

            lensRenderer.GetPropertyBlock(block, lensMaterial);
            block.SetColor(EmissionColour, colour * Mathf.Max(0.2f, intensity * 0.4f));
            lensRenderer.SetPropertyBlock(block, lensMaterial);
        }

        protected override void Fire(Vector3 point)
        {
            flash = 1f;
            recoil = 1f;
        }
    }
}
