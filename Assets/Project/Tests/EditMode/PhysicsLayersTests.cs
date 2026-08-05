using NUnit.Framework;
using Office.Data;
using UnityEngine;

namespace Office.Tests.EditMode
{
    public sealed class PhysicsLayersTests
    {
        [Test]
        public void EveryConstantMatchesTheProjectLayerNames()
        {
            foreach (var (index, expectedName) in PhysicsLayers.Expected)
            {
                var actual = LayerMask.LayerToName(index);

                Assert.AreEqual(expectedName, actual,
                    $"Layer {index} is '{actual}' in TagManager but '{expectedName}' in " +
                    "PhysicsLayers. One of them was changed without the other.");
            }
        }

        [Test]
        public void MasksContainTheLayersTheyClaimTo()
        {
            Assert.IsTrue(Contains(PhysicsLayers.WalkableMask, PhysicsLayers.LevelGeometry));
            Assert.IsTrue(Contains(PhysicsLayers.InteractionMask, PhysicsLayers.Interactable));
            Assert.IsTrue(Contains(PhysicsLayers.OcclusionMask, PhysicsLayers.LevelGeometry));
        }

        [Test]
        public void WalkableMaskExcludesPlayersAndEnemies()
        {
            Assert.IsFalse(Contains(PhysicsLayers.WalkableMask, PhysicsLayers.Player));
            Assert.IsFalse(Contains(PhysicsLayers.WalkableMask, PhysicsLayers.Enemy));
        }

        private static bool Contains(LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;
    }
}
