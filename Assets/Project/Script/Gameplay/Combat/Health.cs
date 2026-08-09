using System;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// How alive one thing is, replicated to everyone and written only by the server.
    /// </summary>
    /// <remarks>
    /// Server-authoritative even on the player, whose movement is not: damage is the
    /// outcome of two parties meeting, and only one machine can decide it. Health is
    /// long-lived state, so it rides a <see cref="NetworkVariable{T}"/> rather than an RPC —
    /// a client that joins late gets the current value for free, which an RPC cannot offer.
    /// One-off consequences of a hit (the flinch, the sound, the screen flash) are the ones
    /// that belong in RPCs.
    /// <para>
    /// The rules themselves live in <see cref="Vitals"/>, which knows nothing about netcode
    /// and is unit tested. This component owns authority and replication only.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Health : NetworkBehaviour, IDamageable
    {
        [Header("Capacity")]
        [Min(1f)]
        [SerializeField] private float maxHealth = GameplayConstants.MaxPlayerHealth;

        [Tooltip("On for players: zero health means downed and revivable (GDD §7.1). Off for " +
                 "enemies and breakable props, which simply stop existing.")]
        [SerializeField] private bool canBeDowned = true;

        [Header("Resistances")]
        [Tooltip("Empty means damage lands as authored. This is where 'digital entities are " +
                 "immune to physical weapons' is expressed — as data, not as an if.")]
        [SerializeField] private DamageResponseTable responses = new();

        private readonly NetworkVariable<VitalsState> vitals = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IEventBus bus;

        /// <summary>The local player's vitals, or null between runs.</summary>
        public static Health Local { get; private set; }

        /// <summary>Fires when <see cref="Local"/> starts or stops pointing at a player.</summary>
        public static event Action<Health> LocalChanged;

        /// <summary>Any change to this instance's state, on every machine.</summary>
        public event Action<VitalsState> Changed;

        /// <summary>Server only. Raised after the state that killed this was published.</summary>
        public event Action<Health> ServerDied;

        /// <summary>Server only. Raised the moment this goes down, before the timer starts.</summary>
        public event Action<Health> ServerDowned;

        public VitalsState State => vitals.Value;

        public float MaxHealth => maxHealth;

        public float Normalised => maxHealth <= 0f ? 0f : Mathf.Clamp01(vitals.Value.Health / maxHealth);

        public bool IsAlive => vitals.Value.IsAlive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            LocalChanged = null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) vitals.Value = Vitals.Spawn(maxHealth);

            vitals.OnValueChanged += OnVitalsChanged;

            // IsPlayerObject, not IsOwner alone: on a host every server-owned enemy also
            // passes IsOwner, and the HUD must not start tracking one of those.
            if (IsOwner && NetworkObject.IsPlayerObject)
            {
                ServiceLocator.TryGet(out bus);
                SetLocal(this);
                PublishLocal(vitals.Value);
            }

            Changed?.Invoke(vitals.Value);
        }

        public override void OnNetworkDespawn()
        {
            vitals.OnValueChanged -= OnVitalsChanged;

            if (!ReferenceEquals(Local, this)) return;

            SetLocal(null);
            bus = null;
        }

        private static void SetLocal(Health health)
        {
            Local = health;
            LocalChanged?.Invoke(health);
        }

        private void OnVitalsChanged(VitalsState previous, VitalsState current)
        {
            Changed?.Invoke(current);

            if (ReferenceEquals(Local, this)) PublishLocal(current);
        }

        private void PublishLocal(in VitalsState state) =>
            bus?.Publish(new LocalVitalsChanged(state.Health, maxHealth, state.IsDowned,
                state.IsDead, state.BleedOutRemaining));

        // Server only, and only while someone is actually bleeding out. Vitals.Tick is a
        // no-op for anyone standing, but the guard keeps this off the profiler entirely.
        private void Update()
        {
            if (!IsServer || !vitals.Value.IsDowned) return;

            var next = Vitals.Tick(vitals.Value, Time.deltaTime);
            if (next.Equals(vitals.Value)) return;

            vitals.Value = next;

            if (next.IsDead) ServerDied?.Invoke(this);
        }

        // ------------------------------------------------------------------ server API

        /// <inheritdoc />
        public float ApplyDamage(in DamageInfo info)
        {
            if (!IsServer || !IsSpawned) return 0f;

            var before = vitals.Value;
            if (!before.IsStanding) return 0f;

            // Resistances are the target's business, so they are resolved here rather than
            // by whoever swung — otherwise every attacker would need the target's table.
            var resolved = responses?.Resolve(info.Amount, info.Type) ?? info.Amount;
            if (resolved <= 0f) return 0f;

            var after = canBeDowned
                ? Vitals.ApplyDamage(before, resolved, GameplayConstants.BleedOutSeconds)
                : Vitals.ApplyDamage(before, resolved, 0f);

            // Something that cannot be downed dies the moment it reaches zero.
            if (!canBeDowned && after.IsDowned) after = Vitals.Kill(after);

            if (after.Equals(before)) return 0f;

            vitals.Value = after;

            if (after.IsDowned) ServerDowned?.Invoke(this);
            else if (after.IsDead) ServerDied?.Invoke(this);

            return Mathf.Min(resolved, before.Health);
        }

        /// <summary>Server only. Brings a downed player back up. Returns false if nothing changed.</summary>
        public bool ServerRevive(float health = GameplayConstants.ReviveHealth)
        {
            if (!IsServer || !IsSpawned) return false;

            var after = Vitals.Revive(vitals.Value, Mathf.Min(health, maxHealth));
            if (after.Equals(vitals.Value)) return false;

            vitals.Value = after;
            return true;
        }

        /// <summary>Server only. First aid kits, not regeneration — GDD §7.1 has none.</summary>
        public bool ServerHeal(float amount)
        {
            if (!IsServer || !IsSpawned) return false;

            var after = Vitals.Heal(vitals.Value, amount, maxHealth);
            if (after.Equals(vitals.Value)) return false;

            vitals.Value = after;
            return true;
        }

        /// <summary>Server only. Skips the downed state entirely. Falls, crushes, scripted deaths.</summary>
        public bool ServerKill()
        {
            if (!IsServer || !IsSpawned) return false;

            var after = Vitals.Kill(vitals.Value);
            if (after.Equals(vitals.Value)) return false;

            vitals.Value = after;
            ServerDied?.Invoke(this);
            return true;
        }
    }
}
