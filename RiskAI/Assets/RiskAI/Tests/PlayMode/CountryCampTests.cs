using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class CountryCampTests
    {
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap=BattleSession.MapForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;
            previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Country camp API");SceneManager.SetActiveScene(scene);
            new GameObject("Country camp bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            var controller=Object.FindFirstObjectByType<RtsController>();if(controller)controller.enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReinforcementHoldsAtCampUntilTheCampHasAnExplicitRally()
        {
            var camp=battle.Camps[0];
            Assert.That(camp,Is.Not.Null);
            Assert.That(battle.RecruitmentPopulation(0),Is.Zero,"Starting guards cannot consume the shared mobile cap on authored maps.");
            var unit=battle.Spawn(0,UnitKind.Archer,camp.SpawnPoint);
            Assert.That(unit,Is.Not.Null);

            camp.ApplyRally(unit);
            Assert.That(camp.HasRally,Is.False);
            Assert.That(camp.RallyPoint,Is.EqualTo(camp.SpawnPoint));
            Assert.That(unit.Agent.hasPath,Is.False,"The default camp must retain its reinforcement.");

            var destination=battle.Towns.First(t=>t.State.Owner==0).Rally;
            Assert.That(camp.SetRally(destination),Is.True);
            camp.ApplyRally(unit);
            Assert.That(camp.HasRally,Is.True);
            Assert.That(Vector3.Distance(unit.Agent.destination,camp.RallyPoint),Is.LessThan(.05f));

            camp.ClearRally();camp.ApplyRally(unit);
            Assert.That(camp.HasRally,Is.False);
            Assert.That(unit.Agent.hasPath,Is.False);

            var memberPort=battle.Naval.Harbors.First(h=>h.LinkedTown&&h.LinkedTown.State.Country==camp.Country);
            var atlas=StrategicMapView.Current.Atlas;
            Assert.That(atlas.Sites.Any(site=>site.Port==memberPort&&site.Country==camp.Country&&Vector2.Distance(site.Point,new Vector2(memberPort.Landing.x,memberPort.Landing.z))<.01f),Is.True,
                "The shared surface must include every country's member port.");
            Assert.That(atlas.Field,Is.SameAs(TerritoryField.Current),"Atlas, camp inspection, border posts and minimap read one territory field.");
            foreach(var town in battle.Towns)
                Assert.That(TerritoryMarkers.CountryAt(town.transform.position),Is.EqualTo(town.State.Country),town.DisplayName+" lies inside its own camp territory.");
            for(int c=0;c<MapLayout.Countries.Length;c++)
            {
                var anchor=atlas.LabelAnchors[c];
                Assert.That(float.IsNaN(anchor.x),Is.False,MapLayout.Countries[c].Name+" has a label anchor.");
                Assert.That(TerritoryField.Current.CountryAt(anchor.x,anchor.y),Is.EqualTo(c),MapLayout.Countries[c].Name+" label sits inside its country.");
                Assert.That(atlas.LabelRadii[c],Is.GreaterThan(1f),MapLayout.Countries[c].Name+" label anchor keeps room from its borders.");
            }
            camp.Select(true);
            Assert.That(StrategicMapView.Current.SelectedCountry,Is.EqualTo(camp.Country));
            var guard=battle.Towns[0].Defender;
            StrategicMapView.Current.SetStrategic(true);
            Assert.That(guard.gameObject.activeInHierarchy,Is.True,"Overview must not suspend simulation actors.");
            Assert.That(Camera.main.cullingMask,Is.EqualTo(1<<StrategicMapView.StrategicLayer));
            Assert.That(StrategicMapView.Current.SurfaceCount,Is.GreaterThan(0));
            StrategicMapView.Current.SetStrategic(false);
            Assert.That(Camera.main.cullingMask&(1<<MapLayout.TerrainLayer),Is.Not.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CampsSitCentredAmongTheirCitiesInsideTheirTerritory()
        {
            AssertCampsCentred(battle);
            yield return null;
        }

        /// <summary>
        /// Authored camps stand inside their own territory, clear of city buildings and claim
        /// circles, and near their members' centroid (unless the centroid is sea or foreign land,
        /// as for a country split by a channel), with a NavMesh path to a member city.
        /// </summary>
        internal static void AssertCampsCentred(BattleSession battle)
        {
            Assert.That(battle.Camps.Count(c=>c),Is.EqualTo(MapLayout.Countries.Length),"Every country has a camp.");
            var field=TerritoryField.Current;var path=new UnityEngine.AI.NavMeshPath();
            foreach(var camp in battle.Camps)
            {
                var members=MapLayout.Towns.Where(t=>t.Country==camp.Country).ToArray();
                var centroid=members.Aggregate(Vector3.zero,(sum,t)=>sum+t.Position)/members.Length;
                float spread=members.Max(t=>Vector2.Distance(new Vector2(t.Position.x,t.Position.z),new Vector2(centroid.x,centroid.z)));
                var at=camp.SpawnPoint;string name=camp.DisplayName;
                Assert.That(field.CountryAt(at.x,at.z),Is.EqualTo(camp.Country),name+" camp lies inside its territory.");
                foreach(var town in MapLayout.Towns)
                {
                    Assert.That(Vector2.Distance(new Vector2(at.x,at.z),new Vector2(town.Position.x,town.Position.z)),Is.GreaterThan(CountryCamp.CityClearance-.5f),name+" camp clears "+town.Id);
                    Assert.That(Vector2.Distance(new Vector2(at.x,at.z),new Vector2(town.ClaimPoint.x,town.ClaimPoint.z)),Is.GreaterThan(CountryCamp.ClaimClearance-.5f),name+" camp clears the claim circle of "+town.Id);
                }
                bool centreUsable=MapLayout.IsWalkable(centroid.x,centroid.z)&&field.CountryAt(centroid.x,centroid.z)==camp.Country;
                if(centreUsable)Assert.That(Vector2.Distance(new Vector2(at.x,at.z),new Vector2(centroid.x,centroid.z)),Is.LessThanOrEqualTo(Mathf.Max(10,spread*.6f)),name+" camp stays near the centre of its cities.");
                Assert.That(members.Any(t=>UnityEngine.AI.NavMesh.SamplePosition(t.Position,out var target,6,UnityEngine.AI.NavMesh.AllAreas)&&
                    UnityEngine.AI.NavMesh.CalculatePath(at,target.position,UnityEngine.AI.NavMesh.AllAreas,path)&&path.status==UnityEngine.AI.NavMeshPathStatus.PathComplete),Is.True,name+" camp has a rally path to a member city.");
            }
        }

        [UnityTest]
        public IEnumerator RecruitmentTickClearsLostCountryRallyEvenWithoutCreditsAndRetainsExistingCredit()
        {
            var camp=battle.Camps[0];
            var towns=battle.Towns.Where(t=>t.State.Country==camp.Country).ToArray();
            foreach(var town in towns)town.State.Owner=0;
            Assert.That(camp.SetRally(towns[0].Rally),Is.True);
            var recruits=new CountryRecruitment(battle);

            foreach(var town in towns)town.State.Owner=1;
            recruits.Tick(.5f);
            Assert.That(camp.HasRally,Is.False,"Owner reconciliation must run even when the country has zero pending credits.");
            Assert.That(recruits.Pending(camp.Country),Is.Zero);

            foreach(var town in towns)town.State.Owner=0;
            Assert.That(camp.SetRally(towns[0].Rally),Is.True);
            recruits.CreditRound();int pending=recruits.Pending(camp.Country);
            Assert.That(pending,Is.GreaterThan(0));
            towns[0].State.Owner=1;
            recruits.Tick(.5f);
            Assert.That(camp.HasRally,Is.False);
            Assert.That(recruits.Pending(camp.Country),Is.EqualTo(pending),"Source-style accumulated country credit survives ownership loss.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NavigationProbeSuspensionPreventsBothCreditsAndSpawnsWithoutChangingNormalRecruitment()
        {
            var country=battle.Camps[0].Country;
            foreach(var town in battle.Towns.Where(t=>t.State.Country==country))town.State.Owner=0;
            var recruits=new CountryRecruitment(battle);
            var suspension=typeof(CountryRecruitment).GetProperty("SuspendedForProbe",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(suspension,Is.Not.Null);
            Assert.That(suspension.GetValue(recruits),Is.False,"Ordinary games must keep source recruitment enabled.");
            recruits.CreditRound();
            int pending=recruits.Pending(country), count=battle.Units.Count;
            Assert.That(pending,Is.GreaterThan(0));
            suspension.SetValue(recruits,true);
            recruits.CreditRound();recruits.Tick(.5f);
            Assert.That(recruits.Pending(country),Is.EqualTo(pending));
            Assert.That(battle.Units.Count,Is.EqualTo(count));
            suspension.SetValue(recruits,false);
            recruits.Tick(.5f);
            Assert.That(recruits.Pending(country),Is.EqualTo(pending-1));
            Assert.That(battle.Units.Count,Is.GreaterThan(count));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;
            BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.PlayerCountForNewMatch=previousPlayers;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
