using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public sealed class NavalWorld : MonoBehaviour
    {
        public static NavalWorld Current { get; private set; }
        public readonly List<Ship> Ships=new List<Ship>();
        public readonly List<Harbor> Harbors=new List<Harbor>();
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
            // Ports are independent posts. Give each side the same number and leave
            // an odd remainder neutral, without compensating with free mobile troops.
            int[] portOwners=StartingAllocation.Generate(session.Seed^0x504f5254,
                new int[mainland.Length+MapLayout.Islands.Length],2,StartingAllocationMode.IndividualCities).CityOwners;
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
                session.Spawn(harbor.Owner>=0?harbor.Owner:2,UnitKind.Archer,harbor.Landing);
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
            harbor.InitializeImported(this,town,berth,found?null:"El puerto no tiene una salida marítima segura.");Harbors.Add(harbor);
        }
        void AddHarbor(string name,Settlement linked,TownState state,Vector3 landing,Vector3 berth)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=berth;
            var harbor=go.AddComponent<Harbor>();harbor.Initialize(this,name,linked,state,landing,berth);Harbors.Add(harbor);
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
        public int PendingShips(int team)
        {
            int count=0;foreach(var harbor in Harbors)count+=harbor.PendingCount(team);return count;
        }
        public int AiSavingsTarget
        {
            get
            {
                if (!Session || Session.BattleTime < Session.AiFirstNavalOffensiveTime || PendingShips(1) > 0) return 0;
                foreach (var ship in Ships) if (ship && ship.IsAlive && ship.Team == 1) return 0;
                foreach (var harbor in Harbors) if (harbor.Owner == 1 && harbor.CanLaunch) return Harbor.Cost(ShipKind.Galley);
                return 0;
            }
        }
        public void Message(string message){if(Session)Session.Message(message);}
        public void SimTick(float delta)
        {
            if(!Session||Session.Paused||Session.Winner>=0||!Session.AiEnabled||Session.BattleTime<nextAi)return;nextAi=Session.BattleTime+18;
            int fleet = PendingShips(1);
            foreach (var ship in Ships) if (ship && ship.IsAlive && ship.Team == 1) fleet++;
            // No free starting fleet: pay through the same port queue as the player.
            if (fleet < 2 && Session.Economy.Gold[1] >= Harbor.Cost(ShipKind.Galley))
                foreach (var harbor in Harbors)
                    if (harbor.Owner == 1 && harbor.QueueCount == 0 && harbor.Buy(ShipKind.Galley, 1) == null) break;
            foreach(var ship in Ships)if(ship&&ship.Team==1&&ship.Kind==ShipKind.Galley&&!ship.CurrentTarget)
            {
                var target=Harbors.Where(h=>h.Owner==0&&h.CanLaunch).OrderBy(h=>FlatDistance(h.Berth,ship.transform.position)).FirstOrDefault();
                if(target)ship.MoveTo(target.Berth,true);
            }
        }
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
