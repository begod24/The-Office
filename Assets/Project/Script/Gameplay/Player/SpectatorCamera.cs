using Office.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Office.Gameplay
{
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

            if (cameraTransform == null) return;

            cameraTransform.localPosition = restLocalPosition;
            cameraTransform.localRotation = restLocalRotation;
        }

        private void LateUpdate()
        {
            if (!watching || cameraTransform == null) return;

            ReadCycleInput();

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
