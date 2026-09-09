using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ImportedPortLayoutTests
    {
        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void EveryImportedPortUsesANearCoastLandCircleAndSeawardBuilding(ScenarioMap scenario)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(scenario);
                foreach(var city in MapLayout.Towns)
                {
                    if(!city.IsPort)continue;
                    var anchors=ImportedPortLayout.Resolve(city.Position,city.ClaimPoint);
                    Assert.That(Vector3.Distance(city.Position,anchors.Claim),Is.LessThanOrEqualTo(ImportedPortLayout.ClaimDistanceFromCoast+.01f),city.Id);
                    Assert.That(Vector3.Distance(city.Position,anchors.Claim),Is.LessThanOrEqualTo(Vector3.Distance(city.Position,city.ClaimPoint)+.001f),city.Id);
                    Assert.That(Vector3.Dot(anchors.Building-city.Position,anchors.Seaward),Is.GreaterThan(0),city.Id);
                }
            }
            finally { MapLayout.Configure(previous); }
        }

        [Test]
        public void DenmarkPortMovesItsCapturePostTowardTheCoast()
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(ScenarioMap.Europe);
                var city=System.Array.Find(MapLayout.Towns,item=>item.Id=="europe-121");
                var anchors=ImportedPortLayout.Resolve(city.Position,city.ClaimPoint);
                Assert.That(Vector3.Distance(city.Position,anchors.Claim),Is.LessThan(Vector3.Distance(city.Position,city.ClaimPoint)));
            }
            finally { MapLayout.Configure(previous); }
        }
    }
}
