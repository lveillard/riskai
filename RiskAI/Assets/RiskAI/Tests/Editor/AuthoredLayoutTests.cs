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
        // Europe’s closest authored pair is 19.2 world units apart.  This is a
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
            Assert.That(groupSizes.Min(), Is.GreaterThanOrEqualTo(2));
            Assert.That(groupSizes.Max(), Is.LessThanOrEqualTo(6));
            Assert.That(groupSizes.Distinct().Count(), Is.GreaterThan(2), "Geography determines country size; it is not a repeated town grid.");
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
