using Office.Data;
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
        private InputAction dropAction;
        private InputAction previousAction;
        private InputAction nextAction;
        private InputAction[] slotActions;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool InteractPressedThisFrame { get; private set; }
        public bool DropPressedThisFrame { get; private set; }

        /// <summary>
        /// Zero-based slot the player asked for by number this frame, or -1 for none.
        /// Keyboard selection is direct: key 1 is slot 1, and nothing steps or wraps.
        /// </summary>
        public int HotbarSlot { get; private set; } = -1;

        /// <summary>
        /// One step through the hotbar this frame, from the gamepad d-pad. Negative is
        /// towards slot one. A pad has no number row, so it is the only stepping input.
        /// </summary>
        public int HotbarStep { get; private set; }

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
            dropAction = Resolve("Drop");
            previousAction = Resolve("Previous");
            nextAction = Resolve("Next");

            // One action per slot rather than one clever action: a designer rebinding this
            // sees five named rows in the Input Actions window instead of scale processors.
            slotActions = new InputAction[GameplayConstants.InventorySlots];
            for (var i = 0; i < slotActions.Length; i++) slotActions[i] = Resolve($"Hotbar{i + 1}");
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
            // WasPressedThisFrame reads the raw actuation, so the 'Hold' interaction the
            // template put on Interact does not delay it. Interact stays press-to-use.
            InteractPressedThisFrame = interactAction?.WasPressedThisFrame() ?? false;
            DropPressedThisFrame = dropAction?.WasPressedThisFrame() ?? false;

            HotbarStep = (nextAction?.WasPressedThisFrame() ?? false ? 1 : 0)
                         - (previousAction?.WasPressedThisFrame() ?? false ? 1 : 0);

            HotbarSlot = ReadSlot();

            var device = lookAction?.activeControl?.device;
            if (device != null) LookIsPointerDelta = device is Pointer;
        }

        private int ReadSlot()
        {
            if (slotActions == null) return -1;

            for (var i = 0; i < slotActions.Length; i++)
                if (slotActions[i] != null && slotActions[i].WasPressedThisFrame())
                    return i;

            return -1;
        }

        private void Clear()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
            CrouchHeld = false;
            JumpPressedThisFrame = false;
            InteractPressedThisFrame = false;
            DropPressedThisFrame = false;
            HotbarStep = 0;
            HotbarSlot = -1;
        }
    }
}
