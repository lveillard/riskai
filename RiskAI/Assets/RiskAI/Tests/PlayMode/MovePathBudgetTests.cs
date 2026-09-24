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
    /// <summary>One move asks for one path and leaves the agent on the path it already has.</summary>
    public sealed class MovePathBudgetTests
    {
        Scene previous, scene;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Move path budget");
            SceneManager.SetActiveScene(scene);
            new GameObject("Move path budget bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator NineHundredMovesRequestOnePathAndDoNotStop()
        {
            var origin = battle.Towns.First(town => town.State.Owner == 0).ClaimPoint;
            Assert.That(NavMesh.SamplePosition(origin, out var start, 8f, NavMesh.AllAreas), Is.True);
            const int count = 900;
            var units = new Soldier[count];
            int placed = 0;
            for (int ring = 0; placed < count && ring < 80; ring++)
            {
                int slots = ring == 0 ? 1 : ring * 6;
                for (int slot = 0; slot < slots && placed < count; slot++)
                {
                    float angle = slot * Mathf.PI * 2f / Mathf.Max(1, slots);
                    var probe = start.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 1.7f);
                    if (!NavMesh.SamplePosition(probe, out var hit, 2f, NavMesh.AllAreas)) continue;
                    units[placed] = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, hit.position);
                    placed++;
                }
            }
            Assert.That(placed, Is.EqualTo(count), "the town needs room for 900 soldiers on the NavMesh");
            yield return null;
            var goal = Walkable(start.position, 12f);
            SoldierPathBudget.Arm();
            Issue(units, goal);
            battle.Commands.Tick();
            AssertBudget(count, "the first move");
            var replaced = Walkable(goal, 10f);
            SoldierPathBudget.Arm();
            Issue(units, replaced);
            battle.Commands.Tick();
            AssertBudget(count, "a replacing move");
            yield return null;
        }

        void Issue(Soldier[] units, Vector3 goal)
        {
            for (int i = 0; i < units.Length; i++)
                Assert.That(battle.Commands.Submit(new UnitCommand(0, units[i].EntityId, UnitCommandKind.Move, goal.x, goal.y, goal.z)), Is.True,
                    battle.Commands.LastRejection);
        }

        static void AssertBudget(int count, string round)
        {
            Assert.That(SoldierPathBudget.SetDestination, Is.EqualTo(count), round + " requests one path");
            Assert.That(SoldierPathBudget.ResetPath, Is.EqualTo(0), round + " keeps the current path");
            Assert.That(SoldierPathBudget.Stopped, Is.EqualTo(0), round + " does not stop the agent");
            Assert.That(SoldierPathBudget.Sampled, Is.EqualTo(count), round + " samples the destination once");
            Assert.That(SoldierPathBudget.Calculated, Is.EqualTo(0), round + " does not build route corners for an unselected unit");
        }

        static Vector3 Walkable(Vector3 origin, float distance)
        {
            var directions = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            for (int i = 0; i < directions.Length; i++)
                if (NavMesh.SamplePosition(origin + directions[i] * distance, out var hit, 4f, NavMesh.AllAreas))
                    return hit.position;
            Assert.Fail("Need a walkable point near " + origin);
            return origin;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SoldierPathBudget.Armed = false;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
