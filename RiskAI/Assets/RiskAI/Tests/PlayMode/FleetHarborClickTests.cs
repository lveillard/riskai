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
    /// <summary>A fleet right-click on an enemy unit inside a harbour circle attacks that unit.</summary>
    public sealed class FleetHarborClickTests
    {
        Scene previous, scene;
        BattleSession battle;
        NavalWorld naval;
        int previousSeed;
        const int FixtureSeed = 7031;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            previousSeed = BattleSession.SeedForNewMatch;
            BattleSession.SeedForNewMatch = FixtureSeed;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Fleet harbour click");
            SceneManager.SetActiveScene(scene);
            new GameObject("Fleet click bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            naval = NavalWorld.Current;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrigateRightClickOnASoldierInThePortCircleAttacksFromRange()
        {
            var port = naval.Harbors.First(harbor => harbor.Owner == 0 && !harbor.IsIsland);
            var frigate = BattleTestScenario.Ship(naval, 0, UnitKind.Frigate, port.Berth);
            frigate.Stop();
            frigate.transform.position = port.Berth;
            var soldier = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, port.Landing);
            soldier.HoldPosition();
            foreach (var tower in battle.Towers.ToArray()) if (tower) tower.gameObject.SetActive(false);
            foreach (var ship in naval.Ships.ToArray()) if (ship && ship != frigate) ship.gameObject.SetActive(false);
            foreach (var unit in battle.Units.ToArray()) if (unit && unit != soldier) unit.gameObject.SetActive(false);

            float range = UnitCatalog.Get(UnitKind.Frigate).Weapon.Range;
            Assert.That(Vector3.Distance(frigate.transform.position, soldier.transform.position), Is.LessThanOrEqualTo(range + 8f));

            var controller = Object.FindFirstObjectByType<RtsController>();
            controller.enabled = true;
            controller.SelectShip(frigate);
            if (controller.QueueOrdersArmed) controller.ToggleQueueOrders();
            controller.Focus(soldier.transform.position);
            var cam = (Camera)typeof(RtsController).GetField("cam", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Vector2 pointer = default;
            bool aimed = false;
            for (int i = 0; i < 45 && !aimed; i++)
            {
                yield return null;
                aimed = Aim(controller, cam, soldier, port, out pointer);
            }
            Assert.That(aimed, Is.True, "The click must hit the soldier and the harbour together, which used to dock the fleet.");
            typeof(RtsController).GetMethod("ContextAction", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { pointer });
            battle.Clock.Advance(SimClock.StepSeconds, false, battle.World.Tick);

            Assert.That(frigate.CurrentTarget, Is.SameAs(soldier));
            Assert.That(frigate.OrderLegCount, Is.GreaterThan(0));
            Assert.That(frigate.OrderLegKind(0), Is.EqualTo(UnitCommandKind.Attack));
            Assert.That(Vector3.Distance(frigate.transform.position, port.Berth), Is.LessThan(2f), "An in-range attack does not sail off toward the berth.");
            yield return null;
        }

        bool Aim(RtsController controller, Camera cam, CombatTarget soldier, Harbor port, out Vector2 pointer)
        {
            pointer = default;
            if (!cam) return false;
            var bounds = RtsPicking.Bounds(cam, soldier);
            if (bounds.width <= 1f) return false;
            var harbor = cam.WorldToScreenPoint(port.transform.position);
            var samples = new[]
            {
                bounds.center,
                new Vector2(bounds.center.x, bounds.yMin + 2f),
                new Vector2(bounds.center.x, bounds.yMax - 2f),
                (Vector2)harbor
            };
            for (int i = 0; i < samples.Length; i++)
            {
                var point = samples[i];
                if (point.x < 0f || point.y < 0f || point.x >= Screen.width || point.y >= Screen.height) continue;
                if (controller.OverHud(point)) continue;
                if (RtsPicking.Target(battle, cam, point, -1) != soldier) continue;
                if (RtsPicking.Harbor(battle, cam, point) != port) continue;
                pointer = point;
                return true;
            }
            return false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.SeedForNewMatch = previousSeed;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
