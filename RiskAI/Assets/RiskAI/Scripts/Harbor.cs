using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed class Harbor : MonoBehaviour
    {
        sealed class Order { public UnitKind Kind; public int Team; public float Remaining; }
        readonly List<Order> queue=new List<Order>();
        NavalWorld world;TownState state;int lastOwner;
        CityClaimZone claimZone;LineRenderer claimRing,selectionRing,navalClaimRing;
        Ship navalDefender=>claimZone?.NavalDefender;
        bool sharesTown,canLaunch;
        bool transportLandingCached;
        Vector3 cachedTransportLanding, cachedTransportBerth;
        string launchBlockReason;
        public Settlement LinkedTown { get; private set; }
        public BuildingId BuildingId { get; private set; }
        public DefenseTower Defense { get; private set; }
        public BuildingVariant VisualVariant { get; private set; }
        public TownState State=>state;
        public bool IsIsland=>!LinkedTown;
        public bool IsImportedPort=>sharesTown;
        public bool CanLaunch=>canLaunch;
        public string LaunchBlockReason=>launchBlockReason;
        public CityClaimZone ClaimZone=>claimZone;
        public Soldier Defender=>claimZone?.Defender;
        public Vector3 Landing { get; private set; }
        public Vector3 Berth { get; private set; }
        Vector3 landRally;
        public Vector3 LandRally => sharesTown && LinkedTown ? LinkedTown.Rally : landRally;
        // The house and dock face the berth. Recruits use the landward threshold
        // instead of appearing at sea or walking through the building.
        public Vector3 LandEntry
        {
            get
            {
                if(sharesTown&&LinkedTown)return LinkedTown.PortLandEntry;
                var landward=Landing-Berth;landward.y=0;
                if(landward.sqrMagnitude<.01f)landward=Vector3.back;
                else landward.Normalize();
                return Landing+landward*2.5f;
            }
        }
        public string DisplayName { get; private set; }
        public int Owner => state!=null?state.Owner:-1;
        public float CaptureProgress => state==null?0:state.Capture;
        public int QueueCount=>queue.Count;
        public const int FleetCapacity = 12;
        /// <summary>Orders that reserve population. A linked town owns those orders.</summary>
        public int PopulationOrders
        {
            get
            {
                if(sharesTown&&LinkedTown)return LinkedTown.QueueCount;
                int count=0;
                for(int i=0;i<queue.Count;i++)if(!UnitCatalog.Get(queue[i].Kind).SeaMotor)count++;
                return count;
            }
        }
        /// <summary>Orders that spawn on the sea motor.</summary>
        public int SeaOrders
        {
            get
            {
                int count=0;
                for(int i=0;i<queue.Count;i++)if(UnitCatalog.Get(queue[i].Kind).SeaMotor)count++;
                return count;
            }
        }
        public bool Selected { get; private set; }
        public const float BerthRadius = 7.5f;
        public float TrainingProgress=>queue.Count==0?0:1-queue[0].Remaining/UnitCatalog.Get(queue[0].Kind).TrainSeconds;
        public UnitKind QueuedKind(int index)=>queue[index].Kind;
        public bool BuildingTower=>Defense&&Defense.UnderConstruction;
        BuildingTrainingView trainingView;
        LineRenderer rallyRing;

        public void Initialize(NavalWorld naval,BuildingId buildingId,string name,Settlement linked,TownState standalone,Vector3 landing,Vector3 berth,BuildingVariant? visualVariant=null)
        {
            transportLandingCached=false;cachedTransportLanding=default;cachedTransportBerth=default;
            VisualVariant=visualVariant??BuildingVariant.PierHarbor;
            if(!BuildingVariants.IsHarbor(VisualVariant))throw new System.ArgumentException("A harbor requires a harbor building variant.",nameof(visualVariant));
            sharesTown=false;canLaunch=SeaNavigation.HasClearance(berth);launchBlockReason=canLaunch?null:"El puerto no tiene una salida marítima segura.";
            world=naval;BuildingId=buildingId;DisplayName=name;LinkedTown=linked;state=standalone??new TownState(name,linked?linked.State.Owner:-1,-1,-1);Landing=landing;Berth=berth;landRally=LandEntry;lastOwner=Owner;
            var entrance=NavalArt.CreateHarbor(this,VisualVariant);
            trainingView=BuildingTrainingView.Create(transform,entrance);
            claimZone=new CityClaimZone(Landing);claimZone.AttachHarbor(this);claimRing=VisualFactory.Ring(transform,ClaimRules.CircleRadius,CityClaimZone.RingWidth,CityClaimZone.RingColor);claimRing.transform.position=Landing;
            var towerObject=new GameObject("Torre de "+name);towerObject.transform.SetParent(transform,false);
            Vector3 direction=Berth-Landing;direction.y=0;direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
            if(VisualVariant==BuildingVariant.IntegratedHarbor)towerObject.transform.position=Landing;
            else
            {
                // Opposite the harbormaster's house, with a clear silhouette and landing corridor.
                Vector3 side=new Vector3(-direction.z,0,direction.x);Vector3 towerPoint=Landing-side*4.8f;
                if(!MapLayout.IsLand(towerPoint.x,towerPoint.z))towerPoint=Landing-side*3.8f;
                towerPoint=MapLayout.Point(towerPoint.x,towerPoint.z);towerObject.transform.position=towerPoint;
            }
            Defense=towerObject.AddComponent<DefenseTower>();Defense.Initialize(world.Session,this,true,VisualVariant);
            selectionRing=BuildingSelection.CreateRing(this);
            CreateNavalClaimRing();
            rallyRing=VisualFactory.Ring(transform,.6f,.09f,new Color(.8f,1,.5f));rallyRing.transform.position=landRally;rallyRing.enabled=false;
        }
        internal void InitializeImported(NavalWorld naval,BuildingId buildingId,Settlement town,Vector3 berth,string unavailableReason,BuildingVariant? visualVariant=null)
        {
            transportLandingCached=false;cachedTransportLanding=default;cachedTransportBerth=default;
            VisualVariant=visualVariant??town.VisualVariant;
            if(!BuildingVariants.IsHarbor(VisualVariant))throw new System.ArgumentException("An imported port requires a harbor building variant.",nameof(visualVariant));
            if(VisualVariant!=town.VisualVariant)throw new System.ArgumentException("A linked harbor must share its settlement variant.",nameof(visualVariant));
            world=naval;BuildingId=buildingId;DisplayName=town.DisplayName;LinkedTown=town;state=town.State;claimZone=town.ClaimZone;Defense=town.Defense;
            Landing=town.ClaimPoint;Berth=berth;lastOwner=Owner;sharesTown=true;
            claimZone.AttachHarbor(this);
            canLaunch=string.IsNullOrEmpty(unavailableReason)&&SeaNavigation.HasClearance(berth);
            launchBlockReason=canLaunch?null:unavailableReason??"El puerto no tiene una salida marítima segura.";
            town.Port=this;
            // Initialization only: later player rally commands remain unchanged.
            town.SetRally(LandEntry);
            town.BindImportedPortEntry(LandEntry,Landing-Berth);
            CreateNavalClaimRing();
        }
        void CreateNavalClaimRing()
        {
            navalClaimRing=VisualFactory.Ring(transform,ClaimRules.CircleRadius,CityClaimZone.RingWidth,CityClaimZone.RingColor);
            navalClaimRing.transform.position=Landing;navalClaimRing.enabled=false;
        }
        internal bool InitializeGarrison()
        {
            if(sharesTown)return Defender;
            Soldier best=null;float score=float.MaxValue;int team=PlayerRules.ToCombatTeam(Owner);
            foreach(var unit in world.Session.Units)
            {
                if(!unit || !unit.IsAlive || unit.Team!=team || unit.IsGarrison)continue;
                float distance=(unit.transform.position-Landing).sqrMagnitude;
                if(distance<score){best=unit;score=distance;}
            }
            if(!best)return false;
            best.Agent.Warp(Landing);claimZone.SetDefender(best);return claimZone.Defender;
        }
        public bool IsEmbarkZone(Vector3 point)
        {
            return FlatDistance(point,Landing)<=UnitCatalog.TransportLoadRadius*UnitCatalog.TransportLoadRadius&&ShoreAccess.TryLanding(point,out _,out _);
        }
        public bool TryTransportLanding(out Vector3 landing,out Vector3 berth)
        {
            if(transportLandingCached){landing=cachedTransportLanding;berth=cachedTransportBerth;return true;}
            berth=default;
            if(!CanLaunch||!ShoreAccess.TryLanding(Landing,out landing,out _))
            {landing=default;return false;}
            // The guard/spawn berth may be farther offshore. Cargo needs its
            // own approach inside the same loading range used by the player.
            if(!SeaNavigation.TryNearestOcean(landing,UnitCatalog.TransportLoadRadius-.45f,out berth))return false;
            cachedTransportLanding=landing;cachedTransportBerth=berth;transportLandingCached=true;return true;
        }
        internal bool IsInBerthCircle(Vector3 point)=>FlatDistance(point,Berth)<=ClaimRules.CircleRadius*ClaimRules.CircleRadius;
        public bool IsShipDocked(Ship ship)=>ship&&FlatDistance(ship.transform.position,Berth)<=BerthRadius*BerthRadius;
        internal bool CanSnapToBerth(Ship ship) => ship && ship.IsAlive && ship.Type.HarborGuard &&
            IsShipDocked(ship) && SeaNavigation.HasClearance(ship.transform.position) &&
            SeaNavigation.HasClearance(Berth) && SeaNavigation.ClearSegment(ship.transform.position,Berth);
        public bool HasLivingDefender=>Defender&&Defender.IsAlive;
        public Ship NavalDefender=>navalDefender&&navalDefender.IsAlive&&navalDefender.Type.HarborGuard&&
            navalDefender.Team==Owner&&IsShipDocked(navalDefender)?navalDefender:null;
        public bool HasNavalDefender=>NavalDefender;
        public LineRenderer NavalClaimRing=>navalClaimRing;
        /// <summary>One post, owner and living guardian, for imported and authored ports.</summary>
        internal int StepClaim(float delta)
        {
            var previous=Defender;
            int owner=claimZone.Step(world.Session,Owner,delta);
            state.Capture=claimZone.Progress;state.Capturing=claimZone.CapturingTeam;state.Contested=claimZone.Contested;
            if(Defender&&Defender!=previous)Defender.HoldPosition();
            return owner;
        }
        public void Select(bool selected)
        {
            Selected=selected;
            if(selectionRing)selectionRing.enabled=selected;
            if(rallyRing)rallyRing.enabled=selected&&Owner==0;
            if(claimRing)claimRing.widthMultiplier=selected ? .11f : CityClaimZone.RingWidth;
        }
        public bool SetRally(Vector3 target)
        {
            if(sharesTown&&LinkedTown)return LinkedTown.SetRally(target);
            if(!NavMesh.SamplePosition(target,out var hit,8,NavMesh.AllAreas))return false;
            landRally=hit.position;if(rallyRing)rallyRing.transform.position=landRally;return true;
        }
        void Update()
        {
            if(rallyRing)rallyRing.enabled=Selected&&Owner==0;
            bool navalGuard=HasNavalDefender;
            if(claimRing)claimRing.enabled=!navalGuard;
            if(navalClaimRing)
            {
                navalClaimRing.enabled=navalGuard;
                navalClaimRing.transform.position=Landing;
                navalClaimRing.widthMultiplier=Selected ? .11f : .065f;
                navalClaimRing.startColor=navalClaimRing.endColor=CityClaimZone.VisibleRingColor(State.Contested);
            }
            if(sharesTown&&LinkedTown)LinkedTown.SetNavalClaimVisual(navalGuard);
        }
        internal bool CanReleaseNavalDefender(Ship departing) =>
            navalDefender != departing || claimZone.HasRelief(world.Session, departing);
        internal bool TryReleaseNavalDefenderForOrder(Ship departing)
        {
            if(navalDefender!=departing)return true;
            return claimZone.TryReleaseGuardianForOrder(world.Session,departing);
        }
        internal CombatTarget FindDockedSuccessor(int owner,CombatTarget excluded,bool alliesOnly)
        {
            if(!world)return null;
            Ship best=null;float bestDistance=float.MaxValue;
            foreach(var ship in world.Ships)
            {
                if(!ship||ship==excluded||!ship.IsAlive||!ship.Type.HarborGuard||ship.Garrison&&ship.Garrison!=this)continue;
                if(alliesOnly&&ship.Team!=owner)continue;
                float distance=FlatDistance(ship.transform.position,Berth);
                float radius=alliesOnly?ClaimRules.ReliefRadius:ship.Team==owner?ClaimRules.ProtectionRadius:ClaimRules.TakeoverRadius;
                // An explicit arrival may use the docking margin, but passive
                // capture and the living guardian's relief radius stay unchanged.
                if(!alliesOnly && ship.IsOrderedToHarbor(this) && CanSnapToBerth(ship))radius=Mathf.Max(radius,BerthRadius);
                if(distance>radius*radius)continue;
                if(ClaimRules.BetterCandidate(PlayerRules.ToCombatTeam(owner),ship.Team,distance,ship.EntityId,
                    best?best.Team:-1,bestDistance,best?best.EntityId:0))
                {
                    best=ship;
                    bestDistance=distance;
                }
            }
            return best;
        }
        void SetNavalDefender(Ship ship)
        {
            claimZone.SetNavalDefender(ship,this);
        }
        /// <summary>One queue. A linked town keeps land-motor orders; the type picks the spawn motor.</summary>
        public string Train(UnitKind kind,int team=0)
        {
            ref readonly var type=ref UnitCatalog.Get(kind);
            if(type.Building!=UnitBuilding.Harbor)return type.SeaMotor?"Tipo de barco inválido.":"Este muelle sólo entrena Marines.";
            if(!type.SeaMotor&&sharesTown&&LinkedTown)return LinkedTown.RecruitPortMarine(kind,team);
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(!PlayerRules.IsPlayer(team)||team>=world.Session.PlayerCount)return "Bando inválido.";
            if(type.SeaMotor&&!CanLaunch)return LaunchBlockReason;
            if(world.Session.Winner>=0)return "La batalla ha terminado.";
            if(world.Session.Paused)return type.SeaMotor?"Reanuda la partida para comprar barcos.":"Reanuda la partida para reclutar.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(queue.Count>=BattleRules.QueueCapacity)return type.SeaMotor?"La cola naval está llena.":"La cola de Marines está llena.";
            if(type.SeaMotor)
            {
                int alive=0;var ships=world.Ships;
                for(int i=0;i<ships.Count;i++){var ship=ships[i];if(ship&&ship.IsAlive&&ship.Team==team)alive++;}
                if(alive+world.PendingShips(team)>=FleetCapacity)return GameText.Format("Límite naval de {0} barcos alcanzado.",FleetCapacity);
            }
            else if(world.Session.RecruitmentReservations(team)>=BattleRules.PopulationLimit)return "Límite de soldados alcanzado.";
            if(!world.Session.Economy.Spend(team,type.Cost))return type.SeaMotor?"Oro insuficiente para comprar este barco.":"Oro insuficiente para reclutar este Marine.";
            queue.Add(new Order{Kind=kind,Team=team,Remaining=type.TrainSeconds});return null;
        }
        internal int PendingLandRecruits(int team)
        {
            if(sharesTown)return 0;
            int count=0;
            for(int i=0;i<queue.Count;i++)if(queue[i].Team==team&&!UnitCatalog.Get(queue[i].Kind).SeaMotor)count++;
            return count;
        }
        internal int PendingCount(int team)
        {
            int count=0;
            for(int i=0;i<queue.Count;i++)if(queue[i].Team==team&&UnitCatalog.Get(queue[i].Kind).SeaMotor)count++;
            return count;
        }
        public string CancelTraining(int index,int team=0)
        {
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(!PlayerRules.IsPlayer(team)||team>=world.Session.PlayerCount)return "Bando inválido.";
            if(world.Session.Winner>=0)return "La batalla ha terminado.";
            if(world.Session.Paused)return "Reanuda la partida para cancelar encargos.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(index<0||index>=queue.Count)return "Este encargo ya no está en la cola.";
            var item=queue[index];queue.RemoveAt(index);world.Session.Economy.Refund(item.Team,UnitCatalog.Get(item.Kind).Cost);return null;
        }
        public void SimTick(float delta)
        {
            if(!world||world.Session.Paused||world.Session.Winner>=0)return;
            if(navalDefender&&navalDefender.Team!=Owner)SetNavalDefender(null);
            if(Owner!=lastOwner)
            {
                RefundQueue();
                if(!sharesTown)Defense.ChangeOwner();
                lastOwner=Owner;
            }
            if(!sharesTown&&state!=null)
            {
                int owner=StepClaim(delta);
                if(owner!=state.Owner){int previous=state.Owner;state.Owner=owner;Captured();world.Session.Feedback.RaiseCaptured(new CaptureEvent(Landing,previous,owner,DisplayName,null,true,-1,false,false));}
                claimRing.startColor=claimRing.endColor=CityClaimZone.VisibleRingColor(state.Contested);
            }
            if(queue.Count>0)
            {
                var first=queue[0];first.Remaining-=delta;
                if(first.Remaining<=0)
                {
                    ref readonly var type=ref UnitCatalog.Get(first.Kind);
                    if(type.SeaMotor)
                    {
                        queue.RemoveAt(0);
                        var ship=CanLaunch?world.Spawn(first.Team,first.Kind,Berth):null;
                        if(!ship)world.Session.Economy.Refund(first.Team,type.Cost);
                    }
                    else if(world.Session.RecruitmentPopulation(first.Team)<BattleRules.PopulationLimit)
                    {
                        queue.RemoveAt(0);
                        var unit=world.Session.Spawn(first.Team,first.Kind,LandEntry);
                        if(unit)unit.TryMoveTo(LandRally,true,false);
                        else world.Session.Economy.Refund(first.Team,type.Cost);
                    }
                }
            }
            RefreshTrainingView();
        }
        void Captured()
        {
            RefundQueue();Defense.ChangeOwner();lastOwner=Owner;world.Session.Message(GameText.Format("{0} conquistado por {1}.",DisplayName,VisualFactory.TeamName(Owner)),MessageKind.Info);
        }
        void RefundQueue(){foreach(var item in queue)world.Session.Economy.Refund(item.Team,UnitCatalog.Get(item.Kind).Cost);queue.Clear();}
        void RefreshTrainingView()
        {
            bool land=false,sea=false;
            for(int i=0;i<queue.Count;i++)if(UnitCatalog.Get(queue[i].Kind).SeaMotor)sea=true;else land=true;
            if(sharesTown&&LinkedTown){LinkedTown.SetPortNavalTraining(sea);return;}
            if(trainingView)trainingView.SetActivity(land,sea,world.Session.BattleTime);
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.SqrMagnitude(a-b);}
    }
}
