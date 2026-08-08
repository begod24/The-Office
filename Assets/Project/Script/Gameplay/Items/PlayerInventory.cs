using System;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// A player's slots, replicated to everyone and written only by the server.
    /// </summary>
    /// <remarks>
    /// Contents are server-authoritative even though movement is not: two players reaching
    /// for the same item in the same frame have to be resolved by one machine, and only
    /// the server can do that. The selected slot is the exception — it is cosmetic, so the
    /// owner writes it directly rather than paying a round trip to move the highlight.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : NetworkBehaviour
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private PlayerInputReader input;

        private readonly NetworkList<ItemStack> slots = new();

        private readonly NetworkVariable<int> selected = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Scratch for ServerAdd. Server only, reused so a pickup allocates nothing.
        private ItemStack[] buffer;

        /// <summary>The local player's inventory, or null between runs.</summary>
        public static PlayerInventory Local { get; private set; }

        /// <summary>Fires when <see cref="Local"/> starts or stops pointing at an inventory.</summary>
        public static event Action<PlayerInventory> LocalChanged;

        /// <summary>Any change to the slots or the selection on this instance.</summary>
        public event Action Changed;

        public int Capacity => slots.Count;

        public int SelectedIndex => selected.Value;

        public ItemStack this[int index] => slots[index];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            LocalChanged = null;
        }

        public override void OnNetworkSpawn()
        {
            // Fixed-size from the start: a slot-based inventory needs stable indices, and
            // the HUD hotbar is generated with exactly this many cells.
            if (IsServer && slots.Count == 0)
                for (var i = 0; i < GameplayConstants.InventorySlots; i++)
                    slots.Add(ItemStack.Empty);

            slots.OnListChanged += OnSlotsChanged;
            selected.OnValueChanged += OnSelectionChanged;

            if (IsOwner) SetLocal(this);

            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            slots.OnListChanged -= OnSlotsChanged;
            selected.OnValueChanged -= OnSelectionChanged;

            if (ReferenceEquals(Local, this)) SetLocal(null);
        }

        public override void OnDestroy()
        {
            slots?.Dispose();
            base.OnDestroy();
        }

        private static void SetLocal(PlayerInventory inventory)
        {
            Local = inventory;
            LocalChanged?.Invoke(inventory);
        }

        private void OnSlotsChanged(NetworkListEvent<ItemStack> changeEvent) => Changed?.Invoke();

        private void OnSelectionChanged(int previous, int current) => Changed?.Invoke();

        // ------------------------------------------------------------------ owner input

        private void Update()
        {
            if (!IsSpawned || !IsOwner || input == null) return;

            // Keyboard picks a slot by number; the d-pad, having no number row, steps.
            if (input.HotbarSlot >= 0) Select(input.HotbarSlot);
            else if (input.HotbarStep != 0) Step(input.HotbarStep);

            if (input.DropPressedThisFrame) RequestDropRpc(selected.Value);
        }

        // Wraps: stepping past the last slot lands on the first. A hotbar that stops dead
        // at the ends makes the player look down to find out why.
        private void Step(int direction)
        {
            var count = slots.Count;
            if (count == 0) return;

            var next = (selected.Value + direction) % count;
            if (next < 0) next += count;

            selected.Value = next;
        }

        /// <summary>Owner only. Moves the highlight; the server is not involved.</summary>
        public void Select(int index)
        {
            if (!IsOwner || index < 0 || index >= slots.Count) return;

            selected.Value = index;
        }

        // ------------------------------------------------------------------ server API

        /// <summary>
        /// Server only. Adds what it can and returns the remainder, which is
        /// <see cref="ItemStack.Empty"/> when everything fit.
        /// </summary>
        public ItemStack ServerAdd(ItemStack incoming)
        {
            if (!IsServer || incoming.IsEmpty) return incoming;

            if (buffer == null || buffer.Length != slots.Count) buffer = new ItemStack[slots.Count];

            for (var i = 0; i < slots.Count; i++) buffer[i] = slots[i];

            var remainder = ItemStacking.Distribute(
                buffer, incoming, ResolveMaxStack(incoming.DefinitionId));

            // Only what moved. Writing an unchanged element still costs a delta on the wire.
            for (var i = 0; i < slots.Count; i++)
                if (!buffer[i].Equals(slots[i]))
                    slots[i] = buffer[i];

            return remainder;
        }

        /// <summary>Server only. Empties a slot and returns what was in it.</summary>
        public ItemStack ServerTake(int index)
        {
            if (!IsServer || index < 0 || index >= slots.Count) return ItemStack.Empty;

            var taken = slots[index];
            if (taken.IsEmpty) return ItemStack.Empty;

            slots[index] = ItemStack.Empty;
            return taken;
        }

        [Rpc(SendTo.Server)]
        private void RequestDropRpc(int index, RpcParams rpcParams = default)
        {
            // Every client can see this object, so anyone could aim an RPC at it.
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (WorldItemSpawner.Server == null)
            {
                Debug.LogWarning("[Item] Nothing owns world items on this server. Drop ignored.");
                return;
            }

            var taken = ServerTake(index);
            if (taken.IsEmpty) return;

            // Placed from the server's copy of the body, never from a position the client
            // sent: an owner-authoritative client could otherwise drop items across the map.
            var distance = config != null ? config.DropDistance : 0.9f;
            var position = transform.position
                           + transform.forward * distance
                           + Vector3.up * 0.25f;

            if (WorldItemSpawner.Server.ServerSpawn(taken, position, transform.rotation) != null)
                return;

            // Spawning failed — put it back rather than deleting the player's item.
            ServerAdd(taken);
        }

        private int ResolveMaxStack(int definitionId)
        {
            if (ServiceLocator.TryGet<DefinitionRegistry>(out var registry) &&
                registry.TryGet<ItemDefinition>(definitionId, out var definition))
                return Mathf.Max(1, definition.MaxStack);

            Debug.LogError($"[Item] Definition id {definitionId} did not resolve. " +
                           "Treating it as unstackable.", this);
            return 1;
        }
    }
}
