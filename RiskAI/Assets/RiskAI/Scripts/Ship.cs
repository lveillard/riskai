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
        NavalWorld world;int routeIndex;float nextAttack,nextTargetPath;
        CombatTarget target;
        bool attackMoveOrder;
        Harbor unloadDestination;
        public ShipKind Kind { get; private set; }
        public bool Selected { get; private set; }
        public IReadOnlyList<Soldier> Cargo=>cargo;
        public int CargoCount=>cargo.Count;
        public CombatTarget CurrentTarget=>target;
        public string DisplayName=>Kind==ShipKind.Galley?"Galera":"Transporte";
        public string OrderLabel=>target?"En combate":route.Count>routeIndex?"Navegando":"En puerto";
        public override float MaxHealth=>Kind==ShipKind.Galley?500:300;
        public override Vector3 AimPoint=>transform.position+Vector3.up*.55f;
        public override AttackKind AttackType=>Kind==ShipKind.Galley?AttackKind.Siege:AttackKind.Normal;
        public override ArmorKind ArmorType=>ArmorKind.Heavy;
        public override float Armor=>Kind==ShipKind.Galley?2:1;
        public float Speed=>Kind==ShipKind.Galley?7:5;
        const int Capacity=6;
        const float AttackDamage=20,AttackRange=17,AttackInterval=1.5f;

        internal void Initialize(NavalWorld naval,int team,ShipKind kind)
        {
            world=naval;Team=team;Kind=kind;Health=MaxHealth;transform.position=new Vector3(transform.position.x,-.24f,transform.position.z);
            NavalArt.CreateShip(this);
        }
        public void Select(bool value){Selected=value;}
        public void MoveTo(Vector3 point,bool attackMove=false)
        {
            if(!IsAlive||!SeaNavigation.TryBuildPath(transform.position,point,out var next))return;
            route.Clear();route.AddRange(next);routeIndex=0;target=null;attackMoveOrder=attackMove;unloadDestination=null;
        }
        public void SailToHarbor(Harbor harbor){if(!harbor)return;MoveTo(harbor.Berth);if(route.Count>0)unloadDestination=harbor;}
        public void Attack(CombatTarget enemy)
        {
            if(!IsAlive||Kind!=ShipKind.Galley||!enemy||enemy.Team==Team||!enemy.IsAlive)return;
            var next=new List<Vector3>();
            float distance=DistanceXZ(transform.position,enemy.transform.position);
            if(distance>AttackRange)
            {
                if(!SeaNavigation.TryNearestOcean(enemy.transform.position,8,out var ocean)||!SeaNavigation.TryBuildPath(transform.position,ocean,out next))return;
            }
            route.Clear();route.AddRange(next);routeIndex=0;target=enemy;attackMoveOrder=true;nextTargetPath=0;
        }
        public void Stop(){route.Clear();routeIndex=0;target=null;attackMoveOrder=false;unloadDestination=null;}
        public bool TryEmbark(Soldier soldier)
        {
            if(world.Session.Paused||world.Session.Winner>=0)return false;
            if(!IsAlive||Kind!=ShipKind.Transport||cargo.Count>=Capacity||!soldier||!soldier.IsAlive||soldier.Team!=Team||cargo.Contains(soldier))return false;
            var harbor=world.NearestHarbor(soldier.transform.position,6);if(!harbor)return false;
            if(DistanceXZ(soldier.transform.position,harbor.Landing)>6||DistanceXZ(transform.position,harbor.Landing)>9.5f)return false;
            soldier.Stop();soldier.Select(false);if(soldier.Agent)soldier.Agent.enabled=false;
            cargo.Add(soldier);soldier.transform.SetParent(transform,false);soldier.gameObject.SetActive(false);return true;
        }
        public bool Unload(Harbor harbor)
        {
            if(world.Session.Paused||world.Session.Winner>=0)return false;
            if(!IsAlive||Kind!=ShipKind.Transport||!harbor||DistanceXZ(transform.position,harbor.Berth)>7)return false;
            bool unloaded=false;
            foreach(var soldier in cargo.ToArray())
            {
                if(!soldier){cargo.Remove(soldier);continue;}
                var spread=harbor.Landing+new Vector3((cargo.IndexOf(soldier)%3-1)*.7f,0,cargo.IndexOf(soldier)/3*.7f);
                if(!NavMesh.SamplePosition(spread,out var hit,5,NavMesh.AllAreas)||!MapLayout.IsLand(hit.position.x,hit.position.z))continue;
                soldier.transform.SetParent(null,true);soldier.transform.position=hit.position;soldier.transform.rotation=Quaternion.identity;soldier.gameObject.SetActive(true);
                if(soldier.Agent){soldier.Agent.enabled=true;soldier.Agent.Warp(hit.position);soldier.Stop();}
                cargo.Remove(soldier);unloaded=true;
            }
            return unloaded;
        }
        void Update()
        {
            if(!IsAlive||!world||world.Session.Paused||world.Session.Winner>=0)return;
            if(unloadDestination&&DistanceXZ(transform.position,unloadDestination.Berth)<4){Unload(unloadDestination);if(CargoCount==0)unloadDestination=null;}
            if(target&&!target.IsAlive||target&&target.Team==Team){target=null;route.Clear();routeIndex=0;}
            if(!target&&Kind==ShipKind.Galley&&(attackMoveOrder||routeIndex>=route.Count))target=FindNearbyEnemy();
            if(target&&DistanceXZ(transform.position,target.transform.position)<=AttackRange&&Visible(target))
            {
                route.Clear();routeIndex=0;Face(target.transform.position);
                if(Time.time>=nextAttack){nextAttack=Time.time+AttackInterval;VisualFactory.Arrow(AimPoint,target.AimPoint,target,AttackDamage,Team,this,AttackType);}
            }
            else if(target&&Time.time>=nextTargetPath)
            {
                if(SeaNavigation.TryNearestOcean(target.transform.position,8,out var ocean)&&SeaNavigation.TryBuildPath(transform.position,ocean,out var next))
                {
                    route.Clear();route.AddRange(next);routeIndex=0;nextTargetPath=Time.time+.7f;
                }
                else nextTargetPath=Time.time+.7f;
                if(routeIndex<route.Count)Advance();
            }
            else if(routeIndex<route.Count)Advance();
        }
        CombatTarget FindNearbyEnemy()
        {
            CombatTarget best=null;float score=float.MaxValue;
            foreach(var ship in world.Ships)
            {
                if(!ship||ship==this||!ship.IsAlive||ship.Team==Team)continue;
                float distance=DistanceXZ(transform.position,ship.transform.position);if(distance<=AttackRange&&distance<score){best=ship;score=distance;}
            }
            return best;
        }
        void Advance()
        {
            Vector3 destination=route[routeIndex];Vector3 direction=destination-transform.position;direction.y=0;
            if(direction.sqrMagnitude<.16f){routeIndex++;return;}
            Vector3 separation=Vector3.zero;
            foreach(var other in world.Ships)if(other&&other!=this)
            {
                Vector3 away=transform.position-other.transform.position;away.y=0;float distance=away.magnitude;
                if(distance<2.8f&&distance>.01f)separation+=away.normalized*(2.8f-distance);
            }
            Vector3 next=Vector3.MoveTowards(transform.position,destination,Speed*Time.deltaTime);
            if(separation.sqrMagnitude>.001f)next+=separation.normalized*Mathf.Min(.8f,separation.magnitude)*Time.deltaTime*Speed;
            next.y=-.24f;
            if(!SeaNavigation.HasClearance(next)||!SeaNavigation.ClearSegment(transform.position,next))next=Vector3.MoveTowards(transform.position,destination,Speed*Time.deltaTime);
            if(SeaNavigation.HasClearance(next)&&SeaNavigation.ClearSegment(transform.position,next)){Face(next);transform.position=next;}
        }
        void Face(Vector3 point)
        {
            Vector3 direction=point-transform.position;direction.y=0;if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),180*Time.deltaTime);
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
            cargo.Clear();route.Clear();routeIndex=0;target=null;
            world.Ships.Remove(this);world.Session.Targets.Remove(this);
            if(attacker>=0&&attacker<2){world.Session.Kills[attacker]++;world.Session.Economy.Gold[attacker]+=2;}
            VisualFactory.Impact(AimPoint,new Color(.72f,.78f,.86f),.75f);Destroy(gameObject);
        }
        static float DistanceXZ(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}
