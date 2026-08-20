using System;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : NetworkBehaviour
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private PlayerInputReader input;

        private readonly NetworkList<ItemStack> slots = new();

        private readonly NetworkVariable<int> selected = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private ItemStack[] buffer;

        public static PlayerInventory Local { get; private set; }

        public static event Action<PlayerInventory> LocalChanged;

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

        private void Update()
        {
            if (!IsSpawned || !IsOwner || input == null) return;

            if (input.HotbarSlot >= 0) Select(input.HotbarSlot);
            else if (input.HotbarStep != 0) Step(input.HotbarStep);

            if (input.DropPressedThisFrame) RequestDropRpc(selected.Value);
        }

        private void Step(int direction)
        {
            var count = Mathf.Min(GameplayConstants.HotbarSlots, slots.Count);
            if (count == 0) return;

            var next = (selected.Value + direction) % count;
            if (next < 0) next += count;

            selected.Value = next;
        }

        public void Select(int index)
        {
            if (!IsOwner || index < 0 || index >= slots.Count) return;
            if (index >= GameplayConstants.HotbarSlots) return;

            selected.Value = index;
        }

        public void RequestDrop(int index)
        {
            if (!IsSpawned || !IsOwner || !InRange(index)) return;

            RequestDropRpc(index);
        }

        public void RequestMove(int from, int to)
        {
            if (!IsSpawned || !IsOwner || from == to) return;
            if (!InRange(from) || !InRange(to)) return;

            RequestMoveRpc(from, to);
        }

        private bool InRange(int index) => index >= 0 && index < slots.Count;

        public ItemStack ServerAdd(ItemStack incoming)
        {
            if (!IsServer || incoming.IsEmpty) return incoming;

            LoadBuffer();

            var remainder = ItemStacking.Distribute(
                buffer, incoming, ResolveMaxStack(incoming.DefinitionId));

            FlushBuffer();

            return remainder;
        }

        private void LoadBuffer()
        {
            if (buffer == null || buffer.Length != slots.Count) buffer = new ItemStack[slots.Count];

            for (var i = 0; i < slots.Count; i++) buffer[i] = slots[i];
        }

        private void FlushBuffer()
        {
            for (var i = 0; i < slots.Count; i++)
                if (!buffer[i].Equals(slots[i]))
                    slots[i] = buffer[i];
        }

        public bool ServerSet(int index, ItemStack stack)
        {
            if (!IsServer || index < 0 || index >= slots.Count) return false;
            if (slots[index].Equals(stack)) return false;

            slots[index] = stack;
            return true;
        }

        public ItemStack ServerTake(int index)
        {
            if (!IsServer || index < 0 || index >= slots.Count) return ItemStack.Empty;

            var taken = slots[index];
            if (taken.IsEmpty) return ItemStack.Empty;

            slots[index] = ItemStack.Empty;
            return taken;
        }

        [Rpc(SendTo.Server)]
        private void RequestMoveRpc(int from, int to, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (from == to || !InRange(from) || !InRange(to)) return;

            var source = slots[from];

            if (source.IsEmpty) return;

            LoadBuffer();

            if (!ItemStacking.Move(buffer, from, to, ResolveMaxStack(source.DefinitionId))) return;

            FlushBuffer();
        }

        [Rpc(SendTo.Server)]
        private void RequestDropRpc(int index, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (WorldItemSpawner.Server == null)
            {
                Debug.LogWarning("[Item] Nothing owns world items on this server. Drop ignored.");
                return;
            }

            var taken = ServerTake(index);
            if (taken.IsEmpty) return;

            var distance = config != null ? config.DropDistance : 0.9f;
            var position = transform.position
                           + transform.forward * distance
                           + Vector3.up * 0.25f;

            if (WorldItemSpawner.Server.ServerSpawn(taken, position, transform.rotation) != null)
                return;

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
