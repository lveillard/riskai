using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    /// <summary>Plan §6 queue acceptance: Shift appends, a click replaces, and a queued capture or attack survives.</summary>
    public sealed class OrderQueueAcceptanceTests
    {
        Scene previous, scene;
        BattleSession battle;
        NavalWorld naval;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ReleaseSimulatedModifiers();
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Order queue acceptance");
            SceneManager.SetActiveScene(scene);
            new GameObject("Order queue bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            naval = NavalWorld.Current;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShiftMoveTwiceThenShiftCaptureKeepsTheCityForLast()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var town = battle.Towns.First(other => other.State.Owner != 0 && NavMesh.SamplePosition(other.ClaimPoint, out _, 3f, NavMesh.AllAreas));
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var first = Walkable(home.Rally, 6f);
            var second = Walkable(first, 6f);
            Submit(unit, UnitCommandKind.Move, first, append: true);
            Submit(unit, UnitCommandKind.Move, second, append: true);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Capture, town.ClaimPoint.x, town.ClaimPoint.y, town.ClaimPoint.z,
                append: true, structureId: town.BuildingId.LocalId, structureKind: BuildingKind.Settlement)), Is.True);
            Step();
            Assert.That(unit.OrderLegCount, Is.EqualTo(3));
            Assert.That(unit.OrderLegKind(0), Is.EqualTo(UnitCommandKind.Move));
            Assert.That(unit.OrderLegKind(2), Is.EqualTo(UnitCommandKind.Capture));
            Assert.That(unit.Orders.Count, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShipShiftMoveTwiceThenShiftAttackKeepsTheAttack()
        {
            var port = naval.Harbors.First(harbor => harbor.Owner == 0);
            var ship = BattleTestScenario.Ship(naval, 0, UnitKind.Frigate, port.Berth);
            var enemyPort = naval.Harbors.First(harbor => harbor != port);
            var enemy = BattleTestScenario.Ship(naval, 1, UnitKind.Transport, enemyPort.Berth);
            var first = SeaPoint(port.Berth, 8f);
            var second = SeaPoint(first, 8f);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, ship.EntityId, UnitCommandKind.Move, first.x, first.y, first.z, append: true)), Is.True);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, ship.EntityId, UnitCommandKind.Move, second.x, second.y, second.z, append: true)), Is.True);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, ship.EntityId, UnitCommandKind.Attack, enemy.transform.position.x, enemy.transform.position.y, enemy.transform.position.z, enemy.EntityId, append: true)), Is.True);
            Step();
            Assert.That(ship.OrderLegCount, Is.EqualTo(3));
            Assert.That(ship.OrderLegKind(2), Is.EqualTo(UnitCommandKind.Attack));
            Assert.That(ship.Orders.Count, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShiftAttackAndFollowAppendAndAClickWithoutShiftReplaces()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var ally = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, home.Rally + Vector3.right * 2f);
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Rally + Vector3.forward * 4f);
            var first = Walkable(home.Rally, 5f);
            Submit(unit, UnitCommandKind.Move, first, append: true);
            Step();
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Attack, targetId: enemy.EntityId, append: true)), Is.True);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Follow, targetId: ally.EntityId, append: true)), Is.True);
            Step();
            Assert.That(unit.Orders.Count, Is.EqualTo(2));
            Assert.That(unit.OrderLegKind(unit.OrderLegCount - 1), Is.EqualTo(UnitCommandKind.Follow));

            var replacement = Walkable(first, 5f);
            Submit(unit, UnitCommandKind.Move, replacement, append: false);
            Step();
            Assert.That(unit.Orders.Count, Is.EqualTo(0), "a click without Shift replaces the queue");
            Assert.That(unit.OrderLegCount, Is.EqualTo(1));
            Assert.That(unit.OrderLegKind(0), Is.EqualTo(UnitCommandKind.Move));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ATargetThatDiesMidQueueAdvances()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Rally + Vector3.forward * 4f);
            var first = Walkable(home.Rally, 6f);
            Submit(unit, UnitCommandKind.Move, first, append: true);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Attack, targetId: enemy.EntityId, append: true)), Is.True);
            Step();
            enemy.TakeDamage(enemy.MaxHealth + 1, 0);
            Assert.That(unit.Agent.Warp(unit.Agent.destination), Is.True);
            for (int i = 0; i < 12 && unit.Orders.Count > 0; i++) Step();
            Assert.That(unit.Orders.Count, Is.EqualTo(0));
            Assert.That(unit.CurrentTarget, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmbarkRestoresThePassengerQueueIntact()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Landing);
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing + Vector3.right * 3f);
            var point = Walkable(home.Landing, 4f);
            Submit(soldier, UnitCommandKind.Move, point, append: true);
            Step();
            Assert.That(battle.Commands.Submit(new UnitCommand(0, soldier.EntityId, UnitCommandKind.Attack, targetId: enemy.EntityId, append: true)), Is.True);
            Step();
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            Assert.That(transport.UnloadAt(home.Landing), Is.True, transport.LastActionError);
            Step();
            Assert.That(soldier.OrderLegCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(soldier.OrderLegKind(0), Is.EqualTo(UnitCommandKind.Move));
            Assert.That(soldier.OrderLegKind(1), Is.EqualTo(UnitCommandKind.Attack));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmbarkApproachKeepsQueuedPassengerOrders()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Landing);
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing + Vector3.right * 3f);
            var point = Walkable(home.Landing, 4f);
            Submit(soldier, UnitCommandKind.Move, point, append: true);
            Step();
            Assert.That(battle.Commands.Submit(new UnitCommand(0, soldier.EntityId, UnitCommandKind.Attack, targetId: enemy.EntityId, append: true)), Is.True);
            Step();
            Assert.That(naval.TryOrderEmbark(transport, soldier, out var error), Is.True, error);
            Step();
            if (soldier.gameObject.activeInHierarchy)
                Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            Assert.That(transport.UnloadAt(home.Landing), Is.True, transport.LastActionError);
            Step();
            Assert.That(soldier.OrderLegCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(soldier.OrderLegKind(0), Is.EqualTo(UnitCommandKind.Move));
            Assert.That(soldier.OrderLegKind(1), Is.EqualTo(UnitCommandKind.Attack));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseRejectsOrdersAndDoesNotDrain()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var point = Walkable(home.Rally, 6f);
            battle.TogglePause();
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, point.x, point.y, point.z)), Is.False);
            Assert.That(battle.Commands.LastRejection, Does.Contain("detenida"));
            battle.TogglePause();
            Submit(unit, UnitCommandKind.Move, point, append: true);
            Step();
            var at = unit.transform.position;
            battle.TogglePause();
            battle.World.Tick(1f);
            Assert.That(unit.transform.position, Is.EqualTo(at));
            Assert.That(unit.OrderLegCount, Is.GreaterThan(0));
            battle.TogglePause();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MixedLandAndSeaSelectionQueuesTogether()
        {
            var port = naval.Harbors.First(harbor => harbor.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, port.Landing);
            var ship = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, port.Berth);
            var controller = Object.FindFirstObjectByType<RtsController>();
            controller.SelectOnly(unit);
            controller.SelectShip(ship, true);
            var first = Walkable(port.Landing, 6f);
            controller.OrderAt(first, false);
            Step();
            controller.ToggleQueueOrders();
            controller.OrderAt(Walkable(first, 5f), false);
            Step();
            Assert.That(unit.Orders.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(ship.Orders.Count, Is.GreaterThanOrEqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MoveCursorMovesOntoAnEnemyAndAttackCursorAttacks()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var enemy = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Rally + Vector3.right * 14f);
            var controller = Object.FindFirstObjectByType<RtsController>();
            controller.SelectOnly(unit);
            // The focus jump eases for about half a second. Pause so a warm suite
            // (short frames) and a cold one (a hitch that simulates many ticks)
            // leave the enemy in the same place, then click only a point the HUD
            // does not swallow. An empty leg buffer reads as Move, so the count is asserted too.
            if (!battle.Paused) battle.TogglePause();
            controller.Focus(enemy.transform.position);
            var cam = (Camera)typeof(RtsController).GetField("cam", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Vector2 pointer = default;
            bool aimed = false;
            for (int i = 0; i < 45 && !aimed; i++)
            {
                yield return null;
                aimed = TryAim(controller, cam, enemy, out pointer);
            }
            if (battle.Paused) battle.TogglePause();
            Assert.That(battle.Paused, Is.False, "the match must be running before a cursor issues an order");
            Assert.That(aimed, Is.True, "the battle camera must show the enemy on a point the HUD does not cover");
            EnsureReplaceOrders(controller);
            controller.ArmMove();
            Assert.That(controller.MoveCursor, Is.True, "the soldier must stay selected so the move cursor can arm");
            Armed(controller, pointer);
            Step();
            Assert.That(unit.CurrentTarget, Is.Not.SameAs(enemy));
            Assert.That(unit.OrderLegCount, Is.GreaterThan(0));
            Assert.That(unit.OrderLegKind(0), Is.EqualTo(UnitCommandKind.Move));
            Assert.That(TryAim(controller, cam, enemy, out pointer), Is.True, "the pointer must still sit on the enemy, clear of the HUD");
            EnsureReplaceOrders(controller);
            controller.ArmAttack();
            Assert.That(controller.AttackCursor, Is.True, "the soldier must stay selected so the attack cursor can arm");
            Armed(controller, pointer);
            Step();
            Assert.That(unit.CurrentTarget, Is.SameAs(enemy),
                $"attack cursor should target the enemy. label={unit.OrderLabel} legs={unit.OrderLegCount} " +
                $"kind={(unit.OrderLegCount > 0 ? unit.OrderLegKind(0).ToString() : "none")} err={unit.LastMoveError} " +
                $"reject={battle.Commands.LastRejection} alive={enemy.IsAlive} queue={controller.QueueOrders} selected={controller.Selection.Count}");
            yield return null;
        }

        bool TryAim(RtsController controller, Camera cam, CombatTarget enemy, out Vector2 pointer)
        {
            pointer = default;
            var bounds = RtsPicking.Bounds(cam, enemy);
            if (bounds.width <= 1f) return false;
            var points = new[]
            {
                bounds.center,
                new Vector2(bounds.center.x, Mathf.Min(bounds.yMax - 2f, bounds.center.y + bounds.height * .25f)),
                new Vector2(bounds.center.x, Mathf.Max(bounds.yMin + 2f, bounds.center.y - bounds.height * .25f)),
                new Vector2(Mathf.Max(bounds.xMin + 2f, bounds.center.x - bounds.width * .25f), bounds.center.y),
                new Vector2(Mathf.Min(bounds.xMax - 2f, bounds.center.x + bounds.width * .25f), bounds.center.y)
            };
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (point.x < 0f || point.y < 0f || point.x >= Screen.width || point.y >= Screen.height) continue;
                if (controller.OverHud(point)) continue;
                if (RtsPicking.Target(battle, cam, point, -1) != enemy) continue;
                pointer = point;
                return true;
            }
            return false;
        }

        void Submit(Soldier unit, UnitCommandKind kind, Vector3 point, bool append) =>
            Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, kind, point.x, point.y, point.z, append: append)), Is.True);

        Vector3 Walkable(Vector3 origin, float distance)
        {
            var directions = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.forward + Vector3.right };
            for (int i = 0; i < directions.Length; i++)
                if (NavMesh.SamplePosition(origin + directions[i].normalized * distance, out var hit, 3f, NavMesh.AllAreas) &&
                    Vector3.Distance(hit.position, origin) > 2f)
                    return hit.position;
            Assert.Fail("Need a walkable point near " + origin);
            return origin;
        }

        Vector3 SeaPoint(Vector3 origin, float distance)
        {
            var directions = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            for (int i = 0; i < directions.Length; i++)
            {
                var point = origin + directions[i] * distance;
                point.y = -.24f;
                if (SeaNavigation.HasClearance(point) && SeaNavigation.TryBuildPath(origin, point, out _)) return point;
            }
            Assert.Fail("Need a sea point near " + origin);
            return origin;
        }

        void Step() => battle.Clock.Advance(SimClock.StepSeconds, false, battle.World.Tick);

        static void Armed(RtsController controller, Vector2 screen) =>
            typeof(RtsController).GetMethod("ExecuteArmedPointer", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { screen });

        /// <summary>
        /// The shared keyboard survives scene unload. A Shift left down by this fixture, or by an
        /// earlier one, would make the next test append instead of replace.
        /// </summary>
        static void EnsureReplaceOrders(RtsController controller)
        {
            ReleaseSimulatedModifiers();
            if (controller.QueueOrdersArmed) controller.ToggleQueueOrders();
            Assert.That(controller.QueueOrders, Is.False, "Shift and Encolar must be up so the click replaces the order.");
        }

        static void ReleaseSimulatedModifiers()
        {
            for (var i = 0; i < InputSystem.devices.Count; i++)
            {
                if (InputSystem.devices[i] is not Keyboard keyboard) continue;
                InputSystem.ResetDevice(keyboard);
                InputSystem.QueueStateEvent(keyboard, default(KeyboardState));
            }
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator ShiftDuringEmbarkRestoresTheEarlierPlanAndTheAddedOrder()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var inland = (home.Landing - home.Berth);
            inland.y = 0;
            if (inland.sqrMagnitude < .01f) inland = Vector3.forward;
            Assert.That(NavMesh.SamplePosition(home.Landing + inland.normalized * 14f, out var spawn, 8f, NavMesh.AllAreas), Is.True);
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, spawn.position);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var pointA = Walkable(spawn.position, 5f);
            Submit(soldier, UnitCommandKind.Move, pointA, false);
            Step();
            Assert.That(naval.TryOrderEmbark(transport, soldier, out var error), Is.True, error);
            Step();
            Assert.That(soldier.gameObject.activeInHierarchy, Is.True, "the soldier is still walking to the transport");
            var pointC = Walkable(pointA, 6f);
            Submit(soldier, UnitCommandKind.Move, pointC, true);
            Step();
            Assert.That(soldier.Orders.StashCount, Is.GreaterThanOrEqualTo(2), "Shift keeps the pre-embark plan and adds the new order");
            Assert.That(soldier.Agent.Warp(home.Landing), Is.True);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            Assert.That(transport.UnloadAt(home.Landing), Is.True, transport.LastActionError);
            Step();
            Assert.That(soldier.OrderLegCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(Vector3.Distance(soldier.OrderLegPoint(0), pointA), Is.LessThan(1.5f), "A is restored first");
            Assert.That(Vector3.Distance(soldier.OrderLegPoint(1), pointC), Is.LessThan(1.5f), "C follows A");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReplacingMoveDuringEmbarkClearsTheStash()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var inland = (home.Landing - home.Berth);
            inland.y = 0;
            if (inland.sqrMagnitude < .01f) inland = Vector3.forward;
            Assert.That(NavMesh.SamplePosition(home.Landing + inland.normalized * 14f, out var spawn, 8f, NavMesh.AllAreas), Is.True);
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, spawn.position);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var pointA = Walkable(spawn.position, 5f);
            Submit(soldier, UnitCommandKind.Move, pointA, false);
            Step();
            Assert.That(naval.TryOrderEmbark(transport, soldier, out var error), Is.True, error);
            Step();
            var pointB = Walkable(pointA, 6f);
            Submit(soldier, UnitCommandKind.Move, pointB, false);
            Step();
            Assert.That(soldier.Orders.StashCount, Is.EqualTo(0), "a replacing order cancels the voyage plan");
            Assert.That(soldier.OrderLegKind(0), Is.EqualTo(UnitCommandKind.Move));
            Assert.That(Vector3.Distance(soldier.OrderLegPoint(0), pointB), Is.LessThan(1.5f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CommandResultRingKeepsAResultPastTheOld64()
        {
            var home = battle.Towns.First(town => town.State.Owner == 0);
            var unit = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Rally);
            var point = Walkable(home.Rally, 6f);
            var first = battle.Commands.SubmitResult(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, point.x, point.y, point.z));
            Assert.That(first.Accepted, Is.True, battle.Commands.LastRejection);
            for (int i = 0; i < 80; i++)
                Assert.That(battle.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, point.x, point.y, point.z, append: true)), Is.True, battle.Commands.LastRejection);
            Assert.That(battle.Commands.TryGetResult(first.CommandId, out var stored), Is.True);
            Assert.That(stored.Accepted, Is.True, "a result older than 64 submits is still the accept, not an eviction");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ReleaseSimulatedModifiers();
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller && controller.QueueOrdersArmed) controller.ToggleQueueOrders();
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
