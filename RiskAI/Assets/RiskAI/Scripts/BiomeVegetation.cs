using System.Collections.Generic;
using UnityEngine;
namespace RiskAI
{
    public static class BiomeVegetation
    {
        static readonly Dictionary<int,Mesh> crowns=new();
        public static void Tree(Transform root,Vector3 p,float height,int seed)
        {
            float x=p.x/MapLayout.Spacing,z=p.z/MapLayout.Spacing;
            bool island=p.z>MapLayout.Coast(p.x);
            int biome=island&&z>60?2:x>28&&z<-25?2:x<-24&&z>15?1:(seed%7==0?0:-1);
            if(island&&z<60)biome=seed%3==0?1:0;
            if(biome<0){WorldArt.Tree(root,p,height,seed);return;}
            var go=new GameObject(biome==2?"Coastal palm":biome==1?"Amber oak":"Green oak");go.transform.SetParent(root,false);go.transform.localPosition=p;
            go.transform.localRotation=Quaternion.Euler(0,seed*137.5f,0);
            float trunkHeight=biome==2?height*.78f:height*.56f;
            var trunk=VisualFactory.Shape(go.transform,PrimitiveType.Cylinder,"Bark",Vector3.up*trunkHeight*.5f,new Vector3(biome==2?.3f:.46f,trunkHeight*.5f,biome==2?.3f:.46f),Color.white,true);
            trunk.GetComponent<Renderer>().sharedMaterial=WorldArt.Painted(2,new Color(.85f,.78f,.58f),.6f);
            WorldArt.GroundShadow(go.transform,new(.3f,.04f,.3f),new(height,height*.85f));
            var crown=new GameObject("Painted foliage crown");crown.transform.SetParent(go.transform,false);crown.transform.localScale=Vector3.one*height;
            crown.AddComponent<MeshFilter>().sharedMesh=Crown(biome,seed%4);crown.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("BiomeFoliage");
        }
        static Mesh Crown(int biome,int variation)
        {
            int key=biome*10+variation;if(crowns.TryGetValue(key,out var found)&&found)return found;
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var colors=new List<Color>();
            Vector2 tile=new((biome%2)*.5f,biome<2?.5f:0);
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float shade)
            {
                int k=v.Count;v.Add(a);v.Add(b);v.Add(c);v.Add(d);
                uv.Add(tile+new Vector2(.012f,.012f));uv.Add(tile+new Vector2(.488f,.012f));uv.Add(tile+new Vector2(.488f,.488f));uv.Add(tile+new Vector2(.012f,.488f));
                t.Add(k);t.Add(k+2);t.Add(k+1);t.Add(k);t.Add(k+3);t.Add(k+2);for(int n=0;n<4;n++)colors.Add(Color.white*shade);
            }
            if(biome==2)
            {
                for(int j=0;j<7;j++)
                {
                    float a=(j*360f/7+variation*11)*Mathf.Deg2Rad;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-dir.z,0,dir.x)*.12f;
                    var center=Vector3.up*(.82f+j%2*.06f);var tip=center+dir*.64f-Vector3.up*.19f;
                    Quad(center-side*.45f,center+side*.45f,tip+side,tip-side,.82f+j%3*.08f);
                }
            }
            else
            {
                for(int tier=0;tier<3;tier++)for(int j=0;j<6;j++)
                {
                    float a=(j*60+tier*31+variation*13)*Mathf.Deg2Rad;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-dir.z,0,dir.x)*(.25f-tier*.035f);
                    var center=dir*(.20f-tier*.06f)+Vector3.up*(.51f+tier*.145f);var up=new Vector3(0,.24f-tier*.02f,0);
                    Quad(center-side-up,center+side-up,center+side+up-dir*.15f,center-side+up-dir*.15f,.84f+tier*.08f);
                    if(tier==2)Quad(center-side-dir*.18f,center+side-dir*.18f,center+side+dir*.18f,center-side+dir*.18f,1);
                }
            }
            var mesh=new Mesh{name="Original biome crown "+key};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();crowns[key]=mesh;return mesh;
        }
    }
}
