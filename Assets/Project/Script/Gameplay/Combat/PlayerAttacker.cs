using System;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Owner-side attack input, and the one door through which attacks reach the server.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="PlayerInteractor"/>, for the same reason: movement is
    /// owner-authoritative, so the client's aim is the only aim that exists and the probe has
    /// to run on the owner. That makes the request untrusted by definition, so
    /// <see cref="RequestSwingRpc"/> re-resolves the target, re-checks reach and line of
    /// sight, and — unlike interaction — also owns the cooldown and reads the weapon out of
    /// the server's own copy of the inventory. A client that lies about what it is holding,
    /// how far it can reach or how fast it can swing changes nothing.
    /// <para>
    /// What the weapon actually <em>does</em> is not here. <see cref="WeaponResolver"/> turns
    /// the selected slot into a <see cref="WeaponLoadout"/> and an
    /// <see cref="IWeaponBehaviour"/> performs it, so this class stays the same size when the
    /// staple gun and the laser pointer arrive.
    /// </para>
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

        private IEventBus bus;
        private bool paused;

        // Two clocks on purpose. The owner's stops it asking for swings it cannot have, so
        // the swing feels immediate; the server's is the one that actually decides.
        private float ownerNextSwingTime;
        private float serverNextSwingTime;

        /// <summary>
        /// A use that the server confirmed, on every machine. The outcome travels with it so
        /// audio, animation and screen shake can differ without asking again — and so that
        /// bouncing off a digital enemy does not look like missing it.
        /// </summary>
        public event Action<WeaponOutcome, Vector3> Swung;

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

            var loadout = ResolveLoadout();

            if (movement != null && !movement.TrySpendStamina(loadout.Profile.StaminaCost)) return;

            ownerNextSwingTime = Time.time + loadout.Profile.Cooldown;

            var context = new WeaponContext(
                config, loadout.Profile, transform, playerCamera.transform, OwnerClientId);

            RequestSwingRpc(loadout.Behaviour.Probe(context));
        }

        // ------------------------------------------------------------------ server

        [Rpc(SendTo.Server)]
        private void RequestSwingRpc(WeaponAim aim, RpcParams rpcParams = default)
        {
            // This component sits on a player object every client can see, so anyone could
            // aim an RPC at it. Only the owner speaks for this player.
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (health != null && !health.State.IsStanding) return;

            var loadout = ResolveLoadout();

            // The server's own clock, with a little slack: without it an honest client loses
            // swings to ordinary jitter, and with too much it gains free ones.
            var tolerance = loadout.Profile.Cooldown * config.CooldownTolerance;
            if (Time.time < serverNextSwingTime - tolerance) return;

            serverNextSwingTime = Time.time + loadout.Profile.Cooldown;

            // Aim is null: the server has no pitch for a remote player worth aiming with, and
            // ServerResolve is written not to need one.
            var context = new WeaponContext(
                config, loadout.Profile, transform, null, OwnerClientId);

            var outcome = loadout.Behaviour.ServerResolve(context, aim, NetworkManager, out var point);

            ServerSpendWear(loadout.Profile);

            // The client's point is only ever a hint for effects, and only when nothing was
            // actually hit — it never decides damage.
            ConfirmSwingRpc(outcome,
                outcome == WeaponOutcome.Missed ? aim.Point : point);
        }

        /// <summary>
        /// Server only. Wears the held item down by one use and replaces it when it breaks.
        /// </summary>
        /// <remarks>
        /// <b>Charged per confirmed swing, not per hit.</b> Durability is meant to limit how
        /// much a player fights, not how well they aim, and the swing is the one event the
        /// server has already ruled on — charging on the hit instead would make a weapon last
        /// longer the worse the player is with it. Flip the call site if that turns out to
        /// read badly in a playtest; nothing else depends on which one it is.
        /// </remarks>
        private void ServerSpendWear(in WeaponProfile profile)
        {
            if (!profile.Wears || inventory == null) return;

            var index = inventory.SelectedIndex;
            if (index < 0 || index >= inventory.Capacity) return;

            var stack = inventory[index];
            if (stack.IsEmpty) return;

            inventory.ServerSet(index, ItemWear.Spend(
                stack, profile.MaxUses, profile.DurabilityCost, profile.BreaksIntoId));
        }

        [Rpc(SendTo.Everyone)]
        private void ConfirmSwingRpc(WeaponOutcome outcome, Vector3 point) =>
            Swung?.Invoke(outcome, point);

        // ------------------------------------------------------------------ weapon lookup

        /// <summary>
        /// What the selected slot is worth, resolved the same way on both sides so the owner's
        /// prediction and the server's ruling agree.
        /// </summary>
        private WeaponLoadout ResolveLoadout() =>
            WeaponResolver.Resolve(ResolveSelectedDefinition(), config);

        private ItemDefinition ResolveSelectedDefinition()
        {
            if (inventory == null || inventory.Capacity == 0) return null;

            var index = inventory.SelectedIndex;
            if (index < 0 || index >= inventory.Capacity) return null;

            var stack = inventory[index];
            if (stack.IsEmpty) return null;

            // A missing registry or a stale id resolves to null, which WeaponResolver reads as
            // an empty hand. Losing a weapon's numbers must not lose the ability to swing.
            return ServiceLocator.TryGet<DefinitionRegistry>(out var registry) &&
                   registry.TryGet<ItemDefinition>(stack.DefinitionId, out var definition)
                ? definition
                : null;
        }
    }
}
