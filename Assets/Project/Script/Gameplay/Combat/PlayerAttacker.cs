using System;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Owner-side swing input, and the one door through which attacks reach the server.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="PlayerInteractor"/>, for the same reason: movement is
    /// owner-authoritative, so the client's aim is the only aim that exists and the probe has
    /// to run on the owner. That makes the request untrusted by definition, so
    /// <see cref="RequestSwingRpc"/> re-resolves the target, re-checks reach, and — unlike
    /// interaction — also owns the cooldown and reads the weapon out of the server's own copy
    /// of the inventory. A client that lies about what it is holding, how far it can reach or
    /// how fast it can swing changes nothing.
    /// <para>
    /// The one thing the client is trusted with is stamina, which is owner-authoritative
    /// throughout <see cref="PlayerMovement"/>. Spending it is a feel gate, not a security
    /// boundary; see <see cref="PlayerMovement.TrySpendStamina"/>.
    /// </para>
    /// </remarks>
    public sealed class PlayerAttacker : NetworkBehaviour
    {
        [SerializeField] private CombatConfig config;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Health health;

        [Tooltip("Probe origin and direction. The owner's camera, not the body — the player " +
                 "swings at what they are looking at.")]
        [SerializeField] private Camera playerCamera;

        // Shared: only the owner probes, and a probe finishes inside one Update with no
        // re-entrancy, so a per-instance buffer would allocate for nothing.
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private IEventBus bus;
        private bool paused;

        // Two clocks on purpose. The owner's stops it asking for swings it cannot have, so
        // the swing feels immediate; the server's is the one that actually decides.
        private float ownerNextSwingTime;
        private float serverNextSwingTime;

        /// <summary>
        /// A swing that the server confirmed, on every machine. <c>hit</c> says whether it
        /// connected, so audio and animation can differ without asking again.
        /// </summary>
        public event Action<bool, Vector3> Swung;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            if (config == null || playerCamera == null)
            {
                Debug.LogError($"[Combat] {name} is missing its config or camera. " +
                               "Attacking disabled for this player.");
                enabled = false;
                return;
            }

            if (ServiceLocator.TryGet(out bus)) bus.Subscribe<LocalPauseChanged>(OnPauseChanged);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);
            bus = null;
            paused = false;
        }

        private void OnPauseChanged(LocalPauseChanged evt) => paused = evt.IsPaused;

        private void Update()
        {
            if (!IsSpawned || !IsOwner || paused) return;
            if (input == null || !input.AttackPressedThisFrame) return;

            TrySwing();
        }

        private void TrySwing()
        {
            // A downed player cannot fight their way up. GDD §7.1 makes being revived the
            // only way out, and letting them swing would quietly undo that.
            if (health != null && !health.State.IsStanding) return;

            if (Time.time < ownerNextSwingTime) return;

            var weapon = ResolveWeapon();

            if (movement != null && !movement.TrySpendStamina(weapon.StaminaCost)) return;

            ownerNextSwingTime = Time.time + weapon.Cooldown;

            var target = Probe(out var point);

            RequestSwingRpc(
                target != null ? new NetworkObjectReference(target) : default,
                point);
        }

        // A sphere, and a wider one than interaction uses: a swing is an arc, and pixel
        // hunting a moving target is not the difficulty this game is after. Geometry sits in
        // the mask on purpose — a wall in the way has to win.
        private NetworkObject Probe(out Vector3 point)
        {
            var origin = playerCamera.transform.position;
            var direction = playerCamera.transform.forward;

            point = origin + direction * config.Range;

            var count = Physics.SphereCastNonAlloc(
                origin, config.ProbeRadius, direction, Hits, config.Range,
                PhysicsLayers.AttackMask, QueryTriggerInteraction.Ignore);

            var nearestDistance = float.PositiveInfinity;
            NetworkObject nearest = null;
            var nearestPoint = point;

            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.distance >= nearestDistance) continue;

                // Plain geometry still counts: finding it here is what blocks everything
                // behind it, even though it resolves to no target.
                var damageable = hit.collider.GetComponentInParent<IDamageable>();

                nearestDistance = hit.distance;
                nearestPoint = hit.point;
                nearest = damageable is NetworkBehaviour behaviour && behaviour.IsSpawned
                    ? behaviour.NetworkObject
                    : null;
            }

            point = nearestPoint;
            return nearest;
        }

        // ------------------------------------------------------------------ server

        [Rpc(SendTo.Server)]
        private void RequestSwingRpc(NetworkObjectReference reference, Vector3 clientPoint,
            RpcParams rpcParams = default)
        {
            // This component sits on a player object every client can see, so anyone could
            // aim an RPC at it. Only the owner speaks for this player.
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (health != null && !health.State.IsStanding) return;

            var weapon = ResolveWeapon();

            // The server's own clock, with a little slack: without it an honest client loses
            // swings to ordinary jitter, and with too much it gains free ones.
            var tolerance = weapon.Cooldown * config.CooldownTolerance;
            if (Time.time < serverNextSwingTime - tolerance) return;

            serverNextSwingTime = Time.time + weapon.Cooldown;

            var hit = TryResolveHit(reference, weapon, out var point);

            // The client's point is only ever a hint for effects, and only when nothing was
            // actually hit — it never decides damage.
            ConfirmSwingRpc(hit, hit ? point : clientPoint);
        }

        private bool TryResolveHit(NetworkObjectReference reference, in Swing weapon,
            out Vector3 point)
        {
            point = Vector3.zero;

            if (!reference.TryGet(out var networkObject, NetworkManager)) return false;

            var damageable = networkObject.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return false;

            point = networkObject.transform.position;
            if (!IsWithinServerReach(point)) return false;

            var direction = (point - transform.position).normalized;

            var applied = damageable.ApplyDamage(new DamageInfo(
                weapon.Damage, weapon.DamageType, OwnerClientId, point, direction));

            // A connected swing that a resistance table zeroed still counts as a hit: the
            // player needs to hear that their stapler bounced off, or they will keep swinging.
            return applied >= 0f;
        }

        private bool IsWithinServerReach(Vector3 point)
        {
            // Measured from the body, not the camera: the server has no pitch for a remote
            // player worth trusting, and the reach already carries a tolerance for that.
            var reach = config.ServerReach;
            return (point - transform.position).sqrMagnitude <= reach * reach;
        }

        [Rpc(SendTo.Everyone)]
        private void ConfirmSwingRpc(bool hit, Vector3 point) => Swung?.Invoke(hit, point);

        // ------------------------------------------------------------------ weapon lookup

        /// <summary>What the selected slot is worth as a weapon, resolved the same way on
        /// both sides so the owner's prediction and the server's ruling agree.</summary>
        private Swing ResolveWeapon()
        {
            var unarmed = new Swing(
                config.UnarmedDamage, config.UnarmedDamageType,
                config.UnarmedCooldown, 0f, config.UnarmedNoiseRadius);

            if (inventory == null || inventory.Capacity == 0) return unarmed;

            var index = inventory.SelectedIndex;
            if (index < 0 || index >= inventory.Capacity) return unarmed;

            var stack = inventory[index];
            if (stack.IsEmpty) return unarmed;

            if (!ServiceLocator.TryGet<DefinitionRegistry>(out var registry) ||
                !registry.TryGetItem(stack.DefinitionId, out var definition))
                return unarmed;

            // No module means the item is simply not a weapon — a coffee cup swings like a
            // fist. That is why nothing here asks "is this a weapon".
            var melee = definition.GetModule<MeleeModule>();
            if (melee == null) return unarmed;

            return new Swing(melee.Damage, melee.DamageType, melee.AttackCooldown,
                melee.StaminaCost, melee.NoiseRadius);
        }

        /// <summary>One swing's worth of numbers, from a module or from the unarmed fallback.</summary>
        private readonly struct Swing
        {
            public readonly float Damage;
            public readonly DamageType DamageType;
            public readonly float Cooldown;
            public readonly float StaminaCost;

            /// <summary>
            /// Metres this swing is audible over. GDD §8.1 makes fighting loud; nothing
            /// listens yet, because enemies do not exist, but the number is authored and
            /// resolved so that the hearing system has something to read on day one.
            /// </summary>
            public readonly float NoiseRadius;

            public Swing(float damage, DamageType damageType, float cooldown,
                float staminaCost, float noiseRadius)
            {
                Damage = damage;
                DamageType = damageType;
                Cooldown = Mathf.Max(0.05f, cooldown);
                StaminaCost = staminaCost;
                NoiseRadius = noiseRadius;
            }
        }
    }
}
