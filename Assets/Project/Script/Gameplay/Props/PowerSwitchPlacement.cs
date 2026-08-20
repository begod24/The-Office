using System.Collections.Generic;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PowerSwitchPlacement : MonoBehaviour
    {
        [Tooltip("Which power zone this switch feeds. GDD §10.2 — one floor has several, and " +
                 "the id is what a light or a door listens for.")]
        [Min(0)]
        [SerializeField] private int zoneId;

        [Tooltip("Verb shown under the crosshair.")]
        [SerializeField] private string prompt = "RESTORE POWER";

        [Tooltip("On for the switch that finishes the shift. The vertical slice has exactly " +
                 "one objective (GDD §16), so exactly one marker should carry this.")]
        [SerializeField] private bool completesRun = true;

        [Header("Volume")]
        [Tooltip("On when the level already draws the switch — a panel modelled into the " +
                 "generator, a lever on a wall. The spawned carrier then hides its own art " +
                 "and contributes only the collider and the behaviour, so what the player " +
                 "sees is the thing the level designer put there.")]
        [SerializeField] private bool useLevelArt;

        [Tooltip("Size of the volume the crosshair has to find, in the marker's own space. " +
                 "It must sit slightly in FRONT of the level art it stands for: the probe " +
                 "takes the nearest hit, and level geometry is in the interaction mask, so a " +
                 "volume behind the mesh loses to it and the prompt never appears.")]
        [SerializeField] private Vector3 volumeSize = new(0.34f, 0.5f, 0.18f);

        private static readonly List<PowerSwitchPlacement> Active = new(8);

        public static IReadOnlyList<PowerSwitchPlacement> All => Active;

        public int ZoneId => zoneId;

        public string Prompt => prompt;

        public bool CompletesRun => completesRun;

        public bool UseLevelArt => useLevelArt;

        public Vector3 VolumeSize => volumeSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = completesRun
                ? new Color(0.95f, 0.75f, 0.20f, 0.9f)
                : new Color(0.35f, 0.70f, 0.95f, 0.9f);

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, volumeSize);
            Gizmos.DrawRay(Vector3.zero, Vector3.forward * 0.4f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
