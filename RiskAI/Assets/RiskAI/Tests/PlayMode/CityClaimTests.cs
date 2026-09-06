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
        public IEnumerator StartingPostsHaveOneUniqueArcherGarrison()
        {
            var naval = NavalWorld.Current;
            var posts = battle.Towns.Select(town => new { Name = town.DisplayName, Owner = town.State.Owner, Defender = town.Defender })
                .Concat(naval.Harbors.Select(harbor => new { Name = harbor.DisplayName, Owner = harbor.Owner, Defender = harbor.Defender }))
                .ToArray();
            Assert.That(MapLayout.Towns.Length, Is.EqualTo(33));
            Assert.That(MapLayout.Countries.Length, Is.EqualTo(11));
            Assert.That(battle.Towns.Count(town => town.State.Owner == 0), Is.EqualTo(2));
            Assert.That(battle.Towns.Count(town => town.State.Owner == 1), Is.EqualTo(2));
            Assert.That(battle.Towns.Count(town => town.State.Owner < 0), Is.EqualTo(MapLayout.Towns.Length - 4));
            Assert.That(posts.Length, Is.EqualTo(MapLayout.Towns.Length + naval.Harbors.Count));
            Assert.That(battle.Units.Count, Is.EqualTo(posts.Length));
            Assert.That(posts.All(post => post.Defender), Is.True, string.Join(", ", posts.Where(post => !post.Defender).Select(post => post.Name)));
            Assert.That(posts.Select(post => post.Defender).Distinct().Count(), Is.EqualTo(posts.Length));
            foreach (var post in posts)
            {
                Assert.That(post.Defender.Kind, Is.EqualTo(UnitKind.Archer));
                Assert.That(post.Defender.IsGarrison, Is.True);
                Assert.That(post.Defender.Team, Is.EqualTo(post.Owner >= 0 ? post.Owner : PlayerRules.NeutralTeam));
            }
            Assert.That(naval.Ships, Is.Empty);
            Assert.That(battle.Units.All(unit => unit.IsGarrison), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyOutsideTakeoverRadiusDoesNotChangeOwner()
        {
            var town = battle.Towns.First(t => t.State.Owner < 0);
            var attacker = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, battle.Towns.First(t => t.State.Owner == 0).Rally);
            DisableAllTowers();
            town.Defender.TakeDamage(10000, 0);
            Move(attacker, OutsideTakeoverPoint(town.ClaimPoint));
            MoveOtherTeamUnitsOutsideProtection(town, attacker);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(-1));
            Assert.That(town.ClaimZone.Defender, Is.Not.SameAs(attacker));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EntryBindsNearestEnemyAndPreservesUnitIdentityAndHealth()
        {
            var town = battle.Towns.First(t => t.State.Owner < 0);
            DisableAllTowers();
            var neutralDefender = town.Defender;
            neutralDefender.TakeDamage(10000, 0);
            var attacker = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, battle.Towns.First(t => t.State.Owner == 0).Rally);
            float health = attacker.Health;
            Move(attacker, town.ClaimPoint);
            MoveOtherTeamUnitsOutsideProtection(town, attacker);
            IsolateClaimCombat(attacker);
            long tick = battle.Clock.TickCount;
            float deadline = Time.realtimeSinceStartup + 2;
            while (battle.Clock.TickCount == tick && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.Clock.TickCount, Is.GreaterThan(tick), "Capture is applied on a simulation tick, not after an arbitrary two rendered frames.");
            Assert.That(town.State.Owner, Is.EqualTo(0));
            Assert.That(town.Defender, Is.SameAs(attacker));
            Assert.That(attacker.Health, Is.EqualTo(health));
        }

        void DisableAllTowers()
        {
            foreach (var tower in battle.Towers.ToArray())
            {
                tower.enabled = false;
                battle.Targets.Remove(tower);
            }
        }

        [UnityTest]
        public IEnumerator LivingDefenderBlocksEnemyUntilItDies()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var defender = town.Defender;
            var attacker = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, battle.Towns.First(t => t.State.Owner == 1).Rally);
            DisableAllTowers();
            Move(attacker, town.ClaimPoint);
            MoveOtherTeamUnitsOutsideProtection(town, attacker);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(0));
            Assert.That(town.ClaimZone.Contested, Is.True);
            defender.TakeDamage(10000, 1);
            MoveOwnerUnitsOutsideProtection(town, 0);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(1));
            Assert.That(town.ClaimZone.Defender, Is.SameAs(attacker));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AlliedUnitOutsideCircleButInsideProtectionRadiusBlocksEnemyClaim()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var defender = town.Defender;
            var ally = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, town.Rally);
            var attacker = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, battle.Towns.First(t => t.State.Owner == 1).Rally);
            DisableAllTowers();
            defender.TakeDamage(10000, 1);
            MoveOwnerUnitsOutsideProtection(town, 0);
            Move(ally, town.ClaimPoint + Vector3.right * 2f);
            Move(attacker, town.ClaimPoint);
            MoveOtherTeamUnitsOutsideProtection(town, attacker);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner, .9f), Is.EqualTo(0));
            // Binding the nearest allied unit clears the transient contested flag;
            // the next sampling tick observes the enemy against that defender.
            Assert.That(town.ClaimZone.Defender, Is.SameAs(ally));
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner, .9f), Is.EqualTo(0));
            Assert.That(town.ClaimZone.Contested, Is.True);
            Move(ally, town.ClaimPoint + Vector3.right * 5f);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(1));
            Assert.That(town.ClaimZone.Defender, Is.SameAs(attacker));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyOutsideProtectionRadiusDoesNotContestLivingDefender()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var attacker = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, battle.Towns.First(t => t.State.Owner == 1).Rally);
            Move(attacker, town.ClaimPoint + Vector3.right * 7f);
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(0));
            Assert.That(town.ClaimZone.Contested, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyOutsideClaimCircleWithinProtectionHeightContestsLivingDefender()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var attacker = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, battle.Towns.First(t => t.State.Owner == 1).Rally);
            Move(attacker, town.ClaimPoint + Vector3.right * 2f);
            MoveOtherTeamUnitsOutsideProtection(town, attacker);
            attacker.transform.position += Vector3.up;
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(0));
            Assert.That(town.ClaimZone.Contested, Is.True);
            attacker.transform.position += Vector3.up * .4f;
            Assert.That(town.ClaimZone.Step(battle.Units, town.State.Owner), Is.EqualTo(0));
            Assert.That(town.ClaimZone.Contested, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TowersDoNotCountAsClaimOccupants()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var defender = town.Defender;
            var ally = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, town.Rally);
            var attacker = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, battle.Towns.First(t => t.State.Owner == 1).Rally);
            DisableAllTowers();
            town.Defense.CompleteBuild();
            float towerHealth = town.Defense.Health;
            town.Defense.enabled = false;
            battle.Targets.Remove(town.Defense);
            defender.TakeDamage(10000, 1);
            MoveOwnerUnitsOutsideProtection(town, 0);
            Move(ally, town.ClaimPoint + Vector3.right * 5f);
            Move(attacker, town.ClaimPoint);
            MoveOtherTeamUnitsOutsideProtection(town, attacker);
            IsolateClaimCombat(attacker);
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(town.State.Owner, Is.EqualTo(1));
            Assert.That(town.Defender, Is.SameAs(attacker));
            Assert.That(town.Defense.HostOwner, Is.EqualTo(1));
            Assert.That(town.Defense.Team, Is.EqualTo(1));
            Assert.That(town.Defense.Health, Is.EqualTo(towerHealth));
        }

        [UnityTest]
        public IEnumerator GarrisonCannotBeReleasedByOrdersOrEmbark()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0);
            var defender = town.Defender;
            var ally = BattleTestScenario.Mobile(battle, defender.Team, UnitKind.Footman, town.Rally);
            var position = defender.transform.position;
            defender.MoveTo(position + Vector3.right * 5f, false, false);
            defender.Stop();
            defender.Follow(ally);
            var harbor = NavalWorld.Current.Harbors.First(port => port.Owner == defender.Team);
            var transport = BattleTestScenario.Ship(NavalWorld.Current, defender.Team, ShipKind.Transport, harbor.Berth);
            Assert.That(transport.TryEmbark(defender), Is.False);
            Assert.That(defender.IsGarrison, Is.True);
            Assert.That(defender.transform.position, Is.EqualTo(position));
            yield return null;
            Assert.That(defender.IsGarrison, Is.True);
        }

        void MoveOwnerUnitsOutsideProtection(Settlement town, int owner)
        {
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || unit.Team != owner || unit == town.Defender) continue;
                Vector3 difference = unit.transform.position - town.ClaimPoint;
                difference.y = 0;
                if (difference.sqrMagnitude <= ClaimRules.ProtectionRadius * ClaimRules.ProtectionRadius)
                    Move(unit, town.ClaimPoint + Vector3.right * 5f);
            }
        }

        void MoveOtherTeamUnitsOutsideProtection(Settlement town, Soldier keep)
        {
            int ownerTeam = town.State.Owner >= 0 ? town.State.Owner : PlayerRules.NeutralTeam;
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || unit == keep || unit.Team == ownerTeam || unit.Team == PlayerRules.NeutralTeam) continue;
                Vector3 difference = unit.transform.position - town.ClaimPoint;
                difference.y = 0;
                if (difference.sqrMagnitude <= ClaimRules.ProtectionRadius * ClaimRules.ProtectionRadius)
                    Move(unit, town.ClaimPoint + Vector3.right * 5f);
            }
        }

        void IsolateClaimCombat(Soldier keep)
        {
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || unit == keep) continue;
                if (unit.IsGarrison) unit.Garrison.SetDefender(null);
                unit.gameObject.SetActive(false);
            }
            var naval = NavalWorld.Current;
            if (!naval) return;
            foreach (var ship in naval.Ships.ToArray())
                if (ship) ship.gameObject.SetActive(false);
        }

        static void Move(Soldier unit, Vector3 point)
        {
            Assert.That(unit && unit.Agent, Is.True);
            if (unit.IsGarrison) unit.Garrison.SetDefender(null);
            Assert.That(NavMesh.SamplePosition(point, out var hit, .9f, NavMesh.AllAreas), Is.True);
            unit.Agent.Warp(hit.position);
            unit.Stop();
        }

        static Vector3 OutsideTakeoverPoint(Vector3 center)
        {
            float minimum = ClaimRules.TakeoverRadius + .15f;
            for (int ring = 0; ring < 4; ring++)
            {
                float radius = minimum + ring * 1.5f;
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI * 2f / 32f;
                    var candidate = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                    var offset = hit.position - center; offset.y = 0;
                    if (offset.sqrMagnitude > minimum * minimum) return hit.position;
                }
            }
            Assert.Fail("Could not find a walkable point outside the takeover radius.");
            return center;
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
