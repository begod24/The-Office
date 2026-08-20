using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class TargetPlacement : MonoBehaviour
    {
        [Tooltip("What stands here. Empty markers are skipped with a warning.")]
        [SerializeField] private TargetDefinition definition;

        private static readonly List<TargetPlacement> Active = new(32);

        public static IReadOnlyList<TargetPlacement> All => Active;

        public TargetDefinition Definition => definition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = definition != null
                ? new Color(0.85f, 0.35f, 0.30f, 0.9f)
                : new Color(0.9f, 0.25f, 0.2f, 0.9f);

            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f,
                new Vector3(0.6f, 1.8f, 0.6f));
        }
    }
}
