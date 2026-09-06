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
    public sealed class PartialPathCommandTests
    {
        Scene previousScene;
        Scene scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers;
        int previousSeed;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousMode = BattleSession.ModeForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch;
            previousSeed = BattleSession.SeedForNewMatch;
            BattleSession.MapForNewMatch = ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.PlayerCountForNewMatch = 2;
            BattleSession.SeedForNewMatch = 43019;

            scene = SceneManager.CreateScene("Partial path command");
            SceneManager.SetActiveScene(scene);
            new GameObject("Partial path command bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisconnectedIslandDestinationReportsPathFailureOnceInsteadOfArrivalOrRetrying()
        {
            var naval = NavalWorld.Current;
            var mainland = naval.Harbors.First(harbor => !harbor.IsIsland && harbor.State.Owner == 0);
            var island = naval.Harbors.First(harbor => harbor.IsIsland);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, mainland.Landing);
            Assert.That(NavMesh.SamplePosition(island.Landing, out var islandPoint, 2f, NavMesh.AllAreas), Is.True);

            var initialPath = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(unit.transform.position, islandPoint.position, NavMesh.AllAreas, initialPath), Is.True);
            Assert.That(initialPath.status, Is.EqualTo(NavMeshPathStatus.PathPartial));
            Assert.That(initialPath.corners.Length, Is.GreaterThan(0));

            // Put the agent at the closest valid mainland endpoint. From there it is
            // stationary, yet its requested island destination still has a partial path.
            Vector3 reachableCorner = initialPath.corners[initialPath.corners.Length - 1];
            Assert.That(Vector3.Distance(reachableCorner, islandPoint.position), Is.GreaterThan(3f));
            Assert.That(unit.Agent.Warp(reachableCorner), Is.True);
            yield return null;

            Assert.That(unit.TryMoveTo(islandPoint.position, false, false), Is.True);
            yield return null;
            float pathDeadline=Time.realtimeSinceStartup+3f;
            while(unit.Agent.pathPending&&Time.realtimeSinceStartup<pathDeadline)yield return null;
            Assert.That(unit.Agent.pathPending, Is.False);
            // The sim may already have reported and cleared the exhausted partial path.
            // Initial CalculatePath above establishes the disconnected destination.

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!unit.IsIdle && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.That(unit.LastMoveError, Is.Not.Null.And.Not.Empty);
            Assert.That(unit.LastMoveError, Does.Contain("camino"));
            Assert.That(Vector3.Distance(unit.transform.position, islandPoint.position), Is.GreaterThan(3f), "A partial route must not be treated as ordinary arrival.");
            Assert.That(unit.IsIdle, Is.True, "The failed order must finish instead of repeatedly assigning the same partial route.");

            string failure = unit.LastMoveError;
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(unit.LastMoveError, Is.EqualTo(failure));
            Assert.That(unit.IsIdle, Is.True, "A completed failed command must not resume itself on later ticks.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.ModeForNewMatch = previousMode;
            BattleSession.PlayerCountForNewMatch = previousPlayers;
            BattleSession.SeedForNewMatch = previousSeed;
            SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
