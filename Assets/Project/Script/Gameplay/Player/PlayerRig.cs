using Office.Core;
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

        [Header("Cursor")]
        [SerializeField] private bool manageCursor = true;

        private IEventBus bus;
        private bool paused;
        private bool inventoryOpen;

        // Two overlays, one cursor. Tracked separately so closing either one cannot re-lock
        // the cursor while the other is still up.
        private bool InputSuspended => paused || inventoryOpen;

        public override void OnNetworkSpawn()
        {
            var owner = IsOwner;

            if (playerCamera != null) playerCamera.gameObject.SetActive(owner);
            if (audioListener != null) audioListener.enabled = owner;
            if (inputReader != null) inputReader.enabled = owner;
            if (characterController != null) characterController.enabled = owner;

            if (bodyRenderers != null)
                foreach (var renderer in bodyRenderers)
                    if (renderer != null)
                        renderer.enabled = !owner;

            if (!owner) return;

            gameObject.name = $"Player_{OwnerClientId}_Local";
            SetCursorLocked(true);

            if (ServiceLocator.TryGet(out bus))
            {
                bus.Subscribe<LocalPauseChanged>(OnPauseChanged);
                bus.Subscribe<LocalInventoryChanged>(OnInventoryChanged);
                bus.Publish(new LocalPlayerSpawned(OwnerClientId));
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);
            bus?.Unsubscribe<LocalInventoryChanged>(OnInventoryChanged);
            bus = null;
            paused = false;
            inventoryOpen = false;

            SetCursorLocked(false);
        }

        // The pause overlay owns the cursor and input while it is open. The game keeps
        // running — co-op never freezes for the other players.
        private void OnPauseChanged(LocalPauseChanged evt)
        {
            paused = evt.IsPaused;
            SetCursorLocked(!InputSuspended);
        }

        // The inventory takes the same two things for the same reason, and is not a pause:
        // the body stands there while the player reads, in front of everyone else.
        private void OnInventoryChanged(LocalInventoryChanged evt)
        {
            inventoryOpen = evt.IsOpen;
            SetCursorLocked(!InputSuspended);
        }

        private void Update()
        {
            if (!IsOwner || !manageCursor || InputSuspended) return;

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

            if (inputReader != null) inputReader.enabled = locked;
        }
    }
}
