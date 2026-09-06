using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ImportedNavalTests
    {
        ScenarioMap previousScenario;

        [SetUp]
        public void SetUp() => previousScenario = MapLayout.Scenario;

        [TearDown]
        public void TearDown() => MapLayout.Configure(previousScenario);

        [TestCase(ScenarioMap.Europe, 44)]
        [TestCase(ScenarioMap.NewWorld, 59)]
        public void ImportedPortCitiesHaveBoundedSafeBerthsAndACoastalRoute(ScenarioMap scenario,int expectedPorts)
        {
            MapLayout.Configure(scenario);
            Assert.That(MapLayout.IsImported, Is.True);
            var ports=MapLayout.Towns.Where(city=>city.IsPort).ToArray();
            Assert.That(ports.Length, Is.EqualTo(expectedPorts));

            var berths=new List<Vector3>(ports.Length);
            foreach(var city in ports)
            {
                var outward=city.ClaimPoint-city.Position;outward.y=0;
                if(outward.sqrMagnitude<.01f)outward=Vector3.forward;else outward.Normalize();
                Assert.That(SeaNavigation.TryNearestOcean(city.ClaimPoint+outward*6f,30f,out var berth), Is.True, city.Id);
                Assert.That(SeaNavigation.HasClearance(berth), Is.True, city.Id);
                Assert.That(Vector3.Distance(berth,city.ClaimPoint), Is.GreaterThan(2.5f), city.Id);
                berths.Add(berth);
            }

            bool routeFound=false;
            for(int distanceRank=0;distanceRank<12&&!routeFound;distanceRank++)
            {
                var pairs=Enumerable.Range(0,berths.Count).SelectMany(a=>Enumerable.Range(a+1,berths.Count-a-1)
                    .Select(b=>new {distance=Vector3.SqrMagnitude(berths[a]-berths[b]),a,b}));
                foreach(var pair in pairs.OrderBy(pair=>pair.distance).Skip(distanceRank).Take(1))
                {
                    if(!SeaNavigation.TryBuildPath(berths[pair.a],berths[pair.b],out var route))continue;
                    Vector3 previous=berths[pair.a];
                    foreach(var point in route){Assert.That(SeaNavigation.ClearSegment(previous,point),Is.True);previous=point;}
                    routeFound=true;
                }
            }
            Assert.That(routeFound, Is.True, "At least one pair of imported port berths must have a traversable coastal route.");
        }
    }
}
