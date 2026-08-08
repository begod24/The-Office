using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The single networked carrier for every item lying in the world.
    /// </summary>
    /// <remarks>
    /// There is exactly one prefab for this — <c>PF_WorldItem</c> — and it is the only
    /// entry the network prefab list ever needs for items. What the player sees is the
    /// definition's view prefab, instantiated locally as a plain child on every machine.
    /// A new item is therefore an asset and a mesh, never a netcode change, which matters
    /// because <c>ForceSamePrefabs</c> makes a forgotten registry entry fail only on the
    /// remote client.
    /// </remarks>
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

        /// <summary>
        /// Server only, before <c>Spawn()</c>. The value is written to the NetworkVariable in
        /// <see cref="OnNetworkSpawn"/> rather than here, so it is part of the spawn payload
        /// instead of a delta a late client could miss.
        /// </summary>
        public void ServerInitialise(ItemStack contents) => pending = contents;

        public override void OnNetworkSpawn()
        {
            if (IsServer && !pending.IsEmpty) stack.Value = pending;

            // Consumed. A carrier that comes back out of the pool must not re-apply what the
            // last spawn asked for.
            pending = ItemStack.Empty;

            stack.OnValueChanged += OnStackChanged;
            ApplyStack(stack.Value);
        }

        public override void OnNetworkDespawn()
        {
            stack.OnValueChanged -= OnStackChanged;
            DestroyView();

            // The view cache has to go with the view. This carrier is pooled, so the same
            // instance comes back for the next item — and if the next one happens to be the
            // same definition, ApplyStack would take its early-out, skip the rebuild, and
            // spawn something with no mesh and no collider: present, promptable, and
            // impossible to see or reach.
            viewDefinitionId = ContentDefinition.NoId;
            definition = null;
        }

        private void OnStackChanged(ItemStack previous, ItemStack current) => ApplyStack(current);

        private void ApplyStack(ItemStack current)
        {
            // Count changes on a partial pickup; rebuilding the mesh for that would be waste.
            if (current.DefinitionId == viewDefinitionId) return;

            viewDefinitionId = current.DefinitionId;
            DestroyView();

            definition = ItemViewFactory.Resolve(current.DefinitionId, this);
            if (definition == null) return;

            // Solid: on the floor the collider is what the interaction probe hits.
            view = ItemViewFactory.Build(definition, transform,
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

            // Nothing moved: the inventory is full. Leave the item where it is rather than
            // deleting it, or a full player walking over a floor would erase the loot.
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
