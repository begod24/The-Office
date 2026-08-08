using System;
using Unity.Netcode;

namespace Office.Gameplay
{
    /// <summary>
    /// Everything about how alive something is, in one replicated value.
    /// </summary>
    /// <remarks>
    /// One struct rather than three NetworkVariables so the three can never be observed out
    /// of step: a client must never see health at zero while the downed flag is still on its
    /// way. Unmanaged and IEquatable so it fits a <see cref="NetworkVariable{T}"/>, the same
    /// shape as <see cref="ItemStack"/>.
    /// <para>
    /// Downed is <b>derived</b> from health rather than stored. A stored flag admits states
    /// that cannot happen — downed at full health, standing at zero — and every one of those
    /// would have to be defended against somewhere.
    /// </para>
    /// </remarks>
    public struct VitalsState : INetworkSerializable, IEquatable<VitalsState>
    {
        public float Health;

        /// <summary>
        /// Seconds left for a teammate to revive. Only meaningful while downed. GDD §15
        /// gives 60; when it reaches zero the player is dead and becomes a spectator.
        /// </summary>
        public float BleedOutRemaining;

        public bool IsDead;

        public VitalsState(float health, float bleedOutRemaining, bool isDead)
        {
            Health = health;
            BleedOutRemaining = bleedOutRemaining;
            IsDead = isDead;
        }

        /// <summary>On the floor, still savable. GDD §7.1: zero HP is downed, not dead.</summary>
        public bool IsDowned => !IsDead && Health <= 0f;

        /// <summary>Upright and able to act.</summary>
        public bool IsStanding => !IsDead && Health > 0f;

        /// <summary>
        /// Alive in the sense the combat system cares about — a downed player still counts,
        /// because they can be brought back.
        /// </summary>
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
