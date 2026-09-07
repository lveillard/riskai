using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed class Harbor : MonoBehaviour
    {
        sealed class Order { public ShipKind Kind; public int Team; public float Remaining; }
        sealed class LandOrder { public UnitKind Kind; public int Team; public float Remaining; }
        readonly List<Order> queue=new List<Order>();
        readonly List<LandOrder> landQueue=new List<LandOrder>();
        NavalWorld world;TownState state;int lastOwner;
        CityClaimZone claimZone;LineRenderer claimRing,selectionRing,navalClaimRing;
        Ship navalDefender;
        bool sharesTown,canLaunch;
        string launchBlockReason;
        public Settlement LinkedTown { get; private set; }
        public BuildingId BuildingId { get; private set; }
        public DefenseTower Defense { get; private set; }
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
        public const int QueueCapacity = 5;
        public int LandQueueCount=>sharesTown&&LinkedTown?LinkedTown.QueueCount:landQueue.Count;
        public UnitKind QueuedLandKind(int index)=>sharesTown&&LinkedTown?LinkedTown.QueuedKind(index):landQueue[index].Kind;
        public float LandTrainingProgress=>sharesTown&&LinkedTown?LinkedTown.TrainingProgress:landQueue.Count==0?0:1-landQueue[0].Remaining/BattleRules.TrainTime(landQueue[0].Kind);
        public bool Selected { get; private set; }
        public const float EmbarkRadius = Ship.LoadRadius;
        public const float BerthRadius = 7.5f;
        public float TrainingProgress=>queue.Count==0?0:1-queue[0].Remaining/TrainTime(queue[0].Kind);
        public ShipKind QueuedKind(int index)=>queue[index].Kind;
        public bool BuildingTower=>Defense&&Defense.UnderConstruction;
        int towerBuilder=-1;float towerBuildRemaining;
        bool embarkIndicatorSynced,embarkIndicatorVisible;
        BuildingTrainingView trainingView;
        LineRenderer rallyRing;

        public void Initialize(NavalWorld naval,BuildingId buildingId,string name,Settlement linked,TownState standalone,Vector3 landing,Vector3 berth)
        {
            sharesTown=false;canLaunch=SeaNavigation.HasClearance(berth);launchBlockReason=canLaunch?null:"El puerto no tiene una salida marítima segura.";
            world=naval;BuildingId=buildingId;DisplayName=name;LinkedTown=linked;state=standalone??new TownState(name,linked?linked.State.Owner:-1,-1,-1);Landing=landing;Berth=berth;landRally=LandEntry;lastOwner=Owner;
            var entrance=NavalArt.CreateHarbor(this);
            trainingView=BuildingTrainingView.Create(transform,entrance);
            claimZone=new CityClaimZone(Landing);claimRing=VisualFactory.Ring(transform,ClaimRules.CircleRadius,.065f,VisualFactory.TeamColor(Owner));claimRing.transform.position=Landing;
            var towerObject=new GameObject("Torre de "+name);towerObject.transform.SetParent(transform,false);
            Vector3 direction=Berth-Landing;direction.y=0;direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
            // Opposite the harbormaster's house, with a clear silhouette and landing corridor.
            Vector3 side=new Vector3(-direction.z,0,direction.x);Vector3 towerPoint=Landing-side*4.8f;
            if(!MapLayout.IsLand(towerPoint.x,towerPoint.z))towerPoint=Landing-side*3.8f;
            towerPoint=MapLayout.Point(towerPoint.x,towerPoint.z);towerObject.transform.position=towerPoint;
            Defense=towerObject.AddComponent<DefenseTower>();Defense.Initialize(world.Session,this,true);
            selectionRing=BuildingSelection.CreateRing(this);
            CreateNavalClaimRing();
            rallyRing=VisualFactory.Ring(transform,.6f,.09f,new Color(.8f,1,.5f));rallyRing.transform.position=landRally;rallyRing.enabled=false;
        }
        internal void InitializeImported(NavalWorld naval,BuildingId buildingId,Settlement town,Vector3 berth,string unavailableReason)
        {
            world=naval;BuildingId=buildingId;DisplayName=town.DisplayName;LinkedTown=town;state=town.State;claimZone=town.ClaimZone;Defense=town.Defense;
            Landing=town.ClaimPoint;Berth=berth;lastOwner=Owner;sharesTown=true;
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
            navalClaimRing=VisualFactory.Ring(transform,ClaimRules.CircleRadius,.065f,VisualFactory.TeamColor(Owner));
            navalClaimRing.transform.position=Berth;navalClaimRing.enabled=false;
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
            return MapLayout.IsLand(point.x,point.z)&&FlatDistance(point,Landing)<=EmbarkRadius*EmbarkRadius;
        }
        internal bool IsInBerthCircle(Vector3 point)=>FlatDistance(point,Berth)<=BerthRadius*BerthRadius;
        public bool IsShipDocked(Ship ship)=>ship&&FlatDistance(ship.transform.position,Berth)<=BerthRadius*BerthRadius;
        public bool HasLivingDefender=>Defender&&Defender.IsAlive;
        public Ship NavalDefender=>navalDefender&&navalDefender.IsAlive&&navalDefender.Kind==ShipKind.Galley&&
            navalDefender.Team==Owner&&IsShipDocked(navalDefender)?navalDefender:null;
        public bool HasNavalDefender=>NavalDefender;
        public LineRenderer NavalClaimRing=>navalClaimRing;
        public void Select(bool selected)
        {
            Selected=selected;
            if(selectionRing)selectionRing.enabled=selected;
            if(rallyRing)rallyRing.enabled=selected&&Owner==0;
            if(claimRing)claimRing.widthMultiplier=selected ? .11f : .065f;
            SyncEmbarkIndicator();
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
                navalClaimRing.transform.position=Berth;
                navalClaimRing.widthMultiplier=Selected ? .11f : .065f;
                navalClaimRing.startColor=navalClaimRing.endColor=State.Contested?new Color(1,.7f,.15f):Color.Lerp(VisualFactory.TeamColor(Owner),Color.white,State.Capture*.65f);
            }
            if(sharesTown&&LinkedTown)LinkedTown.SetNavalClaimVisual(navalGuard);
            // City-sized claim/guard circles stay present. The larger loading-area
            // indicator is an interaction aid, so it appears only when selected.
            SyncEmbarkIndicator();
        }
        void SyncEmbarkIndicator()
        {
            if(!world)return;
            bool found=false;
            foreach(var zone in world.EmbarkZones)
            {
                if(!zone||zone.Harbor!=this)continue;
                found=true;
                if(embarkIndicatorSynced&&embarkIndicatorVisible==Selected)break;
                foreach(var ring in zone.GetComponentsInChildren<LineRenderer>(true))ring.enabled=Selected;
            }
            if(found){embarkIndicatorSynced=true;embarkIndicatorVisible=Selected;}
        }
        /// <summary>Called after land claim resolution; a live land defender always wins.</summary>
        public int ResolveNavalOwner(int previousOwner)
        {
            if(HasLivingDefender){SetNavalDefender(null);return previousOwner;}
            var successor=FindDockedSuccessor(previousOwner,null,false);
            SetNavalDefender(successor);
            return successor?successor.Team:PlayerRules.NeutralOwner;
        }
        internal bool TryReleaseNavalDefenderForOrder(Ship departing)
        {
            if(navalDefender!=departing)return true;
            if(HasLivingDefender&&Defender.Team==departing.Team){SetNavalDefender(null);return true;}
            if(claimZone!=null&&claimZone.TryAssignCircleReplacement(world.Session,departing.Team))
            {
                SetNavalDefender(null);
                return true;
            }
            var successor=FindDockedSuccessor(departing.Team,departing,true);
            if(!successor)return false;
            SetNavalDefender(successor);
            return true;
        }
        Ship FindDockedSuccessor(int owner,Ship excluded,bool alliesOnly)
        {
            if(!world)return null;
            Ship best=null;float bestDistance=float.MaxValue;
            foreach(var ship in world.Ships)
            {
                if(!ship||ship==excluded||!ship.IsAlive||ship.Kind!=ShipKind.Galley||!IsShipDocked(ship))continue;
                if(alliesOnly&&ship.Team!=owner)continue;
                float distance=FlatDistance(ship.transform.position,Berth);
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
            if(navalDefender==ship){if(ship)ship.BindHarborGuard(this);return;}
            var previous=navalDefender;navalDefender=null;
            if(previous)previous.ReleaseHarborGuard(this);
            navalDefender=ship;
            if(ship)ship.BindHarborGuard(this);
        }
        public string Buy(ShipKind kind,int team=0)
        {
            if(!TryCatalogShip(kind,out var catalogKind)||!ProductionCatalog.AllowsHarborShip(catalogKind))return "Tipo de barco inválido.";
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(!PlayerRules.IsPlayer(team)||team>=world.Session.PlayerCount)return "Bando inválido.";
            if(!CanLaunch)return LaunchBlockReason;
            if(world.Session.Winner>=0)return "La batalla ha terminado.";
            if(world.Session.Paused)return "Reanuda la partida para comprar barcos.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(queue.Count>=QueueCapacity)return "La cola naval está llena.";
            if(world.Ships.Count(s=>s&&s.IsAlive&&s.Team==team)+world.PendingShips(team)>=12)return "Límite naval de 12 barcos alcanzado.";
            int cost=Cost(kind);if(!world.Session.Economy.Spend(team,cost))return "Oro insuficiente para comprar este barco.";
            queue.Add(new Order{Kind=kind,Team=team,Remaining=TrainTime(kind)});return null;
        }
        public string RecruitLand(UnitKind kind,int team=0)
        {
            if(!ProductionCatalog.AllowsHarborUnit(kind))return "Este muelle sólo entrena Marines.";
            if(sharesTown&&LinkedTown)return LinkedTown.RecruitPortMarine(kind,team);
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(!PlayerRules.IsPlayer(team)||team>=world.Session.PlayerCount)return "Bando inválido.";
            if(world.Session.Winner>=0)return "La batalla ha terminado.";
            if(world.Session.Paused)return "Reanuda la partida para reclutar.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(landQueue.Count>=5)return "La cola de Marines está llena.";
            if(world.Session.RecruitmentReservations(team)>=BattleRules.PopulationLimit)return "Límite de soldados alcanzado.";
            if(!world.Session.Economy.Spend(team,BattleRules.Cost(kind)))return "Oro insuficiente para reclutar este Marine.";
            landQueue.Add(new LandOrder{Kind=kind,Team=team,Remaining=BattleRules.TrainTime(kind)});return null;
        }
        public string CancelLandTraining(int index,int team=0)
        {
            if(sharesTown&&LinkedTown)return LinkedTown.CancelTraining(index,team);
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(!PlayerRules.IsPlayer(team)||team>=world.Session.PlayerCount)return "Bando inválido.";
            if(world.Session.Paused||world.Session.Winner>=0)return "La partida está detenida.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(index<0||index>=landQueue.Count)return "Este encargo ya no está en la cola.";
            var order=landQueue[index];landQueue.RemoveAt(index);world.Session.Economy.Refund(order.Team,BattleRules.Cost(order.Kind));return null;
        }
        internal int PendingLandRecruits(int team){if(sharesTown)return 0;int count=0;foreach(var order in landQueue)if(order.Team==team)count++;return count;}
        internal int PendingCount(int team){int count=0;foreach(var item in queue)if(item.Team==team)count++;return count;}
        public string CancelTraining(int index,int team=0)
        {
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(!PlayerRules.IsPlayer(team)||team>=world.Session.PlayerCount)return "Bando inválido.";
            if(world.Session.Winner>=0)return "La batalla ha terminado.";
            if(world.Session.Paused)return "Reanuda la partida para cancelar encargos.";
            if(Owner!=team)return "Este puerto no pertenece a tu bando.";
            if(index<0||index>=queue.Count)return "Este encargo ya no está en la cola.";
            var item=queue[index];queue.RemoveAt(index);world.Session.Economy.Refund(item.Team,Cost(item.Kind));return null;
        }
        public string BuildTower(int team=0)
        {
            if(sharesTown)return LinkedTown?LinkedTown.BuildTower(team):"Este puerto no tiene ciudad.";
            if(!world||!world.Session)return "No hay una batalla activa.";
            if(!PlayerRules.IsPlayer(team)||team>=world.Session.PlayerCount)return "Bando inválido.";
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
            if(navalDefender&&navalDefender.Team!=Owner)SetNavalDefender(null);
            if(sharesTown&&HasLivingDefender)SetNavalDefender(null);
            if(Owner!=lastOwner)
            {
                RefundQueue();RefundLandQueue();
                if(!sharesTown){CancelTowerBuild(true);Defense.ChangeOwner();}
                lastOwner=Owner;
            }
            if(!sharesTown&&state!=null)
            {
                var previous=claimZone.Defender;int owner=claimZone.Step(world.Session,state.Owner,delta);
                state.Capture=claimZone.Progress;state.Capturing=claimZone.CapturingTeam;state.Contested=claimZone.Contested;
                if(claimZone.Defender&&claimZone.Defender!=previous)claimZone.Defender.HoldPosition();
                if(!HasLivingDefender)owner=ResolveNavalOwner(state.Owner);
                else SetNavalDefender(null);
                if(owner!=state.Owner){state.Owner=owner;Captured();}
                claimRing.startColor=claimRing.endColor=state.Contested?new Color(1,.7f,.15f):Color.Lerp(VisualFactory.TeamColor(Owner),Color.white,state.Capture*.65f);
            }
            if(!sharesTown&&Defense&&Defense.UnderConstruction)
            {
                towerBuildRemaining-=delta;Defense.SetBuildProgress(1-towerBuildRemaining/BattleRules.ConstructionSeconds);
                if(towerBuildRemaining<=0){Defense.CompleteBuild();towerBuildRemaining=0;towerBuilder=-1;}
            }
            if(queue.Count>0)
            {
                queue[0].Remaining-=delta;
                if(queue[0].Remaining<=0){var item=queue[0];queue.RemoveAt(0);var ship=CanLaunch?world.Spawn(item.Team,item.Kind,Berth):null;if(!ship)world.Session.Economy.Refund(item.Team,Cost(item.Kind));}
            }
            TickLandQueue(delta);
            RefreshTrainingView();
        }
        void Captured()
        {
            RefundQueue();RefundLandQueue();CancelTowerBuild(true);Defense.ChangeOwner();lastOwner=Owner;world.Message(DisplayName+" conquistado por "+VisualFactory.TeamName(Owner)+".");
        }
        void CancelTowerBuild(bool refund)
        {
            if(!Defense||!Defense.UnderConstruction)return;
            if(refund&&towerBuilder>=0)world.Session.Economy.Refund(towerBuilder,BattleRules.TowerCost);
            towerBuilder=-1;towerBuildRemaining=0;Defense.CancelBuild();
        }
        void RefundQueue(){foreach(var item in queue)world.Session.Economy.Refund(item.Team,Cost(item.Kind));queue.Clear();}
        void RefundLandQueue(){foreach(var item in landQueue)world.Session.Economy.Refund(item.Team,BattleRules.Cost(item.Kind));landQueue.Clear();}
        void RefreshTrainingView()
        {
            if(sharesTown&&LinkedTown){LinkedTown.SetPortNavalTraining(queue.Count>0);return;}
            if(trainingView)trainingView.SetActivity(landQueue.Count>0,queue.Count>0,world.Session.BattleTime);
        }
        void TickLandQueue(float delta)
        {
            if(sharesTown||landQueue.Count==0)return;
            landQueue[0].Remaining-=delta;if(landQueue[0].Remaining>0)return;
            var item=landQueue[0];if(world.Session.RecruitmentPopulation(item.Team)>=BattleRules.PopulationLimit)return;landQueue.RemoveAt(0);
            var unit=world.Session.Spawn(item.Team,item.Kind,LandEntry);
            if(unit)unit.MoveTo(LandRally,true,false);else world.Session.Economy.Refund(item.Team,BattleRules.Cost(item.Kind));
        }
        static bool TryCatalogShip(ShipKind kind,out NavalUnitKind catalogKind)
        {
            switch(kind)
            {
                case ShipKind.Galley:catalogKind=NavalUnitKind.Galley;return true;
                case ShipKind.Transport:catalogKind=NavalUnitKind.Transport;return true;
                default:catalogKind=default;return false;
            }
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.SqrMagnitude(a-b);}
    }
}
