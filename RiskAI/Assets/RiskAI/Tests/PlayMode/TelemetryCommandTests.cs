using System.Collections;
using System.Linq;
using System.Reflection;
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
            battle.enabled = false;
            battle.World.ConsumeTelemetry();
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

        [UnityTest]
        public IEnumerator MovementStagesObserveRealNavigationAndSurviveWindowConsumption()
        {
            battle.enabled = false;
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var human = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, ClearDestination(home.Rally));
            human.HoldPosition();
            var start = human.transform.position;
            var destination = ClearDestination(start);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, human.EntityId, UnitCommandKind.Move,
                destination.x, destination.y, destination.z)), Is.True);
            battle.Commands.Tick();

            var applied = battle.Commands.ConsumeTelemetry();
            Assert.That(applied.HumanMoveOutstanding, Is.EqualTo(1));
            Assert.That(applied.HumanRouteReadyCount, Is.Zero, "Application alone is not an observed route.");
            Assert.That(battle.Commands.ConsumeTelemetry().HumanMoveOutstanding, Is.EqualTo(1));
            float deadline = Time.realtimeSinceStartup + 4;
            while (Vector3.Distance(start, human.transform.position) < 1 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Step();
            }
            Assert.That(Vector3.Distance(start, human.transform.position), Is.GreaterThanOrEqualTo(1),
                "The real NavMeshAgent must move; writing counters is not the acceptance condition.");
            var motion = battle.Commands.ConsumeTelemetry();
            Assert.That(motion.HumanRouteReadyCount, Is.EqualTo(1));
            Assert.That(motion.HumanSpeedCount, Is.EqualTo(1));
            Assert.That(motion.HumanFirstMoveCount, Is.EqualTo(1));
            Assert.That(motion.HumanRouteToSpeedCount, Is.EqualTo(1));
            Assert.That(motion.HumanSpeedToDirectedCount, Is.EqualTo(1));
            Assert.That(motion.HumanMoveOutstanding, Is.Zero);
            Assert.That(motion.HumanSubmitToSpeedMilliseconds, Is.LessThanOrEqualTo(motion.HumanFirstMoveMilliseconds + .01));
            Assert.That(motion.HumanApplyToRouteMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(motion.HumanRouteToSpeedMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(motion.HumanSpeedToDirectedMilliseconds, Is.GreaterThanOrEqualTo(0));
            var cleared = battle.Commands.ConsumeTelemetry();
            Assert.That(cleared.HumanRouteReadyCount + cleared.HumanSpeedCount + cleared.HumanFirstMoveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OutstandingMovementClearsOnceOnHoldAndDisable()
        {
            battle.enabled = false;
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var first = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, ClearDestination(home.Rally));
            var second = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, ClearDestination(home.Rally));
            foreach (var unit in new[] { first, second })
            {
                unit.HoldPosition();
                var destination = ClearDestination(unit.transform.position);
                Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move,
                    destination.x, destination.y, destination.z)), Is.True);
            }
            battle.Commands.Tick();
            Assert.That(battle.Commands.ConsumeTelemetry().HumanMoveOutstanding, Is.EqualTo(2));
            first.HoldPosition();
            first.HoldPosition();
            second.gameObject.SetActive(false);
            var cancelled = battle.Commands.ConsumeTelemetry();
            Assert.That(cancelled.HumanFirstMoveCancelled, Is.EqualTo(2));
            Assert.That(cancelled.HumanMoveOutstanding, Is.Zero);
            Assert.That(cancelled.HumanSpeedCount, Is.Zero);
            Assert.That(battle.Commands.ConsumeTelemetry().HumanMoveOutstanding, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpeedObservationDoesNotWaitForVelocityTowardTheFinalDestination()
        {
            battle.enabled = false;
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var human = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, ClearDestination(home.Rally));
            human.HoldPosition();
            var destination = ClearDestination(human.transform.position);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, human.EntityId, UnitCommandKind.Move,
                destination.x, destination.y, destination.z)), Is.True);
            battle.Commands.Tick();
            human.Agent.isStopped = true;
            float deadline = Time.realtimeSinceStartup + 3;
            while (human.Agent.pathPending && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(human.Agent.pathPending, Is.False);
            Assert.That(human.Agent.hasPath, Is.True);
            var toward = destination - human.transform.position;
            toward.y = 0;
            toward.Normalize();
            // Controlled velocity observations distinguish the two predicates; this
            // is not a claim that the fixture generated a natural detour or crowd.
            human.Agent.isStopped = false;
            human.Agent.velocity = -toward;
            yield return null; // NavMesh applies its requested velocity on the next engine update.
            Assert.That(Vector3.Dot(human.Agent.velocity, toward), Is.LessThan(0));
            human.SimTick((float)SimClock.StepSeconds);
            var away = battle.Commands.ConsumeTelemetry();
            Assert.That(away.HumanSpeedCount, Is.EqualTo(1));
            Assert.That(away.HumanFirstMoveCount, Is.Zero);
            Assert.That(away.HumanMoveOutstanding, Is.EqualTo(1));
            human.Agent.velocity = toward;
            yield return null;
            human.SimTick((float)SimClock.StepSeconds);
            var directed = battle.Commands.ConsumeTelemetry();
            Assert.That(directed.HumanSpeedCount, Is.Zero, "The first-speed observation must not be counted twice.");
            Assert.That(directed.HumanFirstMoveCount, Is.EqualTo(1));
            Assert.That(directed.HumanSpeedToDirectedCount, Is.EqualTo(1));
            Assert.That(directed.HumanMoveOutstanding, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MovementStageActiveTimeExcludesAnObservedPause()
        {
            double start = Time.realtimeSinceStartupAsDouble;
            battle.TogglePause();
            yield return null; // RuntimeDiagnostics observes the public pause transition.
            var activeTime = typeof(BattleCommands).GetMethod("HumanMoveActiveSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activeTime, Is.Not.Null);
            double before = (double)activeTime.Invoke(battle.Commands, new object[] { start, 0d });
            yield return new WaitForSecondsRealtime(.15f);
            double during = (double)activeTime.Invoke(battle.Commands, new object[] { start, 0d });
            Assert.That(during, Is.EqualTo(before).Within(.01), "Observed pause must not age any paired movement stage.");
            battle.TogglePause();
            yield return null;
            var telemetry = battle.Commands.ConsumeTelemetry();
            Assert.That(telemetry.ObservedPauseMilliseconds, Is.GreaterThanOrEqualTo(140));
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
