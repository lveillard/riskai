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
            var outward=town.PortSeaward;
            // The source city is the coastal anchor and the source claim may point
            // inland or over water. Use the terrain-derived seaward direction so the
            // berth and pier never cross the land route through a narrow port.
            var probe=town.PortBuildingPoint+outward*3f;
            bool found=SeaNavigation.TryNearestOcean(probe,30f,out var berth);
            if(!found)berth=new Vector3(probe.x,-.24f,probe.z);
            var go=new GameObject("Puerto de "+town.DisplayName);go.transform.SetParent(transform,false);go.transform.position=berth;
            var harbor=go.AddComponent<Harbor>();
            harbor.InitializeImported(this,new BuildingId(BuildingKind.Harbor,"imported/"+town.State.Id),town,berth,found?null:"El puerto no tiene una salida marítima segura.");
            // The imported map supplies a city-to-claim quay, while the safe naval
            // berth may be farther offshore. Join both anchors with the same deck
            // primitive used by authored harbors so the usable berth stays visible.
            NavalArt.CreatePierDeck(harbor.transform,town.ClaimPoint+Vector3.up*.15f,berth+Vector3.up*.15f,
                3.15f,"Harbor berth pier",false,true,.03f);
            Harbors.Add(harbor);AddEmbarkZone(harbor);
        }
        void AddHarbor(BuildingId buildingId,string name,Settlement linked,TownState state,Vector3 landing,Vector3 berth)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=berth;
            var harbor=go.AddComponent<Harbor>();harbor.Initialize(this,buildingId,name,linked,state,landing,berth);Harbors.Add(harbor);AddEmbarkZone(harbor);
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
            if(!ship||!ship.IsAlive||ship.Kind!=ShipKind.Transport){error="Selecciona un transporte.";return false;}
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
            if(!ship||!ship.IsAlive||ship.Kind!=ShipKind.Transport){error="Selecciona un transporte.";return false;}
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
            if(!ship||ship.Kind!=ShipKind.Transport)return "Selecciona un transporte.";
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
            if(!soldier)return false;
            foreach(var expedition in expeditions.Values)
                if(expedition.Reserves(soldier))return true;
            return false;
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
            int fleet=PendingShips(team);
            foreach(var ship in Ships)if(ship&&ship.IsAlive&&ship.Team==team)fleet++;
            // Each AI still receives one decision every 18 simulation seconds, but
            // their phase is staggered to avoid rebuilding every fleet route together.
            if(fleet<2&&Session.Economy.Gold[team]>=Harbor.Cost(ShipKind.Galley))
                foreach(var harbor in Harbors)
                    if(harbor.Owner==team&&harbor.QueueCount==0&&buildingCommands.Execute(team,PlayerBuildingIntent.BuyShip(harbor.BuildingId,NavalUnitKind.Galley))==null)break;
            foreach(var ship in Ships)if(ship&&ship.IsAlive&&ship.Team==team&&ship.Kind==ShipKind.Galley&&!ship.IsGarrison&&!ship.CurrentTarget)
            {
                Harbor target=null;float distance=float.MaxValue;
                foreach(var harbor in Harbors)
                {
                    if(!harbor||!PlayerRules.IsPlayer(harbor.Owner)||harbor.Owner==team||!harbor.CanLaunch)continue;
                    float next=FlatDistance(harbor.Berth,ship.transform.position);
                    if(next<distance){distance=next;target=harbor;}
                }
                if(target&&!ship.IsAtOrRoutingTo(target.Berth))ship.MoveTo(target.Berth,true);
            }
        }
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
