using System;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
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

        private float ownerNextSwingTime;
        private float serverNextSwingTime;

        public event Action<WeaponOutcome, Vector3> Swung;

        public override void OnNetworkSpawn()
        {
            ServiceLocator.TryGet(out bus);

            if (!IsOwner) return;

            if (config == null || playerCamera == null)
            {
                Debug.LogError($"[Combat] {name} is missing its config or camera. " +
                               "Attacking disabled for this player.");
                enabled = false;
                return;
            }

            bus?.Subscribe<LocalPauseChanged>(OnPauseChanged);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);

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
            if (health != null && !health.State.IsStanding) return;

            if (Time.time < ownerNextSwingTime) return;

            var loadout = ResolveLoadout();

            if (movement != null && !movement.TrySpendStamina(loadout.Profile.StaminaCost)) return;

            ownerNextSwingTime = Time.time + loadout.Profile.Cooldown;

            var context = new WeaponContext(
                config, loadout.Profile, transform, playerCamera.transform, OwnerClientId);

            RequestSwingRpc(loadout.Behaviour.Probe(context));
        }

        [Rpc(SendTo.Server)]
        private void RequestSwingRpc(WeaponAim aim, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (health != null && !health.State.IsStanding) return;

            var loadout = ResolveLoadout();

            var tolerance = loadout.Profile.Cooldown * config.CooldownTolerance;
            if (Time.time < serverNextSwingTime - tolerance) return;

            serverNextSwingTime = Time.time + loadout.Profile.Cooldown;

            var context = new WeaponContext(
                config, loadout.Profile, transform, null, OwnerClientId);

            var outcome = loadout.Behaviour.ServerResolve(context, aim, NetworkManager, out var point);

            ServerSpendWear(loadout.Profile);

            bus?.Publish(new NoiseRaised(
                transform.position, loadout.Profile.NoiseRadius, OwnerClientId));

            ConfirmSwingRpc(outcome,
                outcome == WeaponOutcome.Missed ? aim.Point : point);
        }

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

        private WeaponLoadout ResolveLoadout() =>
            WeaponResolver.Resolve(ResolveSelectedDefinition(), config);

        private ItemDefinition ResolveSelectedDefinition()
        {
            if (inventory == null || inventory.Capacity == 0) return null;

            var index = inventory.SelectedIndex;
            if (index < 0 || index >= inventory.Capacity) return null;

            var stack = inventory[index];
            if (stack.IsEmpty) return null;

            return ServiceLocator.TryGet<DefinitionRegistry>(out var registry) &&
                   registry.TryGet<ItemDefinition>(stack.DefinitionId, out var definition)
                ? definition
                : null;
        }
    }
}
