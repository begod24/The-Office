using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ItemPlacement : MonoBehaviour
    {
        [Tooltip("What to put here. Empty markers are skipped with a warning.")]
        [SerializeField] private ItemDefinition definition;

        [Tooltip("How many. Anything over the item's max stack still spawns as one pile — " +
                 "the inventory splits it across slots on pickup.")]
        [Min(1)]
        [SerializeField] private int count = 1;

        private static readonly List<ItemPlacement> Active = new(32);

        public static IReadOnlyList<ItemPlacement> All => Active;

        public ItemDefinition Definition => definition;

        public int Count => count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = definition != null
                ? new Color(0.95f, 0.78f, 0.25f, 0.9f)
                : new Color(0.9f, 0.25f, 0.2f, 0.9f);

            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.15f, Vector3.one * 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
        }
    }
}
