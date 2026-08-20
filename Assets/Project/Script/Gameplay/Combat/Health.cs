using System;
using System.Collections.Generic;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Health : NetworkBehaviour, IDamageable
    {
        [Header("Capacity")]
        [Tooltip("The authored default. A definition-driven spawn overrides it through " +
                 "ServerConfigure before the object spawns.")]
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

        private readonly NetworkVariable<float> capacity = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float pendingCapacity = -1f;
        private DamageResponseTable pendingResponses;

        private IEventBus bus;

        public static Health Local { get; private set; }

        public static event Action<Health> LocalChanged;

        private static readonly List<Health> SpawnedPlayers = new(4);

        public static IReadOnlyList<Health> SpawnedPlayerList => SpawnedPlayers;

        public static event Action SpawnedPlayersChanged;

        public event Action<VitalsState> Changed;

        public event Action<Health> ServerDied;

        public event Action<Health> ServerDowned;

        public VitalsState State => vitals.Value;

        public float MaxHealth => capacity.Value > 0f ? capacity.Value : maxHealth;

        public float Normalised
        {
            get
            {
                var max = MaxHealth;
                return max <= 0f ? 0f : Mathf.Clamp01(vitals.Value.Health / max);
            }
        }

        public bool IsAlive => vitals.Value.IsAlive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            LocalChanged = null;

            SpawnedPlayers.Clear();
            SpawnedPlayersChanged = null;
        }

        public void ServerConfigure(float newMaxHealth, DamageResponseTable responses)
        {
            if (IsSpawned)
            {
                Debug.LogWarning($"[Combat] {name} was configured after spawning. Ignored — " +
                                 "clients already have the authored values.", this);
                return;
            }

            pendingCapacity = newMaxHealth;
            pendingResponses = responses;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                capacity.Value = pendingCapacity > 0f ? pendingCapacity : maxHealth;

                if (pendingResponses != null) responses = pendingResponses;

                vitals.Value = Vitals.Spawn(MaxHealth);
            }

            pendingCapacity = -1f;
            pendingResponses = null;

            vitals.OnValueChanged += OnVitalsChanged;

            if (NetworkObject.IsPlayerObject)
            {
                SpawnedPlayers.Add(this);
                SpawnedPlayersChanged?.Invoke();
            }

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

            if (SpawnedPlayers.Remove(this)) SpawnedPlayersChanged?.Invoke();

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
            bus?.Publish(new LocalVitalsChanged(state.Health, MaxHealth, state.IsDowned,
                state.IsDead, state.BleedOutRemaining));

        private void Update()
        {
            if (!IsServer || !vitals.Value.IsDowned) return;

            var next = Vitals.Tick(vitals.Value, Time.deltaTime);
            if (next.Equals(vitals.Value)) return;

            vitals.Value = next;

            if (next.IsDead) ServerDied?.Invoke(this);
        }

        public float ApplyDamage(in DamageInfo info)
        {
            if (!IsServer || !IsSpawned) return 0f;

            var before = vitals.Value;
            if (!before.IsStanding) return 0f;

            var resolved = responses?.Resolve(info.Amount, info.Type) ?? info.Amount;
            if (resolved <= 0f) return 0f;

            var after = canBeDowned
                ? Vitals.ApplyDamage(before, resolved, GameplayConstants.BleedOutSeconds)
                : Vitals.ApplyDamage(before, resolved, 0f);

            if (!canBeDowned && after.IsDowned) after = Vitals.Kill(after);

            if (after.Equals(before)) return 0f;

            vitals.Value = after;

            if (after.IsDowned) ServerDowned?.Invoke(this);
            else if (after.IsDead) ServerDied?.Invoke(this);

            return Mathf.Min(resolved, before.Health);
        }

        public bool ServerRevive(float health = GameplayConstants.ReviveHealth)
        {
            if (!IsServer || !IsSpawned) return false;

            var after = Vitals.Revive(vitals.Value, Mathf.Min(health, MaxHealth));
            if (after.Equals(vitals.Value)) return false;

            vitals.Value = after;
            return true;
        }

        public bool ServerHeal(float amount)
        {
            if (!IsServer || !IsSpawned) return false;

            var after = Vitals.Heal(vitals.Value, amount, MaxHealth);
            if (after.Equals(vitals.Value)) return false;

            vitals.Value = after;
            return true;
        }

        public bool ServerRestore()
        {
            if (!IsServer || !IsSpawned) return false;

            var after = Vitals.Spawn(MaxHealth);
            if (after.Equals(vitals.Value)) return false;

            vitals.Value = after;
            return true;
        }

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
