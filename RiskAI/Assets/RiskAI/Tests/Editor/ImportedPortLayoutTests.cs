using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ImportedPortLayoutTests
    {
        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void EveryImportedPortPreservesSourceAnchorsAndUsesLandShoulders(ScenarioMap scenario)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(scenario);
                float longestPier=0;string longestPort=null;
                foreach(var city in MapLayout.Towns)
                {
                    if(!city.IsPort)continue;
                    var anchors=ImportedPortLayout.Resolve(city.Position,city.ClaimPoint);
                    Assert.That(anchors.City,Is.EqualTo(city.Position),city.Id+" source h00O");
                    Assert.That(anchors.Claim,Is.EqualTo(city.ClaimPoint),city.Id+" source B00R");
                    Assert.That(MapLayout.IsLand(anchors.Shore.x,anchors.Shore.z),Is.True,city.Id+" shore");
                    Assert.That(MapLayout.IsLand(anchors.Building.x,anchors.Building.z),Is.True,city.Id+" building");
                    Assert.That(MapLayout.IsLand(anchors.Tower.x,anchors.Tower.z),Is.True,city.Id+" tower");
                    Assert.That(Vector3.Distance(anchors.Building,anchors.Tower),Is.GreaterThan(2f),city.Id+" shoulders");
                    float pier=Vector3.Distance(anchors.Shore,anchors.Claim);
                    if(pier>longestPier){longestPier=pier;longestPort=city.Id;}
                }
                Assert.That(longestPier,Is.LessThanOrEqualTo(14.1f),scenario+" longest pier: "+longestPort);
            }
            finally { MapLayout.Configure(previous); }
        }

        [Test]
        public void DenmarkPortKeepsTheOfficialCircleAndGetsALandConnection()
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(ScenarioMap.Europe);
                var city=System.Array.Find(MapLayout.Towns,item=>item.Id=="europe-121");
                var anchors=ImportedPortLayout.Resolve(city.Position,city.ClaimPoint);
                Assert.That(anchors.Claim.x,Is.EqualTo(-98.56f).Within(.001f));
                Assert.That(anchors.Claim.z,Is.EqualTo(43.52f).Within(.001f));
                Assert.That(anchors.Claim,Is.EqualTo(city.ClaimPoint));
                Assert.That(MapLayout.IsLand(anchors.Shore.x,anchors.Shore.z),Is.True);
                Assert.That(Vector3.Distance(anchors.Shore,anchors.Claim),Is.GreaterThan(1f));
            }
            finally { MapLayout.Configure(previous); }
        }
    }
}
