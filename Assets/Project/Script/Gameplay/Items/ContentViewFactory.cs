using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Turns a definition id into the mesh a player actually sees — on the floor under a
    /// <see cref="WorldItem"/>, in a hand under <see cref="HeldItemView"/>, or as the body of
    /// a <see cref="DamageableTarget"/>.
    /// </summary>
    /// <remarks>
    /// Every caller does the same three things: resolve the id through the registry,
    /// instantiate the definition's view prefab, force the layer. The layer is the part worth
    /// centralising — the interaction and attack masks depend on it, so a wrong one makes an
    /// object silently unreachable rather than visibly broken.
    /// <para>
    /// Typed on <see cref="ContentDefinition"/> rather than on items, because the pattern is
    /// the content system's and not the inventory's: a target, a prop and a pickup all consist
    /// of a networked carrier plus a plain local view, which is what keeps the network prefab
    /// list from growing by one entry per asset.
    /// </para>
    /// <para>
    /// Public rather than internal because <c>Office.Enemies</c> is the fourth caller and lives
    /// in its own assembly. Widening it is cheaper than either copying the three steps or
    /// dragging enemies down into <c>Office.Gameplay</c> to be near them.
    /// </para>
    /// </remarks>
    public static class ContentViewFactory
    {
        /// <summary>
        /// The definition behind <paramref name="definitionId"/>, or null with a logged reason.
        /// </summary>
        public static T Resolve<T>(int definitionId, Object context) where T : ContentDefinition
        {
            if (definitionId == ContentDefinition.NoId) return null;

            if (!ServiceLocator.TryGet<DefinitionRegistry>(out var registry))
            {
                Debug.LogError("[Content] No DefinitionRegistry registered. Enter play mode from " +
                               "SCN_Boot so the bootstrap runs.", context);
                return null;
            }

            if (registry.TryGet<T>(definitionId, out var definition)) return definition;

            Debug.LogError($"[Content] Id {definitionId} did not resolve as {typeof(T).Name}. " +
                           "Run 'Office/Content/Rebuild Definition Registry'.", context);
            return null;
        }

        /// <param name="solid">
        /// False strips the colliders. A held item is decoration: leaving its collider live
        /// would let the holder's own item block their interaction probe and shove them.
        /// </param>
        public static GameObject Build(ContentDefinition definition, Transform parent,
            Vector3 localPosition, Quaternion localRotation, int layer, bool solid)
        {
            if (definition == null) return null;

            if (definition.ViewPrefab == null)
            {
                Debug.LogError($"[Content] '{definition.name}' has no view prefab — it would be " +
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
