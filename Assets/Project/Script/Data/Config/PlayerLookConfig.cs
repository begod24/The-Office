using UnityEngine;

namespace Office.Data
{
    [CreateAssetMenu(menuName = "Office/Config/Player Look", fileName = "CFG_PlayerLook")]
    public sealed class PlayerLookConfig : ScriptableObject
    {
        [Header("Sensitivity")]
        [Tooltip("Degrees per unit of mouse delta. Runtime sensitivity from the settings menu multiplies this.")]
        [SerializeField] private float mouseSensitivity = 0.09f;
        [Tooltip("Degrees per second at full gamepad stick deflection.")]
        [SerializeField] private float gamepadSensitivity = 160f;
        [SerializeField] private bool invertY;

        [Header("Limits")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Camera")]
        [Tooltip("Eye height above the controller's feet when standing.")]
        [SerializeField] private float standEyeHeight = 1.62f;
        [Tooltip("Eye height above the controller's feet when crouching.")]
        [SerializeField] private float crouchEyeHeight = 0.92f;
        [SerializeField] private float fieldOfView = 68f;

        [Header("View bob")]
        [SerializeField] private bool bobEnabled = true;
        [SerializeField] private float bobAmplitude = 0.035f;
        [SerializeField] private float bobFrequency = 9f;

        public float MouseSensitivity => mouseSensitivity;
        public float GamepadSensitivity => gamepadSensitivity;
        public bool InvertY => invertY;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float StandEyeHeight => standEyeHeight;
        public float CrouchEyeHeight => crouchEyeHeight;
        public float FieldOfView => fieldOfView;
        public bool BobEnabled => bobEnabled;
        public float BobAmplitude => bobAmplitude;
        public float BobFrequency => bobFrequency;
    }
}
