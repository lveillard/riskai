using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public sealed class Harbor : MonoBehaviour
    {
        sealed class Order { public ShipKind Kind; public int Team; public float Remaining; }
        readonly List<Order> queue=new List<Order>();
        NavalWorld world;TownState state;int lastOwner;
        CityClaimZone claimZone;LineRenderer claimRing;
        public Settlement LinkedTown { get; private set; }
        public DefenseTower Defense { get; private set; }
        public TownState State=>state;
        public bool IsIsland=>!LinkedTown;
        public CityClaimZone ClaimZone=>claimZone;
        public Soldier Defender=>claimZone?.Defender;
        public Vector3 Landing { get; private set; }
        public Vector3 Berth { get; private set; }
        public string DisplayName { get; private set; }
        public int Owner => state!=null?state.Owner:-1;
        public float CaptureProgress => state==null?0:state.Capture;
        public int QueueCount=>queue.Count;
        public float TrainingProgress=>queue.Count==0?0:1-queue[0].Remaining/TrainTime(queue[0].Kind);
        public ShipKind QueuedKind(int index)=>queue[index].Kind;
        public bool BuildingTower=>Defense&&Defense.UnderConstruction;
        int towerBuilder=-1;float towerBuildRemaining;

        public void Initialize(NavalWorld naval,string name,Settlement linked,TownState standalone,Vector3 landing,Vector3 berth)
        {
            world=naval;DisplayName=name;LinkedTown=linked;state=standalone??new TownState(name,linked?linked.State.Owner:-1,-1,-1);Landing=landing;Berth=berth;lastOwner=Owner;
            NavalArt.CreateHarbor(this);
            claimZone=new CityClaimZone(Landing);claimRing=VisualFactory.Ring(transform,ClaimRules.CircleRadius,.065f,VisualFactory.TeamColor(Owner));claimRing.transform.position=Landing;
            var towerObject=new GameObject("Torre de "+name);towerObject.transform.SetParent(transform,false);
            Vector3 direction=Berth-Landing;direction.y=0;direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
            // Opposite the harbormaster's house, with a clear silhouette and landing corridor.
            Vector3 side=new Vector3(-direction.z,0,direction.x);Vector3 towerPoint=Landing-side*4.8f;
            if(!MapLayout.IsLand(towerPoint.x,towerPoint.z))towerPoint=Landing-side*3.8f;
            towerPoint=MapLayout.Point(towerPoint.x,towerPoint.z);towerObject.transform.position=towerPoint;
            Defense=towerObject.AddComponent<DefenseTower>();Defense.Initialize(world.Session,this,true);
        }
        internal bool InitializeGarrison()
        {
            Soldier best=null;float score=float.MaxValue;int team=Owner>=0?Owner:2;
            foreach(var unit in world.Session.Units)
            {
                if(!unit || !unit.IsAlive || unit.Team!=team || unit.IsGarrison)continue;
                float distance=(unit.transform.position-Landing).sqrMagnitude;
                if(distance<score){best=unit;score=distance;}
            }
            if(!best)return false;
            best.Agent.Warp(Landing);claimZone.SetDefender(best);return claimZone.Defender;
        }
        public string Buy(ShipKind kind,int team=0)
        {
            if(kind!=ShipKind.Galley&&kind!=ShipKind.Transport)return "Tipo de barco inválido.";
            if(team<0||team>1)return "Bando inválido.";
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(world.Session.Winner>=0)return "La batalla ha terminado.";
            if(world.Session.Paused)return "Reanuda la partida para comprar barcos.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(queue.Count>=3)return "La cola naval está llena.";
            if(world.Ships.Count(s=>s&&s.IsAlive&&s.Team==team)+world.PendingShips(team)>=12)return "Límite naval de 12 barcos alcanzado.";
            int cost=Cost(kind);if(!world.Session.Economy.Spend(team,cost))return "Oro insuficiente para comprar este barco.";
            queue.Add(new Order{Kind=kind,Team=team,Remaining=TrainTime(kind)});return null;
        }
        internal int PendingCount(int team){int count=0;foreach(var item in queue)if(item.Team==team)count++;return count;}
        public string CancelTraining(int index,int team=0)
        {
            if(team<0||team>1)return "Bando inválido.";
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(world.Session.Winner>=0)return "La batalla ha terminado.";
            if(world.Session.Paused)return "Reanuda la partida para cancelar encargos.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(index<0||index>=queue.Count)return "Este encargo ya no está en la cola.";
            var item=queue[index];queue.RemoveAt(index);world.Session.Economy.Refund(item.Team,Cost(item.Kind));return null;
        }
        public string BuildTower(int team=0)
        {
            if(team<0||team>1)return "Bando inválido.";
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(world.Session.Winner>=0)return "La batalla ha terminado.";
            if(world.Session.Paused)return "Reanuda la partida para construir.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(!Defense||Defense.IsAlive)return "Este puerto ya tiene una torre.";
            if(Defense.UnderConstruction)return "Ya hay una obra en marcha en este puerto.";
            if(!world.Session.Economy.Spend(team,BattleRules.TowerCost))return "Oro insuficiente para esta obra.";
            towerBuilder=team;towerBuildRemaining=BattleRules.ConstructionSeconds;Defense.BeginBuild();return null;
        }
        public static ShipProfile Profile(ShipKind kind)=>NavalProfiles.Profile((NavalUnitKind)kind);
        public static int Cost(ShipKind kind)=>Profile(kind).Cost;
        public static float TrainTime(ShipKind kind)=>Profile(kind).TrainSeconds;
        public void SimTick(float delta)
        {
            if(!world||world.Session.Paused||world.Session.Winner>=0)return;
            if(Owner!=lastOwner){RefundQueue();CancelTowerBuild(true);Defense.ChangeOwner();lastOwner=Owner;}
            if(state!=null)
            {
                var previous=claimZone.Defender;int owner=claimZone.Step(world.Session,state.Owner,delta);
                state.Capture=claimZone.Progress;state.Capturing=claimZone.CapturingTeam;state.Contested=claimZone.Contested;
                if(claimZone.Defender&&claimZone.Defender!=previous)claimZone.Defender.HoldPosition();
                if(owner!=state.Owner){state.Owner=owner;Captured();}
                claimRing.startColor=claimRing.endColor=state.Contested?new Color(1,.7f,.15f):Color.Lerp(VisualFactory.TeamColor(Owner),Color.white,state.Capture*.65f);
            }
            if(Defense&&Defense.UnderConstruction)
            {
                towerBuildRemaining-=delta;Defense.SetBuildProgress(1-towerBuildRemaining/BattleRules.ConstructionSeconds);
                if(towerBuildRemaining<=0){Defense.CompleteBuild();towerBuildRemaining=0;towerBuilder=-1;}
            }
            if(queue.Count==0)return;
            queue[0].Remaining-=delta;if(queue[0].Remaining>0)return;
            var item=queue[0];queue.RemoveAt(0);var ship=world.Spawn(item.Team,item.Kind,Berth);
            if(!ship)world.Session.Economy.Refund(item.Team,Cost(item.Kind));
        }
        void Captured()
        {
            RefundQueue();CancelTowerBuild(true);Defense.ChangeOwner();lastOwner=Owner;world.Message(DisplayName+" conquistado por "+(Owner==0?"la alianza":"la frontera")+".");
        }
        void CancelTowerBuild(bool refund)
        {
            if(!Defense||!Defense.UnderConstruction)return;
            if(refund&&towerBuilder>=0)world.Session.Economy.Refund(towerBuilder,BattleRules.TowerCost);
            towerBuilder=-1;towerBuildRemaining=0;Defense.CancelBuild();
        }
        void RefundQueue(){foreach(var item in queue)world.Session.Economy.Refund(item.Team,Cost(item.Kind));queue.Clear();}
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.SqrMagnitude(a-b);}
    }
}
