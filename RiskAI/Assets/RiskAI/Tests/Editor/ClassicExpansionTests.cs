using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ClassicExpansionTests
    {
        [SetUp] public void SetUp() => MapLayout.Configure(false);

        [Test]
        public void LasMarcasAddsThreeNeutralArcherPairsWithoutMovingTheOriginalMap()
        {
            Assert.That(MapLayout.Towns.Length, Is.EqualTo(18));
            Assert.That(MapLayout.Countries.Length, Is.EqualTo(9));
            Assert.That(MapLayout.Towns.Count(t => t.Owner == 0), Is.EqualTo(2));
            Assert.That(MapLayout.Towns.Count(t => t.Owner == 1), Is.EqualTo(2));
            Assert.That(MapLayout.Towns.Count(t => t.Owner < 0), Is.EqualTo(14));
            Assert.That(MapLayout.Towns.Single(t => t.Id == "dawn").Capital, Is.True);
            Assert.That(MapLayout.Towns.Single(t => t.Id == "dawn").Owner, Is.EqualTo(0));
            Assert.That(MapLayout.Towns.Single(t => t.Id == "red").Capital, Is.True);
            Assert.That(MapLayout.Towns.Single(t => t.Id == "red").Owner, Is.EqualTo(1));

            AssertTown("dawn", -38, -12); AssertTown("pine", -47, 12);
            AssertTown("mill", -21, 5); AssertTown("meadow", -23, 30);
            AssertTown("gate", -2, 24); AssertTown("ford", 8, 3);
            AssertTown("stone", 1, -18); AssertTown("ash", 28, 30);
            AssertTown("watch", 43, 9); AssertTown("red", 38, -25);
            AssertTown("highland", 20, -39); AssertTown("west", -24, -34);

            AssertPair(6, "dehesa", "encina", -56, -65, -56, -82);
            AssertPair(7, "secano", "trigal", -31, -65, -31, -82);
            AssertPair(8, "azafran", "olivar", -6, -65, -6, -82);
            Assert.That(MapLayout.Countries.Skip(6).All(c => c.Reinforcement == UnitKind.Archer && c.PerTurn == 1), Is.True);
        }

        [Test]
        public void ClassicSouthernPadsAreLandAndStayOutsideEveryOtherTowerCircle()
        {
            foreach (var town in MapLayout.Towns)
            {
                Assert.That(MapLayout.IsLand(town.Position.x, town.Position.z), Is.True, town.Id);
                Assert.That(Mathf.Abs(MapLayout.Height(town.Position.x + MapLayout.Spacing, town.Position.z) - town.Position.y), Is.LessThan(.8f), town.Id);
                Assert.That(Mathf.Abs(MapLayout.Height(town.Position.x, town.Position.z + MapLayout.Spacing) - town.Position.y), Is.LessThan(.8f), town.Id);
            }
            foreach (var towerHost in MapLayout.Towns)
            foreach (var guardHost in MapLayout.Towns)
            {
                if (towerHost.Id == guardHost.Id) continue;
                var tower = towerHost.Position + new Vector3(towerHost.Position.x < 0 ? 3.8f : -3.8f, 0, 0);
                var guard = MapLayout.Point(guardHost.Position.x, guardHost.Position.z - 4.2f);
                Assert.That(Vector3.Distance(tower, guard), Is.GreaterThan(ReforgedProfiles.CapturableTower.Range), towerHost.Id + " / " + guardHost.Id);
            }
        }

        static void AssertPair(int country, string first, string second, float ax, float az, float bx, float bz)
        {
            var towns = MapLayout.Towns.Where(t => t.Country == country).ToArray();
            Assert.That(towns.Length, Is.EqualTo(2));
            Assert.That(towns.All(t => t.Owner < 0 && t.Region == country - 3), Is.True);
            AssertTown(first, ax, az); AssertTown(second, bx, bz);
        }
        static void AssertTown(string id, float x, float z)
        {
            var town = MapLayout.Towns.Single(t => t.Id == id);
            Assert.That(town.Position.x, Is.EqualTo(x * MapLayout.Spacing).Within(.001f), id);
            Assert.That(town.Position.z, Is.EqualTo(z * MapLayout.Spacing).Within(.001f), id);
        }
    }
}
