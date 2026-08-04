using Office.Network;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Places a player at an authored spawn point instead of the world origin.
    ///
    /// The server picks the point and writes it into a NetworkVariable during its own
    /// OnNetworkSpawn, so the value travels inside the spawn message and the owner already has
    /// it when its own OnNetworkSpawn runs. The owner then teleports itself, because movement
    /// is owner-authoritative — a server-side transform write would simply be overwritten on
    /// the next frame.
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public sealed class PlayerSpawnAnchor : NetworkBehaviour
    {
        private readonly NetworkVariable<Vector3> spawnPosition = new();
        private readonly NetworkVariable<float> spawnYaw = new();

        private CharacterController characterController;
        private NetworkTransform networkTransform;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            networkTransform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                PlayerSpawnPoints.Next(out var position, out var yaw);
                spawnPosition.Value = position;
                spawnYaw.Value = yaw;
            }

            if (IsOwner) TeleportTo(spawnPosition.Value, spawnYaw.Value);
        }

        private void TeleportTo(Vector3 position, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);

            // A CharacterController overrides direct transform writes on the same frame,
            // so it has to be off while the transform is moved.
            var hadController = characterController != null && characterController.enabled;
            if (hadController) characterController.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            if (hadController) characterController.enabled = true;

            // Without an explicit teleport the NetworkTransform interpolates from the origin
            // and every client watches the player slide across the floor on spawn.
            networkTransform.Teleport(position, rotation, transform.localScale);
        }
    }
}
