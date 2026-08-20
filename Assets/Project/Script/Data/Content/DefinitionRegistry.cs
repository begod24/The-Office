using System;
using System.Collections.Generic;
using UnityEngine;

namespace Office.Data
{
    [CreateAssetMenu(menuName = "Office/Content/Definition Registry", fileName = "REG_Definitions")]
    public sealed class DefinitionRegistry : ScriptableObject
    {
        [SerializeField] private ContentDefinition[] definitions = Array.Empty<ContentDefinition>();

        private Dictionary<int, ContentDefinition> byId;

        public IReadOnlyList<ContentDefinition> All =>
            definitions ?? (IReadOnlyList<ContentDefinition>)Array.Empty<ContentDefinition>();

        private void OnEnable() => Invalidate();

        public void Invalidate() => byId = null;

        public bool TryGet<T>(int id, out T definition) where T : ContentDefinition
        {
            byId ??= BuildIndex(definitions);

            if (byId.TryGetValue(id, out var found) && found is T typed)
            {
                definition = typed;
                return true;
            }

            definition = null;
            return false;
        }

        private static Dictionary<int, ContentDefinition> BuildIndex(ContentDefinition[] source)
        {
            if (source == null) return new Dictionary<int, ContentDefinition>();

            var index = new Dictionary<int, ContentDefinition>(source.Length);

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
