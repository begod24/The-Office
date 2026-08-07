using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Owner-side probe for whatever the player is looking at, and the one door through
    /// which interaction requests reach the server.
    /// </summary>
    /// <remarks>
    /// Movement here is owner-authoritative, so the client's aim is the only aim that
    /// exists — the probe has to run on the owner. That makes the request untrusted by
    /// definition, which is why <see cref="RequestInteractRpc"/> re-resolves the target
    /// and re-checks reach on the server instead of taking the client's word for it.
    /// </remarks>
    public sealed class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Probe origin and direction. The owner's camera, not the body — the player " +
                 "reaches for what they are looking at.")]
        [SerializeField] private Camera playerCamera;

        // Shared: only the owner probes, and a probe finishes inside one Update with no
        // re-entrancy, so a per-instance buffer would allocate for nothing.
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private IEventBus bus;
        private IInteractable target;
        private string publishedPrompt = string.Empty;
        private bool paused;

        /// <summary>What the owner is looking at right now, or null. Owner only.</summary>
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

        // The pause overlay owns the cursor, so the crosshair must stop advertising things
        // the player cannot currently reach for.
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

        // A sphere is far kinder than a ray on small props. LevelGeometry sits in the mask
        // on purpose: a wall between the player and an item has to win.
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

                // A hit on plain geometry still counts: it is what blocks everything behind it.
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
            // This component sits on a player object every client can see, so anyone could
            // aim an RPC at it. Only the owner speaks for this player.
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

            // Measured from the body, not the camera: the server has no pitch for a remote
            // player worth trusting, and the reach already carries a tolerance for that.
            var reach = config.ServerReach;
            return (point - transform.position).sqrMagnitude <= reach * reach;
        }
    }
}
