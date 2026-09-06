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
            var walkableTriangles=new List<int>(nx*nz*6);
            for(int x=0;x<=nx;x++)for(int z=0;z<=nz;z++)
            {
                float wx=Mathf.Lerp(-MapLayout.HalfWidth,MapLayout.HalfWidth,x/(float)nx),top=Mathf.Min(MapLayout.HalfDepth,MapLayout.Coast(wx));
                float wz=Mathf.Lerp(-MapLayout.HalfDepth,top,z/(float)nz);int i=x*(nz+1)+z;
                vertices[i]=new Vector3(wx,MapLayout.Height(wx,wz),wz);
                float quadX=Mathf.Lerp(-MapLayout.HalfWidth,MapLayout.HalfWidth,(x+.5f)/nx),quadZ=Mathf.Lerp(-MapLayout.HalfDepth,top,(z+.5f)/nz);
                if(x==nx||z==nz)continue;int b=i+nz+1;
                triangles.Add(i);triangles.Add(i+1);triangles.Add(b);triangles.Add(i+1);triangles.Add(b+1);triangles.Add(b);
                // Water needs a continuous visible bed. Only navigation excludes the channel.
                if(MapLayout.IsPond(wx+.4f,wz+.4f)||TerrainHydrology.IsChannel(quadX,quadZ))continue;
                walkableTriangles.Add(i);walkableTriangles.Add(i+1);walkableTriangles.Add(b);
                walkableTriangles.Add(i+1);walkableTriangles.Add(b+1);walkableTriangles.Add(b);
            }
            var mesh=new Mesh{name="Irregular continental terrain",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=vertices;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var land=new GameObject("Coastal marches");land.layer=MapLayout.TerrainLayer;land.transform.SetParent(root,false);land.AddComponent<MeshFilter>().sharedMesh=mesh;
            land.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");
            var collisionMesh=new Mesh{name="Walkable land excluding water",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            collisionMesh.vertices=vertices;collisionMesh.SetTriangles(walkableTriangles,0);collisionMesh.RecalculateBounds();
            land.AddComponent<MeshCollider>().sharedMesh=collisionMesh;
            var clearings = BuildingClearings();
            CreateIslands(root);CreateSeabed(root);CreateBackdrop(clearings);
            var sea=VisualFactory.Shape(null,PrimitiveType.Cube,"Northern sea",new Vector3(0,-.3f,0),new Vector3(420,.12f,420),Color.white);
            sea.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("RiverWater");
            var trees=new GameObject("Pine forests");trees.transform.SetParent(root,false);
            var random=new System.Random(4019);int seed=0;
            float mapX=MapLayout.HalfWidth/MapLayout.Spacing,mapZ=MapLayout.HalfDepth/MapLayout.Spacing;
            for(float x=-mapX+2;x<mapX-1;x+=1.9f)for(float z=-mapZ+2;z<mapZ-1;z+=1.9f)
            {
                float px=(x+(float)random.NextDouble()*1.9f)*MapLayout.Spacing,pz=(z+(float)random.NextDouble()*1.9f)*MapLayout.Spacing;
                if(!MapLayout.IsLand(px,pz)||TerrainHydrology.DistanceToRiver(px,pz)<4)continue;
                var point=new Vector3(px,MapLayout.Height(px,pz),pz);
                float bx=px/MapLayout.Spacing,bz=pz/MapLayout.Spacing;
                float west=Mathf.Abs(bx-(-10+5*Mathf.Sin(bz*.095f)));
                float east=Mathf.Abs(bx-(29+6*Mathf.Sin(bz*.12f)));
                float south=Mathf.Abs(bz-(-27+5*Mathf.Sin(bx*.09f)));
                bool island=pz>MapLayout.Coast(px);
                bool southwest=!MapLayout.IsExpanded&&bx<4&&bz<-45;
                if(island){float d=-1;for(int islandIndex=0;islandIndex<MapLayout.Islands.Length;islandIndex++)d=Mathf.Max(d,MapLayout.IslandDistance(px,pz,islandIndex));if(d<3)continue;}
                bool edge=Mathf.Abs(bx)>mapX-5||bz<-mapZ+6||Mathf.Abs(pz-MapLayout.Coast(px))<5;
                bool ribbon=west<3.5f||east<3.1f||south<2.8f;
                bool grove=Mathf.PerlinNoise(px*.046f+14,pz*.046f+8)>.64f;
                // Broad warped noise creates recognisable woods, dry openings and scrub
                // without a second scatter pass or a new asset. The small-scale noise
                // only breaks each patch edge, so it reads as natural cover at RTS range.
                float warpX=Mathf.PerlinNoise(px*.012f+31,pz*.012f+7)*18-9;
                float warpZ=Mathf.PerlinNoise(px*.012f-11,pz*.012f+43)*18-9;
                float patch=Mathf.PerlinNoise((px+warpX)*.021f+4,(pz+warpZ)*.021f+19);
                float fringe=Mathf.PerlinNoise(px*.079f+71,pz*.079f+13);
                bool woodland=patch>.68f&&fringe>.35f;
                if(!(edge||ribbon||grove||woodland||island)||random.NextDouble()<(island?.38:woodland?.055:.16))continue;
                bool rampPass=MapLayout.IsExpanded ? TerrainHydrology.IsChannel(px,pz) : (Mathf.Abs(bx+32)<5.5f&&bz>-14&&bz<7)||(Mathf.Abs(bx-34)<5.5f&&bz>-16&&bz<8)
                    ||(Mathf.Abs(bz-12)<5&&bx>-21&&bx<6)||(Mathf.Abs(bz-13)<5&&bx>44);
                if(!edge&&(Mathf.Abs(bz-2)<3.2f||Mathf.Abs(bz-22)<3||rampPass))continue;
                if(Mathf.Abs(MapLayout.Height(px+1,pz)-point.y)>1||Mathf.Abs(MapLayout.Height(px,pz+1)-point.y)>1)continue;
                float treeHeight=southwest?2.8f+(float)random.NextDouble()*1.55f:3.6f+(float)random.NextDouble()*2.1f;
                int treeSeed=seed++;
                if(ObscuresBuilding(point,treeHeight,clearings))continue;
                BiomeVegetation.Tree(trees.transform,point,treeHeight,treeSeed);
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
                float mountainX=MapLayout.IsExpanded?8:57,mountainZ=MapLayout.IsExpanded?-85:-40;
                float x=(mountainX-13+(float)random.NextDouble()*26)*MapLayout.Spacing,z=(mountainZ-13+(float)random.NextDouble()*26)*MapLayout.Spacing;
                if(Vector2.Distance(new Vector2(x,z)/MapLayout.Spacing,new Vector2(mountainX,mountainZ))>17)continue;
                WorldArt.Rock(root,new Vector3(x,MapLayout.Height(x,z),z),.6f+(float)random.NextDouble()*1.6f,i+80);
            }
            if(!MapLayout.IsExpanded)CreateClassicSouthwestDetails(root,clearings);
        }
        static void CreateClassicSouthwestDetails(Transform parent,List<Vector4> clearings)
        {
            // A handful of repeatable stone clusters gives the olive clearings a
            // dry Mediterranean edge without filling their deployment space.
            var root=new GameObject("Southwestern dry-stone outcrops");root.transform.SetParent(parent,false);
            var centers=new[]{new Vector2(-67,-54),new Vector2(-45,-75),new Vector2(-18,-53),new Vector2(5,-77)};
            var random=new System.Random(9127);int seed=320;
            foreach(var center in centers)for(int i=0;i<6;i++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2,r=1.2f+(float)random.NextDouble()*4.6f;
                float x=(center.x+Mathf.Cos(a)*r)*MapLayout.Spacing,z=(center.y+Mathf.Sin(a)*r)*MapLayout.Spacing;
                if(!MapLayout.IsLand(x,z)||NearClearing(x,z,clearings,3.3f))continue;
                WorldArt.Rock(root.transform,new Vector3(x,MapLayout.Height(x,z)-.03f,z),.42f+(float)random.NextDouble()*.72f,seed++);
            }
            StaticBatchingUtility.Combine(root);
        }
        static bool NearClearing(float x,float z,List<Vector4> clearings,float margin)
        {
            foreach(var site in clearings)
            {
                float radius=site.w+margin,dx=site.x-x,dz=site.z-z;
                if(dx*dx+dz*dz<radius*radius)return true;
            }
            return false;
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
        static void CreateSeabed(Transform root)
        {
            // Extend sand under the shoreline; no collider, so this cannot bake walkable ocean.
            var v=new List<Vector3>();var t=new List<int>();const int columns=360,rows=64;
            for(int x=0;x<=columns;x++)for(int z=0;z<=rows;z++)
            {
                float wx=Mathf.Lerp(-MapLayout.HalfWidth,MapLayout.HalfWidth,x/(float)columns);
                float wz=Mathf.Lerp(MapLayout.Coast(wx),MapLayout.HalfDepth+16,z/(float)rows);
                v.Add(new Vector3(wx,MapLayout.Height(wx,wz)-.012f,wz));
                if(x==columns||z==rows)continue;int i=x*(rows+1)+z,b=i+rows+1;
                t.Add(i);t.Add(i+1);t.Add(b);t.Add(i+1);t.Add(b+1);t.Add(b);
            }
            var mesh=new Mesh{name="Submerged continental and island shelf"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Sandy sea bed · visual only");go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");
        }
        static void CreateBackdrop(List<Vector4> clearings)
        {
            // Visual continuation beyond the playable rectangle: no artificial board edges.
            // It has no colliders and therefore cannot expand the gameplay NavMesh.
            var root=new GameObject("Distant continental woodland");
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(float bx=-140;bx<140;bx+=2)for(float bz=-140;bz<100;bz+=2)
            {
                if(bx>=-MapLayout.HalfWidth/MapLayout.Spacing&&bx<MapLayout.HalfWidth/MapLayout.Spacing&&bz>=-MapLayout.HalfDepth/MapLayout.Spacing&&bz<MapLayout.HalfDepth/MapLayout.Spacing)continue;
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
                var point=new Vector3(px,MapLayout.Height(px,pz),pz);
                float treeHeight=3.1f+(float)random.NextDouble()*1.5f;
                if(ObscuresBuilding(point,treeHeight,clearings))continue;
                WorldArt.Tree(root.transform,point,treeHeight,(int)(x*17+z*31)&32767,false);
            }
            StaticBatchingUtility.Combine(root);
        }
        static List<Vector4> BuildingClearings()
        {
            var sites = new List<Vector4>();
            void Add(Vector3 point, float radius) => sites.Add(new Vector4(point.x, point.y, point.z, radius));
            foreach (var city in MapLayout.Towns)
            {
                Add(city.Position, 3f);
                Add(city.Position + new Vector3(city.Position.x < 0 ? 3.8f : -3.8f, 0, 0), 2f);
                Add(MapLayout.Point(city.Position.x, city.Position.z - 4.2f), 2.5f);
                // Keep deployment space clear for either owner, regardless of the random allocation.
                Add(MapLayout.Point(city.Position.x, city.Position.z - 6), 2.5f);
                Add(MapLayout.Point(city.Position.x, city.Position.z + 6), 2.5f);
            }
            for (int i = 0; i < MapLayout.MainlandHarborX.Length; i++) Add(MapLayout.MainlandHarborLanding(i), 6f);
            for (int i = 0; i < MapLayout.Islands.Length; i++) Add(MapLayout.IslandHarborLanding(i), 6f);
            return sites;
        }
        static bool ObscuresBuilding(Vector3 tree, float height, List<Vector4> clearings)
        {
            // Reserve the canopy, not just the trunk. Project its height along the fixed
            // RTS viewing angle so foreground trees cannot cover a roof or garrison circle.
            var forward = new Vector2(.5f, .8660254f); // camera yaw 30 degrees
            float crownRadius = height * .72f;
            foreach (var site in clearings)
            {
                var delta = new Vector2(site.x - tree.x, site.z - tree.z);
                float projection = Mathf.Max(0, tree.y + height * 1.1f - site.y) / 1.150368f; // tan(49 degrees)
                var nearest = forward * Mathf.Clamp(Vector2.Dot(delta, forward), 0, projection);
                float radius = site.w + crownRadius;
                if ((delta - nearest).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }
    }
}
