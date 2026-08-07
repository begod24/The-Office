using System;
using System.Collections.Generic;
using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// The one place a network id turns back into authored content. Server and client load
    /// the same asset, so both resolve an id to the same definition — which is the whole
    /// reason ids exist instead of asset references.
    /// </summary>
    /// <remarks>
    /// Rebuilt by 'Office/Content/Rebuild Definition Registry', which scans the project,
    /// hands out ids to new assets and fails loudly on a duplicate. Editing the arrays by
    /// hand is allowed but pointless — the next rebuild overwrites them.
    /// </remarks>
    [CreateAssetMenu(menuName = "Office/Content/Definition Registry", fileName = "REG_Definitions")]
    public sealed class DefinitionRegistry : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();
        [SerializeField] private PropDefinition[] props = Array.Empty<PropDefinition>();

        private Dictionary<int, ItemDefinition> itemsById;
        private Dictionary<int, PropDefinition> propsById;

        public IReadOnlyList<ItemDefinition> Items => items;

        public IReadOnlyList<PropDefinition> Props => props;

        // ScriptableObject state outlives play mode in the editor. Dropping the caches here
        // means a rebuilt registry is never served from a stale index.
        private void OnEnable() => Invalidate();

        public void Invalidate()
        {
            itemsById = null;
            propsById = null;
        }

        public bool TryGetItem(int id, out ItemDefinition definition)
        {
            itemsById ??= BuildIndex(items);
            return itemsById.TryGetValue(id, out definition);
        }

        public bool TryGetProp(int id, out PropDefinition definition)
        {
            propsById ??= BuildIndex(props);
            return propsById.TryGetValue(id, out definition);
        }

        private static Dictionary<int, T> BuildIndex<T>(T[] source) where T : ContentDefinition
        {
            var index = new Dictionary<int, T>(source.Length);

            foreach (var definition in source)
            {
                if (definition == null || !definition.HasValidId) continue;

                if (index.TryGetValue(definition.Id, out var existing))
                {
                    Debug.LogError(
                        $"[Content] Id {definition.Id} is claimed by both '{existing.name}' and " +
                        $"'{definition.name}'. One of them will never resolve — rebuild the registry.",
                        definition);
                    continue;
                }

                index.Add(definition.Id, definition);
            }

            return index;
        }
    }
}
