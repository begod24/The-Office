using System;
using Unity.Collections;
using Unity.Netcode;

namespace Office.Network
{
    /// <summary>
    /// One row of the lobby roster, replicated inside a <see cref="NetworkList{T}"/>.
    ///
    /// Must stay <c>unmanaged</c>, which is why the name is a <see cref="FixedString32Bytes"/>
    /// and not a <see cref="string"/> — a managed reference cannot live in a NetworkList and
    /// would allocate on every roster change.
    ///
    /// Names are anonymous by design: GDD §17 recommends anonymous employees over named
    /// characters, because the player is meant to be themselves trapped at work.
    /// </summary>
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
