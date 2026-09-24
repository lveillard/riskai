using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class AuthoredLayoutTests
    {
        const float EuropeCitiesPerThousandLandUnits = .815f;
        const float SamplingStep = 2f;
        // Las Marcas v0.34 rules: 33 cities, 11 countries, 2-5 cities each (one five-city
        // region: the plateau with its shore and island).
        const int ClassicMinimumCitiesPerCountry = 2, ClassicMaximumCitiesPerCountry = 5, ClassicFiveCityCountries = 1;
        const float ClassicPostSpacing = 22f, ClassicIslandPierSpacing = 18f;
        // Cuatro Riberas v0.34 rules (the owner chose 11 countries; one five-city country is
        // allowed on its largest landmass). Island cities sit far enough from their piers for
        // the plain 22-unit floor, but the island rule is kept explicit per map.
        const int RiverlandsMinimumCitiesPerCountry = 2, RiverlandsMaximumCitiesPerCountry = 5, RiverlandsFiveCityCountries = 1;
        const float RiverlandsPostSpacing = 22f, RiverlandsIslandPierSpacing = 18f;
        // Europe's closest authored pair is 19.2 world units apart.  This is a
        // layout-density check, separate from the live tower-to-guard range test.

        [TestCase(ScenarioMap.Classic, 33, 11)]
        [TestCase(ScenarioMap.Riverlands, 44, 11)]
        public void AuthoredMapsMatchImportedDensityWhileKeepingSafeGeographicPosts(ScenarioMap map, int cities, int countries)
        {
            MapLayout.Configure(map);
            Assert.That(MapLayout.Towns.Length, Is.EqualTo(cities));
            Assert.That(MapLayout.Countries.Length, Is.EqualTo(countries));
            Assert.That(MapLayout.Pads.Length, Is.EqualTo(cities));
            Assert.That(MapLayout.Towns.Select(t => t.Id).Distinct().Count(), Is.EqualTo(cities));
            Assert.That(MapLayout.Pads.Select(p => p.x + ":" + p.y).Distinct().Count(), Is.EqualTo(cities), "No copied grid positions.");

            foreach (var city in MapLayout.Towns)
            {
                Assert.That(MapLayout.IsLand(city.Position.x, city.Position.z), Is.True, city.Id + " must remain above water.");
                Assert.That(Mathf.Abs(MapLayout.Height(city.Position.x + MapLayout.Spacing, city.Position.z) - city.Position.y), Is.LessThan(.8f), city.Id + " east pad edge.");
                Assert.That(Mathf.Abs(MapLayout.Height(city.Position.x, city.Position.z + MapLayout.Spacing) - city.Position.y), Is.LessThan(.8f), city.Id + " north pad edge.");
                if(city.Position.z<=MapLayout.Coast(city.Position.x))foreach(var direction in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down})
                {
                    var approach=new Vector2(city.Position.x,city.Position.z)+direction*4;
                    Assert.That(MapLayout.IsLand(approach.x,approach.y),Is.True,city.Id+" must retain a four-metre land approach.");
                    Assert.That(Mathf.Abs(MapLayout.Height(approach.x,approach.y)-city.Position.y),Is.LessThan(1.15f),city.Id+" approach must remain traversable.");
                }
                if (map == ScenarioMap.Riverlands)
                    Assert.That(TerrainHydrology.IsChannel(city.Position.x, city.Position.z), Is.False, city.Id + " must not occupy the river channel.");
            }

            foreach (var first in MapLayout.Towns)
            foreach (var second in MapLayout.Towns)
            {
                if (string.CompareOrdinal(first.Id, second.Id) >= 0) continue;
                Assert.That(Vector3.Distance(first.Position, second.Position), Is.GreaterThanOrEqualTo(19.2f - .0001f), first.Id + " / " + second.Id + " are too close for separate posts.");
            }

            var groupSizes = MapLayout.Towns.GroupBy(t => t.Country).Select(group => group.Count()).ToArray();
            Assert.That(groupSizes.Length, Is.EqualTo(countries));
            // Balance rules are chosen per map (no shared constants).
            if (map == ScenarioMap.Classic)
            {
                Assert.That(groupSizes.Min(), Is.GreaterThanOrEqualTo(ClassicMinimumCitiesPerCountry));
                Assert.That(groupSizes.Max(), Is.LessThanOrEqualTo(ClassicMaximumCitiesPerCountry));
                Assert.That(groupSizes.Count(n => n == ClassicMaximumCitiesPerCountry), Is.LessThanOrEqualTo(ClassicFiveCityCountries));
            }
            else
            {
                Assert.That(groupSizes.Min(), Is.GreaterThanOrEqualTo(RiverlandsMinimumCitiesPerCountry));
                Assert.That(groupSizes.Max(), Is.LessThanOrEqualTo(RiverlandsMaximumCitiesPerCountry));
                Assert.That(groupSizes.Count(n => n == RiverlandsMaximumCitiesPerCountry), Is.LessThanOrEqualTo(RiverlandsFiveCityCountries));
            }
            foreach (var country in MapLayout.Countries)
            {
                int cityCount = MapLayout.Towns.Count(t => t.Country == country.Region);
                Assert.That(country.PerTurn, Is.EqualTo(BattleRules.CountryReinforcementPointsPerRound(cityCount)),
                    country.Name + " must use the common source reinforcement formula.");
            }

            float density = cities / SampleLandArea() * 1000f;
            Assert.That(density, Is.InRange(EuropeCitiesPerThousandLandUnits * .85f, EuropeCitiesPerThousandLandUnits * 1.2f), "Authored city density should remain near Europe at the shared world scale.");
        }

        [Test]
        public void ClassicSouthernSettlementsDoNotRepeatMechanicalLatitudeRows()
        {
            MapLayout.Configure(ScenarioMap.Classic);
            var southern=MapLayout.Towns.Where(t=>t.Position.z/MapLayout.Spacing<-45).ToArray();
            Assert.That(southern.Length,Is.GreaterThanOrEqualTo(12));
            Assert.That(southern.GroupBy(t=>Mathf.RoundToInt(t.Position.z/MapLayout.Spacing)).Max(group=>group.Count()),Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void RiverlandsHasTwinOuterCordillerasAndAuthoredCoastsHaveVariableShelves()
        {
            foreach(var map in new[]{ScenarioMap.Classic,ScenarioMap.Riverlands})
            {
                MapLayout.Configure(map);
                var depths=Enumerable.Range(-8,17).Select(i=>
                {
                    float x=i*MapLayout.HalfWidth/10f;
                    return MapLayout.SeaFloor(x,MapLayout.Coast(x)+9);
                }).ToArray();
                Assert.That(depths.Max()-depths.Min(),Is.GreaterThan(.6f),map+" coast should alternate sheltered shelves and steeper headlands.");
            }

            MapLayout.Configure(ScenarioMap.Riverlands);
            float west=MapLayout.Height(-57*MapLayout.Spacing,-20*MapLayout.Spacing);
            float east=MapLayout.Height(57*MapLayout.Spacing,-20*MapLayout.Spacing);
            Assert.That(west,Is.GreaterThan(4));
            Assert.That(east,Is.GreaterThan(4));
            Assert.That(Mathf.Abs(west-east),Is.LessThan(3.2f),"Mirrored ridge relief should preserve comparable outer routes.");
        }

        // Territory contiguity moved to TerritoryFieldTests, which checks the shared land partition on every map.

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        public void AuthoredCountriesAreCompactBalancedGroups(ScenarioMap map)
        {
            MapLayout.Configure(map);
            int countries = MapLayout.Countries.Length;
            bool OnIsland(MapLayout.City town) { for (int i = 0; i < MapLayout.Islands.Length; i++) if (MapLayout.IslandDistance(town.Position.x, town.Position.z, i) >= 0) return true; return false; }
            // An island joins the shore facing it across its channel; compactness is measured on
            // each country's mainland cities (its island cities would pull the centre out to sea).
            var centroids = new Vector2[countries]; var counts = new int[countries]; var mainland = new int[countries];
            foreach (var town in MapLayout.Towns) { counts[town.Country]++; if (OnIsland(town)) continue; centroids[town.Country] += new Vector2(town.Position.x, town.Position.z); mainland[town.Country]++; }
            for (int c = 0; c < countries; c++)
            {
                int maximum = map == ScenarioMap.Classic ? ClassicMaximumCitiesPerCountry : RiverlandsMaximumCitiesPerCountry;
                int minimum = map == ScenarioMap.Classic ? ClassicMinimumCitiesPerCountry : RiverlandsMinimumCitiesPerCountry;
                Assert.That(counts[c], Is.InRange(minimum, maximum), MapLayout.Countries[c].Name + " city count.");
                Assert.That(mainland[c], Is.GreaterThan(0), MapLayout.Countries[c].Name + " holds mainland.");
                centroids[c] /= mainland[c];
            }
            // No mainland city sits nearer another country's centre than its own: groups do not interleave.
            foreach (var town in MapLayout.Towns)
            {
                if (OnIsland(town)) continue;
                var p = new Vector2(town.Position.x, town.Position.z);
                float own = Vector2.Distance(p, centroids[town.Country]);
                for (int c = 0; c < countries; c++)
                    if (c != town.Country) Assert.That(own, Is.LessThanOrEqualTo(Vector2.Distance(p, centroids[c]) * 1.05f),
                        $"{town.Id} lies nearer {MapLayout.Countries[c].Name} than its own {MapLayout.Countries[town.Country].Name}.");
            }
        }

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        public void AuthoredCountryRewardsFollowTheirCityCount(ScenarioMap map)
        {
            // Rewards scale with the regrouped size: complete-country income is +1 gold per city,
            // reinforcement credits ceil(cities/2) per round, capped at 5 points per city.
            MapLayout.Configure(map);
            for (int c = 0; c < MapLayout.Countries.Length; c++)
            {
                int cities = MapLayout.Towns.Count(t => t.Country == c);
                Assert.That(MapLayout.Countries[c].PerTurn, Is.EqualTo((cities + 1) / 2).And.EqualTo(BattleRules.CountryReinforcementPointsPerRound(cities)), MapLayout.Countries[c].Name + " credits.");
                var economy = new Economy();
                foreach (var town in MapLayout.Towns) economy.Towns.Add(new TownState(town.Id, town.Country == c ? 0 : -1, town.Region, town.Country));
                var breakdown = new Dictionary<int, int>();
                economy.IncomeBreakdown(0, breakdown, out int basic);
                Assert.That(breakdown.TryGetValue(c, out int income) ? income : 0, Is.EqualTo(cities * BattleRules.TownIncome), MapLayout.Countries[c].Name + " income.");
                Assert.That(BattleRules.CountryReinforcementPointCapPerCity * cities, Is.EqualTo(5 * cities), MapLayout.Countries[c].Name + " point cap.");
            }
        }

        // Two claim circles (radius 6), building footprints and the harbour piers between them:
        // any two of cities and harbours stand 22 world units apart; only an island city and
        // the pier on its own island may sit as close as 18 (the channel-facing pair).
        // The closest source pair (Croatia/Bosnia, Greece, Montenegro) stands 19.2 units apart.
        const float ImportedSettlementSpacing = 19f;

        readonly struct Post
        {
            public readonly string Id; public readonly Vector2 Point; public readonly int Island;
            public Post(string id, Vector2 point, int island) { Id = id; Point = point; Island = island; }
        }

        static int IslandIndexOf(Vector3 position)
        {
            for (int i = 0; i < MapLayout.Islands.Length; i++) if (MapLayout.IslandDistance(position.x, position.z, i) >= 0) return i;
            return -1;
        }

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void SettlementsAndHarboursKeepAMinimumSpacing(ScenarioMap map)
        {
            MapLayout.Configure(map);
            var sites = MapLayout.Towns.Select(t => new Post(t.Id, new Vector2(t.Position.x, t.Position.z), IslandIndexOf(t.Position))).ToList();
            if (!MapLayout.IsImported)
            {
                for (int i = 0; i < MapLayout.MainlandHarborX.Length; i++) { var h = MapLayout.MainlandHarborLanding(i); sites.Add(new Post("harbor-" + i, new Vector2(h.x, h.z), -1)); }
                for (int i = 0; i < MapLayout.Islands.Length; i++) { var h = MapLayout.IslandHarborLanding(i); sites.Add(new Post("island-harbor-" + i, new Vector2(h.x, h.z), i)); }
            }
            float minimum = map == ScenarioMap.Classic ? ClassicPostSpacing : map == ScenarioMap.Riverlands ? RiverlandsPostSpacing : ImportedSettlementSpacing;
            float islandPier = map == ScenarioMap.Classic ? ClassicIslandPierSpacing : RiverlandsIslandPierSpacing;
            for (int i = 0; i < sites.Count; i++) for (int j = i + 1; j < sites.Count; j++)
            {
                // The single exception: an island city and the pier on its own island.
                bool ownPier = sites[i].Island >= 0 && sites[i].Island == sites[j].Island &&
                    (sites[i].Id.StartsWith("island-harbor") != sites[j].Id.StartsWith("island-harbor"));
                float limit = ownPier ? islandPier : minimum;
                Assert.That(Vector2.Distance(sites[i].Point, sites[j].Point), Is.GreaterThanOrEqualTo(limit), sites[i].Id + " / " + sites[j].Id);
            }
        }

        static float SampleLandArea()
        {
            int count = 0;
            for (float x = -MapLayout.HalfWidth; x <= MapLayout.HalfWidth; x += SamplingStep)
            for (float z = -MapLayout.HalfDepth; z <= MapLayout.HalfDepth; z += SamplingStep)
                if (MapLayout.IsLand(x, z)) count++;
            return count * SamplingStep * SamplingStep;
        }
    }
}
