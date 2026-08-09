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

        public ItemStack(int definitionId, int count)
        {
            DefinitionId = definitionId;
            Count = count;
        }

        /// <summary>An empty slot. Also what <c>default</c> gives, which is why id 0 is reserved.</summary>
        public static ItemStack Empty => default;

        public bool IsEmpty => DefinitionId == ContentDefinition.NoId || Count <= 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref Count);
        }

        public bool Equals(ItemStack other) =>
            DefinitionId == other.DefinitionId && Count == other.Count;

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(DefinitionId, Count);

        public override string ToString() => IsEmpty ? "empty" : $"#{DefinitionId} x{Count}";
    }
}
