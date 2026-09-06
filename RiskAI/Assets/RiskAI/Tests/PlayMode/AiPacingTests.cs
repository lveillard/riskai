using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class AiPacingTests
    {
        Scene previous, scene;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
            BattleSession.SeedForNewMatch = 9182;
            scene = SceneManager.CreateScene("AI pacing");
            SceneManager.SetActiveScene(scene);
            new GameObject("AI pacing bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator RelaxedDelaysRecruitmentAndKeepsStartingResourcesAndRosterEqual()
        {
            battle.AiEnabled = true;
            int startingGold = battle.Economy.Gold[1];
            var garrisonPositions = battle.Towns.Where(t => t.State.Owner == 1 && t.Defender).Select(t => new { Unit = t.Defender, Position = t.Defender.transform.position }).ToArray();
            Assert.That(battle.Difficulty, Is.EqualTo(BattleSession.AiDifficulty.Relaxed));
            Assert.That(battle.Economy.Gold[0], Is.EqualTo(battle.Economy.Gold[1]));
            Assert.That(battle.Population(0), Is.EqualTo(battle.Population(1)));
            Assert.That(battle.Units.Count, Is.EqualTo(48));
            Assert.That(NavalWorld.Current.Ships.Count(s => s && s.Team == 0), Is.EqualTo(NavalWorld.Current.Ships.Count(s => s && s.Team == 1)));

            Time.timeScale = 10;
            yield return ReachBattleTime(29);
            Assert.That(battle.Economy.Gold[1], Is.EqualTo(startingGold));
            Assert.That(battle.Towns.All(t => t.QueueCount == 0), Is.True);
            Assert.That(battle.Units.Where(u => u && u.Team == 1 && u.IsAlive).All(u => !u.CurrentTarget && (!u.Agent || !u.Agent.hasPath)), Is.True);
            Assert.That(garrisonPositions.All(item => item.Unit && Vector3.Distance(item.Unit.transform.position, item.Position) < .05f), Is.True);
        }

        [UnityTest]
        public IEnumerator RelaxedFirstTickQueuesAtMostOneTown()
        {
            battle.AiEnabled = true;
            Time.timeScale = 10;
            int startingPopulation = battle.Population(1);
            int startingGold = battle.Economy.Gold[1];
            yield return ReachBattleTime(30.2f); // Just after the first 30-second AI tick.
            int queued = battle.Towns.Sum(t => t.QueueCount);
            int trained = battle.Population(1) - startingPopulation;
            // The extracted v0.12 unit costs are raw gold values. The first relaxed
            // choice is a Guard (cost 5), while the scenario starts with 4 gold, so
            // this tick legitimately leaves both the balance and roster unchanged.
            Assert.That(startingGold - battle.Economy.Gold[1], Is.EqualTo(0));
            Assert.That(queued + trained, Is.EqualTo(0));
            Assert.That(battle.Towns.Count(t => t.QueueCount > 0), Is.LessThanOrEqualTo(1));
        }

        IEnumerator ReachBattleTime(float target)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (battle.BattleTime < target && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.BattleTime, Is.GreaterThanOrEqualTo(target));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
