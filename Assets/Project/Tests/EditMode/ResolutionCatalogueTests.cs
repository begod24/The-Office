using NUnit.Framework;
using Office.Data;
using UnityEngine;

namespace Office.Tests.EditMode
{
    public sealed class ResolutionCatalogueTests
    {
        [Test]
        public void ReportedModes_AreDeduplicated()
        {
            var options = ResolutionCatalogue.Build(new[]
            {
                new Vector2Int(1920, 1080),
                new Vector2Int(1920, 1080),
                new Vector2Int(1280, 720)
            });

            Assert.AreEqual(2, options.Count,
                "The picker would step through the same size once per refresh rate.");
        }

        [Test]
        public void NothingReported_FallsBackToTheStandardList()
        {
            var options = ResolutionCatalogue.Build(System.Array.Empty<Vector2Int>());

            CollectionAssert.AreEquivalent(ResolutionCatalogue.Fallback, options,
                "The editor reports no modes, which would leave the picker empty.");
        }

        [Test]
        public void ExtraSizes_AreAlwaysPresent()
        {
            var stored = new Vector2Int(3440, 1440);

            var options = ResolutionCatalogue.Build(
                new[] { new Vector2Int(1920, 1080) }, stored);

            CollectionAssert.Contains(options, stored,
                "A stored resolution missing from its own picker cannot be shown as selected.");
        }

        [Test]
        public void NonPositiveSizes_AreRejected()
        {
            var options = ResolutionCatalogue.Build(new[]
            {
                new Vector2Int(1920, 1080),
                new Vector2Int(0, 0),
                new Vector2Int(-1, 720)
            });

            Assert.AreEqual(1, options.Count);
        }

        [Test]
        public void Options_AreSortedAscending()
        {
            var options = ResolutionCatalogue.Build(new[]
            {
                new Vector2Int(1920, 1080),
                new Vector2Int(1280, 720),
                new Vector2Int(1920, 1200)
            });

            Assert.AreEqual(new Vector2Int(1280, 720), options[0]);
            Assert.AreEqual(new Vector2Int(1920, 1080), options[1]);
            Assert.AreEqual(new Vector2Int(1920, 1200), options[2]);
        }

        [Test]
        public void NearestIndex_PrefersAnExactMatch()
        {
            var options = ResolutionCatalogue.Build(new[]
            {
                new Vector2Int(1280, 720),
                new Vector2Int(1920, 1080)
            });

            Assert.AreEqual(1, ResolutionCatalogue.NearestIndex(options, new Vector2Int(1920, 1080)));
        }

        [Test]
        public void NearestIndex_FallsBackToTheClosestSize()
        {
            var options = ResolutionCatalogue.Build(new[]
            {
                new Vector2Int(1280, 720),
                new Vector2Int(1920, 1080)
            });

            var index = ResolutionCatalogue.NearestIndex(options, new Vector2Int(1900, 1060));

            Assert.AreEqual(1, index);
        }

        [Test]
        public void NearestIndex_EmptyList_IsMinusOne()
        {
            Assert.AreEqual(-1,
                ResolutionCatalogue.NearestIndex(new Vector2Int[0], new Vector2Int(1920, 1080)));
        }
    }
}
