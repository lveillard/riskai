using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    public static class WorldArt
    {
        static readonly Dictionary<string,Material> materials = new Dictionary<string,Material>();
        static readonly Vector4[] roadsA = new Vector4[12], roadsB = new Vector4[12];
        static readonly Vector4[] cities = new Vector4[32];
        static int roadCount;
        public static void GroundShadow(Transform root,Vector3 position,Vector2 size)
        {
            var mat=Resources.Load<Material>("GroundShade");if(!mat)return;
            var shadow=VisualFactory.Shape(root,PrimitiveType.Quad,"Soft ground shadow",position,new Vector3(size.x,size.y,1),Color.white);
            shadow.transform.localRotation=Quaternion.Euler(90,0,0);
            var renderer=shadow.GetComponent<Renderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public static Material Painted(int tile, Color? tint = null, float scale = .33f, bool recolor = false, bool natural = false)
        {
            Color color=tint??Color.white;string key=tile+"/"+color+"/"+scale+"/"+recolor+"/"+natural;
            if(materials.TryGetValue(key,out var found)&&found)return found;
            var template=Resources.Load<Material>("PaintedSurface");
            var mat=template?new Material(template):new Material(Shader.Find("RiskAI/PaintedSurface"));
            mat.SetTexture("_Atlas",Resources.Load<Texture2D>(natural?"Painted/StrategicAtlas":"Painted/ArchitectureAtlas"));
            mat.SetVector("_Tile",new Vector4(tile%2*.5f,tile<2?.5f:0,0,0));mat.SetColor("_Tint",color);
            mat.SetFloat("_Scale",scale);mat.SetFloat("_Recolor",recolor?1:0);materials[key]=mat;return mat;
        }
        static GameObject Block(Transform root,string name,Vector3 position,Vector3 size,int tile=0,Color? tint=null,bool solid=false)
        {
            var go=VisualFactory.Shape(root,PrimitiveType.Cube,name,position,size,Color.white,solid);
            go.GetComponent<Renderer>().sharedMaterial=Painted(tile,tint);return go;
        }
        static void Beam(Transform root,Vector3 from,Vector3 to,float width=.16f)
        {
            var go=Block(root,"Carved oak beam",(from+to)/2,new Vector3(width,(to-from).magnitude,width),2);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);
        }
        static GameObject Roof(Transform root,Vector3 basePoint,float width,float length,float rise,int team)
        {
            float x=width/2,z=length/2;
            var v=new[]{new Vector3(-x,0,-z),new Vector3(0,rise,-z),new Vector3(x,0,-z),new Vector3(-x,0,z),new Vector3(0,rise,z),new Vector3(x,0,z)};
            var mesh=new Mesh();mesh.vertices=new[]{v[0],v[3],v[4],v[1], v[1],v[4],v[5],v[2], v[0],v[1],v[2], v[5],v[4],v[3]};
            mesh.triangles=new[]{0,1,2,0,2,3,4,5,6,4,6,7,8,9,10,11,12,13};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Faction roof");go.transform.SetParent(root,false);go.transform.localPosition=basePoint;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=RoofMaterial(team);
            Beam(root,basePoint+v[0],basePoint+v[1],.2f);Beam(root,basePoint+v[1],basePoint+v[2],.2f);
            Beam(root,basePoint+v[3],basePoint+v[4],.2f);Beam(root,basePoint+v[4],basePoint+v[5],.2f);
            Beam(root,basePoint+v[1],basePoint+v[4],.2f);return go;
        }
        public static Material RoofMaterial(int team) => Painted(1,team==0?new Color(.24f,.42f,.95f):team==1?new Color(.92f,.20f,.13f):new Color(.96f,.91f,.72f),.32f,true);
        static Renderer Banner(Transform root,Vector3 position,int team,float width=.7f,float height=1.6f)
        {
            var go=new GameObject("Banner");go.transform.SetParent(root,false);go.transform.localPosition=position;
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-width/2,0,0),new Vector3(width/2,0,0),new Vector3(width/2,-height*.8f,0),new Vector3(0,-height,0),new Vector3(-width/2,-height*.8f,0)};
            mesh.triangles=new[]{0,2,1,0,4,2,4,3,2,1,2,0,2,4,0,2,3,4};mesh.normals=new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.back,Vector3.back};
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamColor(team));
            VisualFactory.Shape(root,PrimitiveType.Cube,"Gold heraldry",position+new Vector3(0,-height*.4f,-.015f),new Vector3(width*.16f,height*.58f,.035f),new Color(1,.77f,.26f));
            return renderer;
        }
        public static Renderer Town(Transform root,int team,bool capital)
        {
            var model=new GameObject(root.name+" · architecture");model.transform.SetParent(root,false);
            model.transform.localScale=Vector3.one*VisualMetrics.TownScale;root=model.transform;
            GroundShadow(root,new Vector3(1,.035f,1.25f),new Vector2(9,8));
            Block(root,"Stepped stone footing",new Vector3(0,.18f,0),new Vector3(4.3f,.36f,4.2f));
            Block(root,"Masonry hall",new Vector3(0,1.55f,0),new Vector3(3.5f,2.75f,3.15f),0,new Color(1.12f,1.09f,.97f),true);
            Block(root,"Upper plaster storey",new Vector3(0,3.05f,0),new Vector3(3.58f,.95f,3.2f),0,new Color(1.2f,1.08f,.83f));
            Roof(root,new Vector3(0,3.48f,0),4.8f,4.45f,capital?2.2f:1.85f,team);
            for(int side=-1;side<=1;side+=2)
            {
                Block(root,"Masonry buttress",new Vector3(side*1.82f,1.0f,-1.2f),new Vector3(.42f,2,.8f));
                Beam(root,new Vector3(side*1.7f,.4f,-1.65f),new Vector3(side*1.7f,3.5f,-1.65f),.2f);
                Beam(root,new Vector3(side*1.7f,2.7f,-1.65f),new Vector3(side*.65f,3.5f,-1.65f),.14f);
                Block(root,"Window arch stone",new Vector3(side*1.05f,2.14f,-1.66f),new Vector3(.62f,.98f,.12f));
                VisualFactory.Shape(root,PrimitiveType.Cube,"Warm window",new Vector3(side*1.05f,2.14f,-1.74f),new Vector3(.37f,.69f,.06f),new Color(.96f,.64f,.19f));
                Beam(root,new Vector3(side*1.05f,1.79f,-1.78f),new Vector3(side*1.05f,2.5f,-1.78f),.08f);
            }
            Beam(root,new Vector3(-1.8f,2.62f,-1.68f),new Vector3(1.8f,2.62f,-1.68f),.17f);
            Block(root,"Gate surround",new Vector3(0,1.05f,-1.7f),new Vector3(1.34f,2.1f,.32f));
            Block(root,"Heavy oak gates",new Vector3(0,.93f,-1.89f),new Vector3(.95f,1.7f,.12f),2);
            for(int i=0;i<3;i++)Block(root,"Entrance stair",new Vector3(0,.09f+i*.07f,-2.25f+i*.15f),new Vector3(1.55f,.18f+i*.14f,.8f-i*.15f));
            Roof(root,new Vector3(0,2.0f,-1.87f),1.75f,1.25f,.68f,team);
            if(capital)
            {
                Block(root,"Rear bell tower",new Vector3(.95f,4.0f,1.15f),new Vector3(1.35f,3.7f,1.3f));
                Block(root,"Bell opening",new Vector3(.95f,5.25f,.475f),new Vector3(.6f,.85f,.045f),2);
                Roof(root,new Vector3(.95f,5.9f,1.15f),2.15f,2.1f,1.4f,team);
            }
            else
            {
                Block(root,"Chimney",new Vector3(1.25f,4.1f,.6f),new Vector3(.57f,2,.6f));
                Block(root,"Chimney cap",new Vector3(1.25f,5.1f,.6f),new Vector3(.72f,.19f,.73f));
            }
            if(root.name.Contains("Molino"))
            {
                var rotor=new GameObject("Mill wheel");rotor.transform.SetParent(root,false);rotor.transform.localPosition=new Vector3(0,4.25f,-2.33f);
                rotor.AddComponent<MillSails>();
                for(int i=0;i<4;i++)
                {
                    var sail=new GameObject("Sail");sail.transform.SetParent(rotor.transform,false);sail.transform.localRotation=Quaternion.Euler(0,0,i*90+22);
                    Beam(sail.transform,Vector3.zero,Vector3.up*2.2f,.12f);
                    for(int slat=0;slat<5;slat++)Block(sail.transform,"Windmill slat",new Vector3(.2f,.85f+slat*.24f,0),new Vector3(.65f,.14f,.08f),2,new Color(1.6f,1.55f,1.35f));
                }
            }
            return Banner(root,new Vector3(-1.08f,3.28f,-1.78f),team,.66f,1.22f);
        }
        public static void Tower(Transform root,int team,out GameObject upper,out GameObject scaffold,out Renderer banner)
        {
            var model=new GameObject("Tower architecture");model.transform.SetParent(root,false);
            model.transform.localScale=Vector3.one*VisualMetrics.TowerScale;root=model.transform;
            Block(root,"Stone foundation",new Vector3(0,.4f,0),new Vector3(2.8f,.8f,2.8f),0,null,true);
            upper=new GameObject("Watchtower");upper.transform.SetParent(root,false);
            GroundShadow(upper.transform,new Vector3(1,.04f,1.2f),new Vector2(5,6));
            var shaft=VisualFactory.Shape(upper.transform,PrimitiveType.Cylinder,"Stone plinth",new Vector3(0,.88f,0),new Vector3(2.22f,.44f,2.22f),Color.white);
            shaft.GetComponent<Renderer>().sharedMaterial=Painted(0);
            Block(upper.transform,"Gallery supports",new Vector3(0,3.8f,0),new Vector3(2.8f,.38f,2.8f),2);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                Beam(upper.transform,new Vector3(x*1.22f,.8f,z*1.22f),new Vector3(x*.94f,3.8f,z*.94f),.36f);
                Block(upper.transform,"Iron post binding",new Vector3(x*1.05f,2.5f,z*1.05f),new Vector3(.41f,.19f,.41f),2,new Color(.4f,.43f,.44f));
                Beam(upper.transform,new Vector3(x*.9f,3.2f,z*.9f),new Vector3(x*1.23f,3.8f,z*1.23f),.18f);
                Beam(upper.transform,new Vector3(x*1.18f,3.8f,z*1.18f),new Vector3(x*1.18f,5.05f,z*1.18f),.19f);
            }
            var roof=VisualFactory.Cone(upper.transform,"Faction roof",new Vector3(0,5.04f,0),2.25f,1.55f,Color.white,4,45);
            roof.GetComponent<Renderer>().sharedMaterial=RoofMaterial(team);
            for(int s=-1;s<=1;s+=2)
            {
                Beam(upper.transform,new Vector3(-1.1f,1.2f,s*1.07f),new Vector3(1.0f,3.45f,s*1.0f),.16f);
                Block(upper.transform,"Gallery parapet",new Vector3(0,4.02f,s*1.31f),new Vector3(2.85f,.36f,.16f),2);
            }
            Beam(upper.transform,new Vector3(0,5.9f,0),new Vector3(0,6.94f,0),.12f);
            VisualFactory.Cone(upper.transform,"Bronze finial",new Vector3(0,6.82f,0),.17f,.45f,new Color(.82f,.65f,.32f),6);
            banner=Banner(upper.transform,new Vector3(0,3.2f,-1.15f),team,.85f,1.5f);
            scaffold=new GameObject("Scaffolding");scaffold.transform.SetParent(root,false);
            for(int s=-1;s<=1;s+=2){Beam(scaffold.transform,new Vector3(s*1.3f,0,-1.3f),new Vector3(s*1.3f,4,-1.3f));Block(scaffold.transform,"Scaffold",new Vector3(0,s==1?2.8f:1.2f,-1.3f),new Vector3(3,.16f,.6f),2);}
        }
        public static void ResetRoads() {roadCount=0;Shader.SetGlobalInt("_RiskRoadCount",0);}
        public static void Road(Vector3 a,Vector3 b,float width)
        {
            if(roadCount>=12)return;roadsA[roadCount]=new Vector4(a.x,a.z,width,0);roadsB[roadCount]=new Vector4(b.x,b.z,0,0);roadCount++;
            Shader.SetGlobalVectorArray("_RiskRoadsA",roadsA);Shader.SetGlobalVectorArray("_RiskRoadsB",roadsB);Shader.SetGlobalInt("_RiskRoadCount",roadCount);
        }
        public static void Cities(IReadOnlyList<Settlement> towns)
        {
            for(int i=0;i<cities.Length;i++)cities[i]=Vector4.zero;
            int count=Mathf.Min(towns.Count,cities.Length);
            for(int i=0;i<count;i++)cities[i]=new Vector4(towns[i].transform.position.x,towns[i].transform.position.z,0,0);
            Shader.SetGlobalVectorArray("_RiskCities",cities);
            Shader.SetGlobalInt("_RiskCityCount",count);
        }
        public static void Tree(Transform root,Vector3 position,float height,int seed,bool solid=true)
        {
            var tree=new GameObject("Silver fir");tree.transform.SetParent(root,false);tree.transform.localPosition=position;
            GroundShadow(tree.transform,new Vector3(.45f,.04f,.6f),new Vector2(height*.95f,height*.85f));
            var trunk=VisualFactory.Shape(tree.transform,PrimitiveType.Cylinder,"Fir trunk",Vector3.up*height*.22f,new Vector3(.32f,height*.22f,.32f),Color.white,solid);
            trunk.GetComponent<Renderer>().sharedMaterial=Painted(2,new Color(.65f,.7f,.72f),.6f);
            var crown=new GameObject("Layered evergreen boughs");crown.transform.SetParent(tree.transform,false);
            crown.transform.localScale=Vector3.one*height;crown.transform.localRotation=Quaternion.Euler(0,seed*137,0);
            crown.AddComponent<MeshFilter>().sharedMesh=FirMesh(seed%16);
            crown.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("FirFoliage");
        }
        static readonly Dictionary<int,Mesh> firMeshes=new Dictionary<int,Mesh>();
        static Mesh FirMesh(int seed)
        {
            if(firMeshes.TryGetValue(seed,out var found)&&found)return found;
            var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();var colors=new List<Color>();var random=new System.Random(seed*89+23);
            for(int tier=0;tier<6;tier++)
            {
                float y=.34f+tier*.123f,r=.40f-tier*.061f;
                for(int s=0;s<7;s++)
                {
                    float a=(s*360f/7+tier*28+(float)random.NextDouble()*12)*Mathf.Deg2Rad;
                    var outwards=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-outwards.z,0,outwards.x);
                    var inner=Vector3.up*(y+.07f)-outwards*.035f;
                    var tip=Vector3.up*(y-r*.45f)+outwards*r;
                    float width=r*.63f;
                    int k=v.Count;v.Add(inner-side*width);v.Add(inner+side*width);v.Add(tip+side*width);v.Add(tip-side*width);
                    uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(1,1));uv.Add(new Vector2(0,1));
                    t.Add(k);t.Add(k+2);t.Add(k+1);t.Add(k);t.Add(k+3);t.Add(k+2);
                    var tint=new Color(.80f+(seed%3)*.05f,.96f,.83f)*(1+tier*.025f);
                    for(int j=0;j<4;j++)colors.Add(tint);
                }
            }
            var mesh=new Mesh{name="Layered cutout fir boughs "+seed};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();firMeshes[seed]=mesh;return mesh;
        }
        public static void Rock(Transform root,Vector3 position,float size,int seed)
        {
            var rock=VisualFactory.Shape(root,PrimitiveType.Sphere,"Mossy bank rock",position,new Vector3(size,size*.6f,size*.85f),Color.white);
            rock.transform.rotation=Quaternion.Euler(seed*11,seed*29,seed*17);rock.GetComponent<Renderer>().sharedMaterial=Painted(2,new Color(.72f,.84f,1),.38f,false,true);
        }
        public static void Bridge(Transform root,float z)
        {
            Block(root,"Stone bridge",new Vector3(0,.05f,z),new Vector3(9,.3f,7),0,null,true);
            for(int side=-1;side<=1;side+=2)
            {
                Block(root,"Bridge parapet",new Vector3(0,.5f,z+side*3.35f),new Vector3(9,.7f,.3f),0,null,true);
                for(int i=-2;i<=2;i++)Block(root,"Parapet pier",new Vector3(i*2,.8f,z+side*3.35f),new Vector3(.48f,1.3f,.55f));
            }
        }
    }
    public sealed class MillSails : MonoBehaviour
    {void Update(){transform.Rotate(0,0,-Time.deltaTime*16,Space.Self);}}
}
