using System;
using Unity.Collections;
using Unity.Netcode;

namespace Office.Network
{
    public struct PlayerSlot : INetworkSerializable, IEquatable<PlayerSlot>
    {
        public ulong ClientId;
        public FixedString32Bytes DisplayName;
        public bool IsReady;

        public PlayerSlot(ulong clientId, FixedString32Bytes displayName, bool isReady = false)
        {
            ClientId = clientId;
            DisplayName = displayName;
            IsReady = isReady;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref DisplayName);
            serializer.SerializeValue(ref IsReady);
        }

        public bool Equals(PlayerSlot other) =>
            ClientId == other.ClientId
            && IsReady == other.IsReady
            && DisplayName.Equals(other.DisplayName);

        public override bool Equals(object obj) => obj is PlayerSlot other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ClientId, DisplayName, IsReady);
    }
}
