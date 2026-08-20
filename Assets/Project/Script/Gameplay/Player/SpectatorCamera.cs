using Office.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Office.Gameplay
{
    /// <summary>
    /// What a dead player watches. GDD §15: death is not a return to the menu — the run goes
    /// on, and the person who died stays in it as an audience.
    /// </summary>
    /// <remarks>
    /// <b>The body is not despawned and the camera is not reparented.</b> Both are deliberate.
    /// A despawned player object takes their <see cref="Health"/> off every other machine's
    /// squad readout, so the living would stop being able to see that someone is dead; and
    /// reparenting a camera that the rig hands out as
    /// <see cref="PlayerRig.EyeAnchor"/> would move the seat another spectator is watching
    /// from. Driving the transform in world space each frame leaves the hierarchy alone and
    /// undoes itself the moment the player is restored.
    /// <para>
    /// <b>Input is read straight from the device</b>, exactly as <c>PauseScreen</c> reads
    /// Escape. The whole point of being dead is that <see cref="PlayerInputReader"/> is off —
    /// asking it for a keypress would return the frozen values it stopped updating.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SpectatorCamera : NetworkBehaviour
    {
        [SerializeField] private PlayerRig rig;
        [SerializeField] private Health health;

        [Tooltip("Metres behind the watched player's eye. A little back, so the spectator " +
                 "sees the teammate's shoulders and not the inside of their skull.")]
        [SerializeField] private float followDistance = 0.55f;

        [Tooltip("Metres above it, for the same reason.")]
        [SerializeField] private float followHeight = 0.18f;

        private Transform cameraTransform;
        private Vector3 restLocalPosition;
        private Quaternion restLocalRotation;

        private int targetIndex;
        private bool watching;

        /// <summary>Who this spectator is currently watching, or null.</summary>
        public Health Target { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            if (rig == null) rig = GetComponent<PlayerRig>();
            if (health == null) health = GetComponent<Health>();

            cameraTransform = rig != null && rig.PlayerCamera != null
                ? rig.PlayerCamera.transform
                : null;

            if (cameraTransform != null)
            {
                restLocalPosition = cameraTransform.localPosition;
                restLocalRotation = cameraTransform.localRotation;
            }

            if (health != null)
            {
                health.Changed += OnVitalsChanged;
                Apply(health.State);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (health != null) health.Changed -= OnVitalsChanged;

            Stop();
        }

        private void OnVitalsChanged(VitalsState state)
        {
            if (!IsOwner) return;

            Apply(state);
        }

        private void Apply(VitalsState state)
        {
            if (state.IsDead) Begin();
            else Stop();
        }

        private void Begin()
        {
            if (watching || cameraTransform == null) return;

            watching = true;
            targetIndex = 0;
            Target = FindNext(0);
        }

        private void Stop()
        {
            if (!watching) return;

            watching = false;
            Target = null;

            // Straight back to the seat the rig authored. Anything else leaves a revived
            // player looking out of the wrong place with no way to notice why.
            if (cameraTransform == null) return;

            cameraTransform.localPosition = restLocalPosition;
            cameraTransform.localRotation = restLocalRotation;
        }

        private void LateUpdate()
        {
            if (!watching || cameraTransform == null) return;

            ReadCycleInput();

            // The one being watched can go down or leave at any moment, and the frame after
            // that this reference is either destroyed or no longer worth watching.
            if (Target == null || !Target.IsSpawned || !Target.State.IsStanding)
                Target = FindNext(targetIndex);

            if (Target == null) return;

            var anchor = AnchorOf(Target);
            if (anchor == null) return;

            var rotation = anchor.rotation;
            var position = anchor.position
                           - rotation * Vector3.forward * followDistance
                           + Vector3.up * followHeight;

            cameraTransform.SetPositionAndRotation(position, rotation);
        }

        private void ReadCycleInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            var forward = (keyboard != null && (keyboard.dKey.wasPressedThisFrame ||
                                                keyboard.rightArrowKey.wasPressedThisFrame))
                          || (mouse != null && mouse.leftButton.wasPressedThisFrame);

            var back = keyboard != null && (keyboard.aKey.wasPressedThisFrame ||
                                            keyboard.leftArrowKey.wasPressedThisFrame);

            if (!forward && !back) return;

            Target = FindNext(targetIndex + (forward ? 1 : -1));
        }

        /// <summary>
        /// The next watchable player at or after <paramref name="from"/>, wrapping. Null when
        /// nobody is left standing — which is the frame the run is about to end anyway.
        /// </summary>
        private Health FindNext(int from)
        {
            var players = Health.SpawnedPlayerList;
            var count = players.Count;

            if (count == 0) return null;

            for (var step = 0; step < count; step++)
            {
                var index = ((from + step) % count + count) % count;
                var candidate = players[index];

                if (candidate == null || !candidate.IsSpawned) continue;
                if (ReferenceEquals(candidate, health)) continue;
                if (!candidate.State.IsStanding) continue;

                targetIndex = index;
                return candidate;
            }

            return null;
        }

        private static Transform AnchorOf(Health target)
        {
            var otherRig = target.GetComponent<PlayerRig>();
            return otherRig != null ? otherRig.EyeAnchor : target.transform;
        }
    }
}
