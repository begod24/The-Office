using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    public static class ContentViewFactory
    {
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
