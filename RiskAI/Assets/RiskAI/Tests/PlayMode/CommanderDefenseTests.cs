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
    public sealed class CommanderDefenseTests
    {
        Scene scene, previous;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Commander defense");
            SceneManager.SetActiveScene(scene);
            new GameObject("Commander defense bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            foreach (var tower in battle.Towers) if (tower) tower.enabled = false;
            if (battle.Naval)
                foreach (var ship in battle.Naval.Ships.ToArray()) if (ship) ship.gameObject.SetActive(false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NearbyThreatDivertsMarchingUnitsOnceAndKeepsReserve()
        {
            battle.AiEnabled = true;
            var defended = battle.Towns.First(t => t.State.Owner == 1 && !t.IsCapital);
            var neutral = battle.Towns.First(t => t.State.Owner < 0);
            var mobileForce = BattleTestScenario.MobileArmy(battle, 1, UnitKind.Footman, 4, defended.Rally);
            var marching = mobileForce.Take(2).ToArray();
            Assert.That(marching.Length, Is.EqualTo(2));

            Vector3 far = Sample(defended.transform.position + Vector3.forward * 24);
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || unit.IsGarrison || marching.Contains(unit)) continue;
                if (unit.Team == 0 || unit.Team == 1) unit.Agent.Warp(far);
                if (unit.Team == 0) unit.enabled = false;
            }
            foreach (var unit in marching)
            {
                Assert.That(unit.Agent.Warp(Sample(defended.transform.position + Vector3.back * 15)), Is.True);
                unit.MoveTo(neutral.ClaimPoint, true, false);
            }

            var raiders = BattleTestScenario.MobileArmy(battle, 0, UnitKind.Footman, 5,
                Sample(defended.ClaimPoint + Vector3.forward * 3));
            foreach (var raider in raiders) raider.HoldPosition();
            long commandsBefore = battle.Commands.AppliedCount;

            float deadline = Time.realtimeSinceStartup + 3;
            while (battle.Commands.AppliedCount <= commandsBefore && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(battle.Commands.AppliedCount, Is.GreaterThan(commandsBefore), "The commander must submit a defensive formation command immediately.");
            foreach (var unit in marching)
                Assert.That(Vector3.Distance(unit.Agent.destination, defended.ClaimPoint), Is.LessThan(2.5f), "A marching unit should be diverted to the threatened town.");

            long commandsAfterDefense = battle.Commands.AppliedCount;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(battle.Commands.AppliedCount, Is.EqualTo(commandsAfterDefense), "The same threat must not cause repeated defensive orders every decision tick.");
            Assert.That(battle.Units.Count(u => u && u.Team == 1 && !u.IsGarrison && u.Agent && u.Agent.enabled), Is.GreaterThanOrEqualTo(2), "The commander must retain a mobile reserve.");
        }

        static Vector3 Sample(Vector3 point)
        {
            Assert.That(NavMesh.SamplePosition(point, out var hit, 12, NavMesh.AllAreas), Is.True, "Test point must be on the generated NavMesh.");
            return hit.position;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
