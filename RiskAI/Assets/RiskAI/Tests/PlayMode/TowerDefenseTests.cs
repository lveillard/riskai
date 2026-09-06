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
    public sealed class TowerDefenseTests
    {
        Scene previous, scene;
        BattleSession battle;
        NavalWorld naval;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            scene = SceneManager.CreateScene("Tower defense");
            SceneManager.SetActiveScene(scene);
            new GameObject("Tower defense bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            naval = NavalWorld.Current;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryTownAndHarborStartsWithABuiltTowerWithoutChangingArmies()
        {
            Assert.That(battle.Towns.All(town => town.Defense && town.Defense.IsAlive), Is.True);
            Assert.That(naval.Harbors.All(harbor => harbor.Defense && harbor.Defense.IsAlive), Is.True);
            Assert.That(battle.Towns.Where(town => town.State.Owner < 0).All(town => town.Defense.Team == 2), Is.True);
            Assert.That(naval.Harbors.Where(harbor => harbor.Owner < 0).All(harbor => harbor.Defense.Team == 2), Is.True);
            Assert.That(battle.Population(0), Is.EqualTo(battle.Population(1)));
            Assert.That(battle.Units.Count, Is.GreaterThan(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NeutralTowerDoesNotFireAtNeutralSoldiers()
        {
            var tower = battle.Towns.First(town => town.State.Owner < 0).Defense;
            var neutral = battle.Units.First(unit => unit && unit.Team == 2);
            Assert.That(tower.Team, Is.EqualTo(2));
            KeepOnlyTower(tower);
            Assert.That(NavMesh.SamplePosition(tower.transform.position + Vector3.forward * 6, out var hit, 10, NavMesh.AllAreas), Is.True);
            Assert.That(neutral.Agent.Warp(hit.position), Is.True);
            neutral.HoldPosition();
            foreach (var target in battle.Targets.ToArray())
                if (target != tower && target != neutral) battle.Targets.Remove(target);
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(tower.ShotsFired, Is.Zero);
            Assert.That(tower.CurrentTarget, Is.Null);
        }

        [UnityTest]
        public IEnumerator TowerRemainsInvulnerableRegisteredAndFollowsDefenderCapture()
        {
            var town = battle.Towns.First(item => item.State.Owner == 0 && item.Defender);
            var tower = town.Defense;
            var defender = town.Defender;
            var enemy = battle.Spawn(1, UnitKind.Footman, town.ClaimPoint + Vector3.forward * .4f);
            Assert.That(enemy, Is.Not.Null);
            Assert.That(tower.CanBeAttacked, Is.False);
            foreach (var other in battle.Towers.ToArray())
                if (other != tower) other.gameObject.SetActive(false);
            foreach (var unit in battle.Units.ToArray())
                if (unit != defender && unit != enemy)
                {
                    if (unit.Agent) unit.Agent.enabled = false;
                    unit.enabled = false;
                }
            enemy.HoldPosition();
            float health = tower.Health;
            int registered = battle.Targets.Count(target => target == tower);
            tower.TakeDamage(10000, 1);
            Assert.That(tower.IsAlive, Is.True);
            Assert.That(tower.Health, Is.EqualTo(health));
            Assert.That(battle.Targets.Count(target => target == tower), Is.EqualTo(registered));
            defender.TakeDamage(defender.MaxHealth + 1, 1);
            float deadline = Time.realtimeSinceStartup + 3;
            while (town.State.Owner != 1 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(town.State.Owner, Is.EqualTo(1), "A living enemy defender must capture the undefended post.");
            Assert.That(town.Defender, Is.SameAs(enemy));
            Assert.That(tower.IsAlive, Is.True);
            Assert.That(tower.Health, Is.EqualTo(health));
            Assert.That(tower.Team, Is.EqualTo(1));
            Assert.That(battle.Targets.Contains(tower), Is.True);
        }

        [UnityTest]
        public IEnumerator TowerOnlyFiresForALivingDefender()
        {
            var harbor = naval.Harbors.First(item => item.IsIsland);
            var tower = harbor.Defense;
            Vector3 direction = tower.transform.position - harbor.Landing; direction.y = 0;
            direction = direction.sqrMagnitude > .01f ? direction.normalized : Vector3.forward;
            var enemy = battle.Spawn(1, UnitKind.Guard, harbor.Landing + direction * 5.5f);
            Assert.That(enemy, Is.Not.Null);
            KeepOnlyTower(tower);
            enemy.HoldPosition();
            foreach (var target in battle.Targets.ToArray())
                if (target != tower && target != enemy) battle.Targets.Remove(target);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(tower.ShotsFired, Is.Zero, "An unoccupied tower must not fire autonomously.");
            Assert.That(tower.CurrentTarget, Is.Null);
        }

        void KeepOnlyTower(DefenseTower tower)
        {
            foreach (var other in battle.Towers.ToArray())
            {
                if (other == tower) continue;
                other.enabled = false;
                battle.Targets.Remove(other);
            }
        }

        [UnityTest]
        public IEnumerator MainlandHarborOwnStateAndTowerIgnoreLinkedTownOwnerChanges()
        {
            var harbor = naval.Harbors.First(item => item.Owner == 0 && item.LinkedTown);
            Assert.That(harbor.Defense.IsAlive, Is.True);
            Assert.That(harbor.State, Is.Not.SameAs(harbor.LinkedTown.State));
            Assert.That(harbor.ClaimZone, Is.Not.Null);
            int owner = harbor.Owner;
            harbor.LinkedTown.State.Owner = 1;
            yield return null;
            Assert.That(harbor.Owner, Is.EqualTo(owner));
            Assert.That(harbor.State.Owner, Is.EqualTo(owner));
            Assert.That(harbor.Defense.HostOwner, Is.EqualTo(owner));
            Assert.That(harbor.Defense.Team, Is.EqualTo(owner));
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
