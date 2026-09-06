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
    public sealed class CommandFeedbackTests
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
            BattleSession.SeedForNewMatch = 10412;
            scene = SceneManager.CreateScene("Command feedback");
            SceneManager.SetActiveScene(scene);
            new GameObject("Command feedback bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MobileMoveAppliesOnTheNextSimulationTickAndSetsTheAgentDestination()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var destination = ClearDestination(home.Rally);
            long applied = battle.Commands.AppliedCount;

            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move,
                destination.x, destination.y, destination.z)), Is.True);
            Assert.That(battle.Commands.PendingCount, Is.EqualTo(1));

            Step();

            Assert.That(battle.Commands.PendingCount, Is.Zero);
            Assert.That(battle.Commands.AppliedCount, Is.EqualTo(applied + 1));
            Assert.That(unit.IsIdle, Is.False);
            Assert.That(Vector3.Distance(unit.Agent.destination, destination), Is.LessThan(.15f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GarrisonCommandRejectsImmediatelyWithAReason()
        {
            var garrison = battle.Towns.First(town => town.State.Owner == 0).Defender;
            long rejected = battle.Commands.RejectedCount;

            Assert.That(battle.Commands.Submit(new UnitCommand(0, garrison.EntityId, UnitCommandKind.Move,
                garrison.transform.position.x + 4, garrison.transform.position.y, garrison.transform.position.z)), Is.False);

            Assert.That(battle.Commands.PendingCount, Is.Zero);
            Assert.That(battle.Commands.RejectedCount, Is.EqualTo(rejected + 1));
            Assert.That(battle.Commands.LastRejection, Is.Not.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OffNavMeshDestinationRejectsWhenTheQueuedCommandIsApplied()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            long applied = battle.Commands.AppliedCount;
            long rejected = battle.Commands.RejectedCount;
            var outside = home.Rally + new Vector3(10000, 0, 10000);

            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move,
                outside.x, outside.y, outside.z)), Is.True);

            Step();

            Assert.That(battle.Commands.PendingCount, Is.Zero);
            Assert.That(battle.Commands.AppliedCount, Is.EqualTo(applied));
            Assert.That(battle.Commands.RejectedCount, Is.EqualTo(rejected + 1));
            Assert.That(battle.Commands.LastRejection, Is.Not.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PatrolToOffNavMeshDestinationIsRejectedInsteadOfCountedAsApplied()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            long applied = battle.Commands.AppliedCount;
            long rejected = battle.Commands.RejectedCount;
            var outside = home.Rally + new Vector3(10000, 0, 10000);

            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Patrol,
                outside.x, outside.y, outside.z)), Is.True);

            Step();

            Assert.That(battle.Commands.PendingCount, Is.Zero);
            Assert.That(battle.Commands.AppliedCount, Is.EqualTo(applied));
            Assert.That(battle.Commands.RejectedCount, Is.EqualTo(rejected + 1));
            Assert.That(battle.Commands.LastRejection, Is.Not.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FormationSkipsGarrisonsAndStillMovesMobileSoldiers()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var garrison = home.Defender;
            var garrisonPosition = garrison.transform.position;
            var mobile = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var destination = ClearDestination(home.Rally);
            long applied = battle.Commands.AppliedCount;
            long rejected = battle.Commands.RejectedCount;

            BattleSession.GiveFormation(new[] { garrison, mobile }, destination, false, false);
            Assert.That(battle.Commands.PendingCount, Is.EqualTo(1));

            Step();

            Assert.That(battle.Commands.AppliedCount, Is.EqualTo(applied + 1));
            Assert.That(battle.Commands.RejectedCount, Is.EqualTo(rejected));
            Assert.That(mobile.IsIdle, Is.False);
            Assert.That(Vector3.Distance(mobile.Agent.destination, destination), Is.LessThan(.15f));
            Assert.That(Vector3.Distance(garrison.transform.position, garrisonPosition), Is.LessThan(.01f));
            yield return null;
        }

        Vector3 ClearDestination(Vector3 origin)
        {
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            for (int i = 0; i < directions.Length; i++)
                if (NavMesh.SamplePosition(origin + directions[i] * 7, out var hit, 2, NavMesh.AllAreas) &&
                    Vector3.Distance(hit.position, origin) > 3)
                    return hit.position;
            Assert.Fail("The authored home rally needs a second walkable point for command testing.");
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