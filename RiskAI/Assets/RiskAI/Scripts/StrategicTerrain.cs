using System.Collections.Generic;
using UnityEngine;
namespace RiskAI
{
    public static class StrategicTerrain
    {
        public static void Create(Transform root)
        {
            // Sub-metre sampling keeps the bevel and river banks continuous with the walkable surface.
            const int nx=360,nz=400;
            var vertices=new Vector3[(nx+1)*(nz+1)];var triangles=new List<int>(nx*nz*6);
            for(int x=0;x<=nx;x++)for(int z=0;z<=nz;z++)
            {
                float wx=Mathf.Lerp(-MapLayout.HalfWidth,MapLayout.HalfWidth,x/(float)nx),top=Mathf.Min(MapLayout.HalfDepth,MapLayout.Coast(wx));
                float wz=Mathf.Lerp(-MapLayout.HalfDepth,top,z/(float)nz);int i=x*(nz+1)+z;
                vertices[i]=new Vector3(wx,MapLayout.Height(wx,wz),wz);
                if(x==nx||z==nz||MapLayout.IsPond(wx+.4f,wz+.4f))continue;int b=i+nz+1;
                triangles.Add(i);triangles.Add(i+1);triangles.Add(b);triangles.Add(i+1);triangles.Add(b+1);triangles.Add(b);
            }
            var mesh=new Mesh{name="Irregular continental terrain",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=vertices;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var land=new GameObject("Coastal marches");land.layer=MapLayout.TerrainLayer;land.transform.SetParent(root,false);land.AddComponent<MeshFilter>().sharedMesh=mesh;
            land.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");land.AddComponent<MeshCollider>().sharedMesh=mesh;
            CreateIslands(root);CreateBackdrop();
            var sea=VisualFactory.Shape(null,PrimitiveType.Cube,"Northern sea",new Vector3(0,-.3f,0),new Vector3(420,.12f,420),Color.white);
            sea.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("RiverWater");
            var trees=new GameObject("Pine forests");trees.transform.SetParent(root,false);
            var random=new System.Random(4019);int seed=0;
            for(float x=-70;x<71;x+=1.9f)for(float z=-82;z<82;z+=1.9f)
            {
                float px=(x+(float)random.NextDouble()*1.5f)*MapLayout.Spacing,pz=(z+(float)random.NextDouble()*1.5f)*MapLayout.Spacing;
                if(!MapLayout.IsLand(px,pz)||TerrainHydrology.DistanceToRiver(px,pz)<4)continue;
                var point=new Vector3(px,MapLayout.Height(px,pz),pz);bool clear=false;
                foreach(var city in MapLayout.Towns)if(Vector3.Distance(point,city.Position)<9f){clear=true;break;}
                if(clear)continue;
                float bx=px/MapLayout.Spacing,bz=pz/MapLayout.Spacing;
                float west=Mathf.Abs(bx-(-10+5*Mathf.Sin(bz*.095f)));
                float east=Mathf.Abs(bx-(29+6*Mathf.Sin(bz*.12f)));
                float south=Mathf.Abs(bz-(-27+5*Mathf.Sin(bx*.09f)));
                bool island=pz>MapLayout.Coast(px);
                if(island){float d=Mathf.Max(MapLayout.IslandDistance(px,pz,0),MapLayout.IslandDistance(px,pz,1));if(d<3||bz<55&&Mathf.Abs(bx+47)<4||bz>60&&bz<69&&Mathf.Abs(bx+8)<4)continue;}
                bool portClear=false;foreach(float portX in new[]{-58f,-32,-7,20,43})if(Mathf.Abs(bx-portX)<4&&Mathf.Abs(pz-MapLayout.Coast(portX*MapLayout.Spacing))<9)portClear=true;
                if(portClear)continue;
                bool edge=Mathf.Abs(bx)>65||bz<-78||Mathf.Abs(pz-MapLayout.Coast(px))<5;
                bool ribbon=west<3.5f||east<3.1f||south<2.8f;
                bool grove=Mathf.PerlinNoise(px*.046f+14,pz*.046f+8)>.64f;
                if(!(edge||ribbon||grove||island)||random.NextDouble()<(island?.38:.12))continue;
                bool rampPass=(Mathf.Abs(bx+32)<5.5f&&bz>-14&&bz<7)||(Mathf.Abs(bx-34)<5.5f&&bz>-16&&bz<8)
                    ||(Mathf.Abs(bz-12)<5&&bx>-21&&bx<6)||(Mathf.Abs(bz-13)<5&&bx>44);
                if(!edge&&(Mathf.Abs(bz-2)<3.2f||Mathf.Abs(bz-22)<3||rampPass))continue;
                if(Mathf.Abs(MapLayout.Height(px+1,pz)-point.y)>1||Mathf.Abs(MapLayout.Height(px,pz+1)-point.y)>1)continue;
                BiomeVegetation.Tree(trees.transform,point,3.6f+(float)random.NextDouble()*2.1f,seed++);
            }
            StaticBatchingUtility.Combine(trees);
            CliffDetails.Create(root);
            for(int i=0;i<58;i++)
            {
                float x=(-70+(float)random.NextDouble()*140)*MapLayout.Spacing,z=MapLayout.Coast(x)-.5f-(float)random.NextDouble()*1.5f;
                if(z>MapLayout.HalfDepth)continue;WorldArt.Rock(root,new Vector3(x,MapLayout.Height(x,z)-.04f,z),.65f+(float)random.NextDouble()*1.2f,i);
            }
            for(int i=0;i<55;i++)
            {
                float x=(44+(float)random.NextDouble()*25)*MapLayout.Spacing,z=(-53+(float)random.NextDouble()*28)*MapLayout.Spacing;
                if(Vector2.Distance(new Vector2(x,z)/MapLayout.Spacing,new Vector2(57,-40))>17)continue;
                WorldArt.Rock(root,new Vector3(x,MapLayout.Height(x,z),z),.6f+(float)random.NextDouble()*1.6f,i+80);
            }
        }
        static void CreateIslands(Transform root)
        {
            for(int island=0;island<MapLayout.Islands.Length;island++)
            {
                var c=MapLayout.Islands[island];var v=new List<Vector3>();var t=new List<int>();const int rings=18,sides=96;
                for(int r=0;r<=rings;r++)for(int s=0;s<=sides;s++)
                {
                    float a=s*Mathf.PI*2/sides,shape=1+.075f*Mathf.Sin(a*3+island)+.045f*Mathf.Cos(a*5),f=r/(float)rings;
                    float x=(c.x+Mathf.Cos(a)*c.z*shape*f)*MapLayout.Spacing,z=(c.y+Mathf.Sin(a)*c.w*shape*f)*MapLayout.Spacing;
                    v.Add(MapLayout.Point(x,z));if(r==rings||s==sides)continue;int i=r*(sides+1)+s,b=i+sides+1;
                    t.Add(i);t.Add(b+1);t.Add(b);t.Add(i);t.Add(i+1);t.Add(b+1);
                }
                var mesh=new Mesh{name="Sculpted island "+island};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var go=new GameObject(island==0?"Isla de los Robles":"Isla del Viento");go.layer=MapLayout.TerrainLayer;go.transform.SetParent(root,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");go.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
        }
        static void CreateBackdrop()
        {
            // Visual continuation beyond the playable rectangle: no artificial board edges.
            // It has no colliders and therefore cannot expand the gameplay NavMesh.
            var root=new GameObject("Distant continental woodland");
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(float bx=-140;bx<140;bx+=2)for(float bz=-140;bz<100;bz+=2)
            {
                if(bx>=-72&&bx<72&&bz>=-84&&bz<84)continue;
                float x=bx*MapLayout.Spacing,z=bz*MapLayout.Spacing,step=2*MapLayout.Spacing;
                float coastA=MapLayout.Coast(x),coastB=MapLayout.Coast(x+step);
                if(z>=Mathf.Max(coastA,coastB))continue;
                float za=Mathf.Min(z,coastA),zb=Mathf.Min(z+step,coastA),zc=Mathf.Min(z,coastB),zd=Mathf.Min(z+step,coastB);
                int i=vertices.Count;
                vertices.Add(new Vector3(x,MapLayout.Height(x,za),za));vertices.Add(new Vector3(x,MapLayout.Height(x,zb),zb));
                vertices.Add(new Vector3(x+step,MapLayout.Height(x+step,zc),zc));vertices.Add(new Vector3(x+step,MapLayout.Height(x+step,zd),zd));
                triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i+1);triangles.Add(i+3);triangles.Add(i+2);
            }
            var mesh=new Mesh{name="Continental horizon",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");
            var random=new System.Random(561);
            for(float x=-104;x<104;x+=3.1f)for(float z=-88;z<83;z+=3.1f)
            {
                if(Mathf.Abs(x)<MapLayout.HalfWidth&&Mathf.Abs(z)<MapLayout.HalfDepth||z>MapLayout.Coast(x)-1.5f||random.NextDouble()<.4)continue;
                float px=x+(float)random.NextDouble()*1.8f,pz=z+(float)random.NextDouble()*1.8f;
                WorldArt.Tree(root.transform,new Vector3(px,MapLayout.Height(px,pz),pz),3.1f+(float)random.NextDouble()*1.5f,(int)(x*17+z*31)&32767,false);
            }
            StaticBatchingUtility.Combine(root);
        }
    }
}
