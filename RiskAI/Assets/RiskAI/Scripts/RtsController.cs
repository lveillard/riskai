using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskAI
{
    public sealed partial class RtsController : MonoBehaviour
    {
        public readonly List<Soldier> Selection=new List<Soldier>();
        public readonly List<Ship> Fleet=new List<Ship>();
        public CountryCamp SelectedCamp { get; private set; }
        public Settlement SelectedTown { get; private set; }
        public Harbor SelectedHarbor { get; private set; }
        public CombatTarget InspectedTarget { get; private set; }
        public CombatTarget Hovered { get; private set; }
        public bool AttackCursor { get; private set; }
        public bool MoveCursor { get; private set; }
        public bool PatrolCursor { get; private set; }
        public bool UnloadCursor { get; private set; }
        public bool OrderCursor => AttackCursor || MoveCursor || PatrolCursor || UnloadCursor;
        public bool ShowHealthBars => Keyboard.current!=null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);
        public bool HelpVisible;
        public bool ScoreboardVisible => !HelpVisible && Keyboard.current!=null && Keyboard.current.tabKey.isPressed;
        public bool EdgePan=true;
        public bool Dragging { get; private set; }
        public bool CameraDragging { get; private set; }
        public bool CursorCaptured { get; private set; }
        public Vector2 DragStart { get; private set; }
        public Vector2 Pointer => UnityEngine.InputSystem.Pointer.current == null ? Vector2.zero : UnityEngine.InputSystem.Pointer.current.position.ReadValue();
        Vector2 AreaEndPoint => areaPointerActive?areaPointer:Pointer;
        public Rect SelectionRect => Rect.MinMaxRect(Mathf.Min(DragStart.x,AreaEndPoint.x),Screen.height-Mathf.Max(DragStart.y,AreaEndPoint.y),Mathf.Max(DragStart.x,AreaEndPoint.x),Screen.height-Mathf.Min(DragStart.y,AreaEndPoint.y));
        // Pooled soldiers come back as a different actor in the same object, so
        // every ownership record keeps the simulation identity it was taken with.
        readonly struct SoldierRef
        {
            public readonly Soldier Unit;public readonly int Id;
            public SoldierRef(Soldier unit){Unit=unit;Id=unit?unit.EntityId:0;}
            public bool Matches => Unit&&Unit.EntityId==Id;
        }
        readonly struct ShipRef
        {
            public readonly Ship Vessel;public readonly int Id;
            public ShipRef(Ship vessel){Vessel=vessel;Id=vessel?vessel.EntityId:0;}
            public bool Matches => Vessel&&Vessel.EntityId==Id;
        }
        // Identities are keyed by actor object, not by list position: reordering or
        // an external add/remove on the public lists cannot re-key a known record.
        Dictionary<Soldier,int> selectionIds=new Dictionary<Soldier,int>();
        Dictionary<Soldier,int> nextSelectionIds=new Dictionary<Soldier,int>();
        Dictionary<Ship,int> fleetIds=new Dictionary<Ship,int>();
        Dictionary<Ship,int> nextFleetIds=new Dictionary<Ship,int>();
        readonly HashSet<Soldier> selectionLookup=new HashSet<Soldier>();
        readonly HashSet<Ship> fleetLookup=new HashSet<Ship>();
        readonly Dictionary<int,List<SoldierRef>> groups=new Dictionary<int,List<SoldierRef>>();
        readonly Dictionary<int,List<ShipRef>> shipGroups=new Dictionary<int,List<ShipRef>>();
        BattleSession session;Camera cam;Vector2 previousMouse;
        public RtsCameraRig CameraRig { get; private set; }
        bool pressedWorld;
        readonly PointerGesture secondaryGesture = new PointerGesture();
        float lastStrategicTapTime=-10;
        Vector2 lastStrategicTap;
        RtsInputRouter inputRouter;
        bool cursorCaptureRequested;
        bool gameplayFocus;
        bool observedPause;
        float lastSelectTime,lastGroupTime;int lastGroup=-1;UnitKind lastSelectKind;
        LineRenderer hoverRing;
        readonly List<SoldierRef> pendingBoarders=new();Ship pendingBoardingTransport;Vector3 pendingBoardingLanding;
        float nextBoardingCheck,nextBoardingRecovery,lastBoardingProgress,previousBoardingDistance;
        int previousBoarderCount;
        const float BoardingCheckSeconds=.2f,BoardingStallSeconds=20f;
        public void Initialize(BattleSession battle,Camera camera) { session=battle;cam=camera;gameplayFocus=true;CameraRig=gameObject.AddComponent<RtsCameraRig>();CameraRig.Initialize(camera);inputRouter=new RtsInputRouter(this);var home=session.Towns.FirstOrDefault(t=>t.State.Owner==0&&t.IsCapital);if(home)CameraRig.SetHome(home.transform.position);previousMouse=Pointer;RequestCursorCapture(); }
        bool EffectiveFocus => gameplayFocus&&(Application.isEditor||Application.isFocused);
        bool ConfinedCursorSupported => RtsCameraPolicy.SupportsConfinedCursor(Application.isEditor,Application.platform);
        void ApplyCursorCapture()
        {
            bool gameplayActive=session&&isActiveAndEnabled&&!session.Paused&&session.Winner<0&&!HelpVisible;
            if(!RtsCameraPolicy.ShouldCaptureCursor(Application.isEditor,ConfinedCursorSupported,Application.isFocused,gameplayActive,cursorCaptureRequested))
            {
                CursorCaptured=false;
                if(ConfinedCursorSupported) { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
                return;
            }
            Cursor.visible=true;Cursor.lockState=CursorLockMode.Confined;CursorCaptured=Cursor.lockState==CursorLockMode.Confined;
        }
        void RequestCursorCapture()
        {
            cursorCaptureRequested=true;ApplyCursorCapture();
        }
        void ReleaseCursor()
        {
            secondaryGesture.Cancel();inputRouter?.Cancel();cursorCaptureRequested=false;CursorCaptured=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void SyncPauseInput()
        {
            if(observedPause==session.Paused)return;
            observedPause=session.Paused;
            // Discard held actions once at the transition. Pausing releases OS
            // confinement, but must not cancel a new camera drag every frame.
            secondaryGesture.Cancel();inputRouter?.Cancel();CancelAreaSelection();CancelCursor();
            CameraDragging=false;previousMouse=Pointer;
        }
        public bool OverHud(Vector2 screen) => RtsUiInput.BlocksWorld(screen) || HelpVisible || ScoreboardVisible || session.Winner>=0;
        bool Shift => Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        // Selection/Fleet stay plain public lists; the identity maps record which
        // actor each listed object was taken with. A pooled reuse therefore drops
        // the entry instead of inheriting the previous owner's orders, and a
        // record is never re-taken from an actor that is already known here.
        void AddSelected(Soldier unit) { Selection.Add(unit);if(unit&&!selectionIds.ContainsKey(unit))selectionIds[unit]=unit.EntityId; }
        void AddSelected(Ship ship) { Fleet.Add(ship);if(ship&&!fleetIds.ContainsKey(ship))fleetIds[ship]=ship.EntityId; }
        void RemoveSelected(Soldier unit)
        {
            Selection.RemoveAll(listed=>listed==unit);
            if(unit)selectionIds.Remove(unit);
        }
        void ClearSelectionLists() { Selection.Clear();selectionIds.Clear();Fleet.Clear();fleetIds.Clear(); }
        bool OwnsIdentity(Soldier unit) => unit&&selectionIds.TryGetValue(unit,out int id)&&id==unit.EntityId;
        bool OwnsIdentity(Ship ship) => ship&&fleetIds.TryGetValue(ship,out int id)&&id==ship.EntityId;
        /// <summary>
        /// Drops entries whose actor died, left play or was reused by the pool.
        /// One stable compaction pass over each list; no repeated element shifting.
        /// </summary>
        internal void PurgeStaleSelection()
        {
            nextSelectionIds.Clear();
            int write=0;
            for(int i=0;i<Selection.Count;i++)
            {
                var unit=Selection[i];
                if(!unit)continue;
                // An object appended straight to the public list is an unknown
                // actor and establishes its own identity; a recorded one keeps it.
                int id=selectionIds.TryGetValue(unit,out int recorded)?recorded:unit.EntityId;
                // A replaced actor is only dropped: its own state is not this
                // selection's to touch. The same actor leaving play is released.
                if(unit.EntityId!=id)continue;
                if(!IsSelectableSoldier(unit)){unit.Select(false);continue;}
                nextSelectionIds[unit]=id;
                Selection[write++]=unit;
            }
            if(write<Selection.Count)Selection.RemoveRange(write,Selection.Count-write);
            (selectionIds,nextSelectionIds)=(nextSelectionIds,selectionIds);
            nextFleetIds.Clear();
            write=0;
            for(int i=0;i<Fleet.Count;i++)
            {
                var ship=Fleet[i];
                if(!ship)continue;
                int id=fleetIds.TryGetValue(ship,out int recorded)?recorded:ship.EntityId;
                if(ship.EntityId!=id)continue;
                if(!IsSelectableShip(ship)){ship.Select(false);continue;}
                nextFleetIds[ship]=id;
                Fleet[write++]=ship;
            }
            if(write<Fleet.Count)Fleet.RemoveRange(write,Fleet.Count-write);
            (fleetIds,nextFleetIds)=(nextFleetIds,fleetIds);
            write=0;
            for(int i=0;i<pendingBoarders.Count;i++)if(pendingBoarders[i].Matches)pendingBoarders[write++]=pendingBoarders[i];
            if(write<pendingBoarders.Count)pendingBoarders.RemoveRange(write,pendingBoarders.Count-write);
        }
        bool IsSelected(Soldier unit) => OwnsIdentity(unit)&&Selection.Contains(unit);
        bool IsSelected(Ship ship) => OwnsIdentity(ship)&&Fleet.Contains(ship);
        public void Clear()
        {
            if(SelectedCamp)SelectedCamp.Select(false);SelectedCamp=null;
            // Purge first: it drops replaced actors and lets an actor listed by an
            // external caller establish its identity, so nothing owned by this
            // selection is released without also clearing its ring.
            PurgeStaleSelection();
            foreach(var unit in Selection)unit.Select(false);
            foreach(var ship in Fleet)ship.Select(false);
            ClearSelectionLists();
            ClearSelectedBuildings();
            InspectedTarget=null;
        }
        public void SelectCamp(CountryCamp camp) { Clear();SelectedCamp=camp;if(camp)camp.Select(true); }
        CountryCamp PickCamp(Vector2 pointer)
        {
            CountryCamp best=null;float distance=StrategicMapView.Active?10*BattleHud.Scale:24;
            foreach(var camp in session.Camps)if(camp){var p=cam.WorldToScreenPoint(StrategicMapView.Active?StrategicMapView.SurfaceAnchor(camp.SpawnPoint):camp.SpawnPoint+Vector3.up*.7f);float d=Vector2.Distance(pointer,p);if(p.z>0&&d<distance){best=camp;distance=d;}}
            return best;
        }
        public void SelectShip(Ship ship,bool append=false)
        {
            if(!ship||!ship.IsAlive||ship.Team!=0)return;
            PurgeStaleSelection();
            if(append&&IsSelected(ship)){RemoveSelected(ship);ship.Select(false);return;}
            if(!append)Clear();
            if(SelectedCamp)SelectedCamp.Select(false);SelectedCamp=null;
            ClearSelectedBuildings();InspectedTarget=null;
            if(!IsSelected(ship))AddSelected(ship);ship.Select(true);
        }
        void RemoveSelected(Ship ship)
        {
            Fleet.RemoveAll(listed=>listed==ship);
            if(ship)fleetIds.Remove(ship);
        }
        void SelectUnits(IEnumerable<Soldier> units,bool append=false)
        {
            var list=units.Where(IsSelectableSoldier).ToList();PurgeStaleSelection();if(!append)Clear();
            if(SelectedCamp)SelectedCamp.Select(false);SelectedCamp=null;
            ClearSelectedBuildings();InspectedTarget=null;
            // Purged entries are identity-checked already, so a set membership
            // test keeps large selections linear instead of rescanning per unit.
            selectionLookup.Clear();foreach(var current in Selection)selectionLookup.Add(current);
            foreach(var u in list)if(selectionLookup.Add(u)){AddSelected(u);u.Select(true);}
            selectionLookup.Clear();
        }
        void SelectShips(IEnumerable<Ship> ships,bool append=false)
        {
            PurgeStaleSelection();if(!append)Clear();
            if(SelectedCamp)SelectedCamp.Select(false);SelectedCamp=null;
            ClearSelectedBuildings();InspectedTarget=null;
            fleetLookup.Clear();foreach(var current in Fleet)fleetLookup.Add(current);
            foreach(var ship in ships)if(IsSelectableShip(ship)&&fleetLookup.Add(ship)){AddSelected(ship);ship.Select(true);}
            fleetLookup.Clear();
        }
        public void SelectAll()
        {
            SelectUnits(session.Units.Where(u=>IsSelectableSoldier(u)&&!u.IsGarrison));
            if(Selection.Count==0)session.Message("Recluta tropas móviles en una ciudad aliada. Los defensores mantienen sus círculos.");
        }
        public void SelectOnly(Soldier unit) { SelectUnits(new[]{unit});if(unit && unit.IsGarrison)session.Message("El defensor puede salir si un aliado ocupa su círculo como relevo."); }
        public void SelectFleet()
        {
            if(!NavalWorld.Current){Clear();return;}
            SelectShips(NavalWorld.Current.Ships);
        }
        public void CancelCursor() { AttackCursor=MoveCursor=PatrolCursor=UnloadCursor=false; }
        static CombatTarget AttackRecipient(CombatTarget target) => target is DefenseTower tower ? tower.Guardian : target;
        bool HasSelection => Selection.Count>0||Fleet.Count>0;
        public void ArmAttack() { CancelCursor();PurgeStaleSelection();if(HasSelection)AttackCursor=true; }
        public void ArmMove() { CancelCursor();PurgeStaleSelection();if(HasSelection)MoveCursor=true; }
        public void ArmPatrol() { CancelCursor();PurgeStaleSelection();if(HasSelection)PatrolCursor=true; }
        public void Stop() { if(session.Paused||session.Winner>=0)return;PurgeStaleSelection();CancelBoardingForSelection();foreach(var u in Selection)if(IsSelectableSoldier(u))session.Commands.Submit(new UnitCommand(0,u.EntityId,UnitCommandKind.Stop));foreach(var ship in Fleet)if(IsSelectableShip(ship))ship.Stop();CancelCursor(); }
        public void Hold() { if(session.Paused||session.Winner>=0)return;PurgeStaleSelection();CancelBoardingForSelection();foreach(var u in Selection)if(IsSelectableSoldier(u))session.Commands.Submit(new UnitCommand(0,u.EntityId,UnitCommandKind.Hold));foreach(var ship in Fleet)if(IsSelectableShip(ship))ship.Stop();CancelCursor(); }
        static bool IsSelectableSoldier(Soldier unit) => unit&&unit.Team==0&&unit.IsAlive&&unit.isActiveAndEnabled&&unit.Agent&&unit.Agent.enabled;
        static bool IsSelectableShip(Ship ship) => ship&&ship.Team==0&&ship.IsAlive&&ship.isActiveAndEnabled;
        Ship SelectedTransport
        {
            get { foreach(var ship in Fleet)if(IsSelectableShip(ship)&&ship.Kind==ShipKind.Transport)return ship;return null; }
        }
        void CancelPendingBoarding()
        {
            pendingBoarders.Clear();pendingBoardingTransport=null;pendingBoardingLanding=Vector3.zero;
            nextBoardingCheck=nextBoardingRecovery=lastBoardingProgress=0;
            previousBoardingDistance=float.PositiveInfinity;previousBoarderCount=0;
        }
        void CancelBoardingForSelection()
        {
            if(!pendingBoardingTransport)return;
            if(IsSelected(pendingBoardingTransport)){CancelPendingBoarding();return;}
            foreach(var unit in Selection)foreach(var boarder in pendingBoarders)if(boarder.Matches&&boarder.Unit==unit){CancelPendingBoarding();return;}
        }

        public void Recruit(UnitKind kind)
        {
            string error=TryRecruitSelected(kind);
            if(error!=null)session.Message(error);
            else session.Message(LastProductionResult.Feedback(BattleRules.Name(kind)));
        }
        public void BuildTower() { if(SelectedHarbor)Feedback(ExecuteBuilding(PlayerBuildingIntent.BuildTower(SelectedHarbor.BuildingId)));else if(SelectedTown)Feedback(ExecuteBuilding(PlayerBuildingIntent.BuildTower(SelectedTown.BuildingId)));else session.Message("Selecciona una ciudad o un puerto tuyo para reconstruir su torre."); }
        public void UpgradeTown() { if(SelectedTown)Feedback(SelectedTown.Upgrade());else session.Message("Selecciona una ciudad tuya para mejorarla."); }
        public void Feedback(string error) { if(error!=null)session.Message(error); }
        void OnApplicationFocus(bool hasFocus)
        {
            gameplayFocus=hasFocus;
            if(!hasFocus)
            {
                cursorCaptureRequested=false;ReleaseCursor();
            }
            if(hasFocus)return;
            CameraDragging=false;Dragging=false;pressedWorld=false;previousMouse=Pointer;
            if(CameraRig!=null)CameraRig.CancelMotion();
        }
        public void Focus(Vector3 position) => CameraRig.Focus(position);
        public void FocusSelection()
        {
            PurgeStaleSelection();
            Vector3 center=Vector3.zero;int count=0;
            foreach(var unit in Selection)if(IsSelectableSoldier(unit)){center+=unit.transform.position;count++;}
            foreach(var ship in Fleet)if(IsSelectableShip(ship)){center+=ship.transform.position;count++;}
            if(count>0){Focus(center/count);return;}
            else if(SelectedTown)Focus(SelectedTown.transform.position);
            else FocusHome();
        }
        public void FocusFleet()
        {
            PurgeStaleSelection();
            Vector3 center=Vector3.zero;int count=0;
            foreach(var ship in Fleet)if(IsSelectableShip(ship)){center+=ship.transform.position;count++;}
            if(count>0)Focus(center/count);
        }
        public void FocusHarbor()
        {
            var naval=NavalWorld.Current;if(!naval)return;
            Harbor best=naval.Harbors.FirstOrDefault(h=>h&&h.Owner==0);
            if(best){SelectHarbor(best);Focus(best.Landing);}
        }
        public void BuyShip(ShipKind kind)
        {
            string error=TryBuySelected(kind);
            if(!session)return;
            session.Message(error??LastProductionResult.Feedback(Harbor.Profile(kind).Name));
        }
        public void BoardNearby()
        {
            if(session.Paused||session.Winner>=0)return;
            PurgeStaleSelection();
            var transport=SelectedTransport;if(!transport){session.Message("Selecciona un transporte para embarcar.");return;}
            var boarders=session.Units.Where(u=>IsSelectableSoldier(u)&&!u.IsGarrison&&FlatDistance(u.transform.position,transport.transform.position)<=Ship.LoadRadius*Ship.LoadRadius)
                .OrderBy(u=>FlatDistance(u.transform.position,transport.transform.position)).Take(Ship.LoadOrderLimit).ToList();
            if(boarders.Count==0){session.Message("Acerca tropas a la costa o selecciónalas y haz clic derecho en el transporte.");return;}
            BeginBoarding(transport,boarders);
        }
        public void UnloadFleet()
        {
            if(session.Paused||session.Winner>=0)return;
            PurgeStaleSelection();
            var naval=NavalWorld.Current;if(!naval)return;
            Ship anchor=null;foreach(var selected in Fleet)if(IsSelectableShip(selected)){anchor=selected;break;}
            if(!anchor)return;
            Harbor harbor=null;float distance=12*12;
            foreach(var candidate in naval.Harbors)if(candidate)
            {float next=FlatDistance(candidate.Berth,anchor.transform.position);if(next<distance){distance=next;harbor=candidate;}}
            if(!Fleet.Any(s=>IsSelectableShip(s)&&s.Kind==ShipKind.Transport&&s.CargoCount>0)){session.Message("Selecciona un transporte con tropas a bordo.");return;}
            CancelBoardingForSelection();
            if(!harbor){CancelCursor();UnloadCursor=true;session.Message("Desembarco: haz clic en una playa transitable. El transporte navegará hasta ella.");return;}
            foreach(var ship in Fleet)if(IsSelectableShip(ship)&&ship.Kind==ShipKind.Transport)
            {
                ship.SailToHarbor(harbor);
                if(ship.LastActionError!=null)session.Message(ship.LastActionError);
            }
        }
        public void UnloadCargo(Ship transport,Soldier soldier)
        {
            if(session.Paused||session.Winner>=0||!IsSelectableShip(transport))return;
            if(transport.UnloadOneNearby(soldier))session.Message(BattleRules.Name(soldier.Kind)+" ha desembarcado.");
            else Feedback(transport.LastActionError);
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.SqrMagnitude(a-b);}
        public void FocusHome()
        {
            var town=session.Towns.FirstOrDefault(t=>t.State.Owner==0&&t.IsCapital)??session.Towns.FirstOrDefault(t=>t.State.Owner==0);
            if(town){SelectTown(town);Focus(town.transform.position);}
        }
        public void OrderAt(Vector3 point,bool attack=false)
        {
            if(session.Paused||session.Winner>=0)return;
            PurgeStaleSelection();
            CancelBoardingForSelection();
            bool issued=false,attemptedGuardOrder=false;
            var kind=PatrolCursor?UnitCommandKind.Patrol:attack?UnitCommandKind.AttackMove:UnitCommandKind.Move;
            // Guards are submitted ahead of the formation. A successful command
            // atomically binds its in-circle relief, which remains at the post.
            foreach(var guard in Selection.Where(unit=>unit.IsGarrison))
            {
                attemptedGuardOrder=true;
                if(session.Commands.Submit(new UnitCommand(0,guard.EntityId,kind,point.x,point.y,point.z,append:Shift)))issued=true;
            }
            var mobile=Selection.Where(unit=>!unit.IsGarrison).ToArray();
            if(mobile.Length>0)
            {
                BattleSession.GiveFormation(mobile,point,attack,Shift,PatrolCursor);
                issued=true;
            }
            bool fleetIssued=false;
            if(Fleet.Count>0)
            {
                foreach(var ship in Fleet)if(IsSelectableShip(ship))
                {
                    ship.MoveTo(point,attack);Feedback(ship.LastActionError);
                    if(ship.LastActionError==null)fleetIssued=true;
                }
                issued|=fleetIssued;
            }
            if(issued)ShowOrder(point,attack);
            if(!issued&&Selection.Count>0&&!attemptedGuardOrder)session.Message("No hay tropas disponibles para esa orden.");
            if(!issued&&SelectedCamp)
            {
                if(session.Economy.CountryOwner(SelectedCamp.Country)!=0)session.Message("Controla todo el país para fijar la salida de sus refuerzos.");
                else if(ExecuteBuilding(PlayerBuildingIntent.SetLandRally(SelectedCamp.BuildingId,point.x,point.y,point.z))==null){ShowOrder(point,false);session.Message("Salida de la hoguera actualizada.");}
                else session.Message("Elige un punto de salida transitable.");
            }
            else if(!issued&&SetSelectedBuildingRallies(point)) { ShowOrder(point,false);session.Message("Punto de reunión actualizado."); }
            CancelCursor();
        }
        LineRenderer orderMarker;
        float orderMarkerUntil;
        Color orderMarkerColor;
        void ShowOrder(Vector3 point,bool attack)
        {
            if(!orderMarker)orderMarker=VisualFactory.Ring(transform,.9f,.1f,Color.white);
            orderMarker.transform.position=point;orderMarker.enabled=true;
            orderMarkerColor=attack?new Color(1,.35f,.22f):new Color(.55f,1,.65f);
            orderMarker.startColor=orderMarker.endColor=orderMarkerColor;
            orderMarker.transform.localScale=Vector3.one*1.6f;
            orderMarkerUntil=Time.unscaledTime+.7f;
        }
        void AnimateOrderMarker()
        {
            if(!orderMarker || !orderMarker.enabled)return;
            float remaining=orderMarkerUntil-Time.unscaledTime;
            if(remaining<=0){orderMarker.enabled=false;return;}
            float progress=1-remaining/.7f;
            // Reuse one marker: a fast inward pulse confirms the destination,
            // followed by a short fade. No spawned effects or extra materials.
            float pulse=1-Mathf.Pow(1-progress,3);
            orderMarker.transform.localScale=Vector3.one*Mathf.Lerp(1.6f,.55f,pulse);
            var color=orderMarkerColor;color.a=Mathf.Clamp01(remaining/.3f);
            orderMarker.startColor=orderMarker.endColor=color;
        }
        void MoveFleetToHarbor(Harbor harbor)
        {
            PurgeStaleSelection();
            if(!harbor||Fleet.Count==0)return;
            CancelBoardingForSelection();
            foreach(var ship in Fleet)if(IsSelectableShip(ship)){ship.SailToHarbor(harbor);Feedback(ship.LastActionError);}
        }
        void BeginBoarding(Ship transport) => BeginBoarding(transport,Selection);
        void BeginBoarding(Ship transport,IReadOnlyList<Soldier> candidates)
        {
            if(!transport||transport.Team!=0||transport.Kind!=ShipKind.Transport||candidates.Count==0)return;
            var naval=NavalWorld.Current;if(!naval)return;
            var available=candidates.Where(u=>IsSelectableSoldier(u)&&!u.IsGarrison).Take(Mathf.Min(Ship.LoadOrderLimit,transport.Profile.Capacity-transport.CargoCount)).ToList();
            if(available.Count==0){session.Message("Transporte lleno o sólo defensores retenidos seleccionados.");return;}
            CancelPendingBoarding();
            // Already within source loading radius: no arbitrary dock detour.
            for(int i=available.Count-1;i>=0;i--)if(transport.TryEmbark(available[i]))available.RemoveAt(i);
            if(available.Count==0){session.Message("Embarque completado: "+transport.CargoCount+" / "+transport.Profile.Capacity+".");return;}
            if(!naval.TryPlanEmbark(transport,available,out var landing,out var berth,out var error)){session.Message(error);return;}
            pendingBoardingTransport=transport;pendingBoardingLanding=landing;
            transport.MoveTo(berth);
            if(transport.LastActionError!=null){session.Message(transport.LastActionError);CancelPendingBoarding();return;}
            foreach(var soldier in available)
            {pendingBoarders.Add(new SoldierRef(soldier));session.Commands.Submit(new UnitCommand(0,soldier.EntityId,UnitCommandKind.Move,landing.x,landing.y,landing.z));}
            lastBoardingProgress=session.BattleTime;previousBoarderCount=pendingBoarders.Count;
            nextBoardingCheck=session.BattleTime;nextBoardingRecovery=session.BattleTime+1f;
            ShowOrder(landing,false);session.Message("Embarcando: tropas y transporte se reúnen en la costa marcada.");
        }
        void ProcessPendingBoarding()
        {
            if(!pendingBoardingTransport)return;
            if(!IsSelectableShip(pendingBoardingTransport)){CancelPendingBoarding();return;}
            float now=session.BattleTime;
            if(now<nextBoardingCheck)return;
            nextBoardingCheck=now+BoardingCheckSeconds;
            bool recover=now>=nextBoardingRecovery;
            if(recover)nextBoardingRecovery=now+1f;
            string error=null;
            float distance=pendingBoardingTransport.RemainingRouteDistance;
            for(int i=pendingBoarders.Count-1;i>=0;i--)
            {
                var boarder=pendingBoarders[i];var soldier=boarder.Unit;
                if(!boarder.Matches||!IsSelectableSoldier(soldier)||soldier.IsGarrison||pendingBoardingTransport.CargoCount>=pendingBoardingTransport.Profile.Capacity||pendingBoardingTransport.TryEmbark(soldier))
                {pendingBoarders.RemoveAt(i);continue;}
                error=pendingBoardingTransport.LastActionError;
                var agent=soldier.Agent;
                var offset=soldier.transform.position-pendingBoardingLanding;offset.y=0;
                float remaining=agent&&agent.hasPath&&!agent.pathPending?agent.remainingDistance:offset.magnitude;
                distance+=float.IsNaN(remaining)||float.IsInfinity(remaining)?offset.magnitude:remaining;
                // Avoidance can stop a boarder just outside a narrow beach. Retry
                // the already validated landing, without relaxing shore rules.
                if(recover&&agent&&!agent.pathPending&&agent.velocity.sqrMagnitude<.04f&&offset.sqrMagnitude<=Ship.LoadRadius*Ship.LoadRadius)
                    session.Commands.Submit(new UnitCommand(0,soldier.EntityId,UnitCommandKind.Move,pendingBoardingLanding.x,pendingBoardingLanding.y,pendingBoardingLanding.z));
            }
            if(pendingBoarders.Count==0)
            {session.Message("Embarque terminado: "+pendingBoardingTransport.CargoCount+" / "+pendingBoardingTransport.Profile.Capacity+".");CancelPendingBoarding();return;}
            if(pendingBoarders.Count!=previousBoarderCount||distance<previousBoardingDistance-.1f)lastBoardingProgress=now;
            previousBoarderCount=pendingBoarders.Count;previousBoardingDistance=distance;
            if(now-lastBoardingProgress>=BoardingStallSeconds)
            {
                session.Message("Embarque detenido: "+(error??"las tropas no pueden avanzar hasta la costa marcada."));
                CancelPendingBoarding();
            }
        }
        Vector3 Ground(Vector2 pointer)
        {
            return CameraRig.Ground(pointer);
        }
        T Pick<T>(Vector2 pointer) where T:Component
        {
            var hits=Physics.RaycastAll(cam.ScreenPointToRay(pointer),cam.farClipPlane,~0,QueryTriggerInteraction.Collide);
            foreach(var hit in hits.OrderBy(h=>h.distance)) { var item=hit.collider.GetComponentInParent<T>();if(item)return item; }return null;
        }
        bool TryEdgePan(Vector2 point,out Vector3 direction)
        {
            direction=Vector3.zero;
            bool runtimeCursorReleased=RtsCameraPolicy.SupportsConfinedCursor(Application.isEditor,Application.platform)&&!cursorCaptureRequested;
            // A lifted finger or hovering pen must not leave the camera scrolling
            // from its last position. Direct devices pan through explicit gestures.
            bool blocked=!(UnityEngine.InputSystem.Pointer.current is Mouse)||!EdgePan||!EffectiveFocus||runtimeCursorReleased||session.Paused||session.Winner>=0||HelpVisible||Dragging||pressedWorld||CameraDragging;
            var mouse=Mouse.current;
            if(mouse!=null&&(mouse.middleButton.isPressed||mouse.rightButton.isPressed))blocked=true;
            if(mouse!=null&&mouse.leftButton.isPressed)blocked=true;
            if(blocked)return false;
            Vector2 edge=RtsCameraPolicy.EdgePanDirection(point,new Vector2(Screen.width,Screen.height));
            if(edge.sqrMagnitude<.001f)return false;
            direction=new Vector3(edge.x,0,edge.y);
            return true;
        }
        static bool InsideScreen(Vector2 point) => point.x>=0&&point.x<=Screen.width&&point.y>=0&&point.y<=Screen.height;
        void Update()
        {
            if(session&&!session.Paused&&session.Winner<0)ProcessPendingBoarding();
            AnimateOrderMarker();
            // Ownership is released before any focus/modal early return: a dead
            // or pool-reused actor must never stay listed while input is idle.
            PurgeStaleSelection();
            if(!EffectiveFocus)
            {
                ReleaseCursor();CameraDragging=false;Dragging=false;pressedWorld=false;previousMouse=Pointer;
                if(CameraRig!=null)CameraRig.CancelMotion();
                return;
            }
            SyncPauseInput();
            ApplyCursorCapture();
            var mouse=Mouse.current;var key=Keyboard.current;
            if(key!=null)
            {
            if(key.f1Key.wasPressedThisFrame)HelpVisible=!HelpVisible;
            if(key.f1Key.wasPressedThisFrame&&HelpVisible)ReleaseCursor();
            if(key.f10Key.wasPressedThisFrame)
            {
                session.TogglePause();
                SyncPauseInput();ApplyCursorCapture();
            }
            if(key.escapeKey.wasPressedThisFrame&&HelpVisible)
            {
                HelpVisible=false;ReleaseCursor();CameraDragging=false;Dragging=false;pressedWorld=false;previousMouse=Pointer;
                if(CameraRig!=null)CameraRig.CancelMotion();
                return;
            }
            }
            // Modal input exclusion also applies on devices without a keyboard.
            if(HelpVisible||ScoreboardVisible||session.Winner>=0)
            {
                ReleaseCursor();CameraDragging=false;Dragging=false;pressedWorld=false;previousMouse=Pointer;Hovered=null;
                if(CameraRig!=null)CameraRig.CancelMotion();
                return;
            }
            if(key!=null)
            {
            if(key.f2Key.wasPressedThisFrame)FocusHome();
            if(key.escapeKey.wasPressedThisFrame) { ReleaseCursor();if(OrderCursor)CancelCursor();else Clear();return; }
            if(key.backspaceKey.wasPressedThisFrame)CameraRig.ResetView();
            if(key.eKey.wasPressedThisFrame)SelectAll();
            if(key.aKey.wasPressedThisFrame)ArmAttack();
            if(key.mKey.wasPressedThisFrame)ArmMove();
            if(key.pKey.wasPressedThisFrame)ArmPatrol();
            if(key.hKey.wasPressedThisFrame)Hold();
            if(key.sKey.wasPressedThisFrame)Stop();
            if(key.qKey.wasPressedThisFrame){if(SelectedHarbor)BuyShip(ShipKind.Galley);else Recruit(UnitKind.Footman);}
            if(key.wKey.wasPressedThisFrame){if(SelectedHarbor)BuyShip(ShipKind.Transport);else Recruit(UnitKind.Archer);}
            if(key.dKey.wasPressedThisFrame){if(Fleet.Count>0)UnloadFleet();else Recruit(UnitKind.Guard);}
            if(key.fKey.wasPressedThisFrame)Recruit(UnitKind.Mage);
            if(key.rKey.wasPressedThisFrame)Recruit(UnitKind.Mortar);
            if(key.cKey.wasPressedThisFrame)Recruit(SelectedHarbor?UnitKind.MarineGeneral:UnitKind.Medic);
            if(key.vKey.wasPressedThisFrame&&SelectedHarbor)Recruit(UnitKind.MarinePrivate);
            if(key.bKey.wasPressedThisFrame){if(SelectedHarbor)Recruit(UnitKind.MarineMajor);else BoardNearby();}
            if(key.tKey.wasPressedThisFrame)BuildTower();
            if(key.uKey.wasPressedThisFrame)UpgradeTown();
            if(key.nKey.wasPressedThisFrame)SelectFleet();
            if(key.f3Key.wasPressedThisFrame)FocusHarbor();
            if(key.spaceKey.wasPressedThisFrame){if(Fleet.Count>0)FocusFleet();else FocusSelection();}
            for(int i=1;i<=9;i++)if(key[(Key)((int)Key.Digit1+i-1)].wasPressedThisFrame)
            {
                if(key.leftCtrlKey.isPressed||key.rightCtrlKey.isPressed)
                {
                    PurgeStaleSelection();
                    groups[i]=Selection.Where(IsSelectableSoldier).Select(u=>new SoldierRef(u)).ToList();
                    shipGroups[i]=Fleet.Where(IsSelectableShip).Select(s=>new ShipRef(s)).ToList();
                }
                else if(groups.TryGetValue(i,out var stored))
                {
                    // A stored actor that died and came back from the pool is a
                    // different unit: recall keeps only the recorded identities.
                    stored.RemoveAll(entry=>!entry.Matches);
                    SelectUnits(stored.Select(entry=>entry.Unit));
                    if(shipGroups.TryGetValue(i,out var shipStored))
                    {
                        shipStored.RemoveAll(entry=>!entry.Matches);
                        SelectShips(shipStored.Select(entry=>entry.Vessel),true);
                    }
                    if(lastGroup==i&&Time.unscaledTime-lastGroupTime<.35f&&Selection.Count>0)Focus(Selection.Aggregate(Vector3.zero,(v,u)=>v+u.transform.position)/Selection.Count);
                    lastGroup=i;lastGroupTime=Time.unscaledTime;
                }
            }
            }
            inputRouter?.Tick();
            if(inputRouter!=null&&inputRouter.OwnsDirectPointer)
            {
                Hovered=null;RtsCursor.SetAttack(false);
                if(hoverRing)hoverRing.enabled=false;
                return;
            }
            if(mouse==null)return;
            // Mouse buttons and wheel must use that mouse's coordinates, even
            // when a hovering pen was the last generic Pointer device updated.
            var point=mouse.position.ReadValue();
            bool insideScreen=InsideScreen(point);
            if(insideScreen&&(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame||mouse.middleButton.wasPressedThisFrame))RequestCursorCapture();
            Hovered=!insideScreen||OverHud(point)?null:RtsPicking.Target(session,cam,point);
            RtsCursor.SetAttack(insideScreen&&!OverHud(point)&&(AttackCursor||(Hovered&&Hovered.Team!=0&&HasSelection)));
            if(!hoverRing)hoverRing=VisualFactory.Ring(new GameObject("Mouse target highlight").transform,1,.08f,Color.white);
            hoverRing.enabled=Hovered&&!(Hovered is Soldier selectedSoldier&&selectedSoldier.Selected)&&!(Hovered is Ship selectedShip&&selectedShip.Selected);
            if(Hovered)
            {
                hoverRing.transform.position=Hovered.transform.position;
                hoverRing.transform.localScale=Vector3.one*(Hovered is DefenseTower?1.15f:Hovered is Ship?ShipAppearance.SelectionRadius:.47f);
                hoverRing.startColor=hoverRing.endColor=Hovered.Team==0?new Color(.65f,1,.65f):new Color(1,.3f,.2f);
            }
            Vector3 pan=key==null?Vector3.zero:new Vector3((key.rightArrowKey.isPressed?1:0)-(key.leftArrowKey.isPressed?1:0),0,(key.upArrowKey.isPressed?1:0)-(key.downArrowKey.isPressed?1:0));
            float panMultiplier=1;
            if(pan.sqrMagnitude>.001f)
            {
                pan=pan.normalized;panMultiplier=Shift?1.7f:1;
            }
            else if(TryEdgePan(point,out var edgeDirection)){pan=edgeDirection.normalized;panMultiplier=1;}
            bool secondaryClick=false;
            if(mouse.rightButton.wasPressedThisFrame&&insideScreen&&!OverHud(point))
            { secondaryGesture.Begin(point.x,point.y); previousMouse=point; }
            if(!insideScreen)secondaryGesture.Cancel();
            if(mouse.rightButton.isPressed&&secondaryGesture.Move(point.x,point.y))CameraDragging=true;
            if(mouse.rightButton.wasReleasedThisFrame)secondaryClick=secondaryGesture.Release();
            if(mouse.middleButton.wasPressedThisFrame&&insideScreen&&!OverHud(point))
            {
                CameraDragging=true;previousMouse=point;
            }
            if(CameraDragging)
            {
                if(!insideScreen)
                {
                    // Do not apply the large delta produced when the pointer re-enters the window.
                    CameraDragging=false;previousMouse=point;
                }
                else if(mouse.middleButton.isPressed || mouse.rightButton.isPressed && secondaryGesture.Dragging)
                {
                    if((point-previousMouse).sqrMagnitude>.001f)
                    {
                        if(mouse.middleButton.isPressed)CameraRig.Orbit(point-previousMouse);
                        else CameraRig.Drag(previousMouse,point);
                    }
                    previousMouse=point;
                }
                else
                {
                    CameraDragging=false;previousMouse=point;
                }
            }
            if(!CameraDragging)
            {
                if(pan.sqrMagnitude>.001f)CameraRig.Pan(pan,Time.unscaledDeltaTime*panMultiplier);
                if(!mouse.middleButton.isPressed&&insideScreen&&!OverHud(point))CameraRig.ZoomAt(mouse.scroll.ReadValue().y,point);
                previousMouse=point;
            }
            if(mouse.leftButton.wasPressedThisFrame&&!OverHud(point))
            {
                if(OrderCursor)ExecuteArmedPointer(point);
                else BeginAreaSelection(point);
            }
            if(areaPointerActive&&mouse.leftButton.isPressed&&(point-DragStart).sqrMagnitude>36)UpdateAreaSelection(point);
            if(mouse.leftButton.wasReleasedThisFrame&&areaPointerActive)
            {
                if(Dragging)EndAreaSelection(point,Shift);
                else { areaPointerActive=false;Dragging=false;pressedWorld=false;PrimaryTap(point,Shift,true); }
            }
            if(secondaryClick&&insideScreen&&!OverHud(point))ContextAction(point);
        }
        bool InSelection(Soldier unit,Rect rect) { var p=cam.WorldToScreenPoint(unit.transform.position+Vector3.up);return p.z>0&&rect.Contains(new Vector2(p.x,Screen.height-p.y)); }
        bool InSelection(Ship ship,Rect rect) { var p=cam.WorldToScreenPoint(ship.transform.position+Vector3.up*2.4f);return p.z>0&&rect.Contains(new Vector2(p.x,Screen.height-p.y)); }
        void OnEnable() { gameplayFocus=true; }
        void OnDisable() { ReleaseCursor(); }
        void OnDestroy() { ReleaseCursor();inputRouter?.Dispose();RtsCursor.SetAttack(false); }
        bool OnScreen(Soldier unit) { var p=cam.WorldToViewportPoint(unit.transform.position);return p.z>0&&p.x>=0&&p.x<=1&&p.y>=.2f&&p.y<=.92f; }
    }
}
