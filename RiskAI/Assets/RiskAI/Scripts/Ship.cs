using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public enum ShipKind { Galley, Transport }

    public sealed class Ship : CombatTarget
    {
        readonly List<Soldier> cargo=new List<Soldier>();
        readonly List<Vector3> route=new List<Vector3>();
        NavalWorld world;int routeIndex;float nextAttack,nextTargetPath,simDelta,nextSense,lastRouteProgressAt;
        Vector3 routeGoal;
        bool hasRouteGoal;
        readonly List<CombatTarget> nearby = new List<CombatTarget>(48);
        CombatTarget target;
        bool attackMoveOrder;
        Harbor unloadDestination;
        Harbor harborGuard;
        bool pendingShoreUnload;
        Vector3 pendingShore;
        public ShipKind Kind { get; private set; }
        public bool Selected { get; private set; }
        public IReadOnlyList<Soldier> Cargo=>cargo;
        public int CargoCount=>cargo.Count;
        public CombatTarget CurrentTarget=>target;
        public ShipProfile Profile=>NavalProfiles.Profile((NavalUnitKind)Kind);
        public string DisplayName=>Profile.Name;
        public string OrderLabel=>target?"En combate":route.Count>routeIndex?"Navegando":"En puerto";
        public string LastActionError { get; private set; }
        public long RouteRevision { get; private set; }
        const float RouteArrivalDistance=.4f;
        const float RouteStallSeconds=3f;
        const float ShoreBerthSearchRadius=LoadRadius-RouteArrivalDistance-.05f;
        public bool IsAtOrRoutingTo(Vector3 point,float tolerance=1.5f)
        {
            var here=transform.position-point;here.y=0;if(here.sqrMagnitude<=tolerance*tolerance)return true;
            if(!hasRouteGoal||routeIndex>=route.Count||RouteHasStalled())return false;
            var goal=routeGoal-point;goal.y=0;return goal.sqrMagnitude<=tolerance*tolerance;
        }
        // A00V selects up to ten nearby units inside 512 native range (10.24 m).
        public const float LoadRadius = 10.24f;
        public const int LoadOrderLimit = 10;
        public override float MaxHealth=>Profile.Health;
        public override Vector3 AimPoint=>transform.position+Vector3.up*.55f;
        public override AttackKind AttackType=>Profile.Attack;
        public override ArmorKind ArmorType=>ArmorKind.Heavy;
        public override float Armor=>Profile.Armor;
        public float Speed=>Profile.Speed;
        int Capacity=>Profile.Capacity;
        float AttackDamage=>Profile.Damage;
        float AttackRange=>Profile.Range;
        float AttackInterval=>Profile.Cooldown;

        internal void Initialize(NavalWorld naval,int team,ShipKind kind)
        {
            world=naval;Team=team;Kind=kind;Health=MaxHealth;harborGuard=null;transform.position=new Vector3(transform.position.x,-.24f,transform.position.z);
            NavalArt.CreateShip(this);
        }
        public void Select(bool value){Selected=value;}
        public void MoveTo(Vector3 point,bool attackMove=false)
        {
            LastActionError=null;
            if(harborGuard&&harborGuard.IsInBerthCircle(point)){MaintainHarborGuard();return;}
            if(!IsAlive||!SeaNavigation.TryBuildPath(transform.position,point,out var next)){LastActionError="No hay una ruta marítima hasta ese destino.";return;}
            if(!TryLeaveHarborGuard(point))return;
            route.Clear();route.AddRange(next);routeIndex=0;routeGoal=point;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;target=null;attackMoveOrder=attackMove;unloadDestination=null;pendingShoreUnload=false;
        }
        public void SailToHarbor(Harbor harbor){if(!harbor)return;MoveTo(harbor.Berth);if(LastActionError==null)unloadDestination=harbor;}
        public void Attack(CombatTarget enemy)
        {
            if(!IsAlive||Kind!=ShipKind.Galley||!enemy||enemy.Team==Team||!enemy.CanBeAttacked)return;
            var next=new List<Vector3>();
            float distance=DistanceXZ(transform.position,enemy.transform.position);
            if(distance>AttackRange)
            {
                if(!SeaNavigation.TryNearestOcean(enemy.transform.position,8,out var ocean)||!SeaNavigation.TryBuildPath(transform.position,ocean,out next))return;
                if(!TryLeaveHarborGuard(ocean))return;
            }
            route.Clear();route.AddRange(next);routeIndex=0;routeGoal=enemy.transform.position;hasRouteGoal=next.Count>0;NoteRouteAccepted();RouteRevision++;target=enemy;attackMoveOrder=true;nextTargetPath=0;pendingShoreUnload=false;unloadDestination=null;
        }
        public void Stop(){route.Clear();routeIndex=0;hasRouteGoal=false;target=null;attackMoveOrder=false;unloadDestination=null;pendingShoreUnload=false;}
        /// <summary>Queues a source-style unload at a validated shore after sailing there.</summary>
        public string SailToShore(Vector3 shore)
        {
            if(!IsAlive||Kind!=ShipKind.Transport)return "Selecciona un transporte.";
            if(!TryValidateShore(shore,out var landing,out var error))return error;
            if(!SeaNavigation.TryNearestOcean(landing,ShoreBerthSearchRadius,out var berth)||!SeaNavigation.TryBuildPath(transform.position,berth,out var path))return "No hay una ruta marítima segura hasta esa playa.";
            if(!TryLeaveHarborGuard(berth))return LastActionError;
            route.Clear();route.AddRange(path);routeIndex=0;routeGoal=berth;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;target=null;attackMoveOrder=false;unloadDestination=null;
            pendingShore=landing;pendingShoreUnload=true;return null;
        }
        public bool TryEmbark(Soldier soldier)
        {
            LastActionError=null;
            if(world.Session.Paused||world.Session.Winner>=0){LastActionError="No se puede embarcar con la partida detenida.";return false;}
            if(!IsAlive||Kind!=ShipKind.Transport||cargo.Count>=Capacity||!soldier||!soldier.IsAlive||soldier.IsGarrison||soldier.Team!=Team||cargo.Contains(soldier)){LastActionError="El transporte no puede embarcar a ese soldado.";return false;}
            if(DistanceXZ(transform.position,soldier.transform.position)>LoadRadius){LastActionError="Acerca el transporte a menos de 10 m del soldado.";return false;}
            soldier.Stop();soldier.Select(false);if(soldier.Agent)soldier.Agent.enabled=false;
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
            if(!IsAlive||Kind!=ShipKind.Transport){LastActionError="Selecciona un transporte.";return false;}
            if(!TryFindDisembarkPoint(shore,out shore,out var error)){LastActionError=error;return false;}
            bool unloaded=false;
            foreach(var soldier in cargo.ToArray())
            {
                if(!soldier){cargo.Remove(soldier);continue;}
                var spread=shore+new Vector3((cargo.IndexOf(soldier)%3-1)*.7f,0,cargo.IndexOf(soldier)/3*.7f);
                if(!NavMesh.SamplePosition(spread,out var hit,5,NavMesh.AllAreas)||!MapLayout.IsLand(hit.position.x,hit.position.z))continue;
                soldier.transform.SetParent(null,true);soldier.transform.position=hit.position;soldier.transform.rotation=Quaternion.identity;soldier.gameObject.SetActive(true);
                if(soldier.Agent){soldier.Agent.enabled=true;soldier.Agent.Warp(hit.position);soldier.Stop();}
                cargo.Remove(soldier);unloaded=true;
            }
            if(!unloaded&&cargo.Count>0)LastActionError="No hay sitio transitable para desembarcar en esa playa.";
            return unloaded;
        }
        public bool TryFindDisembarkPoint(Vector3 requested,out Vector3 landing,out string error)
        {
            landing=default;error=null;
            if(DistanceXZ(transform.position,requested)>LoadRadius){error="Acerca el transporte a una playa marcada.";return false;}
            return TryValidateShore(requested,out landing,out error);
        }
        static bool TryValidateShore(Vector3 requested,out Vector3 landing,out string error)
        {
            landing=default;error=null;
            if(!MapLayout.IsLand(requested.x,requested.z)||!NavMesh.SamplePosition(requested,out var hit,1.25f,NavMesh.AllAreas)){error="El desembarco necesita una playa transitable.";return false;}
            if(!GentleShore(hit.position)){error="Ese borde es demasiado escarpado para desembarcar.";return false;}
            landing=hit.position;return true;
        }
        static bool GentleShore(Vector3 point)
        {
            const float probe=.8f;
            for(int i=0;i<4;i++)
            {
                Vector3 offset=i==0?Vector3.right*probe:i==1?Vector3.left*probe:i==2?Vector3.forward*probe:Vector3.back*probe;
                if(!NavMesh.SamplePosition(point+offset,out var neighbor,1.25f,NavMesh.AllAreas)||!MapLayout.IsLand(neighbor.position.x,neighbor.position.z)||Mathf.Abs(neighbor.position.y-point.y)>.7f)return false;
            }
            return true;
        }
        public void SimTick(float delta)
        {
            simDelta=delta;
            if(!IsAlive||!world||world.Session.Paused||world.Session.Winner>=0)return;
            MaintainHarborGuard();
            if(unloadDestination&&DistanceXZ(transform.position,unloadDestination.Berth)<4){Unload(unloadDestination);if(CargoCount==0)unloadDestination=null;}
            if(pendingShoreUnload&&DistanceXZ(transform.position,pendingShore)<=LoadRadius)
            {
                if(UnloadAt(pendingShore)&&CargoCount==0)pendingShoreUnload=false;
                else if(!string.IsNullOrEmpty(LastActionError)){world.Message(LastActionError);pendingShoreUnload=false;}
            }
            if(target&&!target.CanBeAttacked||target&&target.Team==Team){target=null;route.Clear();routeIndex=0;hasRouteGoal=false;}
            if(!target&&Kind==ShipKind.Galley&&world.Session.BattleTime>=nextSense&&(attackMoveOrder||routeIndex>=route.Count)){nextSense=world.Session.BattleTime+.2f;target=FindNearbyEnemy();}
            if(target&&DistanceXZ(transform.position,target.transform.position)<=AttackRange&&Visible(target))
            {
                route.Clear();routeIndex=0;Face(target.transform.position);
                if(world.Session.BattleTime>=nextAttack){nextAttack=world.Session.BattleTime+AttackInterval;world.Session.Combat.FireProjectile(AimPoint,target.AimPoint,target,world.Session.RollDamage(Profile),Team,this,AttackType);}
            }
            else if(target&&world.Session.BattleTime>=nextTargetPath)
            {
                if(SeaNavigation.TryNearestOcean(target.transform.position,8,out var ocean)&&SeaNavigation.TryBuildPath(transform.position,ocean,out var next))
                {
                    route.Clear();route.AddRange(next);routeIndex=0;routeGoal=ocean;hasRouteGoal=true;NoteRouteAccepted();RouteRevision++;nextTargetPath=world.Session.BattleTime+.7f;
                }
                else nextTargetPath=world.Session.BattleTime+.7f;
                if(routeIndex<route.Count)Advance();
            }
            else if(routeIndex<route.Count)Advance();
        }
        CombatTarget FindNearbyEnemy()
        {
            CombatTarget best=null;float score=float.MaxValue;
            world.Session.Spatial.Query(transform.position,AttackRange,nearby);
            foreach(var ship in nearby)
            {
                if(!ship||ship==this||!ship.CanBeAttacked||ship.Team==Team)continue;
                float distance=DistanceXZ(transform.position,ship.transform.position);if(distance<=AttackRange&&distance<score&&Visible(ship)){best=ship;score=distance;}
            }
            return best;
        }
        void Advance()
        {
            Vector3 destination=route[routeIndex];Vector3 direction=destination-transform.position;direction.y=0;
            float beforeDistance=direction.magnitude;
            if(beforeDistance<RouteArrivalDistance){routeIndex++;NoteRouteProgress();return;}
            Vector3 separation=Vector3.zero;
            foreach(var other in world.Ships)if(other&&other!=this)
            {
                Vector3 away=transform.position-other.transform.position;away.y=0;float distance=away.magnitude;
                if(distance<2.8f&&distance>.01f)separation+=away.normalized*(2.8f-distance);
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
        internal void BindHarborGuard(Harbor harbor)
        {
            if(!harbor||Kind!=ShipKind.Galley)return;
            harborGuard=harbor;route.Clear();routeIndex=0;hasRouteGoal=false;target=null;attackMoveOrder=false;
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
        bool Visible(CombatTarget enemy)
        {
            Vector3 from=AimPoint,to=enemy.AimPoint,delta=to-from;
            return delta.sqrMagnitude<.001f||!Physics.Raycast(from,delta.normalized,delta.magnitude,1<<MapLayout.TerrainLayer,QueryTriggerInteraction.Ignore);
        }
        public override void TakeDamage(float damage,int attacker,CombatTarget source=null)
        {
            if(!IsAlive||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||attacker==Team)return;
            if(Kind==ShipKind.Galley&&source&&source.IsAlive&&(attackMoveOrder||routeIndex>=route.Count)){target=source;nextTargetPath=0;}
            Health=Mathf.Max(0,Health-damage);if(IsAlive)return;
            foreach(var soldier in cargo.ToArray())if(soldier)soldier.DestroyEmbarked(attacker);
            cargo.Clear();route.Clear();routeIndex=0;hasRouteGoal=false;target=null;ReleaseHarborGuard(harborGuard);
            world.Ships.Remove(this);world.Session.UnregisterTarget(this);
            if(PlayerRules.IsPlayer(attacker)&&attacker<world.Session.PlayerCount){world.Session.Kills[attacker]++;world.Session.Economy.GrantBounty(attacker,Profile.PointValue);}
            VisualFactory.Impact(AimPoint,new Color(.72f,.78f,.86f),.75f);Destroy(gameObject);
        }
        static float DistanceXZ(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}
