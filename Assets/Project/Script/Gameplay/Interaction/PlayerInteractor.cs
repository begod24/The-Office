using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    public sealed class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Probe origin and direction. The owner's camera, not the body — the player " +
                 "reaches for what they are looking at.")]
        [SerializeField] private Camera playerCamera;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private IEventBus bus;
        private IInteractable target;
        private string publishedPrompt = string.Empty;
        private bool paused;

        public IInteractable Target => target;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            if (config == null || playerCamera == null)
            {
                Debug.LogError($"[Interact] {name} is missing its config or camera. " +
                               "Interaction disabled for this player.");
                enabled = false;
                return;
            }

            if (ServiceLocator.TryGet(out bus)) bus.Subscribe<LocalPauseChanged>(OnPauseChanged);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            PublishPrompt(string.Empty);

            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);
            bus = null;
            target = null;
            paused = false;
        }

        private void OnPauseChanged(LocalPauseChanged evt)
        {
            paused = evt.IsPaused;

            if (!paused) return;

            target = null;
            PublishPrompt(string.Empty);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || paused) return;

            target = Probe();

            PublishPrompt(target != null && target.IsAvailable ? target.Prompt : string.Empty);

            if (input != null && input.InteractPressedThisFrame) TryInteract();
        }

        private IInteractable Probe()
        {
            var origin = playerCamera.transform.position;
            var direction = playerCamera.transform.forward;

            var count = Physics.SphereCastNonAlloc(
                origin, config.ProbeRadius, direction, Hits, config.Range,
                PhysicsLayers.InteractionMask, QueryTriggerInteraction.Ignore);

            var nearestDistance = float.PositiveInfinity;
            IInteractable nearest = null;

            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.distance >= nearestDistance) continue;

                var candidate = hit.collider.GetComponentInParent<IInteractable>();

                nearestDistance = hit.distance;
                nearest = candidate;
            }

            return nearest != null && nearest.IsAvailable ? nearest : null;
        }

        private void PublishPrompt(string prompt)
        {
            if (prompt == publishedPrompt) return;

            publishedPrompt = prompt;
            bus?.Publish(new InteractionPromptChanged(prompt));
        }

        private void TryInteract()
        {
            if (target is not NetworkBehaviour behaviour || !behaviour.IsSpawned) return;

            RequestInteractRpc(new NetworkObjectReference(behaviour.NetworkObject));
        }

        [Rpc(SendTo.Server)]
        private void RequestInteractRpc(NetworkObjectReference reference,
            RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (!reference.TryGet(out var networkObject, NetworkManager)) return;

            var interactable = networkObject.GetComponent<IInteractable>();
            if (interactable == null || !interactable.IsAvailable) return;

            if (!IsWithinServerReach(networkObject.transform.position)) return;

            interactable.Interact(OwnerClientId);
        }

        private bool IsWithinServerReach(Vector3 point)
        {
            if (config == null) return false;

            var reach = config.ServerReach;
            return (point - transform.position).sqrMagnitude <= reach * reach;
        }
    }
}
