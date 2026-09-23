using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
namespace RiskAI
{
    /// <summary>Authored group rally and a territory overlay generated only when inspected.</summary>
    public sealed class CountryCamp : MonoBehaviour
    {
        public int Country { get; private set; }
        public BuildingId BuildingId => new BuildingId(BuildingKind.CountryCamp,Country.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public Vector3 SpawnPoint => transform.position;
        public string DisplayName => MapLayout.Countries[Country].Name;
        public int Reinforcements => MapLayout.Countries[Country].PerTurn;
        public bool Selected { get; private set; }
        public bool HasRally => hasRally;
        public Vector3 RallyPoint => hasRally ? rallyPoint : SpawnPoint;

        BattleSession session;
        Renderer banner;
        LineRenderer rallyRing;
        long lastRefresh=-1;
        bool hasRally;
        int rallyOwner=-1;
        Vector3 rallyPoint;

        public static CountryCamp Create(BattleSession battle, int country, Transform parent)
        {
            Settlement anchor=null;
            foreach(var town in battle.Towns)if(town.State.Country==country){anchor=town;break;}
            if(!anchor)return null;
            // Source camps keep their authored position; authored maps centre theirs among the members.
            NavMeshHit hit;
            if(MapLayout.IsImported){if(!NavMesh.SamplePosition(MapLayout.Countries[country].CampPoint,out hit,8,NavMesh.AllAreas))return null;}
            else if(!TryCentredSite(country,out hit)&&!NavMesh.SamplePosition(anchor.Rally+Vector3.left*6,out hit,8,NavMesh.AllAreas))return null;
            var go=new GameObject("Hoguera · "+MapLayout.Countries[country].Name);
            go.transform.SetParent(parent,false);go.transform.position=hit.position;
            var camp=go.AddComponent<CountryCamp>();camp.session=battle;camp.Country=country;
            WorldLife.AddCampfire(parent,hit.position+new Vector3(2.5f,0,3.3f));
            VisualFactory.Shape(go.transform,PrimitiveType.Cylinder,"Asta",new Vector3(1.1f,1.3f,0),new Vector3(.08f,1.3f,.08f),new Color(.3f,.19f,.09f));
            camp.banner=VisualFactory.Shape(go.transform,PrimitiveType.Cube,"Estandarte del grupo",new Vector3(1.55f,2.1f,0),new Vector3(.85f,.55f,.06f),Color.white).GetComponent<Renderer>();
            return camp;
        }

        /// <summary>Clearance from a camp to a city building, to its claim circle centre (radius 6) and to a harbour landing.</summary>
        public const float CityClearance=7f,ClaimClearance=8.5f,HarborClearance=7f;

        /// <summary>
        /// The walkable land point of the country's own territory nearest to the centroid of its
        /// cities, clear of city pads, claim rings and harbours, with a NavMesh path to a member city.
        /// Rings are searched outward, so among equally connected points the first is the most central.
        /// </summary>
        public static bool TryCentredSite(int country,out NavMeshHit site)
        {
            site=default;var centroid=Vector3.zero;int members=0;
            foreach(var town in MapLayout.Towns)if(town.Country==country){centroid+=town.Position;members++;}
            if(members==0)return false;
            centroid/=members;
            var harbors=new List<Vector3>();
            for(int i=0;i<MapLayout.MainlandHarborX.Length;i++)harbors.Add(MapLayout.MainlandHarborLanding(i));
            for(int i=0;i<MapLayout.Islands.Length;i++)harbors.Add(MapLayout.IslandHarborLanding(i));
            var field=TerritoryField.Current;var path=new NavMeshPath();
            const float step=1.2f;int attempts=0,bestReach=0;
            for(int ring=0;ring<=40&&attempts<400;ring++)
            {
                int samples=ring==0?1:Mathf.CeilToInt(2*Mathf.PI*ring);
                for(int k=0;k<samples&&attempts<400;k++)
                {
                    float angle=k*2*Mathf.PI/samples;
                    float x=centroid.x+Mathf.Cos(angle)*ring*step,z=centroid.z+Mathf.Sin(angle)*ring*step;
                    if(!MapLayout.IsWalkable(x,z)||field.CountryAt(x,z)!=country)continue;
                    if(Mathf.Abs(MapLayout.Height(x+1,z)-MapLayout.Height(x-1,z))>.8f||Mathf.Abs(MapLayout.Height(x,z+1)-MapLayout.Height(x,z-1))>.8f)continue;
                    if(!Clear(x,z,harbors))continue;
                    if(!NavMesh.SamplePosition(MapLayout.Point(x,z),out var hit,1.5f,NavMesh.AllAreas))continue;
                    attempts++;
                    // A clear rally path: prefer the most central point that reaches the most member
                    // cities over the NavMesh (a country split by a channel camps on its larger shore).
                    int reachable=0;
                    foreach(var town in MapLayout.Towns)
                    {
                        if(town.Country!=country||!NavMesh.SamplePosition(town.Position,out var target,6,NavMesh.AllAreas))continue;
                        if(NavMesh.CalculatePath(hit.position,target.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)reachable++;
                    }
                    // Ties go to the mainland, where reinforcements have somewhere to march.
                    bool island=false;for(int i=0;i<MapLayout.Islands.Length;i++)island|=MapLayout.IslandDistance(x,z,i)>=0;
                    int score=reachable*2+(island?0:1);
                    if(reachable>0&&score>bestReach){bestReach=score;site=hit;}
                    if(reachable==members)return true;
                }
            }
            return bestReach>0;
        }
        static bool Clear(float x,float z,List<Vector3> harbors)
        {
            foreach(var town in MapLayout.Towns)
            {
                if(new Vector2(town.Position.x-x,town.Position.z-z).sqrMagnitude<CityClearance*CityClearance)return false;
                if(new Vector2(town.ClaimPoint.x-x,town.ClaimPoint.z-z).sqrMagnitude<ClaimClearance*ClaimClearance)return false;
            }
            foreach(var harbor in harbors)
                if(new Vector2(harbor.x-x,harbor.z-z).sqrMagnitude<HarborClearance*HarborClearance)return false;
            return true;
        }

        /// <summary>Stores a walkable rally point for future country reinforcements.</summary>
        public bool SetRally(Vector3 point)
        {
            if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.x)||float.IsInfinity(point.y)||float.IsInfinity(point.z)||!NavMesh.SamplePosition(point,out var hit,8,NavMesh.AllAreas))return false;
            rallyPoint=hit.position;hasRally=true;rallyOwner=session.Economy.CountryOwner(Country);RefreshRallyView();return true;
        }
        public void ClearRally() { hasRally=false;RefreshRallyView(); }
        internal void ReconcileOwner(int owner){if(hasRally&&rallyOwner!=owner)ClearRally();}

        /// <summary>Applies this camp's explicit rally. Without one, reinforcements hold at the camp.</summary>
        public void ApplyRally(Soldier unit)
        {
            if(!unit||!unit.IsAlive)return;
            ReconcileOwner(session.Economy.CountryOwner(Country));
            if(hasRally)unit.TryMoveTo(rallyPoint,true,false);
            else unit.HoldPosition();
        }

        void Update()
        {
            if(!session||lastRefresh==session.Clock.TickCount/20)return;
            lastRefresh=session.Clock.TickCount/20;
            banner.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamMaterialColor(session.Economy.CountryOwner(Country)));
        }
        void RefreshRallyView()
        {
            if(!rallyRing && hasRally)
                rallyRing=VisualFactory.Ring(transform,.8f,.065f,VisualFactory.TeamColor(rallyOwner));
            if(!rallyRing)return;
            rallyRing.enabled=Selected&&hasRally&&rallyOwner==0;
            rallyRing.transform.position=rallyPoint;
        }
        public void Select(bool selected)
        {
            Selected=selected;
            RefreshRallyView();
            if(StrategicMapView.Current)
                StrategicMapView.Current.SelectCountry(selected?Country:-1);
        }
    }
}
