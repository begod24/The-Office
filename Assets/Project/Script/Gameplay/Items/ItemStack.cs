using System;
using Office.Data;
using Unity.Netcode;

namespace Office.Gameplay
{
    public struct ItemStack : INetworkSerializable, IEquatable<ItemStack>
    {
        public int DefinitionId;
        public int Count;

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

        public static ItemStack Empty => default;

        public bool IsEmpty => DefinitionId == ContentDefinition.NoId || Count <= 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref Count);
            serializer.SerializeValue(ref Wear);
        }

        public bool Equals(ItemStack other) =>
            DefinitionId == other.DefinitionId && Count == other.Count && Wear == other.Wear;

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(DefinitionId, Count, Wear);

        public override string ToString() =>
            IsEmpty ? "empty" : Wear == 0 ? $"#{DefinitionId} x{Count}" : $"#{DefinitionId} x{Count} (worn {Wear})";
    }
}
