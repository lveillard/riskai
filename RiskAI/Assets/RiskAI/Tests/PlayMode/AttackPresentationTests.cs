using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class AttackPresentationTests
    {
        Scene scene, previous;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.VictoryMode previousMode;
        BattleSession.StartLayout previousLayout;
        float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap=BattleSession.MapForNewMatch;
            previousMode=BattleSession.ModeForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch;
            previousTimeScale=Time.timeScale;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;
            BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            previous=SceneManager.GetActiveScene();
            scene=SceneManager.CreateScene("Attack presentation timing");
            SceneManager.SetActiveScene(scene);
            new GameObject("Attack presentation bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            var controller=Object.FindFirstObjectByType<RtsController>();if(controller)controller.enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwordsmanContactPoseCoincidesWithItsDamageEvent()
        {
            foreach(var tower in battle.Towers)if(tower)tower.enabled=false;
            foreach(var unit in battle.Units.ToArray())if(unit)unit.gameObject.SetActive(false);
            Vector3 origin=battle.Towns[0].Rally;
            Assert.That(NavMesh.SamplePosition(origin,out var attackerPoint,1,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(attackerPoint.position+Vector3.right*.75f,out var targetPoint,.5f,NavMesh.AllAreas),Is.True);
            var attacker=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,attackerPoint.position);
            var target=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,targetPoint.position);
            Assert.That(attacker.Agent.Warp(attackerPoint.position),Is.True);
            Assert.That(target.Agent.Warp(targetPoint.position),Is.True);
            target.enabled=false;target.Agent.enabled=false;
            float healthBefore=target.Health;
            attacker.Attack(target);

            float deadline=Time.realtimeSinceStartup+2;
            while(attacker.AttackPresentationProgress<0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(attacker.AttackPresentationProgress,Is.GreaterThanOrEqualTo(0));
            while(target.Health==healthBefore&&Time.realtimeSinceStartup<deadline)
            {
                if(attacker.AttackPresentationProgress>=0&&
                   attacker.AttackPresentationProgress<AttackPresentationTiming.ContactNormalizedTime(UnitKind.Footman))
                    Assert.That(target.Health,Is.EqualTo(healthBefore),"Health must remain unchanged during the visible windup.");
                yield return null;
            }

            Assert.That(target.Health,Is.LessThan(healthBefore));
            Assert.That(attacker.AttackPresentationProgress,
                Is.EqualTo(AttackPresentationTiming.ContactNormalizedTime(UnitKind.Footman)).Within(.001f),
                "The damage tick must hold the presentation at the measured contact keyframe.");
            var animation=attacker.GetComponentInChildren<Animation>();
            Assert.That(animation,Is.Not.Null);
            var state=animation["1H_Melee_Attack_Slice_Horizontal"];
            Assert.That(state,Is.Not.Null);
            Assert.That(state.normalizedTime,
                Is.EqualTo(attacker.AttackPresentationProgress).Within(.001f),
                "The rendered clip must be sampled at the pose belonging to the simulation strike.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=previousTimeScale;
            BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.ModeForNewMatch=previousMode;
            BattleSession.MapForNewMatch=previousMap;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
