using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    // Mirrors Core.UnitKind ordinals (cast directly): append new kinds at the end only.
    public sealed class Ship : CombatTarget, IOrderable, IPostClaimant, IQueuedOrderRunner
    {
        readonly List<Soldier> cargo=new List<Soldier>();
        readonly List<Soldier> cargoScratch=new List<Soldier>(10);
        readonly List<Vector3> route=new List<Vector3>();
        readonly List<Vector3> pathScratch=new List<Vector3>(64);
        readonly List<Vector3> plannedPath=new List<Vector3>(64);
        readonly OrderQueue orders=new OrderQueue();
        UnitCommand activeCommand;
        bool hasActiveCommand;
        CaptureOrderState capture;
        CapturePlan.View captureView;
        bool captureSailing;
        int plannedCommandId;
        bool plannedReady;
        Vector3 plannedPoint, plannedFrom;
        NavalWorld world;int routeIndex;float nextAttack,nextTargetPath,simDelta,nextSense,lastRouteProgressAt;
        Vector3 routeGoal, attackMoveGoal;
        bool hasRouteGoal, hasAttackMoveGoal;
        readonly List<CombatTarget> nearby = new List<CombatTarget>(48);
        CombatTarget target;
        bool attackMoveOrder;
        Harbor harborGuard;
        Harbor orderedHarbor;
        internal bool IsOrderedToHarbor(Harbor harbor) => orderedHarbor && orderedHarbor == harbor;
        bool pendingShoreUnload;
        Vector3 pendingShore;
        int unloadSlot;
        bool shoreFailureReported;
        public UnitKind Kind { get; private set; }
        public bool Selected { get; private set; }
        public Harbor Garrison=>harborGuard;
        public bool IsGarrison=>harborGuard;
        public IReadOnlyList<Soldier> Cargo=>cargo;
        public int CargoCount=>cargo.Count;
        public int CargoCapacity=>Capacity;
        public CombatTarget CurrentTarget=>target;
        public override ref readonly UnitType Type=>ref UnitCatalog.Get(Kind);
        public string DisplayName=>Type.Name;
        public string OrderLabel=>IsGarrison?"Guarnición · mantiene el puerto":target?"En combate":route.Count>routeIndex?"Navegando":"En puerto";
        public string LastActionError { get; private set; }
        public long RouteRevision { get; private set; }
        public float RemainingRouteDistance
        {
            get
            {
                float distance=0;Vector3 previous=transform.position;
                for(int i=routeIndex;i<route.Count;i++)
                {
                    var difference=route[i]-previous;difference.y=0;
                    distance+=difference.magnitude;previous=route[i];
                }
                return distance;
            }
        }
        const float RouteArrivalDistance=.4f;
        const float RouteStallSeconds=3f;
        float ShoreBerthSearchRadius=>Type.Transport.LoadRadius-RouteArrivalDistance-.05f;
        public bool IsAtOrRoutingTo(Vector3 point,float tolerance=1.5f)
        {
            var here=transform.position-point;here.y=0;if(here.sqrMagnitude<=tolerance*tolerance)return true;
            if(!hasRouteGoal||routeIndex>=route.Count||RouteHasStalled())return false;
            var goal=routeGoal-point;goal.y=0;return goal.sqrMagnitude<=tolerance*tolerance;
        }
        // A00V selects up to ten nearby units inside 512 native range (10.24 m): units.json transport.
        float LoadRadius=>Type.Transport.LoadRadius;
        public override float MaxHealth=>Type.MaxHealth;
        public override Vector3 AimPoint=>transform.position+Vector3.up*.55f;
        public override AttackKind AttackType=>Type.AttackType;
        public override ArmorKind ArmorType=>Type.ArmorType;
        public override float Armor=>Type.Armor;
        public float Speed=>Type.Speed;
        int Capacity=>Type.Transport.Capacity;
        float AttackRange=>Type.Weapon.Range;
        float AttackInterval=>Type.Weapon.Cooldown;
        BoxCollider targetVolume;

        public override Vector3 ApproachPoint(Vector3 from)
        {
            // Range and firing-position probes should stop at the oriented hull,
            // not at the vessel pivot in water. NavalArt owns the target volume.
            return targetVolume ? targetVolume.ClosestPoint(from) : transform.position;
        }

        internal void Initialize(NavalWorld naval,int team,UnitKind kind)
        {
            world=naval;Team=team;Kind=kind;Health=MaxHealth;harborGuard=orderedHarbor=null;hasActiveCommand=false;capture.Clear();captureView=default;captureSailing=false;plannedCommandId=0;plannedReady=false;plannedPath.Clear();unloadSlot=0;shoreFailureReported=false;orders.Reset();transform.position=new Vector3(transform.position.x,-.24f,transform.position.z);
            NavalArt.CreateShip(this);
            targetVolume=GetComponent<BoxCollider>();
        }
        public void Select(bool value){Selected=value;}
        bool HasPlan() => plannedReady && activeCommand.CommandId != 0 && activeCommand.CommandId == plannedCommandId
            && (transform.position - plannedFrom).sqrMagnitude < 1f;
        public bool RunsCommand(int commandId) => hasActiveCommand && activeCommand.CommandId == commandId;
        void RememberPlan()
        {
            plannedPath.Clear();
            for (int i = 0; i < pathScratch.Count; i++) plannedPath.Add(pathScratch[i]);
        }
        void CopyPlanToRoute()
        {
            route.Clear();
            for (int i = 0; i < plannedPath.Count; i++) route.Add(plannedPath[i]);
        }
        public void MoveTo(Vector3 point,bool attackMove=false)
        {
            LastActionError=null;
            if(HasPlan())
            {
                if(plannedPath.Count==0){ if(harborGuard&&harborGuard.IsInBerthCircle(point)) MaintainHarborGuard(); return; }
                if(!TryLeaveHarborGuard(plannedPoint))return;
                orderedHarbor=null;
                CopyPlanToRoute();routeIndex=0;routeGoal=plannedPoint;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;target=null;attackMoveOrder=attackMove;hasAttackMoveGoal=attackMove;attackMoveGoal=plannedPoint;pendingShoreUnload=false;
                plannedReady=false;
                return;
            }
            if(harborGuard&&harborGuard.IsInBerthCircle(point)){MaintainHarborGuard();return;}
            if(!IsAlive)return;
            Vector3 destination=point;
            if(!SeaNavigation.HasClearance(destination)&&!SeaNavigation.TryNearestOcean(point,UnitCatalog.TransportLoadRadius,out destination))
            {LastActionError="Elige mar o tierra cercana a la costa.";return;}
            if(!SeaNavigation.TryBuildPath(transform.position,destination,pathScratch)){LastActionError="No hay una ruta marítima hasta ese destino.";return;}
            if(!TryLeaveHarborGuard(destination))return;
            orderedHarbor=null;
            CopyScratchToRoute();routeIndex=0;routeGoal=destination;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;target=null;attackMoveOrder=attackMove;hasAttackMoveGoal=attackMove;attackMoveGoal=destination;pendingShoreUnload=false;
        }
        public void SailToHarbor(Harbor harbor)
        {
            LastActionError=null;
            if(!harbor){LastActionError="Elige un puerto de desembarco.";return;}
            if(Type.CanTransport)
            {
                if(!harbor.TryTransportLanding(out var landing,out _))
                {LastActionError="El puerto no tiene una playa o pasarela al alcance del transporte.";return;}
                LastActionError=SailToShore(landing);
                return;
            }
            // Complete an explicit docking order from the existing docking area.
            // The common claim pass still chooses the successor and changes owner;
            // it alone binds/snaps the guardian. Never jump across intervening land.
            if(IsAlive && !harbor.ClaimZone.Guardian && harbor.CanSnapToBerth(this))
            {
                if(!TryLeaveHarborGuard(harbor.Berth))return;
                HaltMotor();orderedHarbor=harbor;
                return;
            }
            MoveTo(harbor.Berth);
            if(LastActionError==null)orderedHarbor=harbor;
        }
        public bool Attack(CombatTarget enemy)
        {
            LastActionError=null;
            if(!IsAlive||!UnitTargeting.CanTarget(this,Team,Type.Weapon,enemy)){LastActionError=OrderQueue.InvalidError;return false;}
            if(HasPlan())
            {
                orderedHarbor=null;
                CopyPlanToRoute();routeIndex=0;routeGoal=enemy.transform.position;hasRouteGoal=plannedPath.Count>0;NoteRouteAccepted();RouteRevision++;target=enemy;attackMoveOrder=false;hasAttackMoveGoal=false;nextTargetPath=0;pendingShoreUnload=false;
                plannedReady=false;
                return true;
            }
            pathScratch.Clear();
            float distance=RangeTo(enemy);
            if(distance>AttackRange)
            {
                if(!SeaNavigation.TryNearestOcean(enemy.transform.position,8,out var ocean)||!SeaNavigation.TryBuildPath(transform.position,ocean,pathScratch))
                {LastActionError="No hay mar accesible a 8 m de ese objetivo.";return false;}
                if(!TryLeaveHarborGuard(ocean))return false;
            }
            orderedHarbor=null;
            CopyScratchToRoute();routeIndex=0;routeGoal=enemy.transform.position;hasRouteGoal=pathScratch.Count>0;NoteRouteAccepted();RouteRevision++;target=enemy;attackMoveOrder=false;hasAttackMoveGoal=false;nextTargetPath=0;pendingShoreUnload=false;
            return true;
        }
        public void Stop(){HaltMotor();orders.Clear();hasActiveCommand=false;capture.Clear();captureView=default;captureSailing=false;plannedReady=false;PublishOrders();}
        void HaltMotor(){orderedHarbor=null;route.Clear();routeIndex=0;hasRouteGoal=false;target=null;attackMoveOrder=false;hasAttackMoveGoal=false;pendingShoreUnload=false;}
        void CopyScratchToRoute(){route.Clear();for(int i=0;i<pathScratch.Count;i++)route.Add(pathScratch[i]);}
        /// <summary>Queues a source-style unload at a validated shore after sailing there.</summary>
        public string SailToShore(Vector3 shore)
        {
            LastActionError=null;
            if(!IsAlive||!Type.CanTransport)return LastActionError="Selecciona un transporte.";
            if(HasPlan())
            {
                if(!TryLeaveHarborGuard(plannedPoint))return LastActionError;
                orderedHarbor=null;
                CopyPlanToRoute();routeIndex=0;routeGoal=plannedPoint;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;target=null;attackMoveOrder=false;hasAttackMoveGoal=false;
                pendingShore=shore;pendingShoreUnload=true;unloadSlot=0;shoreFailureReported=false;plannedReady=false;return null;
            }
            if(!TryValidateShore(shore,out var landing,out var error))return LastActionError=error;
            if(!SeaNavigation.TryNearestOcean(landing,ShoreBerthSearchRadius,out var berth)||!SeaNavigation.TryBuildPath(transform.position,berth,pathScratch))return LastActionError="No hay una ruta marítima segura hasta esa playa.";
            if(!TryLeaveHarborGuard(berth))return LastActionError;
            orderedHarbor=null;
            CopyScratchToRoute();routeIndex=0;routeGoal=berth;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;target=null;attackMoveOrder=false;hasAttackMoveGoal=false;
            pendingShore=landing;pendingShoreUnload=true;unloadSlot=0;shoreFailureReported=false;return null;
        }
        public bool TryEmbark(Soldier soldier)
        {
            LastActionError=null;
            if(world.Session.Paused||world.Session.Winner>=0){LastActionError="No se puede embarcar con la partida detenida.";return false;}
            if(!IsAlive||!Type.CanTransport||cargo.Count>=Capacity||!soldier||!soldier.IsAlive||soldier.IsGarrison||soldier.Team!=Team||cargo.Contains(soldier)){LastActionError="El transporte no puede embarcar a ese soldado.";return false;}
            if(DistanceXZ(transform.position,soldier.transform.position)>LoadRadius){LastActionError="Acerca el transporte a menos de 10 m del soldado.";return false;}
            if(!ShoreAccess.TryLanding(soldier.transform.position,out _,out var shoreError)){LastActionError=shoreError;return false;}
            soldier.StopKeepingPassengerPlan();soldier.Select(false);if(soldier.Agent)soldier.Agent.enabled=false;
            cargo.Add(soldier);soldier.transform.SetParent(transform,false);soldier.gameObject.SetActive(false);return true;
        }
        public bool Unload(Harbor harbor)
        {
            return harbor&&UnloadAt(harbor.Landing);
        }
        /// <summary>A00X unloads at the vessel; this adapter requires a nearby walkable shore.</summary>
        public bool UnloadAt(Vector3 shore)
        {
            LastActionError=null;
            if(world.Session.Paused||world.Session.Winner>=0)return false;
            if(!IsAlive||!Type.CanTransport){LastActionError="Selecciona un transporte.";return false;}
            if(!TryFindDisembarkPoint(shore,out shore,out var error)){LastActionError=error;return false;}
            bool unloaded=false;
            cargoScratch.Clear();for(int i=0;i<cargo.Count;i++)cargoScratch.Add(cargo[i]);
            foreach(var soldier in cargoScratch)
            {
                if(!soldier){cargo.Remove(soldier);continue;}
                if(TryUnloadSoldier(soldier,shore,unloadSlot)){unloaded=true;unloadSlot++;}
            }
            if(!unloaded&&cargo.Count>0)LastActionError="No hay sitio transitable para desembarcar en esa playa.";
            return unloaded;
        }
        public bool TryFindDisembarkPoint(Vector3 requested,out Vector3 landing,out string error)
        {
            landing=default;error=null;
            if(!TryValidateShore(requested,out landing,out error))return false;
            if(DistanceXZ(transform.position,landing)>LoadRadius){error="Acerca el transporte a una playa marcada.";landing=default;return false;}
            return true;
        }
        public bool UnloadOneNearby(Soldier soldier)
        {
            LastActionError=null;
            if(world.Session.Paused||world.Session.Winner>=0)return false;
            if(!IsAlive||!Type.CanTransport||!soldier||!cargo.Contains(soldier))
            {LastActionError="Esa unidad ya no está dentro del transporte.";return false;}
            if(!ShoreAccess.TryNearestLanding(transform.position,LoadRadius,out var landing,out var error))
            {LastActionError=error;return false;}
            if(DistanceXZ(transform.position,landing)>LoadRadius)
            {LastActionError="Acerca el transporte a una playa marcada.";return false;}
            bool unloaded=TryUnloadSoldier(soldier,landing,0);
            if(!unloaded)LastActionError="No hay sitio transitable para desembarcar en esa playa.";
            return unloaded;
        }
        bool TryUnloadSoldier(Soldier soldier,Vector3 landing,int slot)
        {
            float spacing=UnitCatalog.Get(soldier.Kind).SpawnRadius*2+.14f;
            for(int attempt=0;attempt<24;attempt++)
            {
                int index=slot+attempt;
                float radius=index==0?0:spacing*(.75f+Mathf.Sqrt(index)*.55f);
                float angle=index*2.399963f;
                var spread=landing+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(!NavMesh.SamplePosition(spread,out var hit,.9f,NavMesh.AllAreas)||!ShoreAccess.IsWalkableLanding(hit.position))continue;
                soldier.transform.SetParent(null,true);soldier.transform.position=hit.position;soldier.transform.rotation=Quaternion.identity;soldier.gameObject.SetActive(true);
                if(soldier.Agent){soldier.Agent.enabled=true;soldier.Agent.Warp(hit.position);soldier.StopKeepingPassengerPlan();soldier.RestoreEmbarkOrders();}
                cargo.Remove(soldier);return true;
            }
            return false;
        }
        static bool TryValidateShore(Vector3 requested,out Vector3 landing,out string error)
            => ShoreAccess.TryLanding(requested,out landing,out error);
        public void SimTick(float delta)
        {
            simDelta=delta;
            if(!IsAlive||!world||world.Session.Paused||world.Session.Winner>=0)return;
            MaintainHarborGuard();
            if(pendingShoreUnload&&DistanceXZ(transform.position,pendingShore)<=LoadRadius)
            {
                // An empty hold has nothing to land. A blocked beach keeps the order
                // and retries; v0.33 did not sail the next order away with the army aboard.
                if(CargoCount==0){pendingShoreUnload=false;unloadSlot=0;shoreFailureReported=false;}
                else if(UnloadAt(pendingShore)&&CargoCount==0){pendingShoreUnload=false;unloadSlot=0;shoreFailureReported=false;}
                else if(CargoCount>0&&!string.IsNullOrEmpty(LastActionError))
                {
                    if(!shoreFailureReported&&Team==0){world.Session.Message(LastActionError,MessageKind.Info);shoreFailureReported=true;}
                }
            }
            if(!object.ReferenceEquals(target,null)&&!UnitTargeting.CanTarget(this,Team,Type.Weapon,target))
            {
                var kind=attackMoveOrder?UnitCommandKind.AttackMove:UnitCommandKind.Attack;
                target=null;
                if(UnitRules.OnTargetLost(kind)==UnitRules.TargetLost.KeepDestination){if(!ResumeAttackMove())ClearRoute();}
                else ClearRoute();
                PublishOrders();
            }
            // A garrison can fire and turn in place, but autonomous targeting may
            // not move it off the same fixed anchor used by the capture circle.
            if(IsGarrison&&target&&RangeTo(target)>AttackRange)target=null;
            if(!target&&Type.CanAttack&&world.Session.BattleTime>=nextSense&&(attackMoveOrder||routeIndex>=route.Count)){nextSense=world.Session.BattleTime+.2f;target=FindNearbyEnemy();}
            if(target&&RangeTo(target)<=AttackRange&&Visible(target))
            {
                Face(target.transform.position);
                if(world.Session.BattleTime>=nextAttack){nextAttack=world.Session.BattleTime+AttackInterval;world.Session.Combat.FireWeapon(AimPoint,target.AimPoint,target,world.Session.RollDamage(Type.Weapon),Team,this,Type.Weapon);}
            }
            else if(!IsGarrison&&target&&world.Session.BattleTime>=nextTargetPath)
            {
                if(SeaNavigation.TryNearestOcean(target.transform.position,8,out var ocean)&&SeaNavigation.TryBuildPath(transform.position,ocean,pathScratch))
                {
                    CopyScratchToRoute();routeIndex=0;routeGoal=ocean;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;nextTargetPath=world.Session.BattleTime+.7f;
                }
                else nextTargetPath=world.Session.BattleTime+.7f;
                if(routeIndex<route.Count)Advance();
            }
            else if(!IsGarrison&&routeIndex<route.Count)Advance();
            DrainOrders();
        }
        // Only enemies already inside weapon range (units.json acquisition); the query padding covers long hulls.
        CombatTarget FindNearbyEnemy()=>UnitTargeting.Acquire(world.Session,this,Team,Type,Type.Acquisition.RadiusHostile,false,default,0,nearby);
        void Advance()
        {
            Vector3 destination=route[routeIndex];Vector3 direction=destination-transform.position;direction.y=0;
            float beforeDistance=direction.magnitude;
            if(beforeDistance<RouteArrivalDistance)
            {
                routeIndex++;NoteRouteProgress();
                if(routeIndex>=route.Count&&hasAttackMoveGoal&&DistanceXZ(transform.position,attackMoveGoal)<1)hasAttackMoveGoal=false;
                return;
            }
            Vector3 separation=Vector3.zero;float separationDistance=Type.Separation;
            foreach(var other in world.Ships)if(other&&other!=this)
            {
                Vector3 away=transform.position-other.transform.position;away.y=0;float distance=away.magnitude;
                if(distance<separationDistance&&distance>.01f)separation+=away.normalized*(separationDistance-distance);
            }
            Vector3 next=Vector3.MoveTowards(transform.position,destination,Speed*simDelta);
            if(separation.sqrMagnitude>.001f)next+=separation.normalized*Mathf.Min(.8f,separation.magnitude)*simDelta*Speed;
            next.y=-.24f;
            if(!SeaNavigation.HasClearance(next)||!SeaNavigation.ClearSegment(transform.position,next))next=Vector3.MoveTowards(transform.position,destination,Speed*simDelta);
            if(SeaNavigation.HasClearance(next)&&SeaNavigation.ClearSegment(transform.position,next))
            {
                float afterDistance=DistanceXZ(next,destination);
                if(beforeDistance-afterDistance>=.02f)NoteRouteProgress();
                Face(next);transform.position=next;
            }
        }
        void NoteRouteAccepted(){lastRouteProgressAt=world&&world.Session!=null?world.Session.BattleTime:0;}
        void NoteRouteProgress(){lastRouteProgressAt=world&&world.Session!=null?world.Session.BattleTime:0;}
        bool RouteHasStalled()=>world&&world.Session!=null&&world.Session.BattleTime-lastRouteProgressAt>RouteStallSeconds;
        bool ResumeAttackMove()
        {
            if(!attackMoveOrder||!hasAttackMoveGoal)return false;
            if(DistanceXZ(transform.position,attackMoveGoal)<RouteArrivalDistance)
            {
                hasAttackMoveGoal=false;ClearRoute();return true;
            }
            if(!SeaNavigation.TryBuildPath(transform.position,attackMoveGoal,pathScratch))return false;
            CopyScratchToRoute();routeIndex=0;routeGoal=attackMoveGoal;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;
            return true;
        }
        void ClearRoute(){route.Clear();routeIndex=0;hasRouteGoal=false;}
        internal void BindHarborGuard(Harbor harbor)
        {
            if(!harbor||!Type.HarborGuard)return;
            harborGuard=harbor;orderedHarbor=null;route.Clear();routeIndex=0;hasRouteGoal=false;target=null;attackMoveOrder=false;hasAttackMoveGoal=false;
            MaintainHarborGuard();
        }
        internal void ReleaseHarborGuard(Harbor harbor)
        {
            if(harborGuard==harbor)harborGuard=null;
        }
        bool TryLeaveHarborGuard(Vector3 point)
        {
            if(!harborGuard||harborGuard.IsInBerthCircle(point))return true;
            if(harborGuard.TryReleaseNavalDefenderForOrder(this))return true;
            LastActionError="El barco guardia necesita un relevo aliado en el puerto.";
            return false;
        }
        void MaintainHarborGuard()
        {
            if(!harborGuard)return;
            var berth=harborGuard.Berth;
            transform.position=new Vector3(berth.x,-.24f,berth.z);
        }
        void Face(Vector3 point)
        {
            Vector3 direction=point-transform.position;direction.y=0;if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),180*simDelta);
        }
        bool Visible(CombatTarget enemy)=>UnitTargeting.Visible(Type.Acquisition.Visibility,this,enemy);
        public override void TakeDamage(float damage,int attacker,CombatTarget source=null)
        {
            if(!IsAlive||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||attacker==Team)return;
            if(Type.CanAttack&&Type.Acquisition.Retaliate&&source&&source.IsAlive&&!target&&(attackMoveOrder||routeIndex>=route.Count)){target=source;nextTargetPath=0;}
            Health=Mathf.Max(0,Health-damage);if(IsAlive)return;
            cargoScratch.Clear();for(int i=0;i<cargo.Count;i++)cargoScratch.Add(cargo[i]);
            for(int i=0;i<cargoScratch.Count;i++)if(cargoScratch[i])cargoScratch[i].DestroyEmbarked(attacker);
            cargo.Clear();route.Clear();routeIndex=0;hasRouteGoal=false;hasAttackMoveGoal=false;target=null;orderedHarbor=null;ReleaseHarborGuard(harborGuard);
            world.Ships.Remove(this);world.Session.UnregisterTarget(this);
            if(PlayerRules.IsPlayer(attacker)&&attacker<world.Session.PlayerCount){world.Session.Kills[attacker]++;world.Session.Economy.GrantBounty(attacker,Type.Points);}
            VisualFactory.Impact(AimPoint,new Color(.72f,.78f,.86f),.75f);Destroy(gameObject);
        }
        static float DistanceXZ(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        // Weapon range by the units.json measure (ToHull: to the target's attackable surface).
        float RangeTo(CombatTarget enemy)=>enemy?UnitTargeting.WeaponDistance(this,Type.Weapon,enemy):float.MaxValue;
        public OrderQueue Orders => orders;
        public int OrderLegCount => OrderLegView.Count(orders);
        public Vector3 OrderLegPoint(int index) => OrderLegView.Point(orders, index, world != null ? world.Session : null, hasActiveCommand, activeCommand);
        public UnitCommandKind OrderLegKind(int index) => OrderLegView.Kind(orders, index);
        public int ActivePathCount => routeIndex < route.Count ? route.Count - routeIndex + 1 : 0;
        public Vector3 ActivePathPoint(int index) => index == 0 ? transform.position : route[routeIndex + index - 1];
        public void RefreshActivePath() { }
        string IOrderable.OrderError => LastActionError;
        void IOrderable.ClearOrderError() => LastActionError = null;
        bool IOrderable.Authorize(in UnitCommand command, bool plan)
        {
            bool ok = OrderValidation.Check(world != null ? world.Session : null, this, command, plan, out var error);
            if (!ok) LastActionError = string.IsNullOrEmpty(error) ? OrderQueue.InvalidError : error;
            return ok;
        }
        bool IOrderable.ApplyOrder(in UnitCommand command) => ApplyOrder(command);
        bool IOrderable.HumanMoveEligible(in UnitCommand command) => false;
        void IOrderable.BeginHumanMove(in UnitCommand command, double submittedAt, double pausedAtSubmit, bool eligible) { }

        public bool ReleasePost(in UnitCommand command, bool commitRelease, out string error)
        {
            error = null;
            if (!harborGuard || command.Kind == UnitCommandKind.Stop || command.Kind == UnitCommandKind.Hold) return true;
            if (!LeavesBerth(command)) return true;
            if (!commitRelease)
            {
                if (harborGuard.CanReleaseNavalDefender(this)) return true;
                error = "El barco guardia necesita un relevo aliado en el puerto.";
                return false;
            }
            if (TryLeaveHarborGuard(plannedPoint.sqrMagnitude > 0 ? plannedPoint : new Vector3(command.X, command.Y, command.Z))) return true;
            error = string.IsNullOrEmpty(LastActionError) ? "El barco guardia necesita un relevo aliado en el puerto." : LastActionError;
            return false;
        }

        bool LeavesBerth(in UnitCommand command)
        {
            if (command.Kind == UnitCommandKind.Attack)
            {
                var enemy = world != null ? world.Session.FindTarget(command.TargetId) : null;
                return !enemy || RangeTo(enemy) > AttackRange;
            }
            if (command.Kind == UnitCommandKind.Capture)
            {
                var harbor = CaptureHarbor(command);
                if (!harbor || harbor == harborGuard) return false;
                return !harborGuard.IsInBerthCircle(harbor.Berth);
            }
            return !harborGuard.IsInBerthCircle(new Vector3(command.X, command.Y, command.Z));
        }

        public bool Reach(in UnitCommand command, bool plan, out string error)
        {
            if (plan && plannedReady && command.CommandId != 0 && command.CommandId == plannedCommandId
                && (transform.position - plannedFrom).sqrMagnitude < 1f)
            {
                error = null;
                return true;
            }
            LastActionError = null;
            var point = new Vector3(command.X, command.Y, command.Z);
            bool ok;
            switch (command.Kind)
            {
                case UnitCommandKind.Move:
                case UnitCommandKind.AttackMove:
                    ok = plan ? PlanMove(point) : CheapWater(point, UnitCatalog.TransportLoadRadius);
                    break;
                case UnitCommandKind.Attack:
                    ok = plan ? PlanAttack(command) : CheapAttack(command);
                    break;
                case UnitCommandKind.Unload:
                    ok = plan ? PlanUnload(point) : CheapUnload(point);
                    break;
                case UnitCommandKind.Capture:
                    ok = plan ? PlanCapture(command) : CheapCapture(command);
                    break;
                default:
                    ok = true;
                    break;
            }
            if (plan)
            {
                plannedReady = ok;
                plannedCommandId = ok ? command.CommandId : 0;
                if (ok) { plannedFrom = transform.position; RememberPlan(); }
            }
            error = ok ? null : (string.IsNullOrEmpty(LastActionError) ? OrderQueue.InvalidError : LastActionError);
            return ok;
        }

        bool CheapWater(Vector3 point, float search)
        {
            if (harborGuard && harborGuard.IsInBerthCircle(point)) return true;
            if (SeaNavigation.HasClearance(point) || SeaNavigation.TryNearestOcean(point, search, out _)) return true;
            LastActionError = "Elige mar o tierra cercana a la costa.";
            return false;
        }

        bool PlanMove(Vector3 point)
        {
            if (harborGuard && harborGuard.IsInBerthCircle(point)) { pathScratch.Clear(); plannedPoint = point; return true; }
            if (!PlanDestination(point, UnitCatalog.TransportLoadRadius, out plannedPoint)) return false;
            return true;
        }

        bool CheapAttack(in UnitCommand command)
        {
            var enemy = world != null ? world.Session.FindTarget(command.TargetId) : null;
            if (enemy && RangeTo(enemy) <= AttackRange) return true;
            if (enemy && SeaNavigation.TryNearestOcean(enemy.transform.position, 8, out _)) return true;
            LastActionError = "No hay mar accesible a 8 m de ese objetivo.";
            return false;
        }

        bool PlanAttack(in UnitCommand command)
        {
            var enemy = world != null ? world.Session.FindTarget(command.TargetId) : null;
            pathScratch.Clear();
            if (enemy && RangeTo(enemy) <= AttackRange) { plannedPoint = enemy.transform.position; return true; }
            if (enemy && SeaNavigation.TryNearestOcean(enemy.transform.position, 8, out var ocean) && SeaNavigation.TryBuildPath(transform.position, ocean, pathScratch))
            { plannedPoint = ocean; return true; }
            LastActionError = "No hay mar accesible a 8 m de ese objetivo.";
            return false;
        }

        bool CheapUnload(Vector3 point)
        {
            if (!Type.CanTransport) { LastActionError = "Selecciona un transporte."; return false; }
            if (!TryValidateShore(point, out var landing, out var error)) { LastActionError = error; return false; }
            if (SeaNavigation.TryNearestOcean(landing, ShoreBerthSearchRadius, out _)) return true;
            LastActionError = "No hay una ruta marítima segura hasta esa playa.";
            return false;
        }

        bool PlanUnload(Vector3 point)
        {
            if (!Type.CanTransport) { LastActionError = "Selecciona un transporte."; return false; }
            if (!TryValidateShore(point, out var landing, out var error)) { LastActionError = error; return false; }
            if (!SeaNavigation.TryNearestOcean(landing, ShoreBerthSearchRadius, out var berth) || !SeaNavigation.TryBuildPath(transform.position, berth, pathScratch))
            { LastActionError = "No hay una ruta marítima segura hasta esa playa."; return false; }
            plannedPoint = berth;
            return true;
        }

        bool CheapCapture(in UnitCommand command)
        {
            var harbor = CaptureHarbor(command);
            if (!harbor) { LastActionError = "Elige un puerto de desembarco."; return false; }
            if (Type.CanTransport)
            {
                if (!harbor.TryTransportLanding(out var landing, out _))
                { LastActionError = "El puerto no tiene una playa o pasarela al alcance del transporte."; return false; }
                return CheapUnload(landing);
            }
            return CheapWater(harbor.Berth, UnitCatalog.TransportLoadRadius);
        }

        bool PlanCapture(in UnitCommand command)
        {
            var harbor = CaptureHarbor(command);
            if (!harbor) { LastActionError = "Elige un puerto de desembarco."; return false; }
            if (Type.CanTransport)
            {
                if (!harbor.TryTransportLanding(out var landing, out _))
                { LastActionError = "El puerto no tiene una playa o pasarela al alcance del transporte."; return false; }
                return PlanUnload(landing);
            }
            return PlanMove(harbor.Berth);
        }

        bool PlanDestination(Vector3 point, float search, out Vector3 destination)
        {
            destination = point;
            if (SeaNavigation.HasClearance(destination) || SeaNavigation.TryNearestOcean(point, search, out destination))
            {
                if (SeaNavigation.TryBuildPath(transform.position, destination, pathScratch)) return true;
                LastActionError = "No hay una ruta marítima hasta ese destino.";
                return false;
            }
            LastActionError = "Elige mar o tierra cercana a la costa.";
            return false;
        }

        void PublishOrders()
        {
            if (!hasActiveCommand) { OrderLegView.Publish(orders, false, UnitCommandKind.Move, Vector3.zero); return; }
            var kind = pendingShoreUnload ? UnitCommandKind.Unload : activeCommand.Kind;
            Vector3 point = new Vector3(activeCommand.X, activeCommand.Y, activeCommand.Z);
            if (kind == UnitCommandKind.Unload) point = pendingShore;
            else if (kind == UnitCommandKind.Attack && target) point = target.transform.position;
            else if (route.Count > 0) point = route[route.Count - 1];
            OrderLegView.Publish(orders, true, kind, point);
        }

        bool ApproachFinished() => captureSailing && routeIndex >= route.Count && !pendingShoreUnload;

        bool CapturePending()
        {
            if (!hasActiveCommand || activeCommand.Kind != UnitCommandKind.Capture || !capture.Active || world == null) return false;
            captureView = CapturePlan.Fresh(world.Session, activeCommand, captureView);
            return !capture.Done(captureView.Found, captureView.Owner, Team, ApproachFinished(), Type.CanCapture);
        }

        /// <summary>A combat target does not keep a finished move, unload or capture busy.</summary>
        bool MotorIdle()
        {
            if (CapturePending() || routeIndex < route.Count || pendingShoreUnload) return false;
            if (hasAttackMoveGoal) return false;
            if (hasActiveCommand && !OrderAdvance.MotorIdle(activeCommand.Kind, target != null)) return false;
            return true;
        }

        bool BusyOrder() => !MotorIdle();
        void DrainOrders()
        {
            if (hasActiveCommand && activeCommand.Kind == UnitCommandKind.Capture && !CapturePending())
            {
                bool sailing = routeIndex < route.Count;
                hasActiveCommand = false;
                capture.Clear();
                captureView = default;
                captureSailing = false;
                // A pending unload still has to land its troops. Stopping here would leave them aboard.
                if (sailing && !pendingShoreUnload) HaltMotor();
            }
            if (!MotorIdle()) return;
            if (OrderAdvance.Drain(orders, this)) { PublishOrders(); return; }
            if (hasActiveCommand) { hasActiveCommand = false; PublishOrders(); }
        }

        bool IQueuedOrderRunner.TryStartQueued(in UnitCommand command)
        {
            if (!OrderValidation.Check(world != null ? world.Session : null, this, command, true, out _)) return false;
            if (!ReleasePost(command, true, out _)) return false;
            return Run(command);
        }

        bool ApplyOrder(in UnitCommand command)
        {
            bool willRun = UnitRules.Queue(command.Kind, command.Append, BusyOrder()) != UnitRules.OrderQueueAction.Append;
            bool valid = OrderValidation.Check(world != null ? world.Session : null, this, command, willRun, out var error);
            if (willRun && valid && command.Kind != UnitCommandKind.Stop && command.Kind != UnitCommandKind.Hold
                && !ReleasePost(command, true, out error))
                valid = false;
            if (!valid)
            {
                LastActionError = string.IsNullOrEmpty(error) ? OrderQueue.InvalidError : error;
                if (!willRun) plannedReady = false;
            }
            switch (orders.Commit(command, BusyOrder(), valid))
            {
                case OrderQueue.AdmitResult.Full:
                    LastActionError = OrderQueue.FullError;
                    return false;
                case OrderQueue.AdmitResult.Queued:
                    PublishOrders();
                    return true;
                case OrderQueue.AdmitResult.Rejected:
                    if (string.IsNullOrEmpty(LastActionError)) LastActionError = OrderQueue.InvalidError;
                    return false;
            }
            bool ok = Run(command);
            if (!ok) { hasActiveCommand = false; capture.Clear(); }
            PublishOrders();
            return ok;
        }

        bool Run(in UnitCommand command)
        {
            if (command.Kind != UnitCommandKind.Stop && command.Kind != UnitCommandKind.Hold)
            { activeCommand = command; hasActiveCommand = true; }
            var point = new Vector3(command.X, command.Y, command.Z);
            switch (command.Kind)
            {
                case UnitCommandKind.Move: MoveTo(point, false); return string.IsNullOrEmpty(LastActionError);
                case UnitCommandKind.AttackMove: MoveTo(point, true); return string.IsNullOrEmpty(LastActionError);
                case UnitCommandKind.Attack:
                    return Attack(world.Session.FindTarget(command.TargetId));
                case UnitCommandKind.Stop:
                case UnitCommandKind.Hold:
                    Stop();
                    return true;
                case UnitCommandKind.Unload:
                    bool sailed = string.IsNullOrEmpty(SailToShore(point));
                    if (sailed) { activeCommand = command; hasActiveCommand = true; }
                    return sailed;
                case UnitCommandKind.Capture:
                    var view = CapturePlan.Look(world.Session, command);
                    var harbor = view.Harbor ? view.Harbor : view.Town ? view.Town.Port : null;
                    if (!harbor) { LastActionError = "Elige un puerto de desembarco."; return false; }
                    captureView = view;
                    capture.Begin(view.Found, view.Owner);
                    captureSailing = true;
                    SailToHarbor(harbor);
                    if (!string.IsNullOrEmpty(LastActionError)) { capture.Clear(); captureView = default; captureSailing = false; return false; }
                    if (!CapturePending())
                    {
                        hasActiveCommand = false;
                        capture.Clear();
                        captureView = default;
                        captureSailing = false;
                        if (!OrderAdvance.Drain(orders, this)) { hasActiveCommand = false; PublishOrders(); }
                        return true;
                    }
                    return true;
                default:
                    LastActionError = OrderQueue.InvalidError;
                    return false;
            }
        }

        bool IPostClaimant.ContendsOnFoot => false;
        bool IPostClaimant.BlockedByOtherPost(CityClaimZone zone) => harborGuard && (zone == null || harborGuard.ClaimZone != zone);
        bool IPostClaimant.TryBindPost(CityClaimZone zone)
        {
            if (!this) return false;
            var port = zone != null ? zone.Port : null;
            if (!port || !IsAlive || !Type.CanCapture || (harborGuard && harborGuard != port)) return false;
            BindHarborGuard(port);
            return true;
        }
        void IPostClaimant.ReleasePost(CityClaimZone zone)
        {
            if (!this) return;
            var port = zone != null ? zone.Port : null;
            ReleaseHarborGuard(port ? port : harborGuard);
        }
        bool IPostClaimant.DropStale(CityClaimZone zone)
        {
            if (!this) return true;
            var port = zone != null ? zone.Port : null;
            if (port && port.HasNavalDefender) return false;
            ReleaseHarborGuard(harborGuard);
            return true;
        }

        Harbor CaptureHarbor(in UnitCommand command)
        {
            var found = StructureLookup.Find(world.Session, command);
            if (found.Harbor) return found.Harbor;
            return found.Town ? found.Town.Port : null;
        }

        /// <summary>World bounds of the clickable hull volume, for pointer picking.</summary>
        public bool TryGetHullBounds(out Bounds bounds)
        {
            if(targetVolume){bounds=targetVolume.bounds;return true;}
            bounds=default;return false;
        }
    }
}
