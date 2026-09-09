using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class RuntimeCommandProbeFixtureTests
    {
        Scene previousScene, scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch = ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch = 2;
            previousScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Runtime probe cohort fixture");
            SceneManager.SetActiveScene(scene);
            new GameObject("Probe test bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            battle.enabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            yield return null;
        }

        void RemoveBlueOwnership()
        {
            foreach (var town in battle.Towns) town.State.Owner = 1;
            foreach (var harbor in battle.Naval.Harbors) harbor.State.Owner = 1;
        }

        IList Prepare(out string error)
        {
            var method = typeof(RuntimeCommandProbe).GetMethod("SpawnPlayerArchers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { battle, 1, null };
            var result = (IList)method.Invoke(null, arguments);
            error = (string)arguments[2];
            return result;
        }

        [UnityTest]
        public IEnumerator SurvivingMobileIsTrackedWithoutOwningCitiesOrCreatingBlueRecruits()
        {
            var position = battle.Towns.First(t => t.State.Owner == 0).Rally;
            var survivor = battle.Spawn(0, UnitKind.Archer, position);
            var disabled = battle.Spawn(0, UnitKind.Archer, position + Vector3.right * 2);
            Assert.That(survivor && disabled, Is.True);
            disabled.enabled = false;
            RemoveBlueOwnership();
            int blueBefore = battle.Units.Count(u => u && u.Team == 0);

            var tracked = Prepare(out var error);

            Assert.That(error, Is.Null);
            Assert.That(tracked.Count, Is.EqualTo(1), "Disabled troops and surviving garrisons are not mobile probe subjects.");
            Assert.That(tracked[0].GetType().GetField("Soldier").GetValue(tracked[0]), Is.SameAs(survivor));
            Assert.That(tracked[0].GetType().GetField("EntityId").GetValue(tracked[0]), Is.EqualTo(survivor.EntityId));
            Assert.That(battle.Units.Count(u => u && u.Team == 0), Is.EqualTo(blueBefore), "Fallback must not resurrect a faction by spawning recruits without cities.");
            Assert.That(battle.Towns.All(t => t.State.Owner == 1), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EliminatedPlayerHasExplicitFailureAndIsNotResurrected()
        {
            RemoveBlueOwnership();
            foreach (var soldier in battle.Units) if (soldier && soldier.Team == 0) soldier.gameObject.SetActive(false);
            foreach (var ship in battle.Naval.Ships) if (ship && ship.Team == 0) ship.gameObject.SetActive(false);
            int blueBefore = battle.Units.Count(u => u && u.Team == 0);

            var tracked = Prepare(out var error);

            Assert.That(tracked.Count, Is.Zero);
            Assert.That(error, Is.EqualTo("player-zero-eliminated"));
            Assert.That(battle.Units.Count(u => u && u.Team == 0), Is.EqualTo(blueBefore));
            Assert.That(battle.Units.Any(u => u && u.Team == 0 && u.IsAlive), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SurvivingGarrisonWithoutMobileTroopsIsNotReportedAsElimination()
        {
            Assert.That(battle.Units.Any(u => u && u.Team == 0 && u.IsAlive && u.IsGarrison), Is.True);
            RemoveBlueOwnership();

            var tracked = Prepare(out var error);

            Assert.That(tracked.Count, Is.Zero);
            Assert.That(error, Is.EqualTo("player-zero-no-eligible-land-cohort"));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.PlayerCountForNewMatch = previousPlayers;
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
