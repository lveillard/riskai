using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskAI
{
    public sealed partial class RtsController : MonoBehaviour
    {
        public readonly List<CombatTarget> Selection=new List<CombatTarget>();
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
        public bool ShowHealthBars => !ChatInput.IsTyping && Keyboard.current!=null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);
        public bool HelpVisible;
        public bool ScoreboardVisible => !HelpVisible && !ChatInput.IsTyping && Keyboard.current!=null && Keyboard.current.tabKey.isPressed;
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
        readonly struct ActorRef
        {
            public readonly CombatTarget Actor;public readonly int Id;
            public ActorRef(CombatTarget actor){Actor=actor;Id=actor?actor.EntityId:0;}
            public bool Matches => Actor&&Actor.EntityId==Id;
        }
        // Identities are keyed by actor object, not by list position: reordering or
        // an external add/remove on the public list cannot re-key a known record.
        Dictionary<CombatTarget,int> selectionIds=new Dictionary<CombatTarget,int>();
        Dictionary<CombatTarget,int> nextSelectionIds=new Dictionary<CombatTarget,int>();
        readonly HashSet<CombatTarget> selectionLookup=new HashSet<CombatTarget>();
        readonly Dictionary<int,List<ActorRef>> groups=new Dictionary<int,List<ActorRef>>();
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
        bool Shift => !ChatInput.IsTyping && Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        // F4 is free: Alt shows health, Shift queues orders, Ctrl stores groups.
        bool FastPan => !ChatInput.IsTyping && Keyboard.current!=null && Keyboard.current.f4Key.isPressed;
        /// <summary>Shift, or the touch "Encolar" switch, appends an order.</summary>
        public bool QueueOrders => Shift || queueOrders;
        bool queueOrders;
        public bool QueueOrdersArmed => queueOrders;
        public void ToggleQueueOrders() { queueOrders = !queueOrders; }
        // Selection stays a plain public list; the identity map records which
        // actor each listed object was taken with. A pooled reuse therefore drops
        // the entry instead of inheriting the previous owner's orders, and a
        // record is never re-taken from an actor that is already known here.
        void AddSelected(CombatTarget actor) { Selection.Add(actor);if(actor&&!selectionIds.ContainsKey(actor))selectionIds[actor]=actor.EntityId; }
        void RemoveSelected(CombatTarget actor)
        {
            Selection.RemoveAll(listed=>listed==actor);
            if(actor)selectionIds.Remove(actor);
        }
        void ClearSelectionLists() { Selection.Clear();selectionIds.Clear(); }
        bool OwnsIdentity(CombatTarget actor) => actor&&selectionIds.TryGetValue(actor,out int id)&&id==actor.EntityId;
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
                if(!Selectable(unit)){unit.Select(false);continue;}
                nextSelectionIds[unit]=id;
                Selection[write++]=unit;
            }
            if(write<Selection.Count)Selection.RemoveRange(write,Selection.Count-write);
            (selectionIds,nextSelectionIds)=(nextSelectionIds,selectionIds);
            write=0;
            for(int i=0;i<pendingBoarders.Count;i++)if(pendingBoarders[i].Matches)pendingBoarders[write++]=pendingBoarders[i];
            if(write<pendingBoarders.Count)pendingBoarders.RemoveRange(write,pendingBoarders.Count-write);
        }
        bool IsSelected(CombatTarget actor) => OwnsIdentity(actor)&&Selection.Contains(actor);
        public void Clear()
        {
            if(SelectedCamp)SelectedCamp.Select(false);SelectedCamp=null;
            // Purge first: it drops replaced actors and lets an actor listed by an
            // external caller establish its identity, so nothing owned by this
            // selection is released without also clearing its ring.
            PurgeStaleSelection();
            foreach(var actor in Selection)actor.Select(false);
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
            if(!Selectable(ship))return;
            PurgeStaleSelection();
            if(append&&IsSelected(ship)){RemoveSelected(ship);ship.Select(false);return;}
            SelectActors(new CombatTarget[]{ship},append);
        }
        void SelectActors(IEnumerable<CombatTarget> actors,bool append=false)
        {
            PurgeStaleSelection();if(!append)Clear();
            if(SelectedCamp)SelectedCamp.Select(false);SelectedCamp=null;
            ClearSelectedBuildings();InspectedTarget=null;
            selectionLookup.Clear();foreach(var current in Selection)selectionLookup.Add(current);
            foreach(var actor in actors)if(Selectable(actor)&&selectionLookup.Add(actor)){AddSelected(actor);actor.Select(true);}
            selectionLookup.Clear();
        }
        public void SelectAll()
        {
            SelectActors(session.Units.Where(u=>Selectable(u)&&!u.IsGarrison));
            if(Selection.Count==0)session.Message("Recluta tropas móviles en una ciudad aliada. Los defensores mantienen sus círculos.",MessageKind.Info);
        }
        public void SelectOnly(Soldier unit) { SelectActors(new[]{unit});if(unit && unit.IsGarrison)session.Message("El defensor puede salir si un aliado ocupa su círculo como relevo.",MessageKind.Info); }
        public void SelectFleet()
        {
            if(!NavalWorld.Current){Clear();return;}
            SelectActors(NavalWorld.Current.Ships);
        }
        public void CancelCursor() { AttackCursor=MoveCursor=PatrolCursor=UnloadCursor=false; }
        bool HasSelection => Selection.Count>0;
        public void ArmAttack() { CancelCursor();PurgeStaleSelection();if(HasSelection)AttackCursor=true; }
        public void ArmMove() { CancelCursor();PurgeStaleSelection();if(HasSelection)MoveCursor=true; }
        public void ArmPatrol() { CancelCursor();PurgeStaleSelection();if(HasSelection)PatrolCursor=true; }
        public void Stop() { OrderEach(UnitCommandKind.Stop); }
        public void Hold() { OrderEach(UnitCommandKind.Hold); }
        void OrderEach(UnitCommandKind kind)
        {
            if(session.Paused||session.Winner>=0)return;
            PurgeStaleSelection();CancelBoardingForSelection();
            foreach(var actor in Selection)if(Selectable(actor))session.Commands.Submit(new UnitCommand(0,actor.EntityId,kind));
            CancelCursor();
        }
        static bool Selectable(CombatTarget actor) => actor && actor.PlayerSelectable;
        bool SelectionHasSeaMotor()
        {
            for(int i=0;i<Selection.Count;i++)if(Selection[i]&&Selection[i].Type.SeaMotor)return true;
            return false;
        }
        Ship SelectedTransport
        {
            get { foreach(var actor in Selection)if(Selectable(actor)&&actor.Type.CanTransport&&actor is Ship ship)return ship;return null; }
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

        public void UpgradeTown() { if(SelectedTown)Feedback(SelectedTown.Upgrade());else session.Message("Selecciona una ciudad tuya para mejorarla.",MessageKind.Info); }
        public void Feedback(string error) { if(error!=null)session.Message(error,MessageKind.Info); }
        CommandResult OrderShip(CombatTarget actor, UnitCommandKind kind, Vector3 point, int targetId = 0, bool append = false, string structureId = null, BuildingKind structureKind = BuildingKind.Settlement)
        {
            var result = session.Commands.SubmitResult(new UnitCommand(0, actor.EntityId, kind, point.x, point.y, point.z, targetId, append, structureId: structureId, structureKind: structureKind));
            if (!result.Accepted) Feedback(result.Error);
            return result;
        }
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
            foreach(var actor in Selection)if(Selectable(actor)){center+=actor.transform.position;count++;}
            if(count>0){Focus(center/count);return;}
            else if(SelectedTown)Focus(SelectedTown.transform.position);
            else FocusHome();
        }
        public void FocusFleet()
        {
            PurgeStaleSelection();
            Vector3 center=Vector3.zero;int count=0;
            foreach(var actor in Selection)if(Selectable(actor)&&actor.Type.SeaMotor){center+=actor.transform.position;count++;}
            if(count>0)Focus(center/count);
        }
        public void FocusHarbor()
        {
            var naval=NavalWorld.Current;if(!naval)return;
            Harbor best=naval.Harbors.FirstOrDefault(h=>h&&h.Owner==0);
            if(best){SelectHarbor(best);Focus(best.Landing);}
        }
        public void BoardNearby()
        {
            if(session.Paused||session.Winner>=0)return;
            PurgeStaleSelection();
            var transport=SelectedTransport;if(!transport){session.Message("Selecciona un transporte para embarcar.",MessageKind.Info);return;}
            var boarders=session.Units.Where(u=>Selectable(u)&&!u.IsGarrison&&FlatDistance(u.transform.position,transport.transform.position)<=UnitCatalog.TransportLoadRadius*UnitCatalog.TransportLoadRadius)
                .OrderBy(u=>FlatDistance(u.transform.position,transport.transform.position)).Take(UnitCatalog.TransportLoadLimit).ToList();
            if(boarders.Count==0){session.Message("Acerca tropas a la costa o selecciónalas y haz clic derecho en el transporte.",MessageKind.Info);return;}
            BeginBoarding(transport,boarders);
        }
        public void UnloadFleet()
        {
            if(session.Paused||session.Winner>=0)return;
            PurgeStaleSelection();
            var naval=NavalWorld.Current;if(!naval)return;
            CombatTarget anchor=null;foreach(var selected in Selection)if(Selectable(selected)&&selected.Type.SeaMotor){anchor=selected;break;}
            if(!anchor)return;
            Harbor harbor=null;float distance=12*12;
            foreach(var candidate in naval.Harbors)if(candidate)
            {float next=FlatDistance(candidate.Berth,anchor.transform.position);if(next<distance){distance=next;harbor=candidate;}}
            if(!Selection.Any(actor=>Selectable(actor)&&actor.Type.CanTransport&&actor.CargoCount>0)){session.Message("Selecciona un transporte con tropas a bordo.",MessageKind.Info);return;}
            CancelBoardingForSelection();
            if(!harbor){CancelCursor();UnloadCursor=true;session.Message("Desembarco: haz clic en una playa transitable. El transporte navegará hasta ella.",MessageKind.Info);return;}
            foreach(var actor in Selection)if(Selectable(actor)&&actor.Type.CanTransport)
            {
                var sailed=OrderShip(actor,UnitCommandKind.Capture,harbor.Landing,append:QueueOrders,structureId:harbor.BuildingId.LocalId,structureKind:BuildingKind.Harbor);
                if(!sailed.Accepted)session.Message(sailed.Error,MessageKind.Info);
            }
        }
        public void UnloadCargo(Ship transport,Soldier soldier)
        {
            if(session.Paused||session.Winner>=0||!Selectable(transport))return;
            if(transport.UnloadOneNearby(soldier))session.Message(UnitCatalog.Get(soldier.Kind).Name+" ha desembarcado.",MessageKind.Info);
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
            foreach(var guard in Selection.OfType<Soldier>().Where(unit=>unit.IsGarrison))
            {
                attemptedGuardOrder=true;
                if(session.Commands.Submit(new UnitCommand(0,guard.EntityId,kind,point.x,point.y,point.z,append:QueueOrders)))issued=true;
            }
            var mobile=Selection.OfType<Soldier>().Where(unit=>!unit.IsGarrison).ToArray();
            if(mobile.Length>0)
            {
                bool patrol=PatrolCursor;
                for(int i=0;i<mobile.Length;i++)if(!mobile[i].Type.CanPatrol)patrol=false;
                BattleSession.GiveFormation(mobile,point,attack,QueueOrders,patrol);
                issued=true;
            }
            bool fleetIssued=false;
            if(SelectionHasSeaMotor())
            {
                foreach(var actor in Selection)if(Selectable(actor)&&actor.Type.SeaMotor)
                {
                    var sail=attack?UnitCommandKind.AttackMove:UnitCommandKind.Move;
                    if(PatrolCursor&&actor.Type.CanPatrol)sail=UnitCommandKind.Patrol;
                    var sailed=OrderShip(actor,sail,point,append:QueueOrders);
                    if(sailed.Accepted)fleetIssued=true;
                }
                issued|=fleetIssued;
            }
            if(issued)ShowOrder(point,attack);
            if(!issued&&Selection.Count>0&&!attemptedGuardOrder)session.Message("No hay tropas disponibles para esa orden.",MessageKind.Info);
            if(!issued&&SelectedCamp)
            {
                if(session.Economy.CountryOwner(SelectedCamp.Country)!=0)session.Message("Controla todo el país para fijar la salida de sus refuerzos.",MessageKind.Info);
                else if(ExecuteBuilding(PlayerBuildingIntent.SetRally(SelectedCamp.BuildingId,point.x,point.y,point.z))==null){ShowOrder(point,false);session.Message("Salida de la hoguera actualizada.",MessageKind.Info);}
                else session.Message("Elige un punto de salida transitable.",MessageKind.Info);
            }
            else if(!issued&&SetSelectedBuildingRallies(point)) { ShowOrder(point,false);session.Message("Punto de reunión actualizado.",MessageKind.Info); }
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
            orderMarkerUntil=Time.unscaledTime+OrderRoutes.ConfirmSeconds;
            if(!QueueOrders)GetComponent<OrderRoutes>()?.Confirm();
            Sfx.Ui(attack?SfxId.OrderAttack:SfxId.OrderMove);
        }
        void AnimateOrderMarker()
        {
            if(!orderMarker || !orderMarker.enabled)return;
            float remaining=orderMarkerUntil-Time.unscaledTime;
            if(remaining<=0){orderMarker.enabled=false;return;}
            float progress=1-remaining/OrderRoutes.ConfirmSeconds;
            // Reuse one marker: a fast inward pulse confirms the destination,
            // then the same window fades it out.
            float pulse=1-Mathf.Pow(1-progress,3);
            orderMarker.transform.localScale=Vector3.one*Mathf.Lerp(1.6f,.55f,pulse);
            var color=orderMarkerColor;color.a=Mathf.Clamp01(remaining/OrderRoutes.ConfirmSeconds);
            orderMarker.startColor=orderMarker.endColor=color;
        }
        void BeginBoarding(Ship transport) => BeginBoarding(transport,Selection.OfType<Soldier>().ToList());
        void BeginBoarding(Ship transport,IReadOnlyList<Soldier> candidates)
        {
            if(!transport||transport.Team!=0||!transport.Type.CanTransport||candidates.Count==0)return;
            var naval=NavalWorld.Current;if(!naval)return;
            var available=candidates.Where(u=>Selectable(u)&&!u.IsGarrison).Take(Mathf.Min(UnitCatalog.TransportLoadLimit,transport.Type.Transport.Capacity-transport.CargoCount)).ToList();
            if(available.Count==0){session.Message("Transporte lleno o sólo defensores retenidos seleccionados.",MessageKind.Info);return;}
            CancelPendingBoarding();
            // Already within source loading radius: no arbitrary dock detour.
            for(int i=available.Count-1;i>=0;i--)if(transport.TryEmbark(available[i]))available.RemoveAt(i);
            if(available.Count==0){session.Message(GameText.Format("Embarque completado: {0} / {1}.",transport.CargoCount,transport.Type.Transport.Capacity),MessageKind.Info);return;}
            if(!naval.TryPlanEmbark(transport,available,out var landing,out var berth,out var error)){session.Message(error,MessageKind.Info);return;}
            pendingBoardingTransport=transport;pendingBoardingLanding=landing;
            var sailed=OrderShip(transport,UnitCommandKind.Move,berth,append:QueueOrders);
            if(!sailed.Accepted){session.Message(sailed.Error,MessageKind.Info);CancelPendingBoarding();return;}
            foreach(var soldier in available)
            {pendingBoarders.Add(new SoldierRef(soldier));session.Commands.Submit(new UnitCommand(0,soldier.EntityId,UnitCommandKind.Embark,landing.x,landing.y,landing.z,transport.EntityId,append:QueueOrders));}
            lastBoardingProgress=session.BattleTime;previousBoarderCount=pendingBoarders.Count;
            nextBoardingCheck=session.BattleTime;nextBoardingRecovery=session.BattleTime+1f;
            ShowOrder(landing,false);session.Message("Embarcando: tropas y transporte se reúnen en la costa marcada.",MessageKind.Info);
        }
        void ProcessPendingBoarding()
        {
            if(!pendingBoardingTransport)return;
            if(!Selectable(pendingBoardingTransport)){CancelPendingBoarding();return;}
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
                if(!boarder.Matches||!Selectable(soldier)||soldier.IsGarrison||pendingBoardingTransport.CargoCount>=pendingBoardingTransport.Type.Transport.Capacity||pendingBoardingTransport.TryEmbark(soldier))
                {pendingBoarders.RemoveAt(i);continue;}
                error=pendingBoardingTransport.LastActionError;
                var agent=soldier.Agent;
                var offset=soldier.transform.position-pendingBoardingLanding;offset.y=0;
                float remaining=agent&&agent.hasPath&&!agent.pathPending?agent.remainingDistance:offset.magnitude;
                distance+=float.IsNaN(remaining)||float.IsInfinity(remaining)?offset.magnitude:remaining;
                // Avoidance can stop a boarder just outside a narrow beach. Retry
                // the already validated landing, without relaxing shore rules.
                if(recover&&agent&&!agent.pathPending&&agent.velocity.sqrMagnitude<.04f&&offset.sqrMagnitude<=UnitCatalog.TransportLoadRadius*UnitCatalog.TransportLoadRadius)
                    soldier.RetryEmbarkApproach();
            }
            if(pendingBoarders.Count==0)
            {session.Message(GameText.Format("Embarque terminado: {0} / {1}.",pendingBoardingTransport.CargoCount,pendingBoardingTransport.Type.Transport.Capacity),MessageKind.Info);CancelPendingBoarding();return;}
            if(pendingBoarders.Count!=previousBoarderCount||distance<previousBoardingDistance-.1f)lastBoardingProgress=now;
            previousBoarderCount=pendingBoarders.Count;previousBoardingDistance=distance;
            if(now-lastBoardingProgress>=BoardingStallSeconds)
            {
                session.Message(GameText.Format("Embarque detenido: {0}",error??"las tropas no pueden avanzar hasta la costa marcada."),MessageKind.Info);
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
            // Consume the browser bridge before every focus/modal/HUD early return.
            // A menu scroll or stale pre-load touchpad burst must never zoom later.
            var mouse=Mouse.current;
            float wheelSteps=PlatformPresentation.ConsumeWheelSteps(mouse==null?0:mouse.scroll.ReadValue().y);
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
            // Chat owns the keyboard while typing: no game hotkey or arrow pan may fire.
            var key=ChatInput.IsTyping?null:Keyboard.current;
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
            if(key.f8Key.wasPressedThisFrame)session.Message(Music.ToggleMusic()?"Música activada":"Música desactivada",MessageKind.Info);
            if(key.f2Key.wasPressedThisFrame)FocusHome();
            if(key.escapeKey.wasPressedThisFrame) { ReleaseCursor();if(OrderCursor)CancelCursor();else Clear();return; }
            if(key.backspaceKey.wasPressedThisFrame)CameraRig.ResetView();
            if(TryGetProductionCard(out var productionCard))
            {
                // WC3 grid hotkeys: while an own city/harbor is selected its command card
                // owns Q W E R / A S D F / Z X C V, shadowing E/A/S/D unit keys.
                for(int cell=0;cell<ProductionGridKeys.Length;cell++)
                    if(key[ProductionGridKeys[cell]].wasPressedThisFrame)TriggerProductionCell(productionCard,cell);
            }
            else
            {
                if(key.eKey.wasPressedThisFrame)SelectAll();
                if(key.aKey.wasPressedThisFrame)ArmAttack();
                if(key.mKey.wasPressedThisFrame)ArmMove();
                if(key.pKey.wasPressedThisFrame)ArmPatrol();
                if(key.hKey.wasPressedThisFrame)Hold();
                if(key.sKey.wasPressedThisFrame)Stop();
                if(key.dKey.wasPressedThisFrame&&SelectionHasSeaMotor())UnloadFleet();
                if(key.bKey.wasPressedThisFrame)BoardNearby();
            }
            if(key.uKey.wasPressedThisFrame)UpgradeTown();
            if(key.nKey.wasPressedThisFrame)SelectFleet();
            if(key.f3Key.wasPressedThisFrame)FocusHarbor();
            if(key.spaceKey.wasPressedThisFrame){if(SelectionHasSeaMotor())FocusFleet();else if(!FocusLastAlertWhenIdle())FocusSelection();}
            for(int i=1;i<=9;i++)if(key[(Key)((int)Key.Digit1+i-1)].wasPressedThisFrame)
            {
                if(key.leftCtrlKey.isPressed||key.rightCtrlKey.isPressed)
                {
                    PurgeStaleSelection();
                    groups[i]=Selection.Where(Selectable).Select(actor=>new ActorRef(actor)).ToList();
                }
                else if(groups.TryGetValue(i,out var stored))
                {
                    // A stored actor that died and came back from the pool is a
                    // different unit: recall keeps only the recorded identities.
                    stored.RemoveAll(entry=>!entry.Matches);
                    SelectActors(stored.Select(entry=>entry.Actor));
                    if(lastGroup==i&&Time.unscaledTime-lastGroupTime<.35f&&Selection.Count>0)Focus(Selection.Aggregate(Vector3.zero,(v,actor)=>v+actor.transform.position)/Selection.Count);
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
            hoverRing.enabled=Hovered&&!Hovered.Selected;
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
                pan=pan.normalized;panMultiplier=FastPan?1.7f:1;
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
                if(!mouse.middleButton.isPressed&&insideScreen&&!OverHud(point))CameraRig.ZoomAt(wheelSteps,point);
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
        void OnEnable() { gameplayFocus=true; if(!GetComponent<OrderRoutes>())gameObject.AddComponent<OrderRoutes>(); }
        void OnDisable() { ReleaseCursor(); }
        void OnDestroy() { ReleaseCursor();inputRouter?.Dispose();RtsCursor.SetAttack(false); }
        bool OnScreen(Soldier unit) { var p=cam.WorldToViewportPoint(unit.transform.position);return p.z>0&&p.x>=0&&p.x<=1&&p.y>=.2f&&p.y<=.92f; }
    }
}
