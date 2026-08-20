using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// A downed player, seen from the outside: the thing a teammate walks up to and presses
    /// Interact on. GDD §15 gives them sixty seconds, and this is what those seconds are for.
    /// </summary>
    /// <remarks>
    /// <b>A body is an interactable, not a second interaction path.</b> It sits on the root of
    /// the player's own NetworkObject, which is exactly what
    /// <see cref="IInteractable"/> asks for — so the request travels the route every door and
    /// item already uses: the owner's probe finds it, the server re-resolves it, re-checks
    /// reach, and only then calls <see cref="Interact"/>. Nothing here trusts a client.
    /// <para>
    /// <b>The collider is built at runtime and only exists while someone is down.</b> The
    /// player's own collider is a <c>CharacterController</c> on the Player layer, which the
    /// interaction mask deliberately does not contain — teammates are not scenery and must not
    /// swallow the crosshair while standing. A second collider on the Interactable layer solves
    /// that. It is created here rather than authored on the prefab because the character
    /// prefabs carry hand-placed art: adding a child to them means regenerating them, and a
    /// runtime child cannot be lost that way.
    /// <para>
    /// <b>Solid, not a trigger</b>, and that is not a style choice:
    /// <see cref="PlayerInteractor"/> probes with
    /// <c>QueryTriggerInteraction.Ignore</c>, so a trigger volume here would be invisible to
    /// the only thing that was ever going to look for it — a revive prompt that never appears,
    /// with nothing logged anywhere. A body being solid is also the truthful answer: a teammate
    /// stops at it instead of standing inside it.
    /// </para>
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class DownedPlayer : NetworkBehaviour, IInteractable
    {
        private const string ColliderName = "DownedInteractVolume";

        [SerializeField] private Health health;

        [Tooltip("Radius of the volume a teammate has to put the crosshair on. Generous on " +
                 "purpose — a body on the floor is a small target, and hunting for the pixel " +
                 "that answers is not the tension this sixty seconds is meant to carry. Not so " +
                 "generous that it becomes a boulder: it is solid while someone is down.")]
        [Min(0.1f)]
        [SerializeField] private float reachRadius = 0.6f;

        [Tooltip("Height off the floor the volume is centred at. Roughly a fallen body.")]
        [SerializeField] private float reachHeight = 0.5f;

        private SphereCollider volume;

        /// <inheritdoc />
        public string Prompt => IsAvailable
            ? $"REVIVE  [{Mathf.CeilToInt(Mathf.Max(0f, health.State.BleedOutRemaining))}]"
            : string.Empty;

        /// <inheritdoc />
        /// <remarks>
        /// Downed only. A dead player is past reviving — that difference is the whole reason
        /// GDD §15 draws one — and a standing player is not an interactable at all.
        /// </remarks>
        public bool IsAvailable => IsSpawned && health != null && health.State.IsDowned;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();

            EnsureVolume();
        }

        public override void OnNetworkSpawn()
        {
            if (health != null) health.Changed += OnVitalsChanged;

            ApplyVolume();
        }

        public override void OnNetworkDespawn()
        {
            if (health != null) health.Changed -= OnVitalsChanged;

            if (volume != null) volume.enabled = false;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Server only, by contract. The reviver has to be standing: a downed player cannot
        /// pull another one up, which is what makes a two-player wipe final rather than a
        /// stalemate where each keeps reviving the other.
        /// </remarks>
        public void Interact(ulong clientId)
        {
            if (!IsServer || !IsAvailable) return;

            if (!ReviverIsStanding(clientId)) return;

            if (!health.ServerRevive()) return;

            Debug.Log($"[Revive] {clientId} revived {OwnerClientId}.");
        }

        private bool ReviverIsStanding(ulong clientId)
        {
            var players = Health.SpawnedPlayerList;

            for (var i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (candidate == null || candidate.OwnerClientId != clientId) continue;

                return candidate.State.IsStanding;
            }

            // No body found for the asker. Refusing is the safe answer: everything that can
            // legitimately revive has a spawned Health by the time it can reach one.
            return false;
        }

        private void OnVitalsChanged(VitalsState state) => ApplyVolume();

        private void ApplyVolume()
        {
            if (volume == null) return;

            volume.enabled = IsAvailable;
        }

        private void EnsureVolume()
        {
            if (volume != null) return;

            var child = new GameObject(ColliderName) { layer = PhysicsLayers.Interactable };
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.up * reachHeight;

            volume = child.AddComponent<SphereCollider>();
            volume.radius = reachRadius;
            volume.isTrigger = false;

            // Off until someone actually goes down, so a standing squad puts nothing extra in
            // front of the crosshair or in the way of a swing.
            volume.enabled = false;
        }
    }
}
