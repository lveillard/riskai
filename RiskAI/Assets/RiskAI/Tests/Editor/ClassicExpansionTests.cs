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
            // v0.34 west redesign: the plateau with its shore and the island in the channel is
            // one zone; the vega below the plateau's south wall splits along its relief into
            // Marca del Alba (north-east) and Escarpa de Poniente (south-west).
            Assert.That(MembersOf(0), Is.EquivalentTo(new[] { "pine", "mill", "meadow", "cordillera-norte", "isla-bruma" }));
            Assert.That(MembersOf(1), Is.EquivalentTo(new[] { "dawn", "west" }));
            Assert.That(MembersOf(2), Is.EquivalentTo(new[] { "crest-west", "dehesa-norte", "encinar-centro" }));
            Assert.That(MembersOf(3), Is.EquivalentTo(new[] { "ford", "stone" }));
            Assert.That(MembersOf(6), Is.EquivalentTo(new[] { "dehesa", "encina", "isla-roble" }));
            Assert.That(MembersOf(10), Is.EquivalentTo(new[] { "gate", "torre-norte", "isla-viento" }));
            foreach (var country in MapLayout.Countries)
            {
                int cities = MapLayout.Towns.Count(t => t.Country == country.Region);
                Assert.That(country.Reinforcement, Is.EqualTo(UnitKind.Archer));
                Assert.That(country.PerTurn, Is.EqualTo(BattleRules.CountryReinforcementPointsPerRound(cities)),
                    country.Name + " must use the shared source reinforcement formula.");
            }
        }

        static string[] MembersOf(int country) =>
            MapLayout.Towns.Where(t => t.Country == country).Select(t => t.Id).OrderBy(id => id).ToArray();

        // Owner intent (b): the plateau cities -- pine, mill, meadow and one more on or at the
        // plateau -- form one zone. Intent (c): pine and mill stay side by side in that zone.
        [Test]
        public void PlateauCitiesShareOneZoneAndTheFourthStandsOnOrAtThePlateau()
        {
            var plateau = new[] { "pine", "mill", "meadow", "cordillera-norte" };
            int zone = MapLayout.Towns.Single(t => t.Id == "pine").Country;
            foreach (string id in plateau)
                Assert.That(MapLayout.Towns.Single(t => t.Id == id).Country, Is.EqualTo(zone), id + " belongs to the plateau zone.");
            Assert.That(MapLayout.Towns.Single(t => t.Id == "mill").Country,
                Is.EqualTo(MapLayout.Towns.Single(t => t.Id == "pine").Country), "pine and mill are two plateau cities side by side.");
            // The west plateau is cliff polygon 0: its top is positive, its foot slightly negative.
            foreach (string id in new[] { "pine", "mill", "meadow" })
            {
                var town = MapLayout.Towns.Single(t => t.Id == id);
                float at = MapLayout.CliffDistance(new Vector2(town.Position.x / MapLayout.Spacing, town.Position.z / MapLayout.Spacing), 0);
                Assert.That(at, Is.GreaterThanOrEqualTo(2f), id + " stands on the plateau top.");
            }
            var foot = MapLayout.Towns.Single(t => t.Id == "cordillera-norte");
            float d = MapLayout.CliffDistance(new Vector2(foot.Position.x / MapLayout.Spacing, foot.Position.z / MapLayout.Spacing), 0);
            Assert.That(d, Is.InRange(-8f, 2f), "cordillera-norte stands on or at the plateau (its west foot above the shore).");
        }

        // Owner intent (a): the island isla-bruma and the harbour facing it across the channel
        // (Muelle del Oeste, mainland harbour 0) share one zone -- both by land partition and by
        // the port link that the strategic atlas uses for the harbour's country.
        [Test]
        public void IslaBrumaAndTheHarbourFacingItShareOneZone()
        {
            var island = MapLayout.Towns.Single(t => t.Id == "isla-bruma");
            var landing = MapLayout.MainlandHarborLanding(0);
            Assert.That(TerritoryField.Current.CountryAt(landing.x, landing.z), Is.EqualTo(island.Country),
                "Muelle del Oeste's landing lies in isla-bruma's zone.");
            var coast = new Vector3(MapLayout.MainlandHarborX[0] * MapLayout.Spacing, 0, MapLayout.Coast(MapLayout.MainlandHarborX[0] * MapLayout.Spacing));
            var linked = MapLayout.Towns.OrderBy(t => Vector3.Distance(
                new Vector3(t.Position.x, 0, t.Position.z), coast)).First();
            Assert.That(linked.Country, Is.EqualTo(island.Country),
                "the port of Muelle del Oeste links to a town of isla-bruma's zone (" + linked.Id + ").");
            // The harbour faces the island across the channel: its landing sits under the
            // island's western lobe (island x range roughly -59..-35 pads).
            Assert.That(landing.x / MapLayout.Spacing, Is.InRange(-62f, -35f), "Muelle del Oeste sits across the channel from isla-bruma.");
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
                Assert.That(Vector3.Distance(tower, guard), Is.GreaterThan(UnitCatalog.Get(UnitKind.Tower).TownWeapon.Range), towerHost.Id + " / " + guardHost.Id);
            }
        }
    }
}
