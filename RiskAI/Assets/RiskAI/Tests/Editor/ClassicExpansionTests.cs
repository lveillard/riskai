using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ClassicExpansionTests
    {
        [SetUp] public void SetUp() => MapLayout.Configure(ScenarioMap.Classic);

        [Test]
        public void LasMarcasUsesThirtyThreeCitiesAcrossElevenGeographicCountries()
        {
            Assert.That(MapLayout.Towns.Length, Is.EqualTo(33));
            Assert.That(MapLayout.Countries.Length, Is.EqualTo(11));
            Assert.That(MapLayout.Towns.Count(t => t.Owner == 0), Is.EqualTo(2));
            Assert.That(MapLayout.Towns.Count(t => t.Owner == 1), Is.EqualTo(2));
            Assert.That(MapLayout.Towns.Single(t => t.Id == "dawn").Capital, Is.True);
            Assert.That(MapLayout.Towns.Single(t => t.Id == "red").Capital, Is.True);
            Assert.That(MapLayout.Towns.Where(t => t.Country == 10).Select(t => t.Id), Is.EquivalentTo(new[] { "isla-bruma", "isla-viento" }));
            foreach (var country in MapLayout.Countries)
            {
                int cities = MapLayout.Towns.Count(t => t.Country == country.Region);
                Assert.That(country.Reinforcement, Is.EqualTo(UnitKind.Archer));
                Assert.That(country.PerTurn, Is.EqualTo(BattleRules.CountryReinforcementPointsPerRound(cities)),
                    country.Name + " must use the shared source reinforcement formula.");
            }
        }

        [Test]
        public void ClassicPostsHaveTowerAndClaimClearance()
        {
            foreach (var towerHost in MapLayout.Towns)
            foreach (var guardHost in MapLayout.Towns)
            {
                if (towerHost.Id == guardHost.Id) continue;
                var tower = towerHost.Position + new Vector3(towerHost.Position.x < 0 ? 3.8f : -3.8f, 0, 0);
                var guard = MapLayout.Point(guardHost.Position.x, guardHost.Position.z - 4.2f);
                Assert.That(Vector3.Distance(tower, guard), Is.GreaterThan(ReforgedProfiles.CapturableTower.Range), towerHost.Id + " / " + guardHost.Id);
            }
        }
    }
}
