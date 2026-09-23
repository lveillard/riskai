using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed class NavalWorld : MonoBehaviour
    {
        public static NavalWorld Current { get; private set; }
        public readonly List<Ship> Ships=new List<Ship>();
        public readonly List<Harbor> Harbors=new List<Harbor>();
        public readonly List<NavalEmbarkZone> EmbarkZones=new List<NavalEmbarkZone>();
        public BattleSession Session { get; private set; }
        float nextAi;
        PlayerBuildingCommands buildingCommands;
        const float AiDecisionInterval = 18f;
        float[] nextAiByTeam;
        readonly Dictionary<int,NavalExpeditionCommander> expeditions=new Dictionary<int,NavalExpeditionCommander>();

        public static NavalWorld Create(BattleSession session,Transform root)
        {
            var go=new GameObject("Naval world");if(root)go.transform.SetParent(root,false);
            var world=go.AddComponent<NavalWorld>();world.Initialize(session);return world;
        }
        void Initialize(BattleSession session)
        {
            Session=session;session.Naval=this;Current=this;buildingCommands=new PlayerBuildingCommands(session);nextAi=session.AiFirstNavalOffensiveTime;
            // Static W3E clearance, edge validation and disconnected-ocean labels
            // are paid once at setup rather than during every 18-second AI fleet pass.
            SeaNavigation.Prepare();
            if(MapLayout.IsImported)
            {
                foreach(var town in Session.Towns)if(town&&town.IsPort)AddImportedHarbor(town);
                return;
            }
            int[] mainland=MapLayout.MainlandHarborX;
            var linkedTowns=mainland.Select(x=>Session.Towns.OrderBy(t=>FlatDistance(t.transform.position,new Vector3(x*MapLayout.Spacing,0,MapLayout.Coast(x*MapLayout.Spacing)))).FirstOrDefault()).ToArray();
            // Ports are independent posts. Distribute them over as many active
            // players as the coast can host, leaving a remainder neutral.
            int portCount=mainland.Length+MapLayout.Islands.Length;
            int portPlayers=Mathf.Min(session.PlayerCount,portCount);
            int[] portOwners=StartingAllocation.Generate(session.Seed^0x504f5254,
                new int[portCount],portPlayers,StartingAllocationMode.IndividualCities).CityOwners;
            // When there are fewer ports than players, sample across the whole
            // roster rather than always granting extra guards to IDs 0..6.
            if(session.PlayerCount>portPlayers)
            {
                var roster=Enumerable.Range(0,session.PlayerCount).ToArray();
                var random=new System.Random(session.Seed^0x504c4159);
                for(int i=roster.Length-1;i>0;i--)
                { int swap=random.Next(i+1);int value=roster[i];roster[i]=roster[swap];roster[swap]=value; }
                for(int i=0;i<portOwners.Length;i++)if(portOwners[i]>=0)portOwners[i]=roster[portOwners[i]];
            }
            for(int i=0;i<mainland.Length;i++)
            {
                float x=mainland[i]*MapLayout.Spacing,z=MapLayout.Coast(x);
                var linked=linkedTowns[i];
                string name=new[]{"Muelle del Oeste","Puerto del Pinar","Puerto del Paso","Dársena del Roble","Muelle del Este"}[i];
                AddHarbor(new BuildingId(BuildingKind.Harbor,"authored/mainland/"+i),name,linked,new TownState(name,portOwners[i],-1,-1),MapLayout.MainlandHarborLanding(i),new Vector3(x,-.24f,z+4));
            }
            for(int island=0;island<MapLayout.Islands.Length;island++)
                AddIslandHarbor("Muelle insular "+(island+1),island,portOwners[mainland.Length+island]);
            foreach(var harbor in Harbors)
            {
                session.Spawn(PlayerRules.ToCombatTeam(harbor.Owner),UnitKind.Archer,harbor.Landing);
                harbor.InitializeGarrison();
            }
        }
        void AddIslandHarbor(string name,int island,int owner)
        {
            var site=MapLayout.Islands[island];
            float x=site.x*MapLayout.Spacing,z=(site.y-site.w)*MapLayout.Spacing;
            var state=new TownState(name,owner,-1,-1);
            AddHarbor(new BuildingId(BuildingKind.Harbor,"authored/island/"+island),name,null,state,MapLayout.IslandHarborLanding(island),new Vector3(x,-.24f,z-4));
        }
        void AddImportedHarbor(Settlement town)
        {
            bool found=TryResolveImportedBerth(town,out var berth);
            var go=new GameObject("Puerto de "+town.DisplayName);go.transform.SetParent(transform,false);go.transform.position=berth;
            var harbor=go.AddComponent<Harbor>();
            harbor.InitializeImported(this,new BuildingId(BuildingKind.Harbor,"imported/"+town.State.Id),town,berth,
                found?null:"El puerto no tiene una salida marítima segura.",town.VisualVariant);
            Harbors.Add(harbor);AddEmbarkZone(harbor);
        }
        // Adapter seam for the pending source shallow-water query. Building art
        // already stays on the exact source h00O position; this method owns only
        // the hull-safe simulation berth and never moves the claim coordinate.
        static bool TryResolveImportedBerth(Settlement town,out Vector3 berth)
        {
            var outward=town.PortSeaward;
            // Warcraft's B00R post is amphibious. Keep that exact circle and find
            // a hull-safe point inside its capture radius instead of inventing a
            // second offshore objective.
            var probe=town.ClaimPoint+outward*.5f;
            bool found=SeaNavigation.TryNearestOcean(probe,ClaimRules.TakeoverRadius-.5f,out berth);
            if(!found)berth=new Vector3(probe.x,-.24f,probe.z);
            return found;
        }
        void AddHarbor(BuildingId buildingId,string name,Settlement linked,TownState state,Vector3 landing,Vector3 berth,BuildingVariant? visualVariant=null)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=berth;
            var harbor=go.AddComponent<Harbor>();harbor.Initialize(this,buildingId,name,linked,state,landing,berth,visualVariant);Harbors.Add(harbor);AddEmbarkZone(harbor);
        }
        void AddEmbarkZone(Harbor harbor)
        {
            var zone=NavalEmbarkZone.Create(transform,harbor);
            if(!zone)return;
            EmbarkZones.Add(zone);
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.SqrMagnitude(a-b);}
        public Ship Spawn(int team,ShipKind kind,Vector3 point)
        {
            if(Session.IsPlayerEliminated(team) || !SeaNavigation.HasClearance(point))return null;
            var go=new GameObject(kind==ShipKind.Galley?"Galera":"Transporte");go.transform.SetParent(transform,false);go.transform.position=new Vector3(point.x,-.24f,point.z);
            var ship=go.AddComponent<Ship>();ship.Initialize(this,team,kind);Ships.Add(ship);Session.RegisterTarget(ship);return ship;
        }
        public Harbor NearestHarbor(Vector3 point,float radius=float.MaxValue)
        {
            Harbor best=null;float distance=radius*radius;
            foreach(var harbor in Harbors){float next=FlatDistance(harbor.Landing,point);if(next<distance){distance=next;best=harbor;}}
            return best;
        }
        /// <summary>Moves a transport and selected soldier into the same marked embark zone.</summary>
        public string OrderEmbark(Ship ship,Soldier soldier)
        {
            return TryOrderEmbark(ship,soldier,out var error)?"El transporte se acerca al muelle de embarque.":error;
        }
        public bool TryOrderEmbark(Ship ship,Soldier soldier,out string error)
        {
            var selected=new List<Soldier>{soldier};
            if(!TryPlanEmbark(ship,selected,out var landing,out var berth,out error))return false;
            ship.MoveTo(berth);
            if(!string.IsNullOrEmpty(ship.LastActionError)){error=ship.LastActionError;return false;}
            if(!soldier.TryMoveTo(landing,false,false)){error=string.IsNullOrEmpty(soldier.LastMoveError)?"La tropa no puede llegar al embarque marcado.":soldier.LastMoveError;return false;}
            error=null;return true;
        }
        /// <summary>Uses a selected friendly embark post instead of retargeting the nearest harbor.</summary>
        public bool TryOrderEmbarkAt(Ship ship,Soldier soldier,Harbor harbor,out string error)
        {
            error=null;
            if(!ship||!ship.IsAlive||!ship.Profile.CanTransport){error="Selecciona un transporte.";return false;}
            if(!soldier||!soldier.IsAlive||soldier.IsGarrison||soldier.Team!=ship.Team){error="Selecciona una tropa móvil aliada.";return false;}
            if(!harbor||harbor.Owner!=ship.Team||!harbor.TryTransportLanding(out var landing,out var berth))
            {error="El puerto no tiene una playa o pasarela al alcance del transporte.";return false;}
            ship.MoveTo(berth);
            if(!string.IsNullOrEmpty(ship.LastActionError)){error=ship.LastActionError;return false;}
            if(!soldier.TryMoveTo(landing,false,false)){error=string.IsNullOrEmpty(soldier.LastMoveError)?"La tropa no puede llegar al embarque marcado.":soldier.LastMoveError;return false;}
            return true;
        }
        /// <summary>Plans one common visible shore for a controller-owned boarding queue.</summary>
        public bool TryPlanEmbark(Ship ship,IReadOnlyList<Soldier> soldiers,out Vector3 landing,out Vector3 berth,out string error)
        {
            landing=default;berth=default;error=null;
            if(!ship||!ship.IsAlive||!ship.Profile.CanTransport){error="Selecciona un transporte.";return false;}
            if(soldiers==null||soldiers.Count==0){error="Selecciona soldados para embarcar.";return false;}
            Harbor best=null;float score=float.MaxValue;
            foreach(var harbor in Harbors)
            {
                if(!harbor||!harbor.TryTransportLanding(out var shore,out var transportBerth))continue;
                float next=FlatDistance(ship.transform.position,transportBerth);
                bool valid=false;
                for(int i=0;i<soldiers.Count;i++)
                {
                    var soldier=soldiers[i];if(!soldier||!soldier.IsAlive||soldier.IsGarrison||soldier.Team!=ship.Team)continue;
                    valid=true;next+=FlatDistance(soldier.transform.position,harbor.Landing);
                }
                if(valid&&next<score){best=harbor;score=next;landing=shore;berth=transportBerth;}
            }
            if(!best){error="No hay playa o muelle de embarque alcanzable.";return false;}
            return true;
        }
        public string OrderDisembark(Ship ship,Harbor harbor)
        {
            if(!ship||!ship.Profile.CanTransport)return "Selecciona un transporte.";
            if(!harbor)return "Elige una playa o muelle de desembarco marcado.";
            ship.SailToHarbor(harbor);
            return string.IsNullOrEmpty(ship.LastActionError)?"El transporte navega al desembarco marcado.":ship.LastActionError;
        }
        public int PendingShips(int team)
        {
            int count=0;foreach(var harbor in Harbors)count+=harbor.PendingCount(team);return count;
        }
        public int AiSavingsTarget => FirstFleetSavingsTargetFor(1);

        public int FirstFleetSavingsTargetFor(int team)
        {
            if (!Session || !PlayerRules.IsPlayer(team) || team == 0 || team >= Session.PlayerCount ||
                Session.BattleTime < Session.AiFirstNavalOffensiveTime || PendingShips(team) > 0) return 0;
            foreach (var ship in Ships) if (ship && ship.IsAlive && ship.Team == team) return 0;
            foreach (var harbor in Harbors) if (harbor.Owner == team && harbor.CanLaunch) return Harbor.Cost(ShipKind.Galley);
            return 0;
        }
        public void Message(string message){if(Session)Session.Message(message);}
        public void SimTick(float delta)
        {
            if(!Session||Session.Paused||Session.Winner>=0||!Session.AiEnabled)return;
            for(int player=1;player<Session.PlayerCount;player++)ExpeditionFor(player).Tick(delta);
            EnsureAiSchedule();
            int team=NextDueAiTeam();if(team<0)return;
            while(nextAiByTeam[team]<=Session.BattleTime)nextAiByTeam[team]+=AiDecisionInterval;
            RunAiDecision(team);
        }
        public NavalExpeditionCommander ExpeditionFor(int team)
        {
            if(!PlayerRules.IsPlayer(team)||team<=0||team>=Session.PlayerCount)return null;
            if(!expeditions.TryGetValue(team,out var commander)){commander=new NavalExpeditionCommander(this,team);expeditions.Add(team,commander);}
            return commander;
        }
        /// <summary>Expedition reservations are bounded by one small mission per AI player.</summary>
        public bool IsReserved(Soldier soldier)
        {
            // A mission only ever reserves its own team's troops, so one lookup suffices.
            return soldier&&expeditions.TryGetValue(soldier.Team,out var expedition)&&expedition.Reserves(soldier);
        }
        void EnsureAiSchedule()
        {
            if(nextAiByTeam!=null&&nextAiByTeam.Length==Session.PlayerCount)return;
            nextAiByTeam=new float[Session.PlayerCount];
            int aiCount=Mathf.Max(1,Session.PlayerCount-1);
            for(int team=1;team<Session.PlayerCount;team++)
                nextAiByTeam[team]=Session.AiFirstNavalOffensiveTime+(team-1)*AiDecisionInterval/aiCount;
        }
        int NextDueAiTeam()
        {
            int selected=-1;float earliest=float.MaxValue;
            for(int team=1;team<Session.PlayerCount;team++)
                if(nextAiByTeam[team]<=Session.BattleTime&&(nextAiByTeam[team]<earliest||Mathf.Approximately(nextAiByTeam[team],earliest)&&team<selected))
                {selected=team;earliest=nextAiByTeam[team];}
            return selected;
        }
        void RunAiDecision(int team)
        {
            if (Session.IsPlayerEliminated(team)) return;
            var profile=Session.AiProfile;
            int fleet=PendingShips(team),warships=0;
            Vector3 fleetCenter=Vector3.zero;
            foreach(var ship in Ships)if(ship&&ship.IsAlive&&ship.Team==team)
            {
                fleet++;
                if(ship.Profile.CanAttack){warships++;fleetCenter+=ship.transform.position;}
            }
            if(warships>0)fleetCenter/=warships;
            // Harbors under naval attack are both a purchase and an order priority.
            Harbor besieged=null;float siege=0;
            foreach(var harbor in Harbors)
            {
                if(!harbor||harbor.Owner!=team)continue;
                float threat=EnemyFleetPower(team,harbor.Berth);
                if(threat>siege){siege=threat;besieged=harbor;}
            }
            int wanted=profile.FleetTarget+(besieged?1:0);
            // Each AI still receives one decision every 18 simulation seconds, but
            // their phase is staggered to avoid rebuilding every fleet route together.
            if(fleet<wanted&&TryChooseWarship(Session.Economy.Gold[team],out var kind))
                foreach(var harbor in Harbors)
                    if(harbor.Owner==team&&harbor.QueueCount==0&&buildingCommands.Execute(team,PlayerBuildingIntent.BuyShip(harbor.BuildingId,kind))==null)break;
            Harbor target=null;
            foreach(var ship in Ships)if(ship&&ship.IsAlive&&ship.Team==team&&ship.Profile.CanAttack&&!ship.IsGarrison&&!ship.CurrentTarget)
            {
                if(besieged&&siege>0)
                {
                    if(!ship.IsAtOrRoutingTo(besieged.Berth))ship.MoveTo(besieged.Berth,true);
                    continue;
                }
                // Keep an accepted, progressing route to a still valid objective.
                if(RoutingToValidTarget(ship,team))continue;
                // Badly damaged galleys fall back to an own port instead of dying alone.
                if(profile.Level>0&&ship.Health<ship.MaxHealth*.35f)
                {
                    var home=NearestOwnHarbor(team,ship.transform.position);
                    if(home&&!ship.IsAtOrRoutingTo(home.Berth))ship.MoveTo(home.Berth,true);
                    continue;
                }
                // The whole squadron shares one objective so galleys arrive together.
                if(!target)target=ChooseNavalTarget(team,warships>0?fleetCenter:ship.transform.position,profile);
                if(target&&!ship.IsAtOrRoutingTo(target.Berth))ship.MoveTo(target.Berth,true);
            }
        }
        bool RoutingToValidTarget(Ship ship,int team)
        {
            foreach(var harbor in Harbors)
                if(harbor&&PlayerRules.IsPlayer(harbor.Owner)&&harbor.Owner!=team&&harbor.CanLaunch&&ship.IsAtOrRoutingTo(harbor.Berth)&&
                   FlatDistance(ship.transform.position,harbor.Berth)>2.25f)return true;
            return false;
        }
        static bool TryChooseWarship(int gold,out NavalUnitKind kind)
        {
            kind=NavalUnitKind.Galley;float best=float.NegativeInfinity;bool found=false;
            var options=ProductionCatalog.HarborShips;
            for(int i=0;i<options.Count;i++)
            {
                var profile=UnitCatalog.Profile(options[i]);
                if(!profile.CanAttack||profile.Cost>gold)continue;
                float score=AiUnitAnalysis.ShipValue(profile)/Mathf.Pow(Mathf.Max(1,profile.Cost),.7f);
                if(score>best){best=score;kind=options[i];found=true;}
            }
            return found;
        }
        float EnemyFleetPower(int team,Vector3 point,float radius=24f)
        {
            float power=0;
            foreach(var ship in Ships)
                if(ship&&ship.IsAlive&&ship.Team!=team&&PlayerRules.IsPlayer(ship.Team)&&ship.Profile.CanAttack&&FlatDistance(ship.transform.position,point)<=radius*radius)
                    power+=AiUnitAnalysis.ShipValue(ship.Profile)*ship.Health/Mathf.Max(1,ship.MaxHealth);
            return power;
        }
        Harbor NearestOwnHarbor(int team,Vector3 point)
        {
            Harbor best=null;float distance=float.MaxValue;
            foreach(var harbor in Harbors)
            {
                if(!harbor||harbor.Owner!=team||!harbor.CanLaunch)continue;
                float next=FlatDistance(harbor.Berth,point);
                if(next<distance){distance=next;best=harbor;}
            }
            return best;
        }
        /// <summary>Nearest enemy harbor, discounted by its escorting fleet and a live guardian tower.</summary>
        Harbor ChooseNavalTarget(int team,Vector3 from,AiDifficultyProfile profile)
        {
            Harbor best=null;float score=float.MaxValue;
            foreach(var harbor in Harbors)
            {
                if(!harbor||!PlayerRules.IsPlayer(harbor.Owner)||harbor.Owner==team||!harbor.CanLaunch)continue;
                float distance=Mathf.Sqrt(FlatDistance(harbor.Berth,from));
                float next=distance;
                if(profile.Level>0)
                {
                    next+=EnemyFleetPower(team,harbor.Berth)*.25f;
                    var guardian=harbor.ClaimZone!=null?harbor.ClaimZone.Guardian:null;
                    if(harbor.Defense&&harbor.Defense.IsAlive&&!harbor.Defense.UnderConstruction&&guardian)next+=15f;
                }
                if(next<score){score=next;best=harbor;}
            }
            return best;
        }
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
