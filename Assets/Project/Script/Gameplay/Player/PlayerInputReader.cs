using UnityEngine;
using UnityEngine.InputSystem;

namespace Office.Gameplay
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string MapName = "Player";

        [Tooltip("Leave empty to use the project-wide input actions configured in " +
                 "Project Settings > Input System Package.")]
        [SerializeField] private InputActionAsset overrideAsset;

        private InputActionMap map;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction jumpAction;
        private InputAction interactAction;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool InteractPressedThisFrame { get; private set; }

        public bool LookIsPointerDelta { get; private set; } = true;

        private void Awake()
        {
            var asset = overrideAsset != null ? overrideAsset : InputSystem.actions;

            if (asset == null)
            {
                Debug.LogError("[Input] No input actions asset. Assign one on the player prefab " +
                               "or set the project-wide actions.");
                enabled = false;
                return;
            }

            map = asset.FindActionMap(MapName, throwIfNotFound: false);

            if (map == null)
            {
                Debug.LogError($"[Input] Action map '{MapName}' not found in '{asset.name}'.");
                enabled = false;
                return;
            }

            moveAction = Resolve("Move");
            lookAction = Resolve("Look");
            sprintAction = Resolve("Sprint");
            crouchAction = Resolve("Crouch");
            jumpAction = Resolve("Jump");
            interactAction = Resolve("Interact");
        }

        private InputAction Resolve(string actionName)
        {
            var action = map.FindAction(actionName, throwIfNotFound: false);
            if (action == null) Debug.LogError($"[Input] Action '{actionName}' missing from '{MapName}'.");
            return action;
        }

        private void OnEnable() => map?.Enable();

        private void OnDisable()
        {
            map?.Disable();
            Clear();
        }

        private void Update()
        {
            Move = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            Look = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;

            SprintHeld = sprintAction?.IsPressed() ?? false;
            CrouchHeld = crouchAction?.IsPressed() ?? false;
            JumpPressedThisFrame = jumpAction?.WasPressedThisFrame() ?? false;
            InteractPressedThisFrame = interactAction?.WasPressedThisFrame() ?? false;

            var device = lookAction?.activeControl?.device;
            if (device != null) LookIsPointerDelta = device is Pointer;
        }

        private void Clear()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
            CrouchHeld = false;
            JumpPressedThisFrame = false;
            InteractPressedThisFrame = false;
        }
    }
}
