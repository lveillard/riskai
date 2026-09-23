using System.Collections.Generic;
using UnityEngine;
namespace RiskAI
{
    public static class StrategicTerrain
    {
        // The backdrop reaches past the end of the horizon fade on every side.
        const float HorizonFadeStart=10f,HorizonFadeEnd=88f,BackdropReach=112f;
        public static void Create(Transform root)
        {
            var resources=GeneratedResourceOwner.For(root);
            ShoreAccess.BakeSurface(root);
            FictionalGround.Bake(root);
            // Ground and sea beyond the board recede into the camera background instead of ending at a hard edge.
            TerrainBiomes.Horizon(new Vector4(-MapLayout.HalfWidth,-MapLayout.HalfDepth,MapLayout.HalfWidth,MapLayout.HalfDepth),HorizonFadeStart,HorizonFadeEnd);
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
            var mesh=resources.Track(new Mesh{name="Irregular continental terrain",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32});mesh.vertices=vertices;BakeCoastWeights(mesh,vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var land=new GameObject("Coastal marches");land.layer=MapLayout.TerrainLayer;land.transform.SetParent(root,false);land.AddComponent<MeshFilter>().sharedMesh=mesh;
            land.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");
            var collisionMesh=resources.Track(new Mesh{name="Walkable land excluding water",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32});
            collisionMesh.vertices=vertices;collisionMesh.SetTriangles(walkableTriangles,0);collisionMesh.RecalculateBounds();
            land.AddComponent<MeshCollider>().sharedMesh=collisionMesh;
            var clearings = BuildingClearings();
            CreateIslands(root,resources);CreateSeabed(root,resources);CreateBackdrop(clearings,resources);
            float seaSize=2*(Mathf.Max(MapLayout.HalfWidth,MapLayout.HalfDepth)+BackdropReach+20);
            var sea=VisualFactory.Shape(null,PrimitiveType.Cube,"Northern sea",new Vector3(0,-.3f,0),new Vector3(seaSize,.12f,seaSize),Color.white);
            sea.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("RiverWater");
            var trees=new GameObject("Pine forests");trees.transform.SetParent(root,false);
            var random=new System.Random(4019);int seed=0;
            float mapX=MapLayout.HalfWidth/MapLayout.Spacing,mapZ=MapLayout.HalfDepth/MapLayout.Spacing;
            for(float x=-mapX+2;x<mapX-1;x+=1.9f)for(float z=-mapZ+2;z<mapZ-1;z+=1.9f)
            {
                float px=(x+(float)random.NextDouble()*1.9f)*MapLayout.Spacing,pz=(z+(float)random.NextDouble()*1.9f)*MapLayout.Spacing;
                if(random.NextDouble()>=TreeChance(px,pz))continue;
                var point=new Vector3(px,MapLayout.Height(px,pz),pz);
                bool drySouth=FictionalGround.Sample(px,pz).Dry>.5f;
                if(Mathf.Abs(MapLayout.Height(px+1,pz)-point.y)>1||Mathf.Abs(MapLayout.Height(px,pz+1)-point.y)>1)continue;
                float treeHeight=drySouth?2.7f+(float)random.NextDouble()*1.45f:3.6f+(float)random.NextDouble()*2.1f;
                int treeSeed=seed++;
                if(ObscuresBuilding(point,treeHeight,clearings))continue;
                BiomeVegetation.Tree(trees.transform,point,treeHeight,treeSeed);
            }
            StaticBatchingUtility.Combine(trees);
            CliffDetails.Create(root);
            for(int i=0;i<58;i++)
            {
                float x=(-70+(float)random.NextDouble()*140)*MapLayout.Spacing,z=MapLayout.Coast(x)-.5f-(float)random.NextDouble()*1.5f;
                if(z>MapLayout.HalfDepth||ShoreAccess.SurfaceWeights(x,z).y<.55f)continue;WorldArt.Rock(root,new Vector3(x,MapLayout.Height(x,z)-.04f,z),.65f+(float)random.NextDouble()*1.2f,i);
            }
            if(MapLayout.IsExpanded)CreateExpandedCordilleraDetails(root,clearings);
            else for(int i=0,placed=0;i<400&&placed<48;i++)
            {
                // Boulders scattered where the ground-zone field exposes stone, half sunk.
                float x=(float)(random.NextDouble()*2-1)*MapLayout.HalfWidth,z=(float)(random.NextDouble()*2-1)*MapLayout.HalfDepth;
                float stone=FictionalGround.Sample(x,z).Stone;
                if(!MapLayout.IsLand(x,z)||stone<.3f||random.NextDouble()>stone*.8f||NearClearing(x,z,clearings,3f))continue;
                float size=.35f+(float)random.NextDouble()*(random.NextDouble()<.15?1.4f:.7f);
                WorldArt.Rock(root,new Vector3(x,MapLayout.Height(x,z)-size*.18f,z),size,i+80);placed++;
            }
            if(!MapLayout.IsExpanded)CreateClassicSouthwestDetails(root,clearings);
        }
        /// <summary>
        /// Probability that the forest scatter keeps a candidate tree at a world point,
        /// before slope and building-clearance checks. Every term is a continuous field
        /// (ribbons, groves, woods, the board edge, oases), so no rule can plant a
        /// straight line or leave a straight clearing; GroundZoneShapeTests checks it.
        /// </summary>
        public static float TreeChance(float px,float pz)
        {
            if(!MapLayout.IsLand(px,pz)||TerrainHydrology.DistanceToRiver(px,pz)<4)return 0;
            float mapX=MapLayout.HalfWidth/MapLayout.Spacing,mapZ=MapLayout.HalfDepth/MapLayout.Spacing;
            float bx=px/MapLayout.Spacing,bz=pz/MapLayout.Spacing;
            bool island=pz>MapLayout.Coast(px);
            if(island){float d=-1;for(int islandIndex=0;islandIndex<MapLayout.Islands.Length;islandIndex++)d=Mathf.Max(d,MapLayout.IslandDistance(px,pz,islandIndex));if(d<3)return 0;}
            if(FictionalGround.Riverbed(px,pz)>.25f)return 0;
            var zones=FictionalGround.Sample(px,pz);
            bool drySouth=zones.Dry>.5f;
            float jitter=(FictionalGround.Fbm(px*.035f+61,pz*.035f+23)-.5f)*4;
            float warpX=Mathf.PerlinNoise(px*.012f+31,pz*.012f+7)*18-9;
            float warpZ=Mathf.PerlinNoise(px*.012f-11,pz*.012f+43)*18-9;
            float patch=Mathf.PerlinNoise((px+warpX)*.021f+4,(pz+warpZ)*.021f+19);
            // The board edge is framed by a feathered, noise-broken fringe of woods.
            float outside=Mathf.Max(Mathf.Abs(bx)-mapX,bz<0?-bz-mapZ:-1000)+(FictionalGround.Fbm(px*.06f+3,pz*.06f+47)-.5f)*11;
            float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-9,-2,outside))*(drySouth?(patch>.57f?1:.25f):1);
            float coastFringe=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,6.5f,Mathf.Abs(pz-MapLayout.Coast(px))+jitter*.5f));
            // Desert: palms gather in oases around low ground and hollows; elsewhere only a
            // sparse, random scatter. The forest ribbons and groves stop at the desert.
            float desert=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.62f,zones.Arid+(FictionalGround.Fbm(px*.05f+2,pz*.05f+33)-.5f)*.3f))*(island?0:1);
            float oasis=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,.74f,FictionalGround.Fbm(px*.028f+13,pz*.028f+71)+Mathf.Clamp01(1.4f-MapLayout.Height(px,pz))*.25f));
            float desertChance=oasis*.72f+.035f;
            float west=Mathf.Abs(bx-(-10+5*Mathf.Sin(bz*.095f)))+jitter*.4f;
            float east=Mathf.Abs(bx-(29+6*Mathf.Sin(bz*.12f)))+jitter*.4f;
            float south=Mathf.Abs(bz-(-27+5*Mathf.Sin(bx*.09f)))+jitter*.4f;
            bool ribbon=west<3.5f||east<3.1f||south<2.8f;
            bool grove=Mathf.PerlinNoise(px*.046f+14,pz*.046f+8)>.64f;
            float fringe=Mathf.PerlinNoise(px*.079f+71,pz*.079f+13);
            bool woodland=patch>.68f&&fringe>.35f;
            float keep=1-(island?.38f:drySouth?.36f:woodland?.055f:.16f);
            float chance=ribbon||grove||woodland||island?keep:0;
            chance=Mathf.Max(chance,Mathf.Max(edge,coastFringe)*keep);
            // Keep the ramp approaches readable; the clearings meander with the jitter.
            float j=(FictionalGround.Fbm(px*.07f+19,pz*.07f+5)-.5f)*6,k=(FictionalGround.Fbm(px*.041f+7,pz*.041f+91)-.5f)*6;
            bool rampPass=MapLayout.IsExpanded ? TerrainHydrology.IsChannel(px,pz) : (Mathf.Abs(bx+32+j)<5.5f&&bz>-14+k&&bz<7+k)||(Mathf.Abs(bx-34+j)<5.5f&&bz>-16+k&&bz<8+k)
                ||(Mathf.Abs(bz-12+j)<5&&bx>-21+k&&bx<6+k)||(Mathf.Abs(bz-13+j)<5&&bx>44+k);
            if(edge<.5f&&(Mathf.Abs(bz-2+j)<3.2f||Mathf.Abs(bz-22+j)<3||rampPass))chance=0;
            return Mathf.Lerp(chance,desertChance*(rampPass?0:1),desert);
        }
        static void BakeCoastWeights(Mesh mesh,IReadOnlyList<Vector3> vertices)
        {
            var weights=new Vector2[vertices.Count];
            for(int i=0;i<weights.Length;i++)weights[i]=ShoreAccess.SurfaceWeights(vertices[i].x,vertices[i].z);
            mesh.SetUVs(1,weights);
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
            // Boulders strewn along the banks of the dry riverbed.
            for(int i=0;i<90;i++)
            {
                float x=(-72+(float)random.NextDouble()*70)*MapLayout.Spacing,z=(-88+(float)random.NextDouble()*30)*MapLayout.Spacing;
                float bed=FictionalGround.Riverbed(x,z);
                if(bed<.08f||bed>.6f||!MapLayout.IsLand(x,z)||NearClearing(x,z,clearings,3f))continue;
                WorldArt.Rock(root.transform,new Vector3(x,MapLayout.Height(x,z)-.05f,z),.3f+(float)random.NextDouble()*.75f,seed++);
            }
            StaticBatchingUtility.Combine(root);
            // Olive groves planted in loose rows, each at its own angle, in open secano.
            var groves=new GameObject("Secano olive groves");groves.transform.SetParent(parent,false);
            for(int attempt=0,planted=0;attempt<60&&planted<9;attempt++)
            {
                float cx=(-70+(float)random.NextDouble()*68)*MapLayout.Spacing,cz=(-88+(float)random.NextDouble()*40)*MapLayout.Spacing;
                if(FictionalGround.Sample(cx,cz).Dry<.7f||NearClearing(cx,cz,clearings,9f)||FictionalGround.Riverbed(cx,cz)>.02f)continue;
                planted++;float angle=(float)random.NextDouble()*Mathf.PI;
                var along=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var across=new Vector2(-along.y,along.x);
                int rows=3+random.Next(3),columns=4+random.Next(4);
                for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)
                {
                    var offset=along*((c-(columns-1)*.5f)*2.4f)+across*((r-(rows-1)*.5f)*2.4f)+new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.5f;
                    float x=cx+offset.x,z=cz+offset.y;
                    if(!MapLayout.IsLand(x,z)||NearClearing(x,z,clearings,2.5f)||random.NextDouble()<.12)continue;
                    var point=new Vector3(x,MapLayout.Height(x,z),z);float h=1.9f+(float)random.NextDouble()*.7f;
                    if(ObscuresBuilding(point,h,clearings))continue;
                    BiomeVegetation.SecanoOlive(groves.transform,point,h,seed++);
                }
            }
            StaticBatchingUtility.Combine(groves);
        }
        static void CreateExpandedCordilleraDetails(Transform parent,List<Vector4> clearings)
        {
            var root=new GameObject("Cordilleras gemelas de las riberas");root.transform.SetParent(parent,false);
            var random=new System.Random(6821);int seed=440;
            for(int side=-1;side<=1;side+=2)
            {
                var start=new Vector2(side*67,-74);var end=new Vector2(side*45,42);
                var direction=(end-start).normalized;var normal=new Vector2(-direction.y,direction.x);
                for(int i=0;i<42;i++)
                {
                    float along=(i+(float)random.NextDouble())/42f;
                    var point=Vector2.Lerp(start,end,along)+normal*((float)random.NextDouble()*10-5);
                    float x=point.x*MapLayout.Spacing,z=point.y*MapLayout.Spacing;
                    if(!MapLayout.IsLand(x,z)||TerrainHydrology.DistanceToRiver(x,z)<7||NearClearing(x,z,clearings,4.5f))continue;
                    float y=MapLayout.Height(x,z),slope=Mathf.Max(Mathf.Abs(MapLayout.Height(x+1,z)-MapLayout.Height(x-1,z)),Mathf.Abs(MapLayout.Height(x,z+1)-MapLayout.Height(x,z-1)));
                    if(slope<.22f)continue;
                    WorldArt.Rock(root.transform,new Vector3(x,y-.05f,z),.55f+(float)random.NextDouble()*1.45f,seed++);
                }
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
        static void CreateIslands(Transform root,GeneratedResourceOwner resources)
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
                var mesh=resources.Track(new Mesh{name="Sculpted island "+island});mesh.SetVertices(v);BakeCoastWeights(mesh,v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var go=new GameObject(island==0?"Isla de los Robles":"Isla del Viento");go.layer=MapLayout.TerrainLayer;go.transform.SetParent(root,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");go.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
        }
        static void CreateSeabed(Transform root,GeneratedResourceOwner resources)
        {
            // Extend sand under the shoreline; no collider, so this cannot bake walkable ocean.
            // Rows of about half a metre, like the columns: a coarse shelf grid shows its cell
            // edges in the shallow-water shading where the shore curves across it.
            var v=new List<Vector3>();var t=new List<int>();const int columns=360,rows=160;
            var onIsland=new bool[(columns+1)*(rows+1)];
            for(int x=0;x<=columns;x++)for(int z=0;z<=rows;z++)
            {
                float wx=Mathf.Lerp(-MapLayout.HalfWidth,MapLayout.HalfWidth,x/(float)columns);
                float wz=Mathf.Lerp(MapLayout.Coast(wx),MapLayout.HalfDepth+16,z/(float)rows);
                v.Add(new Vector3(wx,SeabedHeight(wx,wz),wz));
                for(int island=0;island<MapLayout.Islands.Length;island++)onIsland[x*(rows+1)+z]|=MapLayout.IslandDistance(wx,wz,island)>.3f;
            }
            // The island meshes own their land: shelf triangles wholly on an island would poke
            // their coarse chords through the sand (straight lines) and hide the territory overlay.
            for(int x=0;x<columns;x++)for(int z=0;z<rows;z++)
            {
                int i=x*(rows+1)+z,b=i+rows+1;
                if(!(onIsland[i]&&onIsland[i+1]&&onIsland[b]))t.AddRange(new[]{i,i+1,b});
                if(!(onIsland[i+1]&&onIsland[b+1]&&onIsland[b]))t.AddRange(new[]{i+1,b+1,b});
            }
            var mesh=resources.Track(new Mesh{name="Submerged continental and island shelf"});mesh.SetVertices(v);BakeCoastWeights(mesh,v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Sandy sea bed · visual only");go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");
        }
        /// <summary>
        /// Shelf height. Under island land the shelf sinks well below the island surface:
        /// a shelf triangle that crosses the shore then disappears beneath the island mesh
        /// instead of intersecting its coarser chords along a ruler-straight line.
        /// </summary>
        public static float SeabedHeight(float x,float z)
        {
            float island=-1;
            for(int i=0;i<MapLayout.Islands.Length;i++)island=Mathf.Max(island,MapLayout.IslandDistance(x,z,i));
            return MapLayout.Height(x,z)-.012f-.45f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.6f,island));
        }
        static void CreateBackdrop(List<Vector4> clearings,GeneratedResourceOwner resources)
        {
            // Visual continuation beyond the playable rectangle: no artificial board edges.
            // It has no colliders and therefore cannot expand the gameplay NavMesh.
            var root=new GameObject("Distant continental woodland");
            var vertices=new List<Vector3>();var triangles=new List<int>();
            float reachX=Mathf.Ceil((MapLayout.HalfWidth+BackdropReach)/MapLayout.Spacing/2)*2,reachZ=Mathf.Ceil((MapLayout.HalfDepth+BackdropReach)/MapLayout.Spacing/2)*2;
            for(float bx=-reachX;bx<reachX;bx+=2)for(float bz=-reachZ;bz<100;bz+=2)
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
            var mesh=resources.Track(new Mesh{name="Continental horizon",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32});
            mesh.SetVertices(vertices);BakeCoastWeights(mesh,vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Meadow");
            var random=new System.Random(561);
            // A thin band of the adjacent biome's trees continues the edge woods; farther out
            // the ground has faded enough that trees would only pop against the horizon.
            const float treeBand=22f;
            for(float x=-MapLayout.HalfWidth-treeBand;x<MapLayout.HalfWidth+treeBand;x+=3.1f)for(float z=-MapLayout.HalfDepth-treeBand;z<MapLayout.HalfDepth+treeBand;z+=3.1f)
            {
                if(Mathf.Abs(x)<MapLayout.HalfWidth&&Mathf.Abs(z)<MapLayout.HalfDepth||z>MapLayout.Coast(x)-1.5f||random.NextDouble()<.45)continue;
                float px=x+(float)random.NextDouble()*1.8f,pz=z+(float)random.NextDouble()*1.8f;
                float outside=Mathf.Max(Mathf.Abs(px)-MapLayout.HalfWidth,Mathf.Abs(pz)-MapLayout.HalfDepth);
                if(random.NextDouble()<outside/treeBand)continue;
                // Beyond a desert edge only rare palms continue, never a wall of trees.
                if(FictionalGround.Sample(px,pz).Arid>.5f&&random.NextDouble()>.12)continue;
                var point=new Vector3(px,MapLayout.Height(px,pz),pz);
                float treeHeight=3.1f+(float)random.NextDouble()*1.5f;
                if(ObscuresBuilding(point,treeHeight,clearings))continue;
                int treeSeed=(int)(x*17+z*31)&32767;
                BiomeVegetation.Tree(root.transform,point,treeHeight,treeSeed);
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
