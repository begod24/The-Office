using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class WorldItem : NetworkBehaviour, IInteractable
    {
        private readonly NetworkVariable<ItemStack> stack = new();

        private ItemStack pending = ItemStack.Empty;
        private int viewDefinitionId = ContentDefinition.NoId;
        private GameObject view;
        private ItemDefinition definition;

        public ItemStack Stack => stack.Value;

        public ItemDefinition Definition => definition;

        public string Prompt
        {
            get
            {
                if (definition == null) return string.Empty;

                var count = stack.Value.Count;
                return count > 1
                    ? $"{definition.PickupVerb} {definition.DisplayName} x{count}"
                    : $"{definition.PickupVerb} {definition.DisplayName}";
            }
        }

        public bool IsAvailable => IsSpawned && !stack.Value.IsEmpty && definition != null;

        public void ServerInitialise(ItemStack contents) => pending = contents;

        public override void OnNetworkSpawn()
        {
            if (IsServer && !pending.IsEmpty) stack.Value = pending;

            pending = ItemStack.Empty;

            stack.OnValueChanged += OnStackChanged;
            ApplyStack(stack.Value);
        }

        public override void OnNetworkDespawn()
        {
            stack.OnValueChanged -= OnStackChanged;
            DestroyView();

            viewDefinitionId = ContentDefinition.NoId;
            definition = null;
        }

        private void OnStackChanged(ItemStack previous, ItemStack current) => ApplyStack(current);

        private void ApplyStack(ItemStack current)
        {
            if (current.DefinitionId == viewDefinitionId) return;

            viewDefinitionId = current.DefinitionId;
            DestroyView();

            definition = ContentViewFactory.Resolve<ItemDefinition>(current.DefinitionId, this);
            if (definition == null) return;

            view = ContentViewFactory.Build(definition, transform,
                new Vector3(0f, definition.GroundOffset, 0f), Quaternion.identity,
                PhysicsLayers.Interactable, solid: true);
        }

        private void DestroyView()
        {
            if (view == null) return;

            Destroy(view);
            view = null;
        }

        public void Interact(ulong clientId)
        {
            if (!IsServer || stack.Value.IsEmpty) return;

            if (!TryGetInventory(clientId, out var inventory))
            {
                Debug.LogWarning($"[Item] Client {clientId} has no PlayerInventory. Pickup ignored.");
                return;
            }

            var remainder = inventory.ServerAdd(stack.Value);

            if (remainder.Equals(stack.Value)) return;

            if (remainder.IsEmpty)
            {
                NetworkObject.Despawn();
                return;
            }

            stack.Value = remainder;
        }

        private bool TryGetInventory(ulong clientId, out PlayerInventory inventory)
        {
            inventory = null;

            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return false;
            if (client.PlayerObject == null) return false;

            inventory = client.PlayerObject.GetComponent<PlayerInventory>();
            return inventory != null;
        }
    }
}
