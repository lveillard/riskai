using System.Collections;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class MedicSupportTests
    {
        Scene scene, previous;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Medic support tests");
            SceneManager.SetActiveScene(scene);
            new GameObject("Medic test bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator MedicHealsMostInjuredFriendlyUnitAndDoesNotHealEnemy()
        {
            var medic = battle.Spawn(0, UnitKind.Medic, new Vector3(-30, 0, -16));
            var lightlyWounded = battle.Spawn(0, UnitKind.Footman, new Vector3(-28, 0, -16));
            var heavilyWounded = battle.Spawn(0, UnitKind.Archer, new Vector3(-26, 0, -16));
            var enemy = battle.Spawn(1, UnitKind.Footman, new Vector3(-23, 0, -16));
            Assert.That(medic, Is.Not.Null);
            Assert.That(heavilyWounded, Is.Not.Null);
            medic.HoldPosition();
            lightlyWounded.HoldPosition();
            heavilyWounded.HoldPosition();
            enemy.HoldPosition();
            lightlyWounded.TakeDamage(8, 1);
            heavilyWounded.TakeDamage(42, 1);
            enemy.TakeDamage(45, 0);
            float enemyBefore = enemy.Health;

            yield return new WaitForSeconds(1.15f);

            var healer = medic.GetComponent<MedicSupport>();
            Assert.That(healer, Is.Not.Null);
            Assert.That(healer.CastCount, Is.EqualTo(1));
            Assert.That(healer.TotalHealing, Is.EqualTo(25).Within(.001f));
            Assert.That(heavilyWounded.Health, Is.EqualTo(heavilyWounded.MaxHealth - 17).Within(.001f));
            Assert.That(lightlyWounded.Health, Is.EqualTo(lightlyWounded.MaxHealth - 8).Within(.001f));
            Assert.That(enemy.Health, Is.LessThanOrEqualTo(enemyBefore + .001f));
        }

        [UnityTest]
        public IEnumerator MedicClampsHealingAndStopsWhenEveryoneIsFull()
        {
            var medic = battle.Spawn(0, UnitKind.Medic, new Vector3(-30, 0, -16));
            var ally = battle.Spawn(0, UnitKind.Footman, new Vector3(-28, 0, -16));
            medic.HoldPosition();
            ally.HoldPosition();
            ally.TakeDamage(5, 1);

            yield return new WaitForSeconds(1.15f);

            var healer = medic.GetComponent<MedicSupport>();
            Assert.That(ally.Health, Is.EqualTo(ally.MaxHealth).Within(.001f));
            Assert.That(healer.CastCount, Is.EqualTo(1));
            Assert.That(healer.TotalHealing, Is.EqualTo(5).Within(.001f));

            yield return new WaitForSeconds(1.15f);
            Assert.That(healer.CastCount, Is.EqualTo(1));
            Assert.That(healer.TotalHealing, Is.EqualTo(5).Within(.001f));
        }

        [UnityTest]
        public IEnumerator MedicDoesNotHealOutsideRadiusOrThroughTerrain()
        {
            var medic = battle.Spawn(0, UnitKind.Medic, new Vector3(-30, 0, -16));
            var blocked = battle.Spawn(0, UnitKind.Footman, new Vector3(-26, 0, -16));
            var distant = battle.Spawn(0, UnitKind.Footman, new Vector3(-15, 0, -16));
            medic.HoldPosition();
            blocked.HoldPosition();
            distant.HoldPosition();
            blocked.TakeDamage(30, 1);
            distant.TakeDamage(30, 1);

            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Medic LOS blocker";
            blocker.layer = MapLayout.TerrainLayer;
            blocker.transform.position = new Vector3(-28, 3, -16);
            blocker.transform.localScale = new Vector3(.5f, 6f, 4f);
            Physics.SyncTransforms();

            yield return new WaitForSeconds(1.15f);

            var healer = medic.GetComponent<MedicSupport>();
            Assert.That(healer.CastCount, Is.Zero);
            Assert.That(healer.TotalHealing, Is.Zero);
            Assert.That(blocked.Health, Is.EqualTo(blocked.MaxHealth - 30).Within(.001f));
            Assert.That(distant.Health, Is.EqualTo(distant.MaxHealth - 30).Within(.001f));

            Object.Destroy(blocker);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.1f);

            Assert.That(healer.CastCount, Is.EqualTo(1));
            Assert.That(healer.TotalHealing, Is.EqualTo(25).Within(.001f));
            Assert.That(blocked.Health, Is.EqualTo(blocked.MaxHealth - 5).Within(.001f));
            Assert.That(distant.Health, Is.EqualTo(distant.MaxHealth - 30).Within(.001f));
        }

        [UnityTest]
        public IEnumerator MedicHealAndWeaponContactNeverResolveOnTheSameSimulationTick()
        {
            var medic=battle.Spawn(0,UnitKind.Medic,new Vector3(-30,0,-16));
            var ally=battle.Spawn(0,UnitKind.Footman,new Vector3(-28,0,-16));
            var enemy=battle.Spawn(1,UnitKind.Footman,new Vector3(-26,0,-16));
            ally.TakeDamage(40,1);enemy.HoldPosition();medic.Attack(enemy);
            var healer=medic.GetComponent<MedicSupport>();
            float deadline=Time.realtimeSinceStartup+2;
            while(healer.CastCount==0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(healer.CastCount,Is.EqualTo(1));
            Assert.That(medic.LastAttackContactTick,Is.Not.EqualTo(healer.LastCastTick),
                "Ahea is an autocast order; its heal cannot share a simulation tick with weapon contact.");
        }
    }
}
