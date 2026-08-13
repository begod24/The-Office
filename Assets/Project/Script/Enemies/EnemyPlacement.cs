using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Enemies
{
    /// <summary>
    /// A level-authored "an enemy starts here" marker.
    /// </summary>
    /// <remarks>
    /// The same arrangement as <c>ItemPlacement</c>, for the same reason: with
    /// <c>EnableSceneManagement</c> off, NGO cannot resolve an in-scene placed NetworkObject
    /// on a remote client, so the marker stays inert scene data on every machine and the
    /// server spawns the registered <c>PF_Enemy</c> from it when the run starts.
    /// <para>
    /// A marker is a spawn point, not a live spawner: one marker, one enemy, once per run.
    /// Waves, respawns and the escalating director from GDD §6.2 belong to whatever reads
    /// the markers, not to the markers themselves.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyPlacement : MonoBehaviour
    {
        [Tooltip("What hunts here. Empty markers are skipped with a warning.")]
        [SerializeField] private EnemyDefinition definition;

        private static readonly List<EnemyPlacement> Active = new(16);

        public static IReadOnlyList<EnemyPlacement> All => Active;

        public EnemyDefinition Definition => definition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        // Registering here rather than scanning the scene keeps the spawner free of a
        // FindObjectsByType sweep on every run start — same as ItemPlacement.
        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = definition != null
                ? new Color(0.85f, 0.22f, 0.18f, 0.9f)
                : new Color(0.9f, 0.2f, 0.9f, 0.9f);

            // A body-sized wire capsule stand-in, so a marker reads as "something stands here"
            // next to the smaller item cubes.
            var height = definition != null ? definition.BodyHeight : 1f;
            var centre = transform.position + Vector3.up * (height * 0.5f);

            Gizmos.DrawWireCube(centre, new Vector3(0.5f, height, 0.5f));
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);
        }
    }
}
