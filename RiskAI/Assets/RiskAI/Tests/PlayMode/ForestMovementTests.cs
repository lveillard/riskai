using System.Collections;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class ForestMovementTests
    {
        Scene scene, previous;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale=1;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            MapLayout.Configure(ScenarioMap.Classic);
            previous=SceneManager.GetActiveScene();
            scene=SceneManager.CreateScene("Forest movement field");
            SceneManager.SetActiveScene(scene);
            new GameObject("Forest movement bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            foreach(var tower in battle.Towers)if(tower)tower.enabled=false;
            foreach(var guard in battle.Units)if(guard)guard.enabled=false;
            // Isolate the dynamic field from the authored visual forest so this
            // test names the two crowns that supply its density.
            var empty=new GameObject("Empty canopy source");
            battle.Canopies.Build(empty.transform);
            Object.Destroy(empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoldierRestoresBaseSpeedAfterLeavingForestAndPoolReuse()
        {
            Assert.That(NavMesh.SamplePosition(new Vector3(-30,0,-16),out var start,3,NavMesh.AllAreas),Is.True);
            var unit=BattleTestScenario.Mobile(battle,0,UnitKind.Archer,start.position);
            var crown=new Bounds(unit.transform.position+Vector3.up*3,new Vector3(6,4,6));
            battle.Canopies.Add(crown);battle.Canopies.Add(crown);
            unit.SimTick(.05f);
            Assert.That(unit.Agent.speed,Is.EqualTo(BattleRules.Speed(UnitKind.Archer)*CanopyOcclusion.ForestSpeedMultiplier).Within(.001f));

            Assert.That(NavMesh.SamplePosition(unit.transform.position+Vector3.right*16,out var open,8,NavMesh.AllAreas),Is.True);
            Assert.That(unit.Agent.Warp(open.position),Is.True);
            unit.SimTick(.05f);
            Assert.That(unit.Agent.speed,Is.EqualTo(BattleRules.Speed(UnitKind.Archer)).Within(.001f),"Leaving a forest cell must restore the profile speed.");

            var original=unit.gameObject;
            unit.TakeDamage(10000,PlayerRules.NeutralTeam);
            yield return new WaitForSecondsRealtime(1.6f);
            var reused=BattleTestScenario.Mobile(battle,0,UnitKind.Archer,open.position);
            Assert.That(reused.gameObject,Is.SameAs(original));
            Assert.That(reused.Agent.speed,Is.EqualTo(BattleRules.Speed(UnitKind.Archer)).Within(.001f),"A pooled soldier must not retain its former forest speed.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=1;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.RandomCities;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
            MapLayout.Configure(ScenarioMap.Classic);
        }
    }
}
