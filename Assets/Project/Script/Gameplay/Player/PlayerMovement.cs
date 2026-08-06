using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private PlayerMovementConfig config;
        [SerializeField] private PlayerInputReader input;

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

        public float CrouchBlend { get; private set; }

        public bool IsCrouching => isCrouching;
        public bool IsSprinting { get; private set; }
        public bool IsGrounded => controller != null && controller.isGrounded;

        public float NormalizedSpeed =>
            config == null ? 0f : Mathf.Clamp01(horizontalVelocity.magnitude / config.SprintSpeed);

        public Vector3 PlanarVelocity => horizontalVelocity;
        public float MaxPlanarSpeed => config == null ? 1f : config.SprintSpeed;

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
                    sprintLocked = true;
                }
            }
            else if (Time.time - lastSprintTime >= config.RecoveryDelay)
            {
                stamina = Mathf.Min(config.MaxStamina, stamina + config.RecoveryPerSecond * deltaTime);

                if (sprintLocked && stamina >= config.SprintUnlockThreshold) sprintLocked = false;
            }

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
                verticalVelocity = config.GroundedStickForce;

                if (config.CanJump && !isCrouching && input.JumpPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(-2f * config.Gravity * config.JumpHeight);
            }

            verticalVelocity += config.Gravity * deltaTime;
        }
    }
}
