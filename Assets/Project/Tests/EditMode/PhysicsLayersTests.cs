using NUnit.Framework;
using Office.Data;
using UnityEngine;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// <see cref="PhysicsLayers"/> duplicates what lives in TagManager.asset. That duplication is
    /// worth it — code should not carry magic layer indices — but it has to be checked, because
    /// the failure mode is silent: a renamed layer makes raycasts miss with no error anywhere.
    /// </summary>
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
            // If players were walkable the crouch headroom probe would refuse to stand up
            // whenever a teammate walked past.
            Assert.IsFalse(Contains(PhysicsLayers.WalkableMask, PhysicsLayers.Player));
            Assert.IsFalse(Contains(PhysicsLayers.WalkableMask, PhysicsLayers.Enemy));
        }

        private static bool Contains(LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;
    }
}
