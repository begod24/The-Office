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

        public float SensitivityScale { get; set; } = 1f;

        private float pitch;
        private float bobPhase;

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
            if (!IsOwner || input == null) return;

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
