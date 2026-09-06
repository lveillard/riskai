using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            SeaNavigation.Prepare();
            int gridBuilds=SeaNavigation.GridBuildCount;
            Assert.That(SeaNavigation.GridComponentCount,Is.GreaterThan(0));
            SeaNavigation.Prepare();
            Assert.That(SeaNavigation.GridBuildCount,Is.EqualTo(gridBuilds),"Repeated map setup must reuse the static clearance graph.");
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
            Assert.That(SeaNavigation.TryBuildPath(berths[0],berths[0],out var direct),Is.True);
            Assert.That(direct.Count,Is.EqualTo(1));
            Assert.That(direct[0],Is.EqualTo(berths[0]));
            Assert.That(SeaNavigation.LastSearchUsedDirectSegment,Is.True);
            Assert.That(SeaNavigation.LastSearchExpanded,Is.Zero,"A clear direct segment must not enter A*.");

            bool routeFound=false;Vector3 routeFrom=default,routeTo=default;
            for(int distanceRank=0;distanceRank<12&&!routeFound;distanceRank++)
            {
                var pairs=Enumerable.Range(0,berths.Count).SelectMany(a=>Enumerable.Range(a+1,berths.Count-a-1)
                    .Select(b=>new {distance=Vector3.SqrMagnitude(berths[a]-berths[b]),a,b}));
                foreach(var pair in pairs.OrderBy(pair=>pair.distance).Skip(distanceRank).Take(1))
                {
                    if(!SeaNavigation.TryBuildPath(berths[pair.a],berths[pair.b],out var route))continue;
                    Vector3 previous=berths[pair.a];
                    foreach(var point in route){Assert.That(SeaNavigation.ClearSegment(previous,point),Is.True);previous=point;}
                    routeFound=true;routeFrom=berths[pair.a];routeTo=berths[pair.b];
                }
            }
            Assert.That(routeFound, Is.True, "At least one pair of imported port berths must have a traversable coastal route.");
            int buildsBeforeRepeat=SeaNavigation.GridBuildCount;
            Assert.That(SeaNavigation.TryBuildPath(routeFrom,routeTo,out var repeated),Is.True);
            Assert.That(SeaNavigation.TryBuildPath(routeFrom,routeTo,out var repeatedAgain),Is.True);
            Assert.That(SeaNavigation.GridBuildCount,Is.EqualTo(buildsBeforeRepeat),"Repeated fleet paths must not rescan water clearance or edges.");
            if(repeated!=null)foreach(var point in repeated)Assert.That(SeaNavigation.HasClearance(point),Is.True);
            if(repeatedAgain!=null)foreach(var point in repeatedAgain)Assert.That(SeaNavigation.HasClearance(point),Is.True);
        }

        [Test]
        public void SameSnappedOceanCellCanStillNeedACoastalDetour()
        {
            const int size=65;int count=size*size;
            var map=new ImportedMapData
            {
                mapId="unit-test",width=size,height=size,originX=-8,originZ=-8,cellSize=.25f,
                heightSamples=new float[count],waterSamples=new float[count],landSamples=new int[count],tileSamples=new int[count],
                cities=System.Array.Empty<ImportedMapData.City>(),countries=new[]{new ImportedMapData.Country{name="test",count=1}}
            };
            for(int i=0;i<count;i++){map.heightSamples[i]=-1;map.waterSamples[i]=-.24f;}
            // The raised source vertex blocks the direct diagonal at (.5,.5),
            // while both orthogonal hull paths via the snapped cell remain sea.
            map.heightSamples[34*size+34]=1;
            var imported=typeof(MapLayout).GetField("<Imported>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(imported,Is.Not.Null,"The test must restore MapLayout's imported map reference.");
            object previous=imported.GetValue(null);
            try
            {
                imported.SetValue(null,map);
                Vector3 center=new Vector3(0,-.24f,0),from=new Vector3(1,-.24f,0),to=new Vector3(0,-.24f,1);
                Assert.That(SeaNavigation.HasClearance(from),Is.True);
                Assert.That(SeaNavigation.HasClearance(to),Is.True);
                Assert.That(SeaNavigation.ClearSegment(from,center),Is.True);
                Assert.That(SeaNavigation.ClearSegment(center,to),Is.True);
                Assert.That(SeaNavigation.ClearSegment(from,to),Is.False);

                Assert.That(SeaNavigation.TryBuildPath(from,to,out var route),Is.True);
                Assert.That(SeaNavigation.LastSearchUsedDirectSegment,Is.False);
                Assert.That(SeaNavigation.LastSearchExpanded,Is.EqualTo(1));
                Assert.That(route.Count,Is.EqualTo(2));
                Assert.That(SeaNavigation.ClearSegment(from,route[0]),Is.True);
                Assert.That(SeaNavigation.ClearSegment(route[0],route[1]),Is.True);
            }
            finally { imported.SetValue(null,previous); }
        }
    }
}
