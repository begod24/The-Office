using System;
using Office.Data;
using Unity.Netcode;

namespace Office.Gameplay
{
    /// <summary>
    /// One inventory slot's worth of an item, as it travels over the wire.
    /// </summary>
    /// <remarks>
    /// Carries a definition id rather than a reference, because an asset reference means
    /// nothing on the other machine. Both ends resolve the id through the shared
    /// <see cref="DefinitionRegistry"/>. Unmanaged and IEquatable so it can live in a
    /// <see cref="NetworkList{T}"/>, the same shape as <c>PlayerSlot</c>.
    /// </remarks>
    public struct ItemStack : INetworkSerializable, IEquatable<ItemStack>
    {
        public int DefinitionId;
        public int Count;

        /// <summary>
        /// Uses already spent, not uses left — see <see cref="ItemWear"/>.
        /// </summary>
        /// <remarks>
        /// Counting upwards is what makes <c>default</c> mean "factory fresh". Counting
        /// downwards would make zero mean both "broken" and "never initialised", and every
        /// path that produces a stack — a placement marker, a drop, a pool reuse, a
        /// <see cref="NetworkList{T}"/> resize — would need to know the item's ceiling just to
        /// create one. Meaningless on an item with no <see cref="DurabilityModule"/>, where it
        /// stays zero for the item's whole life.
        /// </remarks>
        public ushort Wear;

        public ItemStack(int definitionId, int count) : this(definitionId, count, 0)
        {
        }

        public ItemStack(int definitionId, int count, ushort wear)
        {
            DefinitionId = definitionId;
            Count = count;
            Wear = wear;
        }

        /// <summary>An empty slot. Also what <c>default</c> gives, which is why id 0 is reserved.</summary>
        public static ItemStack Empty => default;

        public bool IsEmpty => DefinitionId == ContentDefinition.NoId || Count <= 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref Count);
            serializer.SerializeValue(ref Wear);
        }

        // Wear is part of identity, not a detail: two slots holding the same item at different
        // stages of its life are not interchangeable, and ItemStacking relies on that to keep
        // a worn tool from merging into a fresh one.
        public bool Equals(ItemStack other) =>
            DefinitionId == other.DefinitionId && Count == other.Count && Wear == other.Wear;

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(DefinitionId, Count, Wear);

        public override string ToString() =>
            IsEmpty ? "empty" : Wear == 0 ? $"#{DefinitionId} x{Count}" : $"#{DefinitionId} x{Count} (worn {Wear})";
    }
}
