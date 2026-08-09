using System;
using Office.Core;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The light every player carries, toggled with one key and paid for with a battery.
    /// </summary>
    /// <remarks>
    /// GDD §14 makes darkness the default and light the resource that buys information back.
    /// This is the baseline supply of it — enough to move, never enough to feel safe.
    /// <para>
    /// <b>The on/off state is replicated; the battery is not authoritative.</b> Whether the beam
    /// is lit has to be a fact everyone agrees on, because a teammate's light is the main way
    /// you know where they are. The charge left in it follows the same trade
    /// <see cref="PlayerMovement"/> makes with stamina: written by the owner, replicated
    /// outward for display. The worst a modified client buys is a battery that never dies,
    /// which costs the group nothing and the server nothing to ignore.
    /// </para>
    /// <para>
    /// The optics come from a <see cref="LightSourceModule"/> asset rather than from fields
    /// here, so the built-in light and a flashlight the player picks up off a desk are tuned in
    /// the same place and can share an asset.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerFlashlight : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader input;

        [Tooltip("The spot light itself. Must hang off the camera pivot, not the camera — the " +
                 "camera is switched off on remote instances and its children go with it.")]
        [SerializeField] private Light beam;

        [Tooltip("Range, cone angle, intensity and drain. The same asset type a carried " +
                 "flashlight uses.")]
        [SerializeField] private LightSourceModule optics;

        [Tooltip("Charge the player starts a run with, out of 100.")]
        [Range(0f, 100f)]
        [SerializeField] private float startingCharge = 100f;

        [Tooltip("Charge that has to be recovered before a dead battery can be switched on " +
                 "again. Without it a flat light stutters on and off as it trickles back.")]
        [Range(0f, 100f)]
        [SerializeField] private float relightThreshold = 5f;

        [Tooltip("Charge recovered per second while the light is off. Zero is the honest " +
                 "horror setting: batteries are found, not waited for.")]
        [Min(0f)]
        [SerializeField] private float rechargePerSecond;

        private const float FullCharge = 100f;

        private readonly NetworkVariable<bool> lit = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> replicatedCharge = new(
            FullCharge, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private IEventBus bus;
        private float charge;
        private bool paused;

        /// <summary>Fires on every machine when this player's beam goes on or off.</summary>
        public event Action<bool> LitChanged;

        public bool IsLit => lit.Value;

        public float Charge => IsOwner ? charge : replicatedCharge.Value;

        public float NormalisedCharge => Mathf.Clamp01(Charge / FullCharge);

        public override void OnNetworkSpawn()
        {
            lit.OnValueChanged += OnLitChanged;

            ApplyOptics();
            ApplyBeam(lit.Value);

            if (!IsOwner) return;

            charge = startingCharge;
            replicatedCharge.Value = charge;

            if (ServiceLocator.TryGet(out bus)) bus.Subscribe<LocalPauseChanged>(OnPauseChanged);
        }

        public override void OnNetworkDespawn()
        {
            lit.OnValueChanged -= OnLitChanged;

            // Left dark. This component is on a pooled, respawning player object, and a beam
            // still burning on a despawned body is a light source nobody owns.
            ApplyBeam(false);

            if (!IsOwner) return;

            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);
            bus = null;
            paused = false;
        }

        private void OnPauseChanged(LocalPauseChanged evt) => paused = evt.IsPaused;

        private void OnLitChanged(bool previous, bool current)
        {
            ApplyBeam(current);
            LitChanged?.Invoke(current);
        }

        // Pushed onto the Light once, from the asset. Doing it here rather than authoring the
        // component means retuning every flashlight in the game is one asset, not one prefab
        // per light.
        private void ApplyOptics()
        {
            if (beam == null || optics == null) return;

            beam.type = LightType.Spot;
            beam.range = optics.Range;
            beam.spotAngle = optics.Angle;
            beam.intensity = optics.Intensity;
        }

        private void ApplyBeam(bool on)
        {
            if (beam != null) beam.enabled = on;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            if (!paused && input != null && input.FlashlightPressedThisFrame) Toggle();

            UpdateCharge(Time.deltaTime);
        }

        private void Toggle()
        {
            if (lit.Value)
            {
                lit.Value = false;
                return;
            }

            // A flat battery must not click on for one frame and die again.
            if (charge < relightThreshold) return;

            lit.Value = true;
        }

        private void UpdateCharge(float deltaTime)
        {
            var drain = optics != null ? optics.DrainPerSecond : 0f;

            if (lit.Value && drain > 0f)
            {
                charge -= drain * deltaTime;

                if (charge <= 0f)
                {
                    charge = 0f;
                    lit.Value = false;
                }
            }
            else if (!lit.Value && rechargePerSecond > 0f)
            {
                charge = Mathf.Min(FullCharge, charge + rechargePerSecond * deltaTime);
            }

            PublishCharge();
        }

        // Whole points only, plus the two ends. A bar drawn from this cannot show more
        // resolution than that, and a NetworkVariable written every frame is a message every
        // tick for the length of the run.
        private void PublishCharge()
        {
            var atEnd = (charge <= 0f && replicatedCharge.Value > 0f) ||
                        (Mathf.Approximately(charge, FullCharge) &&
                         replicatedCharge.Value < FullCharge);

            if (!atEnd && Mathf.Abs(replicatedCharge.Value - charge) < 1f) return;

            replicatedCharge.Value = charge;
        }
    }
}
