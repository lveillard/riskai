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
    /// <summary>Melee reach is measured edge to edge: lancers strike from the edge of their reach.</summary>
    public sealed class MeleeReachTests
    {
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch = ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch = 2;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Melee reach");
            SceneManager.SetActiveScene(scene);
            new GameObject("Melee reach bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            foreach (var tower in battle.Towers) if (tower) tower.enabled = false;
            yield return null;
        }

        static float Gap(Soldier a, Soldier b)
        {
            var d = a.transform.position - b.transform.position; d.y = 0;
            return d.magnitude - a.Type.BodyRadius - b.Type.BodyRadius;
        }

        IEnumerator StrikeFromReach(UnitKind attackerKind, float minimumGap, float maximumGap)
        {
            // Open ground away from every post: towns recruit nearby idle units as defenders.
            Vector3 origin = Vector3.zero; bool found = false;
            for (int ring = 3; ring < 40 && !found; ring++)
                for (int step = 0; step < 16 && !found; step++)
                {
                    float angle = step * Mathf.PI / 8, radius = ring * 5f;
                    var candidate = battle.Towns[0].transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(candidate, out var point, 1, NavMesh.AllAreas)) continue;
                    if (!NavMesh.SamplePosition(point.position + Vector3.right * 6f, out var other, 1, NavMesh.AllAreas)) continue;
                    if (NavMesh.Raycast(point.position, other.position, out _, NavMesh.AllAreas)) continue;
                    bool clear = battle.Towns.All(town => Vector3.Distance(town.transform.position, point.position) > 22 && Vector3.Distance(town.ClaimPoint, point.position) > 22);
                    clear &= battle.Units.All(unit => !unit || Vector3.Distance(unit.transform.position, point.position) > 16);
                    if (clear) { origin = point.position; found = true; }
                }
            Assert.That(found, Is.True, "Open ground for the melee reach scenario.");
            Assert.That(NavMesh.SamplePosition(origin, out var start, 1, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(start.position + Vector3.right * 6f, out var goal, 1, NavMesh.AllAreas), Is.True);
            var attacker = BattleTestScenario.Mobile(battle, 0, attackerKind, start.position);
            var target = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, goal.position);
            Assert.That(attacker.Agent.Warp(start.position), Is.True);
            Assert.That(target.Agent.Warp(goal.position), Is.True);
            target.HoldPosition();
            attacker.Attack(target);
            Assert.That(attacker.CurrentTarget, Is.SameAs(target), "Attack order accepted (paused " + battle.Paused + ", winner " + battle.Winner + ").");
            float health = target.Health, until = Time.realtimeSinceStartup + 12f;
            int hits = 0; float smallestGap = float.MaxValue, largestGap = 0;
            Time.timeScale = 2;
            string trace = "";
            var modeField = typeof(Soldier).GetField("mode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float nextTrace = 0;
            try
            {
                while (Time.realtimeSinceStartup < until && hits < 3 && target.IsAlive)
                {
                    yield return null;
                    if (Time.realtimeSinceStartup >= nextTrace && trace.Length < 600)
                    {
                        nextTrace = Time.realtimeSinceStartup + .5f;
                        trace += $" [t={battle.BattleTime:F1} mode={modeField?.GetValue(attacker)} tgt={(attacker.CurrentTarget ? attacker.CurrentTarget.name : "-")} gap={Gap(attacker, target):F2} path={attacker.Agent.hasPath} stop={attacker.Agent.isStopped} v={attacker.Agent.velocity.magnitude:F2} paused={battle.Paused}]";
                    }
                    if (target.Health < health)
                    {
                        hits++; health = target.Health;
                        float gap = Gap(attacker, target);
                        smallestGap = Mathf.Min(smallestGap, gap); largestGap = Mathf.Max(largestGap, gap);
                    }
                }
            }
            finally { Time.timeScale = 1; }
            Assert.That(hits, Is.GreaterThanOrEqualTo(2), attackerKind + " must land repeated blows on a stationary target (gap now " + Gap(attacker, target).ToString("F2") +
                ", target alive " + target.IsAlive + ", attacking " + (attacker.CurrentTarget == target) + ", winner " + battle.Winner + ")." + trace);
            Assert.That(smallestGap, Is.GreaterThanOrEqualTo(minimumGap), attackerKind + " must not press into its target while hitting.");
            Assert.That(largestGap, Is.LessThanOrEqualTo(maximumGap), attackerKind + " hits only inside its edge-to-edge reach.");
        }

        [UnityTest]
        public IEnumerator LancerKeepsItsReachGapWhileHitting()
        {
            float reach = UnitCatalog.Get(UnitKind.Knight).Weapon.Range;
            yield return StrikeFromReach(UnitKind.Knight, 1f, reach + .56f);
        }

        [UnityTest]
        public IEnumerator FootmanStillClosesToSwordReach()
        {
            yield return StrikeFromReach(UnitKind.Footman, -.05f, UnitCatalog.Get(UnitKind.Footman).Weapon.Range + .56f);
        }

        [Test]
        public void LanceTipCoversTheEngagementGap()
        {
            // At contact the lowered, thrust lance reaches the near surface of a footman
            // standing at the melee engagement distance (model space, calibrated below 1%).
            float engage = UnitRules.EngageDistance(UnitCatalog.Get(UnitKind.Knight), UnitCatalog.Get(UnitKind.Footman).BodyRadius);
            float surface = engage - UnitCatalog.Get(UnitKind.Footman).CollisionRadius;
            var tip = MountedKnightView.LanceTip(1);
            Assert.That(tip.z, Is.InRange(surface - .15f, engage), "Contact lance tip reaches the target body.");
            Assert.That(MountedKnightView.LanceTip(0).z, Is.LessThan(surface), "At rest the lance stays short of the target.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.PlayerCountForNewMatch = previousPlayers;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
