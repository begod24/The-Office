using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Enemies
{
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

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = definition != null
                ? new Color(0.85f, 0.22f, 0.18f, 0.9f)
                : new Color(0.9f, 0.2f, 0.9f, 0.9f);

            var height = definition != null ? definition.BodyHeight : 1f;
            var centre = transform.position + Vector3.up * (height * 0.5f);

            Gizmos.DrawWireCube(centre, new Vector3(0.5f, height, 0.5f));
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);
        }
    }
}
