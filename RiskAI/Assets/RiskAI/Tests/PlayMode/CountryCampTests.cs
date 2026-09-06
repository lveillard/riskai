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
            var own=new System.Collections.Generic.List<Vector3>();var other=new System.Collections.Generic.List<Vector3>();
            typeof(CountryCamp).GetMethod("CollectTerritoryPoints",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(camp,new object[]{own,other});
            Assert.That(own.Any(point=>Vector3.Distance(point,memberPort.Landing)<.01f),Is.True,
                "The camp overlay must use its member port as a real territory seed.");
            yield return null;
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
