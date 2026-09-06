using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace RiskAI
{
    /// <summary>Authored group rally and an optional terrain overlay, generated only when inspected.</summary>
    public sealed class CountryCamp : MonoBehaviour
    {
        public int Country { get; private set; }
        public Vector3 SpawnPoint => transform.position;
        public string DisplayName => MapLayout.Countries[Country].Name;
        public int Reinforcements => MapLayout.Countries[Country].PerTurn;
        public bool Selected { get; private set; }
        BattleSession session;
        GameObject overlay;
        Renderer banner;
        Material overlayMaterial;
        Mesh overlayMesh;
        long lastRefresh=-1;
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
        void Update()
        {
            if(lastRefresh==session.Clock.TickCount/20)return;
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
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();float step=MapLayout.IsImported?5:3;
            float minX=MapLayout.HalfWidth,maxX=-minX,minZ=MapLayout.HalfDepth,maxZ=-minZ;
            foreach(var town in session.Towns)if(town.State.Country==Country){minX=Mathf.Min(minX,town.transform.position.x);maxX=Mathf.Max(maxX,town.transform.position.x);minZ=Mathf.Min(minZ,town.transform.position.z);maxZ=Mathf.Max(maxZ,town.transform.position.z);}
            float padding=MapLayout.IsImported?55:30;
            minX=Mathf.Max(-MapLayout.HalfWidth,minX-padding);maxX=Mathf.Min(MapLayout.HalfWidth,maxX+padding);minZ=Mathf.Max(-MapLayout.HalfDepth,minZ-padding);maxZ=Mathf.Min(MapLayout.HalfDepth,maxZ+padding);
            for(float x=minX;x<maxX-step;x+=step)
                for(float z=minZ;z<maxZ-step;z+=step)
                {
                    if(!MapLayout.IsLand(x+step*.5f,z+step*.5f)||GroupWeight(x+step*.5f,z+step*.5f)<.01f)continue;
                    if(!MapLayout.IsLand(x,z)||!MapLayout.IsLand(x+step,z+step))continue;
                    int n=vertices.Count;
                    vertices.Add(MapLayout.Point(x,z)+Vector3.up*.13f);vertices.Add(MapLayout.Point(x,z+step)+Vector3.up*.13f);
                    vertices.Add(MapLayout.Point(x+step,z)+Vector3.up*.13f);vertices.Add(MapLayout.Point(x+step,z+step)+Vector3.up*.13f);
                    colors.Add(new Color(1,1,1,GroupWeight(x,z)));colors.Add(new Color(1,1,1,GroupWeight(x,z+step)));
                    colors.Add(new Color(1,1,1,GroupWeight(x+step,z)));colors.Add(new Color(1,1,1,GroupWeight(x+step,z+step)));
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
            foreach(var town in session.Towns)if(town.State.Country==Country)
            {
                var ring=VisualFactory.Ring(overlay.transform,3.6f,.14f,new Color(1,.86f,.25f));
                ring.transform.position=town.transform.position;
            }
        }
        float GroupWeight(float x,float z)
        {
            float own=float.MaxValue,other=float.MaxValue;
            foreach(var town in session.Towns)
            {
                var delta=new Vector2(x-town.transform.position.x,z-town.transform.position.z);
                if(town.State.Country==Country)own=Mathf.Min(own,delta.sqrMagnitude);
                else other=Mathf.Min(other,delta.sqrMagnitude);
            }
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(-4,4,Mathf.Sqrt(other)-Mathf.Sqrt(own)));
        }
        void OnDestroy(){if(overlayMaterial)Destroy(overlayMaterial);if(overlayMesh)Destroy(overlayMesh);}
    }
}
