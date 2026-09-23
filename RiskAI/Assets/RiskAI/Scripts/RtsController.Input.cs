using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public sealed partial class RtsController
    {
        Vector2 areaPointer;
        bool areaPointerActive;

        internal bool AcceptsDirectPointerInput => session && EffectiveFocus && session.Winner < 0 && !HelpVisible && !ScoreboardVisible;
        internal bool BlocksWorldInput(Vector2 screen) => !InsideScreen(screen) || OverHud(screen);
        internal bool ShiftHeld => Shift;

        /// <summary>Device cancellation must discard a gesture before its synthetic release reaches input.</summary>
        public void CancelDirectPointerInput()
        {
            inputRouter?.Cancel();
            CancelAreaSelection();
        }

        internal void PrepareDirectPointerInput()
        {
            CameraDragging=false;secondaryGesture.Cancel();previousMouse=Pointer;CancelAreaSelection();
        }

        internal void BeginAreaSelection(Vector2 screen)
        {
            if (BlocksWorldInput(screen) || OrderCursor) return;
            DragStart = areaPointer = screen; Dragging = false; areaPointerActive = true;
        }

        internal void UpdateAreaSelection(Vector2 screen)
        {
            if (!areaPointerActive) return;
            areaPointer = screen; Dragging = true;
        }

        internal void EndAreaSelection(Vector2 screen, bool append)
        {
            if (!areaPointerActive) return;
            areaPointer = screen;
            var rect = Rect.MinMaxRect(Mathf.Min(DragStart.x, areaPointer.x), Screen.height - Mathf.Max(DragStart.y, areaPointer.y),
                Mathf.Max(DragStart.x, areaPointer.x), Screen.height - Mathf.Min(DragStart.y, areaPointer.y));
            var units = session.Units.Where(unit => !StrategicMapView.Active && IsSelectableSoldier(unit) && !unit.IsGarrison && InSelection(unit, rect)).ToList();
            var ships = NavalWorld.Current ? NavalWorld.Current.Ships.Where(ship => !StrategicMapView.Active && IsSelectableShip(ship) && InSelection(ship, rect)).ToList() : new List<Ship>();
            if (units.Count + ships.Count > 0)
            {
                SelectUnits(units, append);
                SelectShips(ships, true);
            }
            else SelectBuildingsIn(rect, append);
            areaPointerActive = false; Dragging = false; pressedWorld = false;
        }


        internal void CancelAreaSelection()
        {
            areaPointerActive=false;Dragging=false;pressedWorld=false;
        }

        internal void PrimaryTap(Vector2 point, bool append, bool desktopDoubleSelect)
        {
            if (BlocksWorldInput(point)) return;
            if(StrategicMapView.Active&&desktopDoubleSelect)
            {
                float threshold=24*UiViewport.Scale;
                bool zoom=Time.unscaledTime-lastStrategicTapTime<=.32f&&(point-lastStrategicTap).sqrMagnitude<=threshold*threshold;
                lastStrategicTapTime=Time.unscaledTime;lastStrategicTap=point;
                if(zoom){FocusStrategicPoint(point);lastStrategicTapTime=-10;return;}
            }
            var camp = PickCamp(point); var picked = RtsPicking.Target(session, cam, point); var unit = picked as Soldier; var ship = picked as Ship;
            var town = RtsPicking.Town(session, cam, point); var harbor = RtsPicking.Harbor(session, cam, point);
            bool tacticalActor = !StrategicMapView.Active && (unit || ship);
            if (tacticalActor && unit && unit.Team == 0)
            {
                SelectPrimaryUnit(unit, append, desktopDoubleSelect);
                return;
            }
            if (tacticalActor && ship && ship.Team == 0) { SelectShip(ship, append); return; }
            if (tacticalActor && ship) { Clear(); InspectedTarget = ship; return; }
            if (tacticalActor && unit) { Clear(); InspectedTarget = unit; return; }
            if (camp) { SelectCamp(camp); return; }
            if (unit && unit.Team == 0) { SelectPrimaryUnit(unit, append, desktopDoubleSelect); return; }
            if (ship && ship.Team == 0) { SelectShip(ship, append); return; }
            if (ship) { Clear(); InspectedTarget = ship; return; }
            if (picked is DefenseTower tower)
            {
                if (tower.Harbor) HandleHarborClick(tower.Harbor, append);
                else HandleTownClick(tower.Town, append);
                return;
            }
            if (unit) { Clear(); InspectedTarget = unit; return; }
            if (town) { HandleTownClick(town, append); return; }
            if (harbor) { HandleHarborClick(harbor, append); return; }
            if (!append) Clear();
        }

        void SelectPrimaryUnit(Soldier unit, bool append, bool desktopDoubleSelect)
        {
            PurgeStaleSelection();
            var key=UnityEngine.InputSystem.Keyboard.current;
            bool controlSelect=desktopDoubleSelect&&key!=null&&(key.leftCtrlKey.isPressed||key.rightCtrlKey.isPressed);
            bool sameType = controlSelect || desktopDoubleSelect && Time.unscaledTime - lastSelectTime < .3f && lastSelectKind == unit.Kind;
            if (sameType) SelectUnits(session.Units.Where(candidate => candidate.Team == 0 && candidate.Kind == unit.Kind && !candidate.IsGarrison && OnScreen(candidate)), append);
            else if (append && IsSelected(unit)) { RemoveSelected(unit); unit.Select(false); }
            else SelectUnits(new[] { unit }, append);
            if (unit.IsGarrison && !sameType) session.Message("El defensor puede salir si un aliado ocupa su círculo como relevo.", MessageKind.Info);
            lastSelectTime = Time.unscaledTime; lastSelectKind = unit.Kind;
        }

        internal void ExecuteArmedPointer(Vector2 point)
        {
            if (BlocksWorldInput(point) || session.Paused || session.Winner >= 0) { CancelCursor(); return; }
            PurgeStaleSelection();
            if (UnloadCursor)
            {
                var shore = Ground(point);
                foreach (var ship in Fleet) if (IsSelectableShip(ship) && ship.Type.CanTransport) OrderShip(ship, UnitCommandKind.Unload, shore);
                ShowOrder(shore, false); CancelCursor(); pressedWorld = false; return;
            }
            var picked = RtsPicking.Target(session, cam, point, -1);
            var victim = AttackCursor ? AttackRecipient(picked) : null;
            var armed = ClickRules.Resolve(ClickOf(picked, victim, null, null, null, null, true));
            if (armed == ClickDecision.Capture) OrderCaptureOf(picked);
            else if (armed == ClickDecision.Attack)
            {
                foreach (var unit in Selection) if (IsSelectableSoldier(unit)) session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Attack, targetId: victim.EntityId));
                foreach (var ship in Fleet) if (IsSelectableShip(ship) && ship.Type.CanAttack) OrderShip(ship, UnitCommandKind.Attack, victim.transform.position, victim.EntityId);
                ShowOrder(victim.transform.position, true); GameFeel.FlashTarget(victim);
            }
            else OrderAt(Ground(point), AttackCursor);
            CancelCursor();
            pressedWorld = false;
        }

        bool HasAttackShip()
        {
            foreach (var ship in Fleet) if (IsSelectableShip(ship) && ship.Type.CanAttack) return true;
            return false;
        }

        internal void ContextAction(Vector2 point)
        {
            if (BlocksWorldInput(point) || OrderCursor || session.Paused || session.Winner >= 0) { if (OrderCursor) CancelCursor(); return; }
            if(StrategicMapView.Active){FocusStrategicPoint(point);return;}
            PurgeStaleSelection();
            var clickedEnemy = RtsPicking.Target(session, cam, point, -1); var enemy = AttackRecipient(clickedEnemy);
            var friendly = RtsPicking.Target(session, cam, point, 1);
            var ally = friendly && friendly.Type.Domain == UnitDomain.Land ? friendly as Soldier : null;
            var ownShip = friendly && friendly.Type.Domain == UnitDomain.Sea ? friendly as Ship : null;
            var town = RtsPicking.Town(session, cam, point); var harbor = RtsPicking.Harbor(session, cam, point);
            switch (ClickRules.Resolve(ClickOf(clickedEnemy, enemy, ally, ownShip, town, harbor, false)))
            {
                case ClickDecision.FleetToHarbor: MoveFleetToHarbor(harbor); break;
                case ClickDecision.FleetToTownPort: MoveFleetToHarbor(town.Port); break;
                case ClickDecision.Attack:
                    CancelBoardingForSelection();
                    foreach (var unit in Selection) if (IsSelectableSoldier(unit)) session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Attack, targetId: enemy.EntityId));
                    foreach (var ship in Fleet) if (IsSelectableShip(ship) && ship.Type.CanAttack) OrderShip(ship, UnitCommandKind.Attack, enemy.transform.position, enemy.EntityId);
                    ShowOrder(enemy.transform.position, true); GameFeel.FlashTarget(enemy);
                    break;
                case ClickDecision.Capture: OrderCaptureOf(clickedEnemy, town, harbor); break;
                case ClickDecision.Board: BeginBoarding(ownShip); break;
                case ClickDecision.Follow:
                    CancelBoardingForSelection();
                    foreach (var unit in Selection) if (IsSelectableSoldier(unit)) session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Follow, targetId: ally.EntityId));
                    ShowOrder(ally.transform.position, false);
                    break;
                case ClickDecision.OrderHarbor: OrderAt(harbor.Landing, harbor.Owner != 0); break;
                case ClickDecision.OrderTown: OrderAt(town.ClaimPoint, town.State.Owner != 0); break;
                default: OrderAt(Ground(point), false); break;
            }
        }

        ClickContext ClickOf(CombatTarget clicked, CombatTarget enemy, Soldier ally, Ship ownShip, Settlement town, Harbor harbor, bool armed) =>
            new ClickContext(HasSelection, Selection.Count > 0, Fleet.Count > 0, HasAttackShip(),
                enemy && enemy.Type.Domain == UnitDomain.Sea, enemy && enemy.Team != 0, clicked && clicked.Type.Domain == UnitDomain.Static,
                harbor, town && town.Port, town, ownShip && ownShip.Type.CanTransport, ally && !IsSelected(ally),
                harbor && harbor.Owner != 0, town && town.State.Owner != 0, armed);

        void OrderCaptureOf(CombatTarget clicked, Settlement town = null, Harbor harbor = null)
        {
            var post = clicked as DefenseTower;
            if (post && post.Town) { OrderCapture(BuildingKind.Settlement, post.Town.BuildingId.LocalId, post.Town.ClaimPoint); ShowOrder(post.Town.ClaimPoint, true); return; }
            if (post && post.Harbor) { OrderCapture(BuildingKind.Harbor, post.Harbor.BuildingId.LocalId, post.Harbor.Landing); ShowOrder(post.Harbor.Landing, true); return; }
            if (harbor) { OrderCapture(BuildingKind.Harbor, harbor.BuildingId.LocalId, harbor.Landing); ShowOrder(harbor.Landing, true); return; }
            if (town) { OrderCapture(BuildingKind.Settlement, town.BuildingId.LocalId, town.ClaimPoint); ShowOrder(town.ClaimPoint, true); }
        }

        void FocusStrategicPoint(Vector2 point)
        {
            Clear();CameraRig.FocusAndZoom(Ground(point));
        }
    }
}
