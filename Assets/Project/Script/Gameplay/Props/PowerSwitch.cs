using Office.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
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

        private readonly NetworkVariable<Vector3> volumeSize = new(
            new Vector3(0.34f, 0.5f, 0.18f),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> useLevelArt = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IEventBus bus;
        private RunOutcome outcome;

        private MaterialPropertyBlock block;

        private int pendingZone;
        private bool pendingCompletesRun;
        private FixedString64Bytes pendingLabel;
        private Vector3 pendingVolume = new(0.34f, 0.5f, 0.18f);
        private bool pendingUseLevelArt;

        public string Prompt => isOn.Value ? string.Empty : label.Value.ToString();

        public bool IsAvailable => IsSpawned && !isOn.Value;

        public bool IsOn => isOn.Value;

        public int ZoneId => zone.Value;

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

            if (isOn.Value) Publish(true);
        }

        public override void OnNetworkDespawn()
        {
            isOn.OnValueChanged -= OnPowerChanged;

            bus = null;
            outcome = null;
        }

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

            block.SetColor(BaseColour, colour);
            block.SetColor(EmissionColour, colour * (powered ? 1.6f : 2.2f));

            indicator.SetPropertyBlock(block);
        }

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");
    }
}
