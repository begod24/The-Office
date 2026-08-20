using Office.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The switch that puts a power zone back on — and, for the one that carries the run's
    /// objective, the thing that ends the shift. GDD §10.2 and §16.
    /// </summary>
    /// <remarks>
    /// <b>The first thing to publish <c>PowerStateChanged</c>.</b> The event and its zone state
    /// were authored long before anything raised them; this is the other end. It is published
    /// from the replicated value's change callback rather than from
    /// <see cref="Interact"/>, so every machine raises it from the same fact at the same
    /// moment — a server-side publish would leave every client's lights, doors and HUD reading
    /// a zone that is still off.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PowerSwitch : NetworkBehaviour, IInteractable
    {
        [Tooltip("Lit while the zone is off, dark once it is on — the difference a player " +
                 "reads from across the room before they can read the prompt.")]
        [SerializeField] private Renderer indicator;

        [SerializeField] private Color offColour = new(0.85f, 0.20f, 0.15f, 1f);
        [SerializeField] private Color onColour = new(0.35f, 0.85f, 0.35f, 1f);

        private readonly NetworkVariable<bool> isOn = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> zone = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString64Bytes> label = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> completesRun = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Replicated rather than applied on the server alone: the collider is what every
        // client's own crosshair probes against, and the hidden art is what every client
        // draws. Both are per-machine facts that have to arrive with the spawn.
        private readonly NetworkVariable<Vector3> volumeSize = new(
            new Vector3(0.34f, 0.5f, 0.18f),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> useLevelArt = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IEventBus bus;
        private RunOutcome outcome;

        private MaterialPropertyBlock block;

        // Server only, consumed at spawn. Held in plain fields rather than written straight
        // into the NetworkVariables above for the same reason Health.ServerConfigure defers:
        // a value written before the spawn rides the spawn payload, and one written after
        // arrives as a delta a client that was still loading can miss.
        private int pendingZone;
        private bool pendingCompletesRun;
        private FixedString64Bytes pendingLabel;
        private Vector3 pendingVolume = new(0.34f, 0.5f, 0.18f);
        private bool pendingUseLevelArt;

        /// <inheritdoc />
        public string Prompt => isOn.Value ? string.Empty : label.Value.ToString();

        /// <inheritdoc />
        public bool IsAvailable => IsSpawned && !isOn.Value;

        /// <summary>Whether this zone is powered. Replicated, so every machine agrees.</summary>
        public bool IsOn => isOn.Value;

        public int ZoneId => zone.Value;

        /// <summary>
        /// Server only, before <c>Spawn()</c>. Everything the marker authored, plus the judge
        /// to tell when the objective is done.
        /// </summary>
        /// <remarks>
        /// Written before the spawn on purpose: these ride the spawn payload, so a late joiner
        /// reads the same prompt and the same zone as everyone else instead of receiving them
        /// as a delta it may have missed.
        /// </remarks>
        public void ServerInitialise(PowerSwitchPlacement placement, RunOutcome judge)
        {
            outcome = judge;

            if (placement == null) return;

            pendingZone = placement.ZoneId;
            pendingCompletesRun = placement.CompletesRun;
            pendingLabel = new FixedString64Bytes(placement.Prompt ?? string.Empty);
            pendingUseLevelArt = placement.UseLevelArt;

            var size = placement.VolumeSize;
            pendingVolume = new Vector3(
                Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), Mathf.Max(0.05f, size.z));
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Assigned unconditionally, never topped up: this carrier is pooled, and a
                // returning instance still holds the last marker's zone and prompt.
                zone.Value = pendingZone;
                completesRun.Value = pendingCompletesRun;
                label.Value = pendingLabel;
                volumeSize.Value = pendingVolume;
                useLevelArt.Value = pendingUseLevelArt;
                isOn.Value = false;
            }

            pendingZone = 0;
            pendingCompletesRun = false;
            pendingLabel = default;
            pendingVolume = new Vector3(0.34f, 0.5f, 0.18f);
            pendingUseLevelArt = false;

            ApplyVolume();

            ServiceLocator.TryGet(out bus);

            isOn.OnValueChanged += OnPowerChanged;

            ApplyIndicator(isOn.Value);

            // A late joiner walks into a zone that is already on and has to see it that way.
            if (isOn.Value) Publish(true);
        }

        public override void OnNetworkDespawn()
        {
            isOn.OnValueChanged -= OnPowerChanged;

            bus = null;
            outcome = null;
        }

        /// <inheritdoc />
        public void Interact(ulong clientId)
        {
            if (!IsServer || !IsAvailable) return;

            isOn.Value = true;

            Debug.Log($"[Power] Zone {zone.Value} restored by client {clientId}.");

            if (!completesRun.Value) return;

            if (outcome != null) outcome.ServerCompleteRun();
            else Debug.LogWarning("[Power] The objective switch has no RunOutcome. The shift " +
                                  "will not end.", this);
        }

        /// <summary>
        /// Sizes the volume the crosshair finds, and hides this carrier's own art when the
        /// level already draws the switch.
        /// </summary>
        private void ApplyVolume()
        {
            var box = GetComponent<BoxCollider>();

            if (box != null)
            {
                box.size = volumeSize.Value;
                box.center = Vector3.zero;
            }

            if (!useLevelArt.Value) return;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        private void OnPowerChanged(bool previous, bool current)
        {
            ApplyIndicator(current);
            Publish(current);
        }

        private void Publish(bool powered) =>
            bus?.Publish(new PowerStateChanged(zone.Value, powered));

        private void ApplyIndicator(bool powered)
        {
            if (indicator == null) return;

            block ??= new MaterialPropertyBlock();

            indicator.GetPropertyBlock(block);

            var colour = powered ? onColour : offColour;

            // Both, because URP's lit shader reads _BaseColor and the emission that makes this
            // readable in an unlit room reads _EmissionColor. A property block rather than a
            // material instance: one switch must not leak a material per spawn.
            block.SetColor(BaseColour, colour);
            block.SetColor(EmissionColour, colour * (powered ? 1.6f : 2.2f));

            indicator.SetPropertyBlock(block);
        }

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");
    }
}
