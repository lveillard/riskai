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
    public sealed class SixteenPlayerCombatTests
    {
        Scene previous, scene;
        BattleSession battle;
        int savedPlayerCount;
        BattleSession.StartLayout savedLayout;
        int savedSeed;
        ScenarioMap savedMap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedPlayerCount = BattleSession.PlayerCountForNewMatch;
            savedLayout = BattleSession.LayoutForNewMatch;
            savedSeed = BattleSession.SeedForNewMatch;
            savedMap = BattleSession.MapForNewMatch;
            BattleSession.PlayerCountForNewMatch = PlayerRules.MaxPlayers;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            BattleSession.SeedForNewMatch = 16015;
            BattleSession.MapForNewMatch = ScenarioMap.Classic;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Sixteen player combat");
            SceneManager.SetActiveScene(scene);
            new GameObject("Sixteen player combat bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            foreach (var tower in battle.Towers) if (tower) tower.enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TeamFifteenCanCommandKillAndCapture()
        {
            Assert.That(battle.PlayerCount, Is.EqualTo(PlayerRules.MaxPlayers));
            var home = battle.Towns.First(town => town.State.Owner == 15);
            var neutral = battle.Towns.First(town => town.State.Owner == PlayerRules.NeutralOwner);
            var attacker = BattleTestScenario.Mobile(battle, 15, UnitKind.Footman, home.Rally);
            var victim = BattleTestScenario.Mobile(battle, 14, UnitKind.Footman, home.Rally + Vector3.right * 2);

            Assert.That(battle.Commands.Submit(new UnitCommand(15, attacker.EntityId, UnitCommandKind.Hold)), Is.True);
            battle.Commands.Tick();
            int killsBefore = battle.Kills[15];
            int goldBefore = battle.Economy.Gold[15];
            victim.TakeDamage(10000, 15, attacker);
            Assert.That(battle.Kills[15], Is.EqualTo(killsBefore + 1));
            Assert.That(battle.Economy.Gold[15], Is.EqualTo(goldBefore), "One footman grants a quarter-gold, retained until a whole coin is earned.");
            for(int i=1;i<BattleRules.BountyDivisor;i++)
            {
                var next=BattleTestScenario.Mobile(battle,14,UnitKind.Footman,home.Rally+Vector3.right*2);
                next.TakeDamage(10000,15,attacker);
            }
            Assert.That(battle.Kills[15],Is.EqualTo(killsBefore+BattleRules.BountyDivisor));
            Assert.That(battle.Economy.Gold[15],Is.EqualTo(goldBefore+1));

            neutral.Defender.TakeDamage(10000, 15, attacker);
            Assert.That(NavMesh.SamplePosition(neutral.ClaimPoint, out var point, .9f, NavMesh.AllAreas), Is.True);
            Assert.That(attacker.Agent.Warp(point.position), Is.True);
            attacker.Stop();
            Assert.That(neutral.ClaimZone.Step(battle.Units, PlayerRules.NeutralOwner), Is.EqualTo(15));
            Assert.That(neutral.ClaimZone.Defender, Is.SameAs(attacker));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.PlayerCountForNewMatch = savedPlayerCount;
            BattleSession.LayoutForNewMatch = savedLayout;
            BattleSession.SeedForNewMatch = savedSeed;
            BattleSession.MapForNewMatch = savedMap;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
