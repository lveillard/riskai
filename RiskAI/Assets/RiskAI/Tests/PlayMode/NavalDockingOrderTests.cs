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
    public sealed class NavalDockingOrderTests
    {
        Scene previousScene, scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers, previousSeed;
        BattleSession battle;
        NavalWorld naval;

        [SetUp]
        public void SaveState()
        {
            previousScene = SceneManager.GetActiveScene(); previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch; previousPlayers = BattleSession.PlayerCountForNewMatch;
            previousSeed = BattleSession.SeedForNewMatch;
        }

        IEnumerator Build(ScenarioMap map)
        {
            BattleSession.MapForNewMatch = map; BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch = 2; BattleSession.SeedForNewMatch = 19031;
            scene = SceneManager.CreateScene("Naval docking order " + map); SceneManager.SetActiveScene(scene);
            new GameObject("Docking bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current; battle.AiEnabled = false; battle.enabled = false; naval = battle.Naval;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            yield return null;
        }

        Harbor EmptyPort()
        {
            var port = naval.Harbors.First(h => h.CanLaunch);
            port.ClaimZone.SetDefender(null);
            foreach (var unit in battle.Units) if (unit) unit.gameObject.SetActive(false);
            port.State.Owner = PlayerRules.NeutralOwner;
            return port;
        }

        static Vector3 Approach(Harbor port, float distance)
        {
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                var point = port.Berth + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                if (SeaNavigation.HasClearance(point) && SeaNavigation.ClearSegment(point, port.Berth)) return point;
            }
            Assert.Fail("Fixture needs a clear sea approach at distance " + distance);
            return default;
        }

        void Tick(Harbor port)
        {
            battle.Spatial.Rebuild(battle.Targets, battle.Units);
            if (port.IsImportedPort) port.LinkedTown.SimTick(.1f);
            port.SimTick(.1f);
        }

        [UnityTest]
        public IEnumerator ExplicitDockingNeverLetsWarshipsCaptureInClassicAndEurope()
        {
            foreach (var map in new[] { ScenarioMap.Classic, ScenarioMap.Europe })
            {
                yield return Build(map);
                var port = EmptyPort();
                var near = Approach(port, 6.75f);
                var ship = BattleTestScenario.Ship(naval, 0, ShipKind.Galley, near);
                Tick(port);
                Assert.That(port.ClaimZone.Guardian, Is.Null, "Passive capture keeps its existing 6m radius.");
                ship.SailToHarbor(port);
                Assert.That(ship.LastActionError, Is.Null);
                Assert.That(Vector3.Distance(ship.transform.position, near), Is.LessThan(.001f), "Submitting must not move a candidate before shared ranking.");
                Tick(port);
                Assert.That(port.ClaimZone.Guardian, Is.Null);
                Assert.That(port.Owner, Is.EqualTo(PlayerRules.NeutralOwner));
                Assert.That(ship.IsGarrison,Is.False,"A warship cannot occupy a land capture slot.");

                ship.TakeDamage(ship.MaxHealth + 1, 1);
                port.State.Owner = PlayerRules.NeutralOwner;
                var far = Approach(port, 14);
                var arriving = BattleTestScenario.Ship(naval, 0, ShipKind.Galley, far);
                arriving.SailToHarbor(port);
                Assert.That(arriving.LastActionError, Is.Null);
                Tick(port);
                Assert.That(arriving.IsGarrison, Is.False, "A distant order cannot teleport directly to the port.");
                for (int tick = 0; tick < 120; tick++)
                {
                    arriving.SimTick(.1f);
                    Tick(port);
                }
                Assert.That(arriving.IsGarrison, Is.False);
                Assert.That(port.ClaimZone.Guardian,Is.Null);
                Assert.That(port.Owner,Is.EqualTo(PlayerRules.NeutralOwner));
                SceneManager.SetActiveScene(previousScene); yield return SceneManager.UnloadSceneAsync(scene); scene = default;
            }
        }

        [UnityTest]
        public IEnumerator StopMoveAndDeathLeaveLandCaptureSlotEmpty()
        {
            yield return Build(ScenarioMap.Classic);
            var port = EmptyPort(); var near = Approach(port, 6.75f);
            var ship = BattleTestScenario.Ship(naval, 0, ShipKind.Galley, near);
            ship.SailToHarbor(port); ship.Stop(); Tick(port);
            Assert.That(port.ClaimZone.Guardian, Is.Null);
            ship.SailToHarbor(port); ship.MoveTo(near); Tick(port);
            Assert.That(ship.LastActionError, Is.Null);
            Assert.That(port.ClaimZone.Guardian, Is.Null, "A new accepted move replaces the harbor intent.");

            port.State.Owner = 1;
            ship.SailToHarbor(port);
            var allyPoint = Vector3.Lerp(port.Berth, near, 3f / 6.75f);
            var ally = BattleTestScenario.Ship(naval, 1, ShipKind.Galley, allyPoint);
            Tick(port);
            Assert.That(port.ClaimZone.Guardian, Is.Null);
            Assert.That(Vector3.Distance(ship.transform.position, near), Is.LessThan(.001f), "Losing candidates must never snap.");
            ship.SailToHarbor(port); Tick(port);
            Assert.That(port.ClaimZone.Guardian, Is.Null);
            ally.TakeDamage(ally.MaxHealth + 1, 0); port.State.Owner = PlayerRules.NeutralOwner;
            ship.SailToHarbor(port); ship.TakeDamage(ship.MaxHealth + 1, 1); Tick(port);
            Assert.That(port.ClaimZone.Guardian, Is.Null);
        }

        [UnityTest]
        public IEnumerator DockingMarginCannotTeleportAcrossAnIsthmus()
        {
            yield return Build(ScenarioMap.Classic);
            var port = EmptyPort();
            var ship = BattleTestScenario.Ship(naval, 0, ShipKind.Galley, port.Berth);
            const int size = 65;
            var map = new ImportedMapData { width = size, height = size, originX = -8, originZ = -8, cellSize = .25f,
                heightSamples = new float[size * size], waterSamples = new float[size * size], landSamples = new int[size * size],
                tileSamples = new int[size * size], cities = new ImportedMapData.City[0], countries = new[] { new ImportedMapData.Country { name = "barrier" } } };
            for (int i = 0; i < size * size; i++) { map.heightSamples[i] = -1; map.waterSamples[i] = -.24f; }
            for (int z = 0; z < size; z++) { map.heightSamples[z * size + 32] = 1; map.landSamples[z * size + 32] = 1; }
            var imported = typeof(MapLayout).GetField("<Imported>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var berth = typeof(Harbor).GetProperty("Berth");
            var oldMap = imported.GetValue(null); var oldBerth = port.Berth;
            try
            {
                imported.SetValue(null, map); berth.SetValue(port, new Vector3(3.5f, -.24f, 0));
                ship.transform.position = new Vector3(-3.5f, -.24f, 0); var start = ship.transform.position;
                Assert.That(SeaNavigation.HasClearance(start) && SeaNavigation.HasClearance(port.Berth), Is.True);
                Assert.That(SeaNavigation.ClearSegment(start, port.Berth), Is.False);
                ship.SailToHarbor(port); Tick(port);
                Assert.That(ship.LastActionError, Is.Not.Null);
                Assert.That(port.ClaimZone.Guardian, Is.Null);
                Assert.That(ship.transform.position, Is.EqualTo(start));
            }
            finally { imported.SetValue(null, oldMap); berth.SetValue(port, oldBerth); }
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            BattleSession.MapForNewMatch = previousMap; BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.PlayerCountForNewMatch = previousPlayers; BattleSession.SeedForNewMatch = previousSeed;
            SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            MapLayout.Configure(previousMap);
        }
    }
}
