using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class CityClaimTests
    {
        Scene previous, scene;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.SeedForNewMatch = 6137;
            scene = SceneManager.CreateScene("City claim");
            SceneManager.SetActiveScene(scene);
            new GameObject("City claim bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartingGarrisonsUseExistingUnitsOncePerTown()
        {
            Assert.That(battle.Units.Count, Is.EqualTo(48));
            Assert.That(battle.Towns.All(town => town.Defender), Is.True, string.Join(", ",battle.Towns.Where(t=>!t.Defender).Select(t=>t.DisplayName+" @ "+t.ClaimPoint)));
            Assert.That(battle.Towns.Select(town => town.Defender).Distinct().Count(), Is.EqualTo(battle.Towns.Count));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnitsOutsideClaimSquareDoNotChangeOwner()
        {
            var town = battle.Towns.First(t => t.State.Owner < 0);
            var attacker = battle.Units.First(unit => unit && unit.Team == 0 && unit != town.Defender);
            Move(attacker, town.ClaimPoint + Vector3.right * 5f);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(-1));
            Assert.That(town.ClaimZone.Defender, Is.Not.SameAs(attacker));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EntryClaimsInstantlyAndPreservesUnitIdentityAndHealth()
        {
            var town = battle.Towns.First(t => t.State.Owner < 0);
            var neutralDefender = town.Defender;
            neutralDefender.TakeDamage(10000, 0);
            var attacker = battle.Units.First(unit => unit && unit.Team == 0);
            float health = attacker.Health;
            Move(attacker, town.ClaimPoint);
            yield return null;
            Assert.That(town.State.Owner, Is.EqualTo(0));
            Assert.That(town.Defender, Is.SameAs(attacker));
            Assert.That(attacker.Health, Is.EqualTo(health));
        }

        [UnityTest]
        public IEnumerator LivingDefenderBlocksEnemyUntilItDies()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var defender = town.Defender;
            var attacker = battle.Units.First(unit => unit && unit.Team == 1 && unit != defender);
            Move(attacker, town.ClaimPoint);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(0));
            Assert.That(town.ClaimZone.Contested, Is.True);
            defender.TakeDamage(10000, 1);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AlliedUnitOutsideSquareCannotBlockEnemyClaim()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var defender = town.Defender;
            var ally = battle.Units.First(unit => unit && unit.Team == 0 && unit != defender);
            var attacker = battle.Units.First(unit => unit && unit.Team == 1 && unit != defender);
            defender.TakeDamage(10000, 1);
            Move(ally, town.ClaimPoint + Vector3.right * 5f);
            Move(attacker, town.ClaimPoint);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(1));
            Assert.That(town.ClaimZone.Defender, Is.SameAs(attacker));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyOutsideSquareDoesNotContestLivingDefender()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var attacker = battle.Units.First(unit => unit && unit.Team == 1 && unit != town.Defender);
            Move(attacker, town.ClaimPoint + Vector3.right * 5f);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(0));
            Assert.That(town.ClaimZone.Contested, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TowersDoNotCountAsClaimOccupants()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var defender = town.Defender;
            var ally = battle.Units.First(unit => unit && unit.Team == 0 && unit != defender);
            var attacker = battle.Units.First(unit => unit && unit.Team == 1 && unit != defender);
            town.Defense.CompleteBuild();
            defender.TakeDamage(10000, 1);
            Move(ally, town.ClaimPoint + Vector3.right * 5f);
            Move(attacker, town.ClaimPoint);
            yield return null;
            Assert.That(town.State.Owner, Is.EqualTo(1));
            Assert.That(town.Defender, Is.SameAs(attacker));
            Assert.That(town.Defense.Team, Is.EqualTo(0));
        }

        static void Move(Soldier unit, Vector3 point)
        {
            Assert.That(unit && unit.Agent, Is.True);
            Assert.That(NavMesh.SamplePosition(point, out var hit, .9f, NavMesh.AllAreas), Is.True);
            unit.Agent.Warp(hit.position);
            unit.Stop();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
