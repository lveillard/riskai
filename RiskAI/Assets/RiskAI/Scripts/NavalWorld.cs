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
            int[] mainland={-58,-32,-7,20,43};
            var linkedTowns=mainland.Select(x=>Session.Towns.OrderBy(t=>FlatDistance(t.transform.position,new Vector3(x*MapLayout.Spacing,0,MapLayout.Coast(x*MapLayout.Spacing)))).FirstOrDefault()).ToArray();
            // Guarantee a starting port using the authored, well-spaced harbor sites.
            // Extra docks beside neutral towers used to trigger combat before the AI grace period.
            for(int team=0;team<2;team++)
            {
                if(linkedTowns.Any(t=>t&&t.State.Owner==team))continue;
                var home=Session.Towns.Where(t=>t.State.Owner==team).OrderBy(t=>MapLayout.Coast(t.transform.position.x)-t.transform.position.z).FirstOrDefault();
                if(!home)continue;
                int index=Enumerable.Range(0,mainland.Length)
                    .Where(i=>!linkedTowns[i]||linkedTowns[i].State.Owner<0||linkedTowns.Count(t=>t&&t.State.Owner==linkedTowns[i].State.Owner)>1)
                    .OrderBy(i=>Mathf.Abs(mainland[i]*MapLayout.Spacing-home.transform.position.x)).First();
                linkedTowns[index]=home;
            }
            for(int i=0;i<mainland.Length;i++)
            {
                float x=mainland[i]*MapLayout.Spacing,z=MapLayout.Coast(x);
                var linked=linkedTowns[i];
                AddHarbor(new[]{"Muelle del Oeste","Puerto del Pinar","Puerto del Paso","Dársena del Roble","Muelle del Este"}[i],linked,null,LandPoint(x,z-4),new Vector3(x,-.24f,z+4));
            }
            for(int island=0;island<MapLayout.Islands.Length;island++){var c=MapLayout.Islands[island];AddIslandHarbor("Muelle insular "+(island+1),c.x,c.y,c.z,c.w);}
            foreach(var harbor in Harbors)if(!harbor.IsIsland)harbor.InitializeGarrison();
            for(int team=0;team<2;team++)
            {
                var port=Harbors.FirstOrDefault(h=>h.Owner==team);
                if(!port)continue;
                Spawn(team,ShipKind.Transport,port.Berth);
                var outward=port.Berth-port.Landing;outward.y=0;
                if(SeaNavigation.TryNearestOcean(port.Berth+outward.normalized*5+Vector3.right*4,6,out var sea))Spawn(team,ShipKind.Galley,sea);
                int i=0;foreach(var unit in session.Units.Where(u=>u.Team==team&&!u.IsGarrison).OrderBy(u=>FlatDistance(u.transform.position,port.Landing)).Take(3))
                {if(UnityEngine.AI.NavMesh.SamplePosition(port.Landing+new Vector3(i++-1,0,-1),out var hit,5,UnityEngine.AI.NavMesh.AllAreas)){unit.Agent.Warp(hit.position);unit.Stop();}}
            }
        }
        void AddIslandHarbor(string name,float centerX,float centerZ,float radiusX,float radiusZ)
        {
            float x=centerX*MapLayout.Spacing,z=(centerZ-radiusZ)*MapLayout.Spacing;
            var state=new TownState(name,-1,-1,-1);
            AddHarbor(name,null,state,LandPoint(x,z+4),new Vector3(x,-.24f,z-4));
        }
        void AddHarbor(string name,Settlement linked,TownState state,Vector3 landing,Vector3 berth)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=berth;
            var harbor=go.AddComponent<Harbor>();harbor.Initialize(this,name,linked,state,landing,berth);Harbors.Add(harbor);
        }
        static Vector3 LandPoint(float x,float z)=>MapLayout.IsLand(x,z)?MapLayout.Point(x,z):new Vector3(x,MapLayout.Height(x,z),z);
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
        public void Message(string message){if(Session)Session.Message(message);}
        public void SimTick(float delta)
        {
            if(!Session||Session.Paused||Session.Winner>=0||!Session.AiEnabled||Session.BattleTime<nextAi)return;nextAi=Session.BattleTime+18;
            foreach(var ship in Ships)if(ship&&ship.Team==1&&ship.Kind==ShipKind.Galley&&!ship.CurrentTarget)
            {
                var target=Harbors.Where(h=>h.Owner==0).OrderBy(h=>FlatDistance(h.Berth,ship.transform.position)).FirstOrDefault();
                if(target)ship.MoveTo(target.Berth,true);
            }
        }
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
