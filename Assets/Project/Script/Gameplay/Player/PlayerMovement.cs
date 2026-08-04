using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Walk, sprint, crouch and stamina. Technical Plan §7.2.
    ///
    /// Authority: client-authoritative (§2.3). The owner simulates its own movement and a
    /// NetworkTransform in Owner mode replicates the result. This is a friends-only co-op game,
    /// so cheating is not a threat, and it saves implementing prediction and reconciliation —
    /// weeks of work for no benefit here.
    ///
    /// Non-owners run no logic at all: <see cref="PlayerRig"/> disables their CharacterController
    /// so it cannot fight the replicated transform.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private PlayerMovementConfig config;
        [SerializeField] private PlayerInputReader input;

        /// <summary>Replicated so teammate HUDs and breathing audio can react. Owner writes.</summary>
        private readonly NetworkVariable<float> replicatedStamina = new(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private CharacterController controller;

        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float stamina;
        private float lastSprintTime = float.NegativeInfinity;
        private bool sprintLocked;
        private bool isCrouching;
        private float currentHeight;

        /// <summary>0 when standing, 1 when fully crouched. Drives the camera eye height.</summary>
        public float CrouchBlend { get; private set; }

        public bool IsCrouching => isCrouching;
        public bool IsSprinting { get; private set; }
        public bool IsGrounded => controller != null && controller.isGrounded;

        /// <summary>Horizontal speed as a fraction of sprint speed. Used by view bob and audio.</summary>
        public float NormalizedSpeed =>
            config == null ? 0f : Mathf.Clamp01(horizontalVelocity.magnitude / config.SprintSpeed);

        public float Stamina => IsOwner ? stamina : replicatedStamina.Value;
        public float NormalizedStamina => config == null ? 1f : Stamina / config.MaxStamina;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (config == null)
            {
                Debug.LogError($"[Player] {name} has no PlayerMovementConfig. Movement disabled.");
                enabled = false;
                return;
            }

            stamina = config.MaxStamina;
            currentHeight = config.StandHeight;
            ApplyControllerDimensions();
        }

        public override void OnNetworkSpawn()
        {
            // Non-owners keep this component for its replicated properties but simulate nothing.
            if (!IsOwner) return;

            stamina = config.MaxStamina;
            replicatedStamina.Value = stamina;
        }

        private void Update()
        {
            if (!IsOwner || input == null) return;

            var deltaTime = Time.deltaTime;

            UpdateCrouch(deltaTime);
            UpdateStamina(deltaTime);
            UpdateHorizontalVelocity(deltaTime);
            UpdateVerticalVelocity(deltaTime);

            var motion = horizontalVelocity;
            motion.y = verticalVelocity;
            controller.Move(motion * deltaTime);
        }

        private void UpdateCrouch(float deltaTime)
        {
            var wantsCrouch = input.CrouchHeld;

            // Refusing to stand under a desk is better than clipping through it.
            if (!wantsCrouch && isCrouching && !HasHeadroomToStand()) wantsCrouch = true;

            isCrouching = wantsCrouch;

            var targetHeight = isCrouching ? config.CrouchHeight : config.StandHeight;
            var range = Mathf.Max(0.01f, config.StandHeight - config.CrouchHeight);
            var speed = range / Mathf.Max(0.01f, config.CrouchTransitionTime);

            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, speed * deltaTime);
            CrouchBlend = Mathf.InverseLerp(config.StandHeight, config.CrouchHeight, currentHeight);

            ApplyControllerDimensions();
        }

        private void ApplyControllerDimensions()
        {
            controller.radius = config.Radius;
            controller.height = currentHeight;
            // The prefab pivot sits at the feet, so the capsule centre is half its height up.
            controller.center = new Vector3(0f, currentHeight * 0.5f, 0f);
        }

        private bool HasHeadroomToStand()
        {
            var headCentre = transform.position + Vector3.up * (config.StandHeight - config.Radius);
            var probeRadius = Mathf.Max(0.05f, config.Radius - 0.02f);

            return !Physics.CheckSphere(headCentre, probeRadius, PhysicsLayers.WalkableMask,
                QueryTriggerInteraction.Ignore);
        }

        private void UpdateStamina(float deltaTime)
        {
            if (IsSprinting)
            {
                stamina -= config.SprintDrainPerSecond * deltaTime;
                lastSprintTime = Time.time;

                if (stamina <= 0f)
                {
                    stamina = 0f;
                    // Latched: you cannot stutter-sprint at zero. Recover past the threshold first.
                    sprintLocked = true;
                }
            }
            else if (Time.time - lastSprintTime >= config.RecoveryDelay)
            {
                stamina = Mathf.Min(config.MaxStamina, stamina + config.RecoveryPerSecond * deltaTime);

                if (sprintLocked && stamina >= config.SprintUnlockThreshold) sprintLocked = false;
            }

            // Replicate on meaningful change only. Writing a float every frame would burn
            // bandwidth for a value nobody reads at that resolution.
            if (Mathf.Abs(replicatedStamina.Value - stamina) >= 1f ||
                (stamina <= 0f && replicatedStamina.Value > 0f) ||
                (Mathf.Approximately(stamina, config.MaxStamina) &&
                 replicatedStamina.Value < config.MaxStamina))
            {
                replicatedStamina.Value = stamina;
            }
        }

        private void UpdateHorizontalVelocity(float deltaTime)
        {
            var move = Vector2.ClampMagnitude(input.Move, 1f);
            var wishDirection = transform.right * move.x + transform.forward * move.y;

            IsSprinting = input.SprintHeld
                          && !isCrouching
                          && !sprintLocked
                          && stamina > 0f
                          && move.y > 0.1f;

            var speed = isCrouching ? config.CrouchSpeed
                : IsSprinting ? config.SprintSpeed
                : config.WalkSpeed;

            var target = wishDirection * speed;
            var acceleration = controller.isGrounded
                ? config.GroundAcceleration
                : config.AirAcceleration;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity, target, acceleration * deltaTime);
        }

        private void UpdateVerticalVelocity(float deltaTime)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                // A small constant downward force keeps the controller glued to stairs and
                // slopes; zero would make it hop down every step.
                verticalVelocity = config.GroundedStickForce;

                if (config.CanJump && !isCrouching && input.JumpPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(-2f * config.Gravity * config.JumpHeight);
            }

            verticalVelocity += config.Gravity * deltaTime;
        }
    }
}
