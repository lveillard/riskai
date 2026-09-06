using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace RiskAI
{
    /// <summary>Authored group rally and a territory overlay generated only when inspected.</summary>
    public sealed class CountryCamp : MonoBehaviour
    {
        public int Country { get; private set; }
        public Vector3 SpawnPoint => transform.position;
        public string DisplayName => MapLayout.Countries[Country].Name;
        public int Reinforcements => MapLayout.Countries[Country].PerTurn;
        public bool Selected { get; private set; }
        public bool HasRally => hasRally;
        public Vector3 RallyPoint => hasRally ? rallyPoint : SpawnPoint;

        BattleSession session;
        GameObject overlay;
        Renderer banner;
        Material overlayMaterial;
        Mesh overlayMesh;
        long lastRefresh=-1;
        bool hasRally;
        int rallyOwner=-1;
        Vector3 rallyPoint;

        public static CountryCamp Create(BattleSession battle, int country, Transform parent)
        {
            Settlement anchor=null;
            foreach(var town in battle.Towns)if(town.State.Country==country){anchor=town;break;}
            if(!anchor)return null;
            var probe=MapLayout.IsImported?MapLayout.Countries[country].CampPoint:anchor.Rally+Vector3.left*6;
            if(!NavMesh.SamplePosition(probe,out var hit,8,NavMesh.AllAreas))return null;
            var go=new GameObject("Hoguera · "+MapLayout.Countries[country].Name);
            go.transform.SetParent(parent,false);go.transform.position=hit.position;
            var camp=go.AddComponent<CountryCamp>();camp.session=battle;camp.Country=country;
            WorldLife.AddCampfire(parent,hit.position+new Vector3(2.5f,0,3.3f));
            VisualFactory.Shape(go.transform,PrimitiveType.Cylinder,"Asta",new Vector3(1.1f,1.3f,0),new Vector3(.08f,1.3f,.08f),new Color(.3f,.19f,.09f));
            camp.banner=VisualFactory.Shape(go.transform,PrimitiveType.Cube,"Estandarte del grupo",new Vector3(1.55f,2.1f,0),new Vector3(.85f,.55f,.06f),Color.white).GetComponent<Renderer>();
            return camp;
        }

        /// <summary>Stores a walkable rally point for future country reinforcements.</summary>
        public bool SetRally(Vector3 point)
        {
            if(!NavMesh.SamplePosition(point,out var hit,8,NavMesh.AllAreas))return false;
            rallyPoint=hit.position;hasRally=true;rallyOwner=session.Economy.CountryOwner(Country);return true;
        }
        public void ClearRally() => hasRally=false;
        internal void ReconcileOwner(int owner){if(hasRally&&rallyOwner!=owner)ClearRally();}

        /// <summary>Applies this camp's explicit rally. Without one, reinforcements hold at the camp.</summary>
        public void ApplyRally(Soldier unit)
        {
            if(!unit||!unit.IsAlive)return;
            ReconcileOwner(session.Economy.CountryOwner(Country));
            if(hasRally)unit.MoveTo(rallyPoint,true,false);
            else unit.HoldPosition();
        }

        void Update()
        {
            if(!session||lastRefresh==session.Clock.TickCount/20)return;
            lastRefresh=session.Clock.TickCount/20;
            banner.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamColor(session.Economy.CountryOwner(Country)));
        }
        public void Select(bool selected)
        {
            Selected=selected;
            if(selected&&!overlay)BuildOverlay();
            if(overlay)overlay.SetActive(selected);
        }
        void BuildOverlay()
        {
            var own=new List<Vector3>();var other=new List<Vector3>();
            CollectTerritoryPoints(own,other);
            if(own.Count==0)return;
            float step=MapLayout.IsImported?5:3;
            float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
            foreach(var point in own)
            {
                minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);
                minZ=Mathf.Min(minZ,point.z);maxZ=Mathf.Max(maxZ,point.z);
            }
            float padding=MapLayout.IsImported?55:30;
            minX=Mathf.Max(MapLayout.PlayableMin.x,minX-padding);maxX=Mathf.Min(MapLayout.PlayableMax.x,maxX+padding);
            minZ=Mathf.Max(MapLayout.PlayableMin.y,minZ-padding);maxZ=Mathf.Min(MapLayout.PlayableMax.y,maxZ+padding);

            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();
            for(float x=minX;x<maxX-step;x+=step)
                for(float z=minZ;z<maxZ-step;z+=step)
                {
                    float centerX=x+step*.5f,centerZ=z+step*.5f;
                    if(!MapLayout.IsLand(centerX,centerZ)||Weight(centerX,centerZ,own,other)<.01f)continue;
                    if(!MapLayout.IsLand(x,z)||!MapLayout.IsLand(x+step,z+step))continue;
                    int n=vertices.Count;
                    vertices.Add(MapLayout.Point(x,z)+Vector3.up*.13f);vertices.Add(MapLayout.Point(x,z+step)+Vector3.up*.13f);
                    vertices.Add(MapLayout.Point(x+step,z)+Vector3.up*.13f);vertices.Add(MapLayout.Point(x+step,z+step)+Vector3.up*.13f);
                    colors.Add(new Color(1,1,1,Weight(x,z,own,other)));colors.Add(new Color(1,1,1,Weight(x,z+step,own,other)));
                    colors.Add(new Color(1,1,1,Weight(x+step,z,own,other)));colors.Add(new Color(1,1,1,Weight(x+step,z+step,own,other)));
                    triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n+2);triangles.Add(n+1);triangles.Add(n+3);
                }
            overlay=new GameObject("Territorio seleccionado");overlay.transform.SetParent(transform,false);
            overlay.transform.position=Vector3.zero;
            var mesh=new Mesh{name="Country inspection overlay",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            overlayMesh=mesh;
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            overlay.AddComponent<MeshFilter>().sharedMesh=mesh;
            overlayMaterial=new Material(Shader.Find("RiskAI/TerritoryOverlay"));
            overlayMaterial.SetColor("_Tint",new Color(.8f,.64f,.2f,.12f));
            var renderer=overlay.AddComponent<MeshRenderer>();renderer.sharedMaterial=overlayMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            // City rings identify the country's capture posts. Ports shape the territory
            // above but do not add duplicate rings for linked waterfront settlements.
            foreach(var town in session.Towns)if(town.State.Country==Country)
            {
                var ring=VisualFactory.Ring(overlay.transform,3.6f,.14f,new Color(1,.86f,.25f));
                ring.transform.position=town.transform.position;
            }
        }
        void CollectTerritoryPoints(List<Vector3> own,List<Vector3> other)
        {
            foreach(var town in session.Towns)AddPoint(town.State.Country,town.ClaimPoint,own,other);
            if(session.Naval==null)return;
            foreach(var harbor in session.Naval.Harbors)
            {
                if(!harbor)continue;
                int country=harbor.LinkedTown?harbor.LinkedTown.State.Country:harbor.State!=null?harbor.State.Country:-1;
                if(country>=0)AddPoint(country,harbor.Landing,own,other);
            }
        }
        void AddPoint(int country,Vector3 point,List<Vector3> own,List<Vector3> other)
        {
            if(country==Country)own.Add(point);else if(country>=0)other.Add(point);
        }
        static float Weight(float x,float z,List<Vector3> own,List<Vector3> other)
        {
            float nearestOwn=Nearest(x,z,own);
            if(other.Count==0)return 1;
            float nearestOther=Nearest(x,z,other);
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(-4,4,Mathf.Sqrt(nearestOther)-Mathf.Sqrt(nearestOwn)));
        }
        static float Nearest(float x,float z,List<Vector3> points)
        {
            float nearest=float.MaxValue;
            foreach(var point in points){float dx=x-point.x,dz=z-point.z;nearest=Mathf.Min(nearest,dx*dx+dz*dz);}
            return nearest;
        }
        void OnDestroy(){if(overlayMaterial)Destroy(overlayMaterial);if(overlayMesh)Destroy(overlayMesh);}
    }
}