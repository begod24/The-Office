using UnityEngine;

namespace Office.Data
{
    [CreateAssetMenu(menuName = "Office/Config/Player Movement", fileName = "CFG_PlayerMovement")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [Header("Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float sprintSpeed = 5.6f;
        [SerializeField] private float crouchSpeed = 1.5f;

        [Header("Acceleration")]
        [Tooltip("How fast horizontal velocity reaches the target speed on the ground. Higher is snappier.")]
        [SerializeField] private float groundAcceleration = 14f;
        [Tooltip("Airborne control. Deliberately low — these are office workers, not soldiers.")]
        [SerializeField] private float airAcceleration = 2.5f;

        [Header("Body")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.05f;
        [SerializeField] private float radius = 0.32f;
        [Tooltip("Seconds to blend between stand and crouch height.")]
        [SerializeField] private float crouchTransitionTime = 0.14f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -18f;
        [Tooltip("Downward force kept while grounded so the controller stays glued to slopes and stairs.")]
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Jump")]
        [Tooltip("Off by design — GDD §7.1 lists walk, sprint, crouch and vault, not jump. " +
                 "Enable only in greybox sandbox configs for testing geometry.")]
        [SerializeField] private bool canJump;
        [SerializeField] private float jumpHeight = 0.9f;

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float sprintDrainPerSecond = 18f;
        [SerializeField] private float recoveryPerSecond = 12f;
        [Tooltip("Seconds after sprinting before stamina starts to recover.")]
        [SerializeField] private float recoveryDelay = 0.8f;
        [Tooltip("Stamina required to start a sprint. Prevents stutter-sprinting at zero.")]
        [SerializeField] private float sprintUnlockThreshold = 15f;

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float CrouchSpeed => crouchSpeed;
        public float GroundAcceleration => groundAcceleration;
        public float AirAcceleration => airAcceleration;
        public float StandHeight => standHeight;
        public float CrouchHeight => crouchHeight;
        public float Radius => radius;
        public float CrouchTransitionTime => crouchTransitionTime;
        public float Gravity => gravity;
        public float GroundedStickForce => groundedStickForce;
        public bool CanJump => canJump;
        public float JumpHeight => jumpHeight;
        public float MaxStamina => maxStamina;
        public float SprintDrainPerSecond => sprintDrainPerSecond;
        public float RecoveryPerSecond => recoveryPerSecond;
        public float RecoveryDelay => recoveryDelay;
        public float SprintUnlockThreshold => sprintUnlockThreshold;
    }
}
