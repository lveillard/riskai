using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class CanopyOcclusionTests
    {
        [Test]
        public void FoliageInFrontOfUnitRevealsBarButFoliageBehindItDoesNot()
        {
            var index = new CanopyOcclusion();
            index.Add(new Bounds(new Vector3(0, 4, -3), new Vector3(4, 3, 4)));
            Assert.That(index.Obscures(new Vector3(0, 12, -12), Vector3.up), Is.True);
            Assert.That(index.Obscures(new Vector3(0, 12, 12), Vector3.up), Is.False);
            Assert.That(index.Obscures(new Vector3(20, 12, -12), new Vector3(20, 1, 0)), Is.False);
        }

        [Test]
        public void CrownCrossingGridBoundaryAndHighTerrainDoNotLoseVisibility()
        {
            var index = new CanopyOcclusion();
            index.Add(new Bounds(new Vector3(16, 14, -3), new Vector3(4, 3, 4)));
            Assert.That(index.Obscures(new Vector3(15.9f, 22, -12), new Vector3(15.9f, 11, 0)), Is.True);
            Assert.That(index.Obscures(new Vector3(15.9f, 32, -12), new Vector3(15.9f, 20, 0)), Is.False);
        }

        [Test]
        public void MovementFieldNeedsDenseCrownsAndBuildClearsItsPreviousCoverage()
        {
            var index = new CanopyOcclusion();
            var crown = new Bounds(new Vector3(2, 4, 2), new Vector3(6, 4, 6));
            var cell = index.MovementCellAt(new Vector3(2, 0, 2));
            index.Add(crown);
            Assert.That(index.MovementMultiplier(cell), Is.EqualTo(1), "An isolated decorative tree must not create forest drag.");
            index.Add(crown);
            Assert.That(index.MovementMultiplier(cell), Is.EqualTo(CanopyOcclusion.ForestSpeedMultiplier));
            Assert.That(index.MovementMultiplier(index.MovementCellAt(new Vector3(10, 0, 2))), Is.EqualTo(1));

            var empty = new GameObject("Empty canopy rebuild");
            index.Build(empty.transform);
            Object.DestroyImmediate(empty);
            Assert.That(index.MovementMultiplier(cell), Is.EqualTo(1), "A rebuilt field must not retain a previous map's forest cells.");
        }
    }
}
