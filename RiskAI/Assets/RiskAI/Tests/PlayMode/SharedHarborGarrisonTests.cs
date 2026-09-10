using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class SharedHarborGarrisonTests
    {
        Scene previousScene;
        Scene activeScene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;
        float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousMode = BattleSession.ModeForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch;
            previousSeed = BattleSession.SeedForNewMatch;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourMapsRequireALandGuardianAtEveryHarbor()
        {
            foreach (var map in new[] { ScenarioMap.Classic, ScenarioMap.Riverlands, ScenarioMap.Europe, ScenarioMap.NewWorld })
                yield return RunMap(map);
        }

        IEnumerator RunMap(ScenarioMap map)
        {
            BattleSession.MapForNewMatch = map;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.PlayerCountForNewMatch = 2;
            BattleSession.SeedForNewMatch = 23000 + (int)map;
            activeScene = SceneManager.CreateScene("Shared harbor garrison " + map);
            SceneManager.SetActiveScene(activeScene);
            new GameObject("Shared harbor garrison bootstrap").AddComponent<RiskBootstrap>();
            var battle = BattleSession.Current;
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            yield return null;

            var naval = NavalWorld.Current;
            var port = naval.Harbors.FirstOrDefault(h => h.Owner == 1 && h.CanLaunch);
            Assert.That(port, Is.Not.Null, map + " must provide an enemy launchable harbor.");
            var initial = port.Defender;
            if (initial)
            {
                port.ClaimZone.SetDefender(null);
                initial.gameObject.SetActive(false);
            }
            DisableOtherSoldiers(battle);
            var frigate = BattleTestScenario.Ship(naval, 0, ShipKind.Galley, port.Berth);
            TickPort(battle, port);
            Assert.That(port.Owner, Is.EqualTo(PlayerRules.NeutralOwner));
            Assert.That(port.ClaimZone.Guardian, Is.Null);
            Assert.That(frigate.IsGarrison,Is.False);

            var enemyFootman = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, port.Landing);
            TickPort(battle,port);
            Assert.That(port.Owner,Is.EqualTo(1));
            Assert.That(port.ClaimZone.Guardian,Is.SameAs(enemyFootman));

            enemyFootman.TakeDamage(enemyFootman.MaxHealth+1,0);
            var alliedFootman=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,port.Landing);
            TickPort(battle, port);
            Assert.That(port.Owner,Is.EqualTo(0));
            Assert.That(port.ClaimZone.Guardian,Is.SameAs(alliedFootman));

            var commands = new PlayerBuildingCommands(battle);
            battle.Economy.Gold[0] = Harbor.Cost(ShipKind.Galley);
            Assert.That(commands.Execute(0, PlayerBuildingIntent.BuyShip(port.BuildingId, NavalUnitKind.Galley)), Is.Null,
                "A harbor captured by landed troops remains usable through the player command boundary.");
            Assert.That(port.QueueCount, Is.EqualTo(1));

            alliedFootman.TakeDamage(alliedFootman.MaxHealth + 1, 1);
            TickPort(battle, port);
            Assert.That(port.Owner, Is.EqualTo(PlayerRules.NeutralOwner));
            Assert.That(port.ClaimZone.Guardian, Is.Null);
            var transport = BattleTestScenario.Ship(naval, 0, ShipKind.Transport, port.Berth);
            TickPort(battle, port);
            Assert.That(port.Owner, Is.Not.EqualTo(0), "A transport must never capture or hold a harbor by itself.");
            Assert.That(port.ClaimZone.Guardian, Is.Null);

            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(activeScene);
            activeScene = default;
        }

        static void DisableOtherSoldiers(BattleSession battle)
        {
            foreach (var unit in battle.Units.ToArray()) if (unit) unit.gameObject.SetActive(false);
        }

        static void TickPort(BattleSession battle, Harbor port)
        {
            battle.Spatial.Rebuild(battle.Targets, battle.Units);
            if (port.IsImportedPort) port.LinkedTown.SimTick(.1f);
            port.SimTick(.1f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.ModeForNewMatch = previousMode;
            BattleSession.PlayerCountForNewMatch = previousPlayers;
            BattleSession.SeedForNewMatch = previousSeed;
            Time.timeScale = previousTimeScale;
            SceneManager.SetActiveScene(previousScene);
            if (activeScene.IsValid() && activeScene.isLoaded) yield return SceneManager.UnloadSceneAsync(activeScene);
            MapLayout.Configure(previousMap);
            yield return null;
        }
    }
}
