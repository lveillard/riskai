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
    public sealed class TelemetryCommandTests
    {
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousMode = BattleSession.ModeForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch;
            previousSeed = BattleSession.SeedForNewMatch;
            BattleSession.MapForNewMatch = ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.PlayerCountForNewMatch = 2;
            BattleSession.SeedForNewMatch = 18032;
            scene = SceneManager.CreateScene("Command telemetry");
            SceneManager.SetActiveScene(scene);
            new GameObject("Command telemetry bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            yield return null;
            // Exclude any setup activity from this test's telemetry window.
            battle.Commands.ConsumeTelemetry();
            battle.World.ConsumeTelemetry();
        }

        [UnityTest]
        public IEnumerator ConsumeTelemetrySeparatesHumanAiAndClearsTheWindow()
        {
            var humanHome = battle.Towns.First(town => town.State.Owner == 0);
            var aiHome = battle.Towns.First(town => town.State.Owner == 1);
            var human = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, humanHome.Rally);
            var ai = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, aiHome.Rally);
            human.HoldPosition();
            ai.HoldPosition();
            var humanDestination = ClearDestination(humanHome.Rally);
            var aiDestination = ClearDestination(aiHome.Rally);

            Assert.That(battle.Commands.Submit(new UnitCommand(0, human.EntityId, UnitCommandKind.Move,
                humanDestination.x, humanDestination.y, humanDestination.z)), Is.True);
            Assert.That(battle.Commands.Submit(new UnitCommand(1, ai.EntityId, UnitCommandKind.Move,
                aiDestination.x, aiDestination.y, aiDestination.z)), Is.True);

            var submitted = battle.Commands.ConsumeTelemetry();
            Assert.That(submitted.HumanSubmitted, Is.EqualTo(1));
            Assert.That(submitted.AiSubmitted, Is.EqualTo(1));
            Assert.That(submitted.MaxQueueDepth, Is.EqualTo(2));
            Assert.That(battle.Commands.PendingCount, Is.EqualTo(2), "Consuming diagnostics must not consume gameplay commands.");

            Step();
            var applied = battle.Commands.ConsumeTelemetry();
            Assert.That(applied.HumanApplied, Is.EqualTo(1));
            Assert.That(applied.AiApplied, Is.EqualTo(1));
            Assert.That(applied.HumanSubmitToApplyMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(applied.AiSubmitToApplyMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(battle.Commands.ConsumeTelemetry().HumanApplied, Is.Zero, "Telemetry consumption must reset only its aggregate window.");
            var world = battle.World.ConsumeTelemetry();
            Assert.That(world.TickCount, Is.EqualTo(1));
            Assert.That(world.TotalMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(world.SoldiersMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(battle.World.ConsumeTelemetry().TickCount, Is.Zero, "World telemetry consumption must reset only timing aggregates.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectedCommandIsAttributedAndDoesNotRaiseQueueDepth()
        {
            var humanHome = battle.Towns.First(town => town.State.Owner == 0);
            var human = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, humanHome.Rally);
            Assert.That(battle.Commands.Submit(new UnitCommand(1, human.EntityId, UnitCommandKind.Stop)), Is.False);

            var telemetry = battle.Commands.ConsumeTelemetry();
            Assert.That(telemetry.AiRejected, Is.EqualTo(1));
            Assert.That(telemetry.MaxQueueDepth, Is.Zero);
            Assert.That(battle.Commands.PendingCount, Is.Zero);
            yield return null;
        }

        static Vector3 ClearDestination(Vector3 origin)
        {
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            for (int i = 0; i < directions.Length; i++)
                if (NavMesh.SamplePosition(origin + directions[i] * 7, out var hit, 2, NavMesh.AllAreas) &&
                    Vector3.Distance(hit.position, origin) > 3)
                    return hit.position;
            Assert.Fail("The authored home rally needs a second walkable point for telemetry testing.");
            return origin;
        }

        void Step() => battle.Clock.Advance(SimClock.StepSeconds, false, battle.World.Tick);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.ModeForNewMatch = previousMode;
            BattleSession.PlayerCountForNewMatch = previousPlayers;
            BattleSession.SeedForNewMatch = previousSeed;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
