using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskAI
{
    public sealed class RtsController : MonoBehaviour
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
        public bool OrderCursor => AttackCursor || MoveCursor || PatrolCursor;
        public bool ShowHealthBars => Keyboard.current!=null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);
        public bool HelpVisible;
        public bool EdgePan=true;
        public bool Dragging { get; private set; }
        public bool CameraDragging { get; private set; }
        public bool CursorCaptured { get; private set; }
        public Vector2 DragStart { get; private set; }
        public Vector2 Pointer => Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
        public Rect SelectionRect => Rect.MinMaxRect(Mathf.Min(DragStart.x,Pointer.x),Screen.height-Mathf.Max(DragStart.y,Pointer.y),Mathf.Max(DragStart.x,Pointer.x),Screen.height-Mathf.Min(DragStart.y,Pointer.y));
        readonly Dictionary<int,List<Soldier>> groups=new Dictionary<int,List<Soldier>>();
        readonly Dictionary<int,List<Ship>> shipGroups=new Dictionary<int,List<Ship>>();
        BattleSession session;Camera cam;Vector2 previousMouse;
        public RtsCameraRig CameraRig { get; private set; }
        bool pressedWorld;
        readonly PointerGesture secondaryGesture = new PointerGesture();
        bool cursorCaptureRequested;
        bool gameplayFocus;
        float lastSelectTime,lastGroupTime;int lastGroup=-1;UnitKind lastSelectKind;
        LineRenderer hoverRing;
        readonly List<Soldier> pendingBoarders=new();Ship pendingBoardingTransport;Vector3 pendingBoardingLanding;
        Harbor pendingUnloadHarbor;
        public void Initialize(BattleSession battle,Camera camera) { session=battle;cam=camera;gameplayFocus=true;CameraRig=gameObject.AddComponent<RtsCameraRig>();CameraRig.Initialize(camera);var home=session.Towns.FirstOrDefault(t=>t.State.Owner==0&&t.IsCapital);if(home)CameraRig.SetHome(home.transform.position);previousMouse=Pointer;RequestCursorCapture(); }
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
            secondaryGesture.Cancel();cursorCaptureRequested=false;CursorCaptured=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public bool OverHud(Vector2 screen) => screen.y<BattleHud.BottomPixels || screen.y>Screen.height-BattleHud.TopPixels || HelpVisible || session.Winner>=0;
        bool Shift => Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        public void Clear()
        {
            if(SelectedCamp)SelectedCamp.Select(false);SelectedCamp=null;
            foreach(var u in Selection)if(u)u.Select(false);Selection.Clear();
            foreach(var ship in Fleet)if(ship)ship.Select(false);Fleet.Clear();
            if(SelectedTown)SelectedTown.Selected=false;SelectedTown=null;
            SelectedHarbor=null;InspectedTarget=null;CancelPendingBoarding();pendingUnloadHarbor=null;
        }
        public void SelectCamp(CountryCamp camp) { Clear();SelectedCamp=camp;if(camp)camp.Select(true); }
        CountryCamp PickCamp(Vector2 pointer)
        {
            CountryCamp best=null;float distance=24;
            foreach(var camp in session.Camps)if(camp){var p=cam.WorldToScreenPoint(camp.transform.position+Vector3.up*.7f);float d=Vector2.Distance(pointer,p);if(p.z>0&&d<distance){best=camp;distance=d;}}
            return best;
        }
        public void SelectTown(Settlement town) { if(town&&town.Port){SelectHarbor(town.Port);return;}Clear();SelectedTown=town;if(town)town.Selected=true; }
        public void SelectHarbor(Harbor harbor) { Clear();SelectedHarbor=harbor; }
        public void SelectShip(Ship ship,bool append=false)
        {
            if(!ship||!ship.IsAlive||ship.Team!=0)return;
            if(append&&Fleet.Contains(ship)){Fleet.Remove(ship);ship.Select(false);CancelPendingBoarding();return;}
            if(!append)Clear();
            if(SelectedTown)SelectedTown.Selected=false;SelectedTown=null;SelectedHarbor=null;InspectedTarget=null;
            if(!Fleet.Contains(ship))Fleet.Add(ship);ship.Select(true);
        }
        void SelectUnits(IEnumerable<Soldier> units,bool append=false)
        {
            var list=units.Where(IsSelectableSoldier).ToList();if(!append)Clear();
            if(SelectedTown)SelectedTown.Selected=false;SelectedTown=null;
            foreach(var u in list)if(!Selection.Contains(u)){Selection.Add(u);u.Select(true);}
        }
        void SelectShips(IEnumerable<Ship> ships,bool append=false)
        {
            if(!append)Clear();
            if(SelectedTown)SelectedTown.Selected=false;SelectedTown=null;SelectedHarbor=null;InspectedTarget=null;
            foreach(var ship in ships)if(IsSelectableShip(ship)&&!Fleet.Contains(ship)){Fleet.Add(ship);ship.Select(true);}
        }
        public void SelectAll()
        {
            SelectUnits(session.Units.Where(u=>IsSelectableSoldier(u)&&!u.IsGarrison));
            if(Selection.Count==0)session.Message("Recluta tropas móviles en una ciudad aliada. Los defensores mantienen sus círculos.");
        }
        public void SelectOnly(Soldier unit) => SelectUnits(new[]{unit});
        public void SelectFleet()
        {
            if(!NavalWorld.Current){Clear();return;}
            SelectShips(NavalWorld.Current.Ships);
        }
        public void CancelCursor() { AttackCursor=MoveCursor=PatrolCursor=false;CancelPendingBoarding();pendingUnloadHarbor=null; }
        static CombatTarget AttackRecipient(CombatTarget target) => target is DefenseTower tower ? tower.Defender : target;
        bool HasSelection => Selection.Count>0||Fleet.Count>0;
        public void ArmAttack() { CancelCursor();if(HasSelection)AttackCursor=true; }
        public void ArmMove() { CancelCursor();if(HasSelection)MoveCursor=true; }
        public void ArmPatrol() { CancelCursor();if(HasSelection)PatrolCursor=true; }
        public void Stop() { if(session.Paused||session.Winner>=0)return;foreach(var u in Selection)if(IsSelectableSoldier(u))session.Commands.Submit(new UnitCommand(0,u.EntityId,UnitCommandKind.Stop));foreach(var ship in Fleet)if(IsSelectableShip(ship))ship.Stop();CancelCursor(); }
        public void Hold() { if(session.Paused||session.Winner>=0)return;foreach(var u in Selection)if(IsSelectableSoldier(u))session.Commands.Submit(new UnitCommand(0,u.EntityId,UnitCommandKind.Hold));foreach(var ship in Fleet)if(IsSelectableShip(ship))ship.Stop();CancelCursor(); }
        static bool IsSelectableSoldier(Soldier unit) => unit&&unit.Team==0&&unit.IsAlive&&unit.isActiveAndEnabled&&unit.Agent&&unit.Agent.enabled;
        static bool IsSelectableShip(Ship ship) => ship&&ship.Team==0&&ship.IsAlive&&ship.isActiveAndEnabled;
        Ship SelectedTransport
        {
            get { foreach(var ship in Fleet)if(IsSelectableShip(ship)&&ship.Kind==ShipKind.Transport)return ship;return null; }
        }
        void CancelPendingBoarding() { pendingBoarders.Clear();pendingBoardingTransport=null;pendingBoardingLanding=Vector3.zero; }

        public void Recruit(UnitKind kind)
        {
            var town=SelectedTown ? SelectedTown : session.Towns.FirstOrDefault(t=>t.State.Owner==0&&!t.IsPort);
            string error=town?town.Recruit(kind):"Conquista una ciudad para reclutar.";
            if(error!=null)session.Message(error);
            else { if(!SelectedTown)SelectTown(town);session.Message(BattleRules.Name(kind)+" en la cola de "+town.DisplayName+"."); }
        }
        public void BuildTower() { if(SelectedHarbor)Feedback(SelectedHarbor.BuildTower());else if(SelectedTown)Feedback(SelectedTown.BuildTower());else session.Message("Selecciona una ciudad o un puerto tuyo para reconstruir su torre."); }
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
            CancelPendingBoarding();pendingUnloadHarbor=null;
            if(CameraRig!=null)CameraRig.CancelMotion();
        }
        public void Focus(Vector3 position) => CameraRig.Focus(position);
        public void FocusSelection()
        {
            Vector3 center=Vector3.zero;int count=0;
            foreach(var unit in Selection)if(IsSelectableSoldier(unit)){center+=unit.transform.position;count++;}
            foreach(var ship in Fleet)if(IsSelectableShip(ship)){center+=ship.transform.position;count++;}
            if(count>0){Focus(center/count);return;}
            else if(SelectedTown)Focus(SelectedTown.transform.position);
            else FocusHome();
        }
        public void FocusFleet()
        {
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
            if(SelectedHarbor)Feedback(SelectedHarbor.Buy(kind));
            else if(session)session.Message("Selecciona un puerto para comprar barcos.");
        }
        public void BoardNearby()
        {
            if(session.Paused||session.Winner>=0)return;
            var transport=SelectedTransport;if(!transport)return;
            CancelPendingBoarding();
            foreach(var unit in session.Units.Where(IsSelectableSoldier).OrderBy(u=>FlatDistance(u.transform.position,transport.transform.position)).ToArray())
                if(transport.CargoCount<6)transport.TryEmbark(unit);
            session.Message("Transporte: "+transport.CargoCount+" / 6 soldados embarcados.");
        }
        public void UnloadFleet()
        {
            if(session.Paused||session.Winner>=0)return;
            var naval=NavalWorld.Current;if(!naval)return;
            Ship anchor=null;foreach(var selected in Fleet)if(IsSelectableShip(selected)){anchor=selected;break;}
            if(!anchor)return;
            Harbor harbor=null;float distance=12*12;
            foreach(var candidate in naval.Harbors)if(candidate)
            {float next=FlatDistance(candidate.Berth,anchor.transform.position);if(next<distance){distance=next;harbor=candidate;}}
            if(!harbor)return;
            foreach(var ship in Fleet)if(IsSelectableShip(ship)&&ship.Kind==ShipKind.Transport)ship.Unload(harbor);
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
            CancelPendingBoarding();pendingUnloadHarbor=null;
            Selection.RemoveAll(u=>!IsSelectableSoldier(u));Fleet.RemoveAll(s=>!IsSelectableShip(s));
            bool issued=Selection.Count>0;
            if(issued) { BattleSession.GiveFormation(Selection,point,attack,Shift,PatrolCursor);ShowOrder(point,attack); }
            if(Fleet.Count>0)
            {
                foreach(var ship in Fleet)if(IsSelectableShip(ship))ship.MoveTo(point,attack);
                issued=true;
            }
            if(!issued&&SelectedTown&&SelectedTown.State.Owner==0) { SelectedTown.SetRally(point);ShowOrder(point,false);session.Message("Punto de reunión actualizado."); }
            CancelCursor();
        }
        LineRenderer orderMarker;
        float orderMarkerUntil;
        void ShowOrder(Vector3 point,bool attack)
        {
            if(!orderMarker)orderMarker=VisualFactory.Ring(transform,.9f,.1f,Color.white);
            orderMarker.transform.position=point;orderMarker.enabled=true;
            orderMarker.startColor=orderMarker.endColor=attack?new Color(1,.35f,.22f):new Color(.55f,1,.65f);
            orderMarkerUntil=Time.unscaledTime+.7f;
        }
        void MoveFleetToHarbor(Harbor harbor)
        {
            if(!harbor||Fleet.Count==0)return;
            CancelPendingBoarding();pendingUnloadHarbor=harbor;
            foreach(var ship in Fleet)if(IsSelectableShip(ship))ship.SailToHarbor(harbor);
        }
        void BeginBoarding(Ship transport)
        {
            if(!transport||transport.Team!=0||transport.Kind!=ShipKind.Transport||Selection.Count==0)return;
            var naval=NavalWorld.Current;if(!naval)return;
            var harbor=naval.NearestHarbor(transport.transform.position,14);if(!harbor){session.Message("Acerca el transporte a un puerto para embarcar.");return;}
            CancelPendingBoarding();pendingBoardingTransport=transport;pendingBoardingLanding=harbor.Landing;pendingUnloadHarbor=null;
            foreach(var soldier in Selection.Where(IsSelectableSoldier).Take(6-transport.CargoCount))
            {pendingBoarders.Add(soldier);session.Commands.Submit(new UnitCommand(0,soldier.EntityId,UnitCommandKind.Move,harbor.Landing.x,harbor.Landing.y,harbor.Landing.z));}
        }
        void ProcessPendingBoarding()
        {
            if(!pendingBoardingTransport)return;
            if(!IsSelectableShip(pendingBoardingTransport)){CancelPendingBoarding();return;}
            for(int i=pendingBoarders.Count-1;i>=0;i--)
            {
                var soldier=pendingBoarders[i];
                if(!IsSelectableSoldier(soldier)||soldier.CurrentTarget||pendingBoardingTransport.CargoCount>=6||pendingBoardingTransport.TryEmbark(soldier))pendingBoarders.RemoveAt(i);
            }
            if(pendingBoarders.Count==0)CancelPendingBoarding();
        }
        void ProcessPendingUnload()
        {
            if(!pendingUnloadHarbor)return;
            bool waiting=false;
            foreach(var ship in Fleet)if(IsSelectableShip(ship)&&ship.Kind==ShipKind.Transport)
            {
                if(FlatDistance(ship.transform.position,pendingUnloadHarbor.Berth)>7*7){waiting=true;continue;}
                ship.Unload(pendingUnloadHarbor);
                if(ship.CargoCount>0)waiting=true;
            }
            if(!waiting)pendingUnloadHarbor=null;
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
            bool blocked=!EdgePan||!EffectiveFocus||runtimeCursorReleased||session.Paused||session.Winner>=0||HelpVisible||Dragging||pressedWorld||CameraDragging;
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
            if(orderMarker && Time.unscaledTime>=orderMarkerUntil)orderMarker.enabled=false;
            if(!EffectiveFocus)
            {
                ReleaseCursor();CameraDragging=false;Dragging=false;pressedWorld=false;previousMouse=Pointer;
                if(CameraRig!=null)CameraRig.CancelMotion();
                return;
            }
            if(session.Paused||session.Winner>=0)ReleaseCursor();
            else ApplyCursorCapture();
            var mouse=Mouse.current;var key=Keyboard.current;if(mouse==null||key==null)return;
            Selection.RemoveAll(u=>!IsSelectableSoldier(u));
            Fleet.RemoveAll(s=>!IsSelectableShip(s));
            ProcessPendingBoarding();ProcessPendingUnload();
            if(key.f1Key.wasPressedThisFrame)HelpVisible=!HelpVisible;
            if(key.f1Key.wasPressedThisFrame&&HelpVisible)ReleaseCursor();
            if(key.f10Key.wasPressedThisFrame)
            {
                session.TogglePause();
                if(session.Paused)ReleaseCursor();
            }
            if(key.escapeKey.wasPressedThisFrame&&HelpVisible)
            {
                HelpVisible=false;ReleaseCursor();CameraDragging=false;Dragging=false;pressedWorld=false;previousMouse=Pointer;
                if(CameraRig!=null)CameraRig.CancelMotion();
                return;
            }
            if(HelpVisible)
            {
                ReleaseCursor();CameraDragging=false;Dragging=false;pressedWorld=false;previousMouse=Pointer;Hovered=null;
                if(CameraRig!=null)CameraRig.CancelMotion();
                return;
            }
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
            if(key.cKey.wasPressedThisFrame)Recruit(UnitKind.Medic);
            if(key.bKey.wasPressedThisFrame)BoardNearby();
            if(key.tKey.wasPressedThisFrame)BuildTower();
            if(key.uKey.wasPressedThisFrame)UpgradeTown();
            if(key.nKey.wasPressedThisFrame)SelectFleet();
            if(key.f3Key.wasPressedThisFrame)FocusHarbor();
            if(key.spaceKey.wasPressedThisFrame){if(Fleet.Count>0)FocusFleet();else FocusSelection();}
            for(int i=1;i<=9;i++)if(key[(Key)((int)Key.Digit1+i-1)].wasPressedThisFrame)
            {
                if(key.leftCtrlKey.isPressed||key.rightCtrlKey.isPressed){groups[i]=Selection.Where(IsSelectableSoldier).ToList();shipGroups[i]=Fleet.Where(IsSelectableShip).ToList();}
                else if(groups.TryGetValue(i,out var stored))
                {
                    SelectUnits(stored);
                    if(shipGroups.TryGetValue(i,out var shipStored))SelectShips(shipStored,true);
                    if(lastGroup==i&&Time.unscaledTime-lastGroupTime<.35f&&Selection.Count>0)Focus(Selection.Aggregate(Vector3.zero,(v,u)=>v+u.transform.position)/Selection.Count);
                    lastGroup=i;lastGroupTime=Time.unscaledTime;
                }
            }
            var point=Pointer;
            bool insideScreen=InsideScreen(point);
            if(insideScreen&&(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame||mouse.middleButton.wasPressedThisFrame))RequestCursorCapture();
            Hovered=!insideScreen||OverHud(point)?null:RtsPicking.Target(session,cam,point);
            RtsCursor.SetAttack(insideScreen&&!OverHud(point)&&(AttackCursor||(Hovered&&Hovered.Team!=0&&HasSelection)));
            if(!hoverRing)hoverRing=VisualFactory.Ring(new GameObject("Mouse target highlight").transform,1,.08f,Color.white);
            hoverRing.enabled=Hovered;
            if(Hovered)
            {
                hoverRing.transform.position=Hovered.transform.position;
                hoverRing.transform.localScale=Vector3.one*(Hovered is DefenseTower?1.15f:Hovered is Ship?1.6f:.47f);
                hoverRing.startColor=hoverRing.endColor=Hovered.Team==0?new Color(.65f,1,.65f):new Color(1,.3f,.2f);
            }
            Vector3 pan=new Vector3((key.rightArrowKey.isPressed?1:0)-(key.leftArrowKey.isPressed?1:0),0,(key.upArrowKey.isPressed?1:0)-(key.downArrowKey.isPressed?1:0));
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
                    if((point-previousMouse).sqrMagnitude>.001f)CameraRig.Drag(previousMouse,point);
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
                if(OrderCursor)
                {
                    if(session.Paused||session.Winner>=0){CancelCursor();return;}
                    var victim=AttackCursor?AttackRecipient(RtsPicking.Target(session,cam,point,-1)):null;
                    if(victim&&victim.Team!=0) { foreach(var u in Selection)session.Commands.Submit(new UnitCommand(0,u.EntityId,UnitCommandKind.Attack,targetId:victim.EntityId));foreach(var ship in Fleet)ship.Attack(victim);CancelCursor(); }
                    else OrderAt(Ground(point),AttackCursor);
                    pressedWorld=false;
                }
                else { pressedWorld=true;DragStart=point;Dragging=false; }
            }
            if(pressedWorld&&mouse.leftButton.isPressed&&(point-DragStart).sqrMagnitude>36)Dragging=true;
            if(mouse.leftButton.wasReleasedThisFrame&&pressedWorld)
            {
                if(Dragging)
                {
                    var rect=SelectionRect;
                    SelectUnits(session.Units.Where(u=>IsSelectableSoldier(u)&&InSelection(u,rect)),Shift);
                    if(NavalWorld.Current)SelectShips(NavalWorld.Current.Ships.Where(s=>IsSelectableShip(s)&&InSelection(s,rect)),true);
                }
                else
                {
                    var camp=PickCamp(point);var picked=RtsPicking.Target(session,cam,point);var unit=picked as Soldier;var ship=picked as Ship;var town=RtsPicking.Town(session,cam,point);var harbor=RtsPicking.Harbor(session,cam,point);
                    if(camp)SelectCamp(camp);
                    else if(unit&&unit.Team==0)
                    {
                        bool sameType=(Time.unscaledTime-lastSelectTime<.3f&&lastSelectKind==unit.Kind)||key.leftCtrlKey.isPressed||key.rightCtrlKey.isPressed;
                        if(sameType)SelectUnits(session.Units.Where(u=>u.Team==0&&u.Kind==unit.Kind&&OnScreen(u)),Shift);
                        else if(Shift&&Selection.Contains(unit)){Selection.Remove(unit);unit.Select(false);}else SelectUnits(new[]{unit},Shift);
                        lastSelectTime=Time.unscaledTime;lastSelectKind=unit.Kind;
                    }
                    else if(ship&&ship.Team==0)SelectShip(ship,Shift);
                    else if(ship){Clear();InspectedTarget=ship;}
                    else if(picked is DefenseTower tower){if(tower.Team==0){if(tower.Harbor)SelectHarbor(tower.Harbor);else SelectTown(tower.Town);}else{Clear();InspectedTarget=tower;}}
                    else if(unit) { Clear();InspectedTarget=unit; }
                    else if(town)SelectTown(town);
                    else if(harbor)SelectHarbor(harbor);
                    else if(!Shift)Clear();
                }
                Dragging=false;pressedWorld=false;
            }
            if(secondaryClick&&insideScreen&&!OverHud(point))
            {
                if(OrderCursor){CancelCursor();return;}
                if(session.Paused||session.Winner>=0)return;
                var clickedEnemy=RtsPicking.Target(session,cam,point,-1);var enemy=AttackRecipient(clickedEnemy);var ally=RtsPicking.Target(session,cam,point,1) as Soldier;var town=RtsPicking.Town(session,cam,point);var harbor=RtsPicking.Harbor(session,cam,point);
                var ownShip=RtsPicking.Target(session,cam,point,1) as Ship;
                if(enemy&&enemy.Team!=0&&HasSelection)
                {
                    CancelPendingBoarding();pendingUnloadHarbor=null;
                    foreach(var u in Selection)if(IsSelectableSoldier(u))session.Commands.Submit(new UnitCommand(0,u.EntityId,UnitCommandKind.Attack,targetId:enemy.EntityId));
                    foreach(var ship in Fleet)if(IsSelectableShip(ship)&&ship.Kind==ShipKind.Galley)ship.Attack(enemy);
                    ShowOrder(enemy.transform.position,true);
                }
                else if(ownShip&&ownShip.Kind==ShipKind.Transport&&Selection.Count>0)BeginBoarding(ownShip);
                else if(harbor&&Fleet.Count>0)MoveFleetToHarbor(harbor);
                else if(ally&&!Selection.Contains(ally)&&Selection.Count>0){foreach(var u in Selection)session.Commands.Submit(new UnitCommand(0,u.EntityId,UnitCommandKind.Follow,targetId:ally.EntityId));ShowOrder(ally.transform.position,false);}
                else if(clickedEnemy is DefenseTower fort)OrderAt(fort.Town?fort.Town.ClaimPoint:fort.Harbor.Landing,true);
                else OrderAt(harbor?harbor.Landing:town?town.ClaimPoint:Ground(point),harbor?harbor.Owner!=0:town&&town.State.Owner!=0);
            }
        }
        bool InSelection(Soldier unit,Rect rect) { var p=cam.WorldToScreenPoint(unit.transform.position+Vector3.up);return p.z>0&&rect.Contains(new Vector2(p.x,Screen.height-p.y)); }
        bool InSelection(Ship ship,Rect rect) { var p=cam.WorldToScreenPoint(ship.transform.position+Vector3.up*2.4f);return p.z>0&&rect.Contains(new Vector2(p.x,Screen.height-p.y)); }
        void OnEnable() { gameplayFocus=true; }
        void OnDisable() { ReleaseCursor(); }
        void OnDestroy() { ReleaseCursor();RtsCursor.SetAttack(false); }
        bool OnScreen(Soldier unit) { var p=cam.WorldToViewportPoint(unit.transform.position);return p.z>0&&p.x>=0&&p.x<=1&&p.y>=.2f&&p.y<=.92f; }
    }
}
