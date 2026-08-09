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
    /// hands out ids to new assets and fails loudly on a duplicate. Editing the array by
    /// hand is allowed but pointless — the next rebuild overwrites it.
    /// <para>
    /// <b>One array, not one per type.</b> The id space is already shared across every kind
    /// of content, so a per-type array bought nothing and cost a pair of fields, a
    /// dictionary, a <c>TryGet</c> and an edit to the builder for every new type. GDD still
    /// has <c>EnemyDefinition</c>, <c>RoomDefinition</c>, <c>RecipeDefinition</c>,
    /// <c>ObjectiveDefinition</c> and <c>AnomalyDefinition</c> coming; with this shape,
    /// adding one means creating an asset and nothing else.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Office/Content/Definition Registry", fileName = "REG_Definitions")]
    public sealed class DefinitionRegistry : ScriptableObject
    {
        [SerializeField] private ContentDefinition[] definitions = Array.Empty<ContentDefinition>();

        private Dictionary<int, ContentDefinition> byId;

        /// <summary>Everything the registry knows about, in the order the builder wrote it.</summary>
        public IReadOnlyList<ContentDefinition> All =>
            definitions ?? (IReadOnlyList<ContentDefinition>)Array.Empty<ContentDefinition>();

        // ScriptableObject state outlives play mode in the editor. Dropping the cache here
        // means a rebuilt registry is never served from a stale index.
        private void OnEnable() => Invalidate();

        public void Invalidate() => byId = null;

        /// <summary>
        /// Resolves an id, and only to the type asked for.
        /// </summary>
        /// <remarks>
        /// The type check is a real guard, not a cast convenience: ids are shared across every
        /// kind of content, so a stale id that now belongs to a prop must fail to resolve as
        /// an item rather than come back as something the caller will misuse.
        /// </remarks>
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
