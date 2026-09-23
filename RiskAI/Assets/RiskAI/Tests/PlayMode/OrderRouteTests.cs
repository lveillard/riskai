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
    /// <summary>Three queued move points draw three legs; arriving at the first drops that leg.</summary>
    public sealed class OrderRouteTests
    {
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch = ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch = 2;
            scene = SceneManager.CreateScene("Order routes");
            SceneManager.SetActiveScene(scene);
            new GameObject("Order route bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreeQueuedPointsDrawThreeLegsAndDropTheFirstOnArrival()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var first = Walkable(home.Rally, 6f);
            var second = Walkable(first, 6f);
            var third = Walkable(second, 6f);
            unit.Select(true);
            var routes = Object.FindFirstObjectByType<RtsController>().GetComponent<OrderRoutes>();
            Assert.That(routes, Is.Not.Null);

            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, first.x, first.y, first.z, append: true)), Is.True);
            Step();
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, second.x, second.y, second.z, append: true)), Is.True);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, third.x, third.y, third.z, append: true)), Is.True);
            Step();

            Assert.That(unit.OrderLegCount, Is.EqualTo(3));
            routes.Refresh();
            Assert.That(routes.LegCount, Is.EqualTo(3), "Three queued points are three route legs.");

            Assert.That(unit.Agent.Warp(unit.Agent.destination), Is.True);
            for (int i = 0; i < 8 && unit.OrderLegCount > 2; i++) Step();
            Assert.That(unit.OrderLegCount, Is.EqualTo(2));
            routes.Refresh();
            Assert.That(routes.LegCount, Is.EqualTo(2), "The leg that was reached is gone.");
            yield return null;
        }

        Vector3 Walkable(Vector3 origin, float distance)
        {
            var directions = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.forward + Vector3.right };
            for (int i = 0; i < directions.Length; i++)
                if (NavMesh.SamplePosition(origin + directions[i].normalized * distance, out var hit, 3f, NavMesh.AllAreas) &&
                    Vector3.Distance(hit.position, origin) > 3f)
                    return hit.position;
            Assert.Fail("Need a walkable point near " + origin);
            return origin;
        }

        void Step() => battle.Clock.Advance(SimClock.StepSeconds, false, battle.World.Tick);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.PlayerCountForNewMatch = previousPlayers;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
