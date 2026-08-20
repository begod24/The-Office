using System;
using Unity.Netcode;

namespace Office.Gameplay
{
    public struct VitalsState : INetworkSerializable, IEquatable<VitalsState>
    {
        public float Health;

        public float BleedOutRemaining;

        public bool IsDead;

        public VitalsState(float health, float bleedOutRemaining, bool isDead)
        {
            Health = health;
            BleedOutRemaining = bleedOutRemaining;
            IsDead = isDead;
        }

        public bool IsDowned => !IsDead && Health <= 0f;

        public bool IsStanding => !IsDead && Health > 0f;

        public bool IsAlive => !IsDead;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref BleedOutRemaining);
            serializer.SerializeValue(ref IsDead);
        }

        public bool Equals(VitalsState other) =>
            Health.Equals(other.Health) &&
            BleedOutRemaining.Equals(other.BleedOutRemaining) &&
            IsDead == other.IsDead;

        public override bool Equals(object obj) => obj is VitalsState other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Health, BleedOutRemaining, IsDead);

        public override string ToString()
        {
            if (IsDead) return "dead";
            return IsDowned ? $"downed ({BleedOutRemaining:0.0}s)" : $"{Health:0} hp";
        }
    }
}
