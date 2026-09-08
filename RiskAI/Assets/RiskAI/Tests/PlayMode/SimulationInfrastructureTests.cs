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
            var mover = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, new Vector3(-30, 0, -16));
            var victim = BattleTestScenario.Mobile(battle, PlayerRules.NeutralTeam, UnitKind.Footman, new Vector3(-24, 0, -16));
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
            var victim = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, new Vector3(-30, 0, -16));
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
        public IEnumerator FollowStopsWhenItsEntityDiesAndDoesNotAttachToThePooledReplacement()
        {
            var follower = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, new Vector3(-30, 0, -16));
            var leader = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, new Vector3(-28, 0, -16));
            StopBackgroundUnits(follower, leader);
            int retiredId = leader.EntityId;
            GameObject pooledObject = leader.gameObject;
            Vector3 leaderPosition = leader.transform.position;

            follower.Follow(leader);
            Assert.That(follower.IsIdle, Is.False);
            leader.TakeDamage(10000, 1);
            Assert.That(battle.FindTarget(retiredId), Is.Null);

            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(follower.IsIdle, Is.True, "Follow must end as soon as the stored entity identity leaves the battle.");

            yield return new WaitForSecondsRealtime(1.5f);
            var replacement = battle.Spawn(0, UnitKind.Footman, leaderPosition + Vector3.right * 4);
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.gameObject, Is.SameAs(pooledObject), "The fixture must exercise reuse of the followed GameObject.");
            Assert.That(replacement.EntityId, Is.Not.EqualTo(retiredId));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(follower.IsIdle, Is.True, "A pooled actor with a fresh identity must not inherit the old Follow order.");
        }

        [UnityTest]
        public IEnumerator SourceWeaponDeliveryControlsImpactInsteadOfDamageCategory()
        {
            battle.Combat.PresentationEnabled = false;
            var source = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, new Vector3(-30, 0, -16));
            var target = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, new Vector3(-28, 0, -16));
            var bystander = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, new Vector3(-27.8f, 0, -16));
            StopBackgroundUnits(source, target);
            source.enabled = target.enabled = bystander.enabled = false;
            source.Agent.enabled = target.Agent.enabled = bystander.Agent.enabled = false;

            float targetBefore = target.Health, bystanderBefore = bystander.Health;
            int projectileCount = battle.Combat.ActiveProjectileCount;
            var instant = SourceWeapons.For(UnitKind.Archer, AttackKind.Piercing);
            int instantId = battle.Combat.FireWeapon(source.AimPoint, target.AimPoint, target, 20,
                source.Team, source, instant);
            Assert.That(instant.Delivery, Is.EqualTo(WeaponDelivery.Instant));
            Assert.That(instantId, Is.Zero);
            Assert.That(battle.Combat.ActiveProjectileCount, Is.EqualTo(projectileCount));
            Assert.That(target.Health, Is.LessThan(targetBefore), "h00B instant delivery must resolve at release.");

            targetBefore = target.Health;
            Vector3 from = target.AimPoint + Vector3.left * 44;
            var magicMissile = new WeaponProfile(AttackKind.Magic, WeaponDelivery.Missile, 22);
            int missileId = battle.Combat.FireWeapon(from, target.AimPoint, target, 20,
                source.Team, source, magicMissile);
            Assert.That(battle.Combat.TryGetProjectile(missileId, out var missile), Is.True);
            Assert.That(missile.Duration, Is.EqualTo(2).Within(.001f), "Source flight time must be distance / speed without the legacy clamp.");
            Assert.That(target.Health, Is.EqualTo(targetBefore));
            battle.Combat.Tick(2.01f);
            Assert.That(target.Health, Is.LessThan(targetBefore));
            Assert.That(bystander.Health, Is.EqualTo(bystanderBefore), "Magic damage alone must not create splash.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RiflemanAcquiresInsideItsSourceRadiusBeyondWeaponRange()
        {
            var rifleman = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, new Vector3(-30, 0, -16));
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, new Vector3(-28, 0, -16));
            StopBackgroundUnits(rifleman, enemy);
            foreach (var unit in battle.Units.ToArray())
                if (unit != rifleman && unit != enemy) battle.Targets.Remove(unit);
            enemy.enabled = false;
            enemy.Agent.enabled = false;
            enemy.transform.position = rifleman.transform.position + Vector3.right * 11;
            rifleman.Stop();
            battle.Spatial.Rebuild(battle.Targets, battle.Units);

            Assert.That(Vector3.Distance(rifleman.transform.position, enemy.transform.position), Is.EqualTo(11).Within(.01f));
            Assert.That(Vector3.Distance(rifleman.transform.position, enemy.transform.position), Is.GreaterThan(BattleRules.Range(UnitKind.Archer) + 2));
            Assert.That(SourceWeapons.AcquisitionRange(UnitKind.Archer), Is.EqualTo(12));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(rifleman.CurrentTarget, Is.SameAs(enemy));
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(rifleman.CurrentTarget, Is.SameAs(enemy), "The source acquisition target must survive consecutive validity checks.");
        }

        [UnityTest]
        public IEnumerator RiflemanDoesNotAcquireBeyondItsSourceRadius()
        {
            var rifleman = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, new Vector3(-30, 0, -16));
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, new Vector3(-28, 0, -16));
            StopBackgroundUnits(rifleman, enemy);
            foreach (var unit in battle.Units.ToArray())
                if (unit != rifleman && unit != enemy) battle.Targets.Remove(unit);
            enemy.enabled = false;
            enemy.Agent.enabled = false;
            enemy.transform.position = rifleman.transform.position + Vector3.right * 12.5f;
            rifleman.Stop();
            battle.Spatial.Rebuild(battle.Targets, battle.Units);

            Assert.That(Vector3.Distance(rifleman.transform.position, enemy.transform.position), Is.EqualTo(12.5f).Within(.01f));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(rifleman.CurrentTarget, Is.Null);
        }

        [UnityTest]
        public IEnumerator MortarDoesNotAcquireBeyondItsSourceRadius()
        {
            var mortar = BattleTestScenario.Mobile(battle, 0, UnitKind.Mortar, new Vector3(-30, 0, -16));
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, new Vector3(-11.5f, 0, -16));
            StopBackgroundUnits(mortar, enemy);
            foreach (var unit in battle.Units.ToArray())
                if (unit != mortar && unit != enemy) battle.Targets.Remove(unit);
            enemy.enabled = false;
            enemy.Agent.enabled = false;
            mortar.HoldPosition();
            battle.Spatial.Rebuild(battle.Targets, battle.Units);

            Assert.That(Vector3.Distance(mortar.transform.position, enemy.transform.position), Is.GreaterThan(SourceWeapons.AcquisitionRange(UnitKind.Mortar)));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(mortar.CurrentTarget, Is.Null);
        }

        [UnityTest]
        public IEnumerator MortarArtilleryLocksItsImpactPointAndUsesThreeSourceBands()
        {
            battle.Combat.PresentationEnabled = false;
            Vector3 center = new Vector3(-28, 0, -16);
            var source = BattleTestScenario.Mobile(battle, 0, UnitKind.Mortar, center + Vector3.left * 18);
            var original = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, center);
            var full = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, center + Vector3.right * .25f);
            var medium = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, center + Vector3.right * 2);
            var small = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, center + Vector3.right * 4);
            var outside = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, center + Vector3.right * 6);
            var ally = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, center + Vector3.left * .4f);
            var actors = new[] { source, original, full, medium, small, outside, ally };
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || actors.Contains(unit)) continue;
                unit.enabled = false;
                if (unit.Agent) unit.Agent.enabled = false;
            }
            foreach (var unit in actors)
            {
                unit.enabled = false;
                unit.Agent.enabled = false;
            }

            var weapon = SourceWeapons.For(UnitKind.Mortar, AttackKind.Siege);
            Assert.That(weapon.Delivery, Is.EqualTo(WeaponDelivery.Artillery));
            Assert.That(weapon.Targeting, Is.EqualTo(WeaponTargeting.LaunchPoint));
            float originalBefore = original.Health, fullBefore = full.Health, mediumBefore = medium.Health;
            float smallBefore = small.Health, outsideBefore = outside.Health, allyBefore = ally.Health, sourceBefore = source.Health;
            battle.Combat.FireWeapon(source.AimPoint, original.AimPoint, original, 100, source.Team, source, weapon);
            original.transform.position += Vector3.forward * 8;
            source.transform.position = center + Vector3.left * .25f;
            battle.Spatial.Rebuild(battle.Targets, battle.Units);
            battle.Combat.Tick(1.01f);

            Assert.That(original.Health, Is.EqualTo(originalBefore), "Artillery must not home onto a moved target.");
            Assert.That(full.Health, Is.EqualTo(fullBefore - CombatRules.ResolveDamage(100, AttackKind.Siege, full.ArmorType, full.Armor)).Within(.001f));
            Assert.That(medium.Health, Is.EqualTo(mediumBefore - CombatRules.ResolveDamage(35, AttackKind.Siege, medium.ArmorType, medium.Armor)).Within(.001f));
            Assert.That(small.Health, Is.EqualTo(smallBefore - CombatRules.ResolveDamage(10, AttackKind.Siege, small.ArmorType, small.Armor)).Within(.001f));
            Assert.That(outside.Health, Is.EqualTo(outsideBefore));
            Assert.That(ally.Health, Is.EqualTo(allyBefore - CombatRules.ResolveDamage(100, AttackKind.Siege, ally.ArmorType, ally.Armor)).Within(.001f),
                "h00H has no relationship restriction in its explicit splash-target mask.");
            Assert.That(source.Health, Is.EqualTo(sourceBefore), "h00H splash omits the self target flag.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarshipSplashHonorsItsExplicitEnemyAndNeutralRelations()
        {
            battle.Combat.PresentationEnabled = false;
            Vector3 center = new Vector3(-28, 0, -16);
            var source = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, center + Vector3.left * 22);
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, center);
            var neutral = BattleTestScenario.Mobile(battle, PlayerRules.NeutralTeam, UnitKind.Footman, center + Vector3.right * .3f);
            var ally = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, center + Vector3.left * .3f);
            var actors = new[] { source, enemy, neutral, ally };
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || actors.Contains(unit)) continue;
                unit.enabled = false;
                if (unit.Agent) unit.Agent.enabled = false;
            }
            foreach (var unit in actors)
            {
                unit.enabled = false;
                unit.Agent.enabled = false;
            }

            float enemyBefore = enemy.Health, neutralBefore = neutral.Health, allyBefore = ally.Health;
            var weapon = SourceWeapons.For(NavalUnitKind.Galley, AttackKind.Normal);
            battle.Combat.FireWeapon(source.AimPoint, enemy.AimPoint, enemy, 40, source.Team, source, weapon);
            // Keep the identity registered while excluding the primary from the area-query fixture.
            // A missile-splash weapon must still apply its full primary hit.
            battle.Targets.Remove(enemy);
            battle.Spatial.Rebuild(battle.Targets, battle.Units);
            battle.Combat.Tick(1.01f);

            Assert.That(enemy.Health, Is.LessThan(enemyBefore));
            Assert.That(neutral.Health, Is.LessThan(neutralBefore));
            Assert.That(ally.Health, Is.EqualTo(allyBefore), "h00W explicitly restricts splash to enemies and neutral targets.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleCommandsValidateOwnershipFiniteValuesGarrisonsDeferredAndStaleIds()
        {
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, new Vector3(-30, 0, -16));
            var stale = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, new Vector3(-29, 0, -16));
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
            Assert.That(battle.Commands.Submit(new UnitCommand(0, garrison.EntityId, UnitCommandKind.Stop)), Is.True, "Stop is harmless for a garrison and must not demand a relief.");
            battle.Commands.Tick();
            Assert.That(garrison.IsGarrison, Is.True);

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

            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, center + Vector3.forward * 2);
            var attackers = BattleTestScenario.MobileArmy(battle, 0, UnitKind.Archer, 3, center + Vector3.back * 2);
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
