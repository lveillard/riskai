using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ImportedPortLayoutTests
    {
        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void LegacyShoulderAdapterPreservesSourceAnchorsWhenThatVariantIsRequested(ScenarioMap scenario)
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

        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void ImportedCatalogSelectsIntegratedBuildingsExplicitly(ScenarioMap scenario)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(scenario);
                Assert.That(MapLayout.Towns,Is.Not.Empty);
                foreach(var city in MapLayout.Towns)
                    Assert.That(city.Variant,Is.EqualTo(city.IsPort?BuildingVariant.IntegratedHarbor:BuildingVariant.IntegratedTown),city.Id);
            }
            finally { MapLayout.Configure(previous); }
        }

        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void SourceCenteredIntegratedTowerCoversOnlyItsOwnOpeningPost(ScenarioMap scenario)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(scenario);
                foreach(var city in MapLayout.Towns)
                {
                    float own=FlatDistance(city.Position,city.ClaimPoint);
                    Assert.That(own,Is.GreaterThan(3f),city.Id+" guard stays outside the integrated building footprint");
                    Assert.That(own,Is.LessThan(ReforgedProfiles.CapturableTower.Range),city.Id+" tower covers its own source guard");
                    foreach(var other in MapLayout.Towns)
                    {
                        if(other.Id==city.Id)continue;
                        Assert.That(FlatDistance(city.Position,other.ClaimPoint),Is.GreaterThan(ReforgedProfiles.CapturableTower.Range),
                            city.Id+" centered tower must not acquire "+other.Id+" at match start");
                    }
                }
            }
            finally { MapLayout.Configure(previous); }
        }

        static float FlatDistance(Vector3 a,Vector3 b)
        {
            a.y=b.y=0;return Vector3.Distance(a,b);
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
