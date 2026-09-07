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

        internal bool AcceptsDirectPointerInput => session && EffectiveFocus && !session.Paused && session.Winner < 0 && !HelpVisible && !ScoreboardVisible;
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
            var key=UnityEngine.InputSystem.Keyboard.current;
            bool controlSelect=desktopDoubleSelect&&key!=null&&(key.leftCtrlKey.isPressed||key.rightCtrlKey.isPressed);
            bool sameType = controlSelect || desktopDoubleSelect && Time.unscaledTime - lastSelectTime < .3f && lastSelectKind == unit.Kind;
            if (sameType) SelectUnits(session.Units.Where(candidate => candidate.Team == 0 && candidate.Kind == unit.Kind && !candidate.IsGarrison && OnScreen(candidate)), append);
            else if (append && Selection.Contains(unit)) { Selection.Remove(unit); unit.Select(false); }
            else SelectUnits(new[] { unit }, append);
            if (unit.IsGarrison && !sameType) session.Message("El defensor puede salir si un aliado ocupa su círculo como relevo.");
            lastSelectTime = Time.unscaledTime; lastSelectKind = unit.Kind;
        }

        internal void ExecuteArmedPointer(Vector2 point)
        {
            if (BlocksWorldInput(point) || session.Paused || session.Winner >= 0) { CancelCursor(); return; }
            if (UnloadCursor)
            {
                var shore = Ground(point);
                foreach (var ship in Fleet) if (IsSelectableShip(ship) && ship.Kind == ShipKind.Transport) Feedback(ship.SailToShore(shore));
                ShowOrder(shore, false); CancelCursor(); pressedWorld = false; return;
            }
            var victim = AttackCursor ? AttackRecipient(RtsPicking.Target(session, cam, point, -1)) : null;
            if (victim && victim.Team != 0)
            {
                foreach (var unit in Selection) if (IsSelectableSoldier(unit)) session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Attack, targetId: victim.EntityId));
                foreach (var ship in Fleet) if (IsSelectableShip(ship)) ship.Attack(victim);
                CancelCursor();
            }
            else OrderAt(Ground(point), AttackCursor);
            pressedWorld = false;
        }

        internal void ContextAction(Vector2 point)
        {
            if (BlocksWorldInput(point) || OrderCursor || session.Paused || session.Winner >= 0) { if (OrderCursor) CancelCursor(); return; }
            var clickedEnemy = RtsPicking.Target(session, cam, point, -1); var enemy = AttackRecipient(clickedEnemy);
            var ally = RtsPicking.Target(session, cam, point, 1) as Soldier; var town = RtsPicking.Town(session, cam, point); var harbor = RtsPicking.Harbor(session, cam, point);
            var ownShip = RtsPicking.Target(session, cam, point, 1) as Ship;
            if (harbor && Fleet.Count > 0) MoveFleetToHarbor(harbor);
            else if (town && town.Port && Fleet.Count > 0) MoveFleetToHarbor(town.Port);
            else if (enemy && enemy.Team != 0 && HasSelection)
            {
                CancelBoardingForSelection();
                foreach (var unit in Selection) if (IsSelectableSoldier(unit)) session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Attack, targetId: enemy.EntityId));
                foreach (var ship in Fleet) if (IsSelectableShip(ship) && ship.Kind == ShipKind.Galley) ship.Attack(enemy);
                ShowOrder(enemy.transform.position, true);
            }
            else if (ownShip && ownShip.Kind == ShipKind.Transport && Selection.Count > 0) BeginBoarding(ownShip);
            else if (harbor && Fleet.Count > 0) MoveFleetToHarbor(harbor);
            else if (ally && !Selection.Contains(ally) && Selection.Count > 0)
            {
                CancelBoardingForSelection();
                foreach (var unit in Selection) if (IsSelectableSoldier(unit)) session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Follow, targetId: ally.EntityId));
                ShowOrder(ally.transform.position, false);
            }
            else if (clickedEnemy is DefenseTower fort) OrderAt(fort.Town ? fort.Town.ClaimPoint : fort.Harbor.Landing, true);
            else OrderAt(harbor ? harbor.Landing : town ? town.ClaimPoint : Ground(point), harbor ? harbor.Owner != 0 : town && town.State.Owner != 0);
        }
    }
}
