using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class SimulationInfrastructureTests
    {
        Scene scene, previous;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Simulation infrastructure");
            SceneManager.SetActiveScene(scene);
            new GameObject("Simulation infrastructure bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            foreach (var tower in battle.Towers) if (tower) tower.enabled = false;
            if (battle.Naval)
                foreach (var ship in battle.Naval.Ships.ToArray())
                    if (ship) ship.gameObject.SetActive(false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseFreezesSimulationWithoutChangingTimeScale()
        {
            var mover = battle.Units.First(u => u && u.Team == 0 && !u.IsGarrison);
            var victim = battle.Units.First(u => u && u.Team == 2);
            StopBackgroundUnits(mover, victim);
            victim.enabled = false;
            mover.MoveTo(mover.transform.position + mover.transform.forward * 8, false, false);

            float timeScale = Time.timeScale;
            float healthBefore = victim.Health;
            battle.Combat.FireProjectile(mover.AimPoint, victim.AimPoint, victim, 24, mover.Team, mover, AttackKind.Piercing);
            int projectilesBeforePause = battle.Combat.ActiveProjectileCount;
            Vector3 positionBeforePause = mover.transform.position;
            long ticksBeforePause = battle.Clock.TickCount;
            float battleTimeBeforePause = battle.BattleTime;

            battle.TogglePause();
            Assert.That(battle.Paused, Is.True);
            yield return new WaitForSecondsRealtime(.35f);

            Assert.That(Time.timeScale, Is.EqualTo(timeScale));
            Assert.That(Vector3.Distance(mover.transform.position, positionBeforePause), Is.LessThan(.001f));
            Assert.That(battle.Clock.TickCount, Is.EqualTo(ticksBeforePause));
            Assert.That(battle.BattleTime, Is.EqualTo(battleTimeBeforePause));
            Assert.That(battle.Combat.ActiveProjectileCount, Is.EqualTo(projectilesBeforePause));
            Assert.That(victim.Health, Is.EqualTo(healthBefore));

            battle.TogglePause();
            Assert.That(battle.Paused, Is.False);
            yield return new WaitForSecondsRealtime(.85f);

            Assert.That(Time.timeScale, Is.EqualTo(timeScale));
            Assert.That(battle.Clock.TickCount, Is.GreaterThan(ticksBeforePause));
            Assert.That(battle.Combat.ActiveProjectileCount, Is.Zero);
            Assert.That(victim.Health, Is.LessThan(healthBefore));
        }

        [UnityTest]
        public IEnumerator SoldierPoolReusesIdentityWithFreshStateAndRejectsOldProjectileTarget()
        {
            battle.Combat.PresentationEnabled = false;
            var victim = battle.Units.First(u => u && u.Team == 0 && !u.IsGarrison);
            StopBackgroundUnits(victim, null);
            int oldEntityId = victim.EntityId;
            GameObject oldObject = victim.gameObject;
            UnitKind kind = victim.Kind;
            Vector3 spawnPoint = victim.transform.position;
            int opposingTeam = victim.Team == 0 ? 1 : 0;

            battle.Combat.FireProjectile(spawnPoint + Vector3.forward * 30, victim.AimPoint, victim, 100, opposingTeam, null, AttackKind.Normal);
            victim.TakeDamage(10000, opposingTeam);
            Assert.That(battle.FindTarget(oldEntityId), Is.Null);

            yield return new WaitForSecondsRealtime(1.7f);
            Assert.That(battle.Combat.ActiveProjectileCount, Is.Zero);

            var replacement = battle.Spawn(victim.Team, kind, spawnPoint);
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.gameObject, Is.SameAs(oldObject));
            Assert.That(replacement.EntityId, Is.Not.EqualTo(oldEntityId));
            Assert.That(battle.FindTarget(oldEntityId), Is.Null);
            Assert.That(battle.FindTarget(replacement.EntityId), Is.SameAs(replacement));
            Assert.That(replacement.Health, Is.EqualTo(replacement.MaxHealth));

            replacement.Stop();
            Assert.That(replacement.CurrentTarget, Is.Null);
            replacement.MoveTo(replacement.transform.position + Vector3.forward * 2, false, false);
            Assert.That(replacement.CurrentTarget, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleCommandsValidateOwnershipFiniteValuesGarrisonsDeferredAndStaleIds()
        {
            var unit = battle.Units.First(u => u && u.Team == 0 && !u.IsGarrison);
            var stale = battle.Units.First(u => u && u.Team == 0 && !u.IsGarrison && u != unit);
            var garrison = battle.Units.FirstOrDefault(u => u && u.Team == 0 && u.IsGarrison);
            Assert.That(garrison, Is.Not.Null);
            StopBackgroundUnits(unit, stale);
            Assert.That(unit.Agent.Warp(new Vector3(-30, 0, -16)), Is.True);
            Assert.That(stale.Agent.Warp(new Vector3(-29, 0, -16)), Is.True);
            stale.HoldPosition();
            garrison.gameObject.SetActive(true);
            garrison.enabled = false;

            Assert.That(battle.Commands.Submit(new UnitCommand(1, unit.EntityId, UnitCommandKind.Stop)), Is.False, "A command from the other player must be rejected.");
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, float.NaN, 0, 0)), Is.False, "Non-finite command coordinates must be rejected.");
            Assert.That(battle.Commands.Submit(new UnitCommand(0, garrison.EntityId, UnitCommandKind.Stop)), Is.False, "Garrisoned units must not accept board commands.");

            unit.HoldPosition();
            Vector3 destination = battle.Towns.First(t => t.State.Owner == 0).Rally;
            long appliedBefore = battle.Commands.AppliedCount;
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, destination.x, destination.y, destination.z)), Is.True);
            Assert.That(battle.Commands.PendingCount, Is.EqualTo(1));
            Assert.That(battle.Commands.AppliedCount, Is.EqualTo(appliedBefore));

            float tickBefore = battle.BattleTime;
            float deadline = Time.realtimeSinceStartup + 2;
            while (battle.BattleTime <= tickBefore && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.Commands.PendingCount, Is.Zero);
            Assert.That(battle.Commands.AppliedCount, Is.EqualTo(appliedBefore + 1));
            Assert.That(Vector3.Distance(unit.Agent.destination, destination), Is.LessThan(.25f));

            long appliedAfter = battle.Commands.AppliedCount;
            Assert.That(battle.Commands.Submit(new UnitCommand(0, stale.EntityId, UnitCommandKind.Hold)), Is.True);
            stale.TakeDamage(10000, 1);
            float staleTick = battle.BattleTime;
            deadline = Time.realtimeSinceStartup + 2;
            while (battle.BattleTime <= staleTick && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.BattleTime, Is.GreaterThan(staleTick));
            Assert.That(battle.Commands.PendingCount, Is.Zero);
            Assert.That(battle.Commands.AppliedCount, Is.EqualTo(appliedAfter), "A queued command must be ignored after its unit ID becomes stale.");
        }

        [UnityTest]
        public IEnumerator SpatialIndexReturnsBroadphaseSupersetAndExactPressure()
        {
            var index = battle.Spatial;
            index.Rebuild(battle.Targets, battle.Units);
            var center = battle.Units.First(u => u && u.IsAlive).transform.position;
            const float radius = 5.5f;
            var candidates = new List<CombatTarget>();
            index.Query(center, radius, candidates);
            var nearby = new HashSet<CombatTarget>(battle.Targets.Where(t => t && t.IsAlive && XzDistanceSquared(t.transform.position, center) <= radius * radius));

            Assert.That(candidates.Count, Is.EqualTo(index.LastCandidateCount));
            Assert.That(nearby.IsSubsetOf(candidates), Is.True, "Every precise nearby target must be in the broadphase candidate set.");

            var enemy = battle.Units.First(u => u && u.Team == 1 && !u.IsGarrison);
            var attackers = battle.Units.Where(u => u && u.Team == 0 && !u.IsGarrison).Take(3).ToArray();
            Assert.That(attackers.Length, Is.GreaterThan(0));
            for (int i = 0; i < attackers.Length; i++) attackers[i].Attack(enemy);
            foreach (var unit in battle.Units)
                if (unit && unit != enemy && !attackers.Contains(unit)) unit.enabled = false;
            index.Rebuild(battle.Targets, battle.Units);

            int expectedPressure = attackers.Count(u => u.CurrentTarget == enemy);
            Assert.That(expectedPressure, Is.EqualTo(attackers.Length));
            Assert.That(index.Pressure(0, enemy), Is.EqualTo(expectedPressure));
            Assert.That(index.Pressure(1, enemy), Is.Zero);
            yield return null;
        }

        void StopBackgroundUnits(Soldier keepA, Soldier keepB)
        {
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || unit == keepA || unit == keepB) continue;
                unit.enabled = false;
                if (unit.Agent) unit.Agent.enabled = false;
            }
        }

        static float XzDistanceSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
