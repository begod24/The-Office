using System.Collections.Generic;
using NUnit.Framework;
using Office.Data;
using UnityEditor;
using UnityEngine;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// Guards the contract the whole item pipeline rests on: an id resolves to exactly one
    /// definition, on every machine. A duplicate or a missing id fails only on the remote
    /// client at runtime, so it has to be caught here instead.
    /// </summary>
    public sealed class DefinitionRegistryTests
    {
        private const string RegistryPath =
            "Assets/Project/ScriptableObject/Config/REG_Definitions.asset";

        private static DefinitionRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<DefinitionRegistry>(RegistryPath);

            if (registry == null)
                Assert.Ignore($"No registry at {RegistryPath}. " +
                              "Run 'Office/Content/Rebuild Definition Registry'.");

            return registry;
        }

        private static IEnumerable<ContentDefinition> AllDefinitions()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ContentDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<ContentDefinition>(path);
                if (definition != null) yield return definition;
            }
        }

        [Test]
        public void EveryDefinitionInTheProjectHasAnId()
        {
            foreach (var definition in AllDefinitions())
                Assert.IsTrue(definition.HasValidId,
                    $"'{definition.name}' has no id. Rebuild the definition registry.");
        }

        [Test]
        public void NoTwoDefinitionsShareAnId()
        {
            var seen = new Dictionary<int, string>();

            foreach (var definition in AllDefinitions())
            {
                if (!definition.HasValidId) continue;

                Assert.IsFalse(seen.TryGetValue(definition.Id, out var owner),
                    $"'{definition.name}' and '{owner}' both claim id {definition.Id}.");

                seen[definition.Id] = definition.name;
            }
        }

        [Test]
        public void RegistryResolvesEveryItemItLists()
        {
            var registry = LoadRegistry();

            foreach (var item in registry.Items)
            {
                Assert.IsNotNull(item, "The registry holds a null item entry.");

                Assert.IsTrue(registry.TryGetItem(item.Id, out var resolved),
                    $"Id {item.Id} ('{item.name}') is listed but does not resolve.");

                Assert.AreSame(item, resolved);
            }
        }

        [Test]
        public void RegistryListsEveryItemInTheProject()
        {
            var registry = LoadRegistry();
            var listed = new HashSet<Object>(registry.Items);

            foreach (var definition in AllDefinitions())
            {
                if (definition is not ItemDefinition item) continue;

                Assert.IsTrue(listed.Contains(item),
                    $"'{item.name}' exists but is not in the registry — it will never resolve " +
                    "at runtime. Rebuild the definition registry.");
            }
        }

        [Test]
        public void UnknownIdDoesNotResolve()
        {
            var registry = LoadRegistry();

            Assert.IsFalse(registry.TryGetItem(ContentDefinition.NoId, out _),
                "Id 0 is reserved for 'no content' and must never resolve.");
        }

        [Test]
        public void EveryItemHasAViewPrefab()
        {
            var registry = LoadRegistry();

            foreach (var item in registry.Items)
                Assert.IsNotNull(item.ViewPrefab,
                    $"'{item.name}' has no view prefab — it would spawn invisible and " +
                    "unreachable.");
        }
    }
}
