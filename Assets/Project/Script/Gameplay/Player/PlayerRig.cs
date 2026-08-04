using Office.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Office.Gameplay
{
    /// <summary>
    /// Splits the player prefab into what the owner runs and what everyone else only sees.
    ///
    /// Every player prefab instance exists on every client. Without this, four cameras and four
    /// audio listeners would be active at once, remote input readers would fight for the mouse,
    /// and each remote CharacterController would argue with the replicated transform.
    /// </summary>
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

            if (ServiceLocator.TryGet<IEventBus>(out var bus))
                bus.Publish(new LocalPlayerSpawned(OwnerClientId));
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) SetCursorLocked(false);
        }

        private void Update()
        {
            if (!IsOwner || !manageCursor) return;

            // Escape releases the mouse so the editor and the session UI stay usable.
            // A real pause menu replaces this in M1.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetCursorLocked(false);

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
