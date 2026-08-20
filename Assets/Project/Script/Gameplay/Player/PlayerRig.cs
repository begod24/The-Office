using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Office.Gameplay
{
    public sealed class PlayerRig : NetworkBehaviour
    {
        [Header("Owner only")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private PlayerInputReader inputReader;

        [Tooltip("Renderers hidden from the owner — their own body would fill the first-person view.")]
        [SerializeField] private Renderer[] bodyRenderers;

        [Header("Remote only")]
        [Tooltip("Disabled on remote instances so it cannot fight the replicated transform.")]
        [SerializeField] private CharacterController characterController;

        [Tooltip("Read to know when the owner stops being able to act. Resolved from this " +
                 "object when empty.")]
        [SerializeField] private Health health;

        [Header("Cursor")]
        [SerializeField] private bool manageCursor = true;

        private IEventBus bus;
        private bool paused;
        private bool inventoryOpen;
        private bool standing = true;

        /// <summary>
        /// Where this player is looking from. Used by a spectator watching them, which is why
        /// it is public: a dead player's camera has to go somewhere that still sees the run.
        /// </summary>
        public Transform EyeAnchor => playerCamera != null ? playerCamera.transform : transform;

        /// <summary>The owner's camera, active on this machine only for the local player.</summary>
        public Camera PlayerCamera => playerCamera;

        // Two overlays and a set of vitals, one input reader. Tracked separately so that
        // closing either overlay cannot hand control back to a player who is on the floor.
        private bool InputSuspended => paused || inventoryOpen || !standing;

        // Being down is not being in a menu. The view stays locked and first-person — what
        // the player has lost is the ability to act, and taking the mouse away as well would
        // read as the game having crashed at the exact moment they need to see the room.
        private bool CursorFree => paused || inventoryOpen;

        public override void OnNetworkSpawn()
        {
            var owner = IsOwner;

            if (health == null) health = GetComponent<Health>();

            if (playerCamera != null) playerCamera.gameObject.SetActive(owner);
            if (audioListener != null) audioListener.enabled = owner;
            if (characterController != null) characterController.enabled = owner;

            if (bodyRenderers != null)
                foreach (var renderer in bodyRenderers)
                    if (renderer != null)
                        renderer.enabled = !owner;

            if (!owner)
            {
                if (inputReader != null) inputReader.enabled = false;
                return;
            }

            gameObject.name = $"Player_{OwnerClientId}_Local";

            if (health != null)
            {
                health.Changed += OnVitalsChanged;
                standing = health.State.IsStanding;
            }

            SetCursorLocked(true);
            ApplyInput();

            if (ServiceLocator.TryGet(out bus))
            {
                bus.Subscribe<LocalPauseChanged>(OnPauseChanged);
                bus.Subscribe<LocalInventoryChanged>(OnInventoryChanged);
                bus.Publish(new LocalPlayerSpawned(OwnerClientId));
            }
        }

        public override void OnNetworkDespawn()
        {
            if (health != null) health.Changed -= OnVitalsChanged;

            if (!IsOwner) return;

            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);
            bus?.Unsubscribe<LocalInventoryChanged>(OnInventoryChanged);
            bus = null;
            paused = false;
            inventoryOpen = false;
            standing = true;

            SetCursorLocked(false);
        }

        // The pause overlay owns the cursor and input while it is open. The game keeps
        // running — co-op never freezes for the other players.
        private void OnPauseChanged(LocalPauseChanged evt)
        {
            paused = evt.IsPaused;
            ApplyState();
        }

        // The inventory takes the same two things for the same reason, and is not a pause:
        // the body stands there while the player reads, in front of everyone else.
        private void OnInventoryChanged(LocalInventoryChanged evt)
        {
            inventoryOpen = evt.IsOpen;
            ApplyState();
        }

        /// <remarks>
        /// The gate that makes being downed mean something. Before this, zero health took the
        /// swing away and left everything else: a downed player walked, looked, picked things
        /// up and opened their inventory, which reads as a bug rather than as a state. One
        /// switch here covers all of it, because every one of those systems reads its input
        /// from the same reader.
        /// </remarks>
        private void OnVitalsChanged(VitalsState state)
        {
            if (!IsOwner) return;

            var next = state.IsStanding;
            if (next == standing) return;

            standing = next;
            ApplyState();
        }

        private void ApplyState()
        {
            SetCursorLocked(!CursorFree);
            ApplyInput();
        }

        private void ApplyInput()
        {
            if (inputReader != null) inputReader.enabled = !InputSuspended;
        }

        private void Update()
        {
            if (!IsOwner || !manageCursor || CursorFree) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        private void SetCursorLocked(bool locked)
        {
            if (!manageCursor) return;

            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
