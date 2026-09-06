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
            Assert.That(battle.Units.Count, Is.EqualTo(48));
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
        public IEnumerator HarborTowerDamagesAnEnemyInRange()
        {
            var harbor = naval.Harbors.First(item => item.Owner == 0);
            var tower = harbor.Defense;
            Vector3 away = tower.transform.position - harbor.Landing;
            away.y = 0;
            away = away.sqrMagnitude > .01f ? away.normalized : Vector3.forward;
            var enemy = battle.Spawn(1, UnitKind.Guard, tower.transform.position + away * 7);
            Assert.That(enemy, Is.Not.Null);
            KeepOnlyTower(tower);
            enemy.HoldPosition();
            foreach (var target in battle.Targets.ToArray())
                if (target != tower && target != enemy) battle.Targets.Remove(target);
            float health = enemy.Health;
            yield return new WaitForSecondsRealtime(1.7f);
            Assert.That(tower.ShotsFired, Is.GreaterThan(0));
            Assert.That(enemy.Health, Is.LessThan(health));
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
        public IEnumerator HarborTowerRebuildUsesSixtyGoldAndSevenSeconds()
        {
            var harbor = naval.Harbors.First(item => item.Owner == 0);
            harbor.Defense.TakeDamage(10000, 1);
            battle.Economy.Gold[0] = 100;
            Assert.That(harbor.BuildTower(0), Is.Null);
            Assert.That(battle.Economy.Gold[0], Is.EqualTo(40));
            Assert.That(harbor.BuildingTower, Is.True);
            yield return new WaitForSecondsRealtime(7.3f);
            Assert.That(harbor.Defense.IsAlive, Is.True);
            Assert.That(harbor.Defense.Team, Is.EqualTo(0));
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
