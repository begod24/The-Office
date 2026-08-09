using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    public sealed class PlayerLook : NetworkBehaviour
    {
        [SerializeField] private PlayerLookConfig config;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMovement movement;

        [Tooltip("Child transform the camera is parented to. Moves with crouch and view bob.")]
        [SerializeField] private Transform cameraPivot;

        [SerializeField] private Camera playerCamera;

        /// <summary>
        /// Where this player is looking vertically, in degrees. Owner-written and readable
        /// everywhere.
        /// </summary>
        /// <remarks>
        /// Yaw comes free — it is the body's rotation, which <c>NetworkTransform</c> already
        /// replicates. Pitch does not, because it lives on a child pivot that only the owner
        /// turns. Without this, anything mounted on the pivot points straight ahead on every
        /// other machine: a teammate lighting the ceiling would appear to light the far wall.
        /// <para>
        /// Owner-authoritative, like stamina and the selected slot, and for the same reason —
        /// it decides nothing. Every ruling the server makes about reach is measured from the
        /// body, never from this.
        /// </para>
        /// </remarks>
        private readonly NetworkVariable<float> replicatedPitch = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Degrees of change before the wire hears about it.</summary>
        /// <remarks>
        /// A mouse moves every frame, and a NetworkVariable written every frame is a message
        /// every tick for the whole run. Two degrees is invisible on a light cone at any range
        /// worth aiming at, and it turns continuous mouse movement into a handful of updates.
        /// </remarks>
        private const float PitchSendThreshold = 2f;

        public float SensitivityScale { get; set; } = 1f;

        private float pitch;
        private float bobPhase;

        /// <summary>Look pitch in degrees — the owner's own, or the replicated one.</summary>
        public float Pitch => IsOwner ? pitch : replicatedPitch.Value;

        private void Awake()
        {
            if (config == null || cameraPivot == null)
            {
                Debug.LogError($"[Player] {name} is missing its look config or camera pivot.");
                enabled = false;
                return;
            }

            if (playerCamera != null) playerCamera.fieldOfView = config.FieldOfView;
        }

        private void LateUpdate()
        {
            if (!IsOwner)
            {
                // Remote instances still have to aim the pivot, or everything hanging off it
                // stays level while its owner looks up and down.
                if (IsSpawned && cameraPivot != null)
                    cameraPivot.localRotation = Quaternion.Euler(replicatedPitch.Value, 0f, 0f);

                return;
            }

            if (input == null) return;

            ApplyRotation();
            ApplyEyePosition();
        }

        private void ApplyRotation()
        {
            var look = input.Look;

            var scale = input.LookIsPointerDelta
                ? config.MouseSensitivity
                : config.GamepadSensitivity * Time.deltaTime;

            scale *= SensitivityScale;

            var yawDelta = look.x * scale;
            var pitchDelta = look.y * scale * (config.InvertY ? 1f : -1f);

            if (!Mathf.Approximately(yawDelta, 0f))
                transform.Rotate(0f, yawDelta, 0f, Space.Self);

            pitch = Mathf.Clamp(pitch + pitchDelta, config.MinPitch, config.MaxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            PublishPitch();
        }

        // Also sends when the pitch settles on a limit, so a player holding the mouse against
        // the top of their range does not leave everyone else seeing them a step short of it.
        private void PublishPitch()
        {
            if (!IsSpawned) return;

            var atLimit = Mathf.Approximately(pitch, config.MinPitch) ||
                          Mathf.Approximately(pitch, config.MaxPitch);

            if (!atLimit && Mathf.Abs(replicatedPitch.Value - pitch) < PitchSendThreshold) return;
            if (Mathf.Approximately(replicatedPitch.Value, pitch)) return;

            replicatedPitch.Value = pitch;
        }

        private void ApplyEyePosition()
        {
            var crouchBlend = movement != null ? movement.CrouchBlend : 0f;
            var eyeHeight = Mathf.Lerp(config.StandEyeHeight, config.CrouchEyeHeight, crouchBlend);

            var bobOffset = 0f;

            if (config.BobEnabled && movement != null)
            {
                var speed = movement.NormalizedSpeed;

                if (movement.IsGrounded && speed > 0.05f)
                {
                    bobPhase += Time.deltaTime * config.BobFrequency * speed;
                    bobOffset = Mathf.Sin(bobPhase) * config.BobAmplitude * speed;
                }
                else
                {
                    bobPhase = 0f;
                }
            }

            cameraPivot.localPosition = new Vector3(0f, eyeHeight + bobOffset, 0f);
        }
    }
}
