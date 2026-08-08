using System.Collections.Generic;
using NUnit.Framework;
using Office.Data;
using UnityEditor;
using UnityEngine;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// Guards the lookup that composition rests on.
    /// </summary>
    /// <remarks>
    /// Every combat decision starts with "does the held item have a MeleeModule". If that
    /// question ever answers wrongly, a stapler swings like a fist and nobody gets an error —
    /// the same silent class of failure the definition registry tests exist for.
    /// </remarks>
    public sealed class ItemModuleTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in created)
                if (instance != null)
                    Object.DestroyImmediate(instance);

            created.Clear();
        }

        private T New<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            created.Add(instance);
            return instance;
        }

        // The array is private and serialized, which is how it should stay — an authored
        // list is not something runtime code assembles.
        private static void SetModules(ItemDefinition definition, params ItemModule[] modules)
        {
            var serialized = new SerializedObject(definition);
            var array = serialized.FindProperty("modules");

            array.arraySize = modules.Length;

            for (var i = 0; i < modules.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = modules[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void FreshDefinition_HasNoModules()
        {
            var definition = New<ItemDefinition>();

            Assert.IsNotNull(definition.Modules, "Modules must never be null.");
            Assert.AreEqual(0, definition.Modules.Count);
            Assert.IsNull(definition.GetModule<MeleeModule>());
            Assert.IsFalse(definition.HasModule<MeleeModule>());
        }

        [Test]
        public void GetModule_FindsTheModuleThatIsThere()
        {
            var definition = New<ItemDefinition>();
            var melee = New<MeleeModule>();

            SetModules(definition, melee);

            Assert.AreSame(melee, definition.GetModule<MeleeModule>());
            Assert.IsTrue(definition.HasModule<MeleeModule>());
        }

        [Test]
        public void GetModule_IgnoresModulesOfOtherTypes()
        {
            var definition = New<ItemDefinition>();

            SetModules(definition, New<LightSourceModule>());

            Assert.IsNull(definition.GetModule<MeleeModule>(),
                "A light source answered as a weapon.");
        }

        [Test]
        public void GetModule_FindsEachTypeOnAMultiRoleItem()
        {
            // The laser pointer from GDD §8.3: a weapon and a light source and consumable,
            // which is the combination inheritance cannot express.
            var definition = New<ItemDefinition>();
            var melee = New<MeleeModule>();
            var light = New<LightSourceModule>();
            var durability = New<DurabilityModule>();

            SetModules(definition, melee, light, durability);

            Assert.AreSame(melee, definition.GetModule<MeleeModule>());
            Assert.AreSame(light, definition.GetModule<LightSourceModule>());
            Assert.AreSame(durability, definition.GetModule<DurabilityModule>());
        }

        [Test]
        public void GetModule_SurvivesAnEmptySlotInTheList()
        {
            // An inspector list with a cleared row is a normal state to be in mid-edit.
            var definition = New<ItemDefinition>();
            var melee = New<MeleeModule>();

            SetModules(definition, null, melee);

            Assert.AreSame(melee, definition.GetModule<MeleeModule>(),
                "A null row stopped the scan before it reached a real module.");
        }

        [Test]
        public void GetModule_ByBaseType_MatchesAnyModule()
        {
            var definition = New<ItemDefinition>();
            var light = New<LightSourceModule>();

            SetModules(definition, light);

            Assert.AreSame(light, definition.GetModule<ItemModule>());
        }
    }
}
