using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Turns a definition id into the mesh a player actually sees — on the floor under a
    /// <see cref="WorldItem"/>, or in a hand under <see cref="HeldItemView"/>.
    /// </summary>
    /// <remarks>
    /// Both callers do the same three things: resolve the id through the registry,
    /// instantiate the definition's view prefab, force the layer. The layer is the part
    /// worth centralising — the interaction probe's mask depends on it, so a wrong one makes
    /// an item silently unreachable rather than visibly broken.
    /// </remarks>
    internal static class ItemViewFactory
    {
        public static ItemDefinition Resolve(int definitionId, Object context)
        {
            if (definitionId == ContentDefinition.NoId) return null;

            if (!ServiceLocator.TryGet<DefinitionRegistry>(out var registry))
            {
                Debug.LogError("[Item] No DefinitionRegistry registered. Enter play mode from " +
                               "SCN_Boot so the bootstrap runs.", context);
                return null;
            }

            if (registry.TryGetItem(definitionId, out var definition)) return definition;

            Debug.LogError($"[Item] Definition id {definitionId} is not in the registry. Run " +
                           "'Office/Content/Rebuild Definition Registry'.", context);
            return null;
        }

        /// <param name="solid">
        /// False strips the colliders. A held item is decoration: leaving its collider live
        /// would let the holder's own item block their interaction probe and shove them.
        /// </param>
        public static GameObject Build(ItemDefinition definition, Transform parent,
            Vector3 localPosition, Quaternion localRotation, int layer, bool solid)
        {
            if (definition == null) return null;

            if (definition.ViewPrefab == null)
            {
                Debug.LogError($"[Item] '{definition.name}' has no view prefab — it would be " +
                               "invisible wherever it appears.", definition);
                return null;
            }

            var view = Object.Instantiate(definition.ViewPrefab, parent);
            view.transform.SetLocalPositionAndRotation(localPosition, localRotation);

            SetLayerRecursively(view, layer);

            if (!solid)
                foreach (var collider in view.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;

            return view;
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;

            for (var i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }
    }
}
