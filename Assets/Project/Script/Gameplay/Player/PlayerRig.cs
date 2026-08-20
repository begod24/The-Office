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

        public Transform EyeAnchor => playerCamera != null ? playerCamera.transform : transform;

        public Camera PlayerCamera => playerCamera;

        private bool InputSuspended => paused || inventoryOpen || !standing;

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

        private void OnPauseChanged(LocalPauseChanged evt)
        {
            paused = evt.IsPaused;
            ApplyState();
        }

        private void OnInventoryChanged(LocalInventoryChanged evt)
        {
            inventoryOpen = evt.IsOpen;
            ApplyState();
        }

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
