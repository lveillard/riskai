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

        public static NavalWorld Create(BattleSession session,Transform root)
        {
            var go=new GameObject("Naval world");if(root)go.transform.SetParent(root,false);
            var world=go.AddComponent<NavalWorld>();world.Initialize(session);return world;
        }
        void Initialize(BattleSession session)
        {
            Session=session;session.Naval=this;Current=this;nextAi=session.AiFirstNavalOffensiveTime;
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
                AddHarbor(name,linked,new TownState(name,portOwners[i],-1,-1),MapLayout.MainlandHarborLanding(i),new Vector3(x,-.24f,z+4));
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
            AddHarbor(name,null,state,MapLayout.IslandHarborLanding(island),new Vector3(x,-.24f,z-4));
        }
        void AddImportedHarbor(Settlement town)
        {
            var outward=town.ClaimPoint-town.transform.position;outward.y=0;
            if(outward.sqrMagnitude<.01f)outward=Vector3.forward;else outward.Normalize();
            // Imported claim circles are authored at waterfront coordinates.  Their
            // gameplay deck is walkable, so launch from beyond it instead of finding
            // the nearest water directly under the defender.
            var probe=town.ClaimPoint+outward*6f;
            bool found=SeaNavigation.TryNearestOcean(probe,30f,out var berth);
            if(!found)berth=new Vector3(probe.x,-.24f,probe.z);
            var go=new GameObject("Puerto de "+town.DisplayName);go.transform.SetParent(transform,false);go.transform.position=berth;
            var harbor=go.AddComponent<Harbor>();
            harbor.InitializeImported(this,town,berth,found?null:"El puerto no tiene una salida marítima segura.");Harbors.Add(harbor);AddEmbarkZone(harbor);
        }
        void AddHarbor(string name,Settlement linked,TownState state,Vector3 landing,Vector3 berth)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=berth;
            var harbor=go.AddComponent<Harbor>();harbor.Initialize(this,name,linked,state,landing,berth);Harbors.Add(harbor);AddEmbarkZone(harbor);
        }
        void AddEmbarkZone(Harbor harbor)
        {
            var zone=NavalEmbarkZone.Create(transform,harbor);if(zone)EmbarkZones.Add(zone);
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.SqrMagnitude(a-b);}
        public Ship Spawn(int team,ShipKind kind,Vector3 point)
        {
            if(!SeaNavigation.HasClearance(point))return null;
            var go=new GameObject(kind==ShipKind.Galley?"Galera":"Transporte");go.transform.SetParent(transform,false);go.transform.position=new Vector3(point.x,-.24f,point.z);
            var ship=go.AddComponent<Ship>();ship.Initialize(this,team,kind);Ships.Add(ship);Session.RegisterTarget(ship);return ship;
        }
        public Harbor NearestHarbor(Vector3 point,float radius=float.MaxValue)
        {
            Harbor best=null;float distance=radius*radius;
            foreach(var harbor in Harbors){float next=FlatDistance(harbor.Landing,point);if(next<distance){distance=next;best=harbor;}}
            return best;
        }
        public Harbor NearestEmbarkHarbor(Vector3 point,float radius=float.MaxValue)
        {
            Harbor best=null;float distance=radius*radius;
            foreach(var harbor in Harbors)if(harbor&&harbor.IsEmbarkZone(point))
            {float next=FlatDistance(harbor.Landing,point);if(next<distance){distance=next;best=harbor;}}
            return best;
        }
        /// <summary>Moves a transport and selected soldier into the same marked embark zone.</summary>
        public string OrderEmbark(Ship ship,Soldier soldier)
        {
            var selected=new List<Soldier>{soldier};
            if(!TryPlanEmbark(ship,selected,out var landing,out var berth,out var error))return error;
            ship.MoveTo(berth);soldier.MoveTo(landing,false,false);
            return "El transporte se acerca al muelle de embarque.";
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
                if(!harbor||!harbor.CanLaunch||!MapLayout.IsLand(harbor.Landing.x,harbor.Landing.z)||!NavMesh.SamplePosition(harbor.Landing,out var shore,1.25f,NavMesh.AllAreas))continue;
                float next=FlatDistance(ship.transform.position,harbor.Berth);
                bool valid=false;
                for(int i=0;i<soldiers.Count;i++)
                {
                    var soldier=soldiers[i];if(!soldier||!soldier.IsAlive||soldier.IsGarrison||soldier.Team!=ship.Team)continue;
                    valid=true;next+=FlatDistance(soldier.transform.position,harbor.Landing);
                }
                if(valid&&next<score){best=harbor;score=next;landing=shore.position;}
            }
            if(!best){error="No hay playa o muelle de embarque alcanzable.";return false;}
            berth=best.Berth;return true;
        }
        public string OrderDisembark(Ship ship,Harbor harbor)
        {
            if(!ship||ship.Kind!=ShipKind.Transport)return "Selecciona un transporte.";
            if(!harbor)return "Elige una playa o muelle de desembarco marcado.";
            ship.SailToHarbor(harbor);return "El transporte navega al desembarco marcado.";
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
            if(!Session||Session.Paused||Session.Winner>=0||!Session.AiEnabled||Session.BattleTime<nextAi)return;nextAi=Session.BattleTime+18;
            for (int team = 1; team < Session.PlayerCount; team++)
            {
                int fleet = PendingShips(team);
                foreach (var ship in Ships) if (ship && ship.IsAlive && ship.Team == team) fleet++;
                // Each AI uses its own gold and one of its own queues.
                if (fleet < 2 && Session.Economy.Gold[team] >= Harbor.Cost(ShipKind.Galley))
                    foreach (var harbor in Harbors)
                        if (harbor.Owner == team && harbor.QueueCount == 0 && harbor.Buy(ShipKind.Galley, team) == null) break;
                foreach(var ship in Ships)if(ship&&ship.Team==team&&ship.Kind==ShipKind.Galley&&!ship.CurrentTarget)
                {
                    var target=Harbors.Where(h=>PlayerRules.IsPlayer(h.Owner)&&h.Owner!=team&&h.CanLaunch)
                        .OrderBy(h=>FlatDistance(h.Berth,ship.transform.position)).FirstOrDefault();
                    if(target)ship.MoveTo(target.Berth,true);
                }
            }
        }
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
