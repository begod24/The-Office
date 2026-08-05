using Office.Network;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Office.Gameplay
{
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

            var hadController = characterController != null && characterController.enabled;
            if (hadController) characterController.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            if (hadController) characterController.enabled = true;

            networkTransform.Teleport(position, rotation, transform.localScale);
        }
    }
}
