using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI
{
    /// <summary>Chunked terrain rebuilt from numerical W3E geography using our materials.</summary>
    public static class ImportedTerrain
    {
        // Presentation only: keep a continuous bed beneath the transparent
        // shallows. Navigation still comes exclusively from the original WPM.
        public const float VisibleBedDepth=1.8f;
        static readonly Color[] GroundColors={new Color(.69f,.76f,.58f),new Color(.50f,.67f,.35f),new Color(.66f,.63f,.38f),new Color(.56f,.63f,.46f),new Color(.72f,.67f,.42f),new Color(.78f,.83f,.77f),new Color(.50f,.59f,.42f),new Color(.60f,.66f,.53f),new Color(.57f,.66f,.47f)};
        public static void Create(Transform root)
        {
            var data=MapLayout.Imported;
            ShoreAccess.BakeSurface(root);
            TerrainBiomes.Bake(root);
            var resources=GeneratedResourceOwner.For(root);
            Material ground=Resources.Load<Material>("ImportedGround"),water=Resources.Load<Material>("ImportedWater");
            const int chunk=32;
            for(int z=0;z<data.height-1;z+=chunk)for(int x=0;x<data.width-1;x+=chunk)
                CreateChunk(root,resources,data,x,z,Mathf.Min(chunk,data.width-1-x),Mathf.Min(chunk,data.height-1-z),ground);
            if(data.HasSourcePathing)CreateFineGroundNavigation(root,resources,data,ground);
            CreateWaterSurface(root,resources,data,water);
            CreateHorizonSkirt(root,resources,data,ground,water);
            // Old hand-authored fixtures have no WPM grid and retain their explicit
            // causeway. Source-backed maps use their flooded walkable ground instead.
            if(!data.HasSourcePathing)CreatePortPlatforms(root,data);
            CreateVegetation(root,data);
        }
        static void CreateChunk(Transform root,GeneratedResourceOwner resources,ImportedMapData data,int sx,int sz,int nx,int nz,Material ground)
        {
            var vertices=new Vector3[(nx+1)*(nz+1)];var normals=new Vector3[vertices.Length];var colors=new Color[vertices.Length];
            // All three meshes share the bounded coastal vertex deformation;
            // ImportedMapData resolves these same triangles for CPU queries.
            var shoreBand=new Vector3[vertices.Length];
            var triangles=new List<int>(nx*nz*6);var walkable=new List<int>(nx*nz*6);
            // Outside the W3I playable rectangle the visual mesh extrudes the edge
            // terrain. Collision triangles only exist inside it, where both agree.
            var visual=ImportedMapSkirt.For(data);
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int ix=sx+x,iz=sz+z,source=iz*data.width+ix,index=z*(nx+1)+x;
                var point=ImportedMapSkirt.Vertex(data,visual,ix,iz);float wx=point.x,wz=point.y;
                vertices[index]=new Vector3(wx,visual.height[source],wz);
                normals[index]=TerrainNormal(data,visual,ix,iz);
                colors[index]=GroundTint(visual.tile[source],wx,wz);
                colors[index].a=ImportedLandscapeAugment.Enabled&&!ImportedMapSkirt.Outside(data,wx,wz)?ImportedLandscapeAugment.RockSnowWeightAt(data,wx,wz):0;
                var coast=ShoreAccess.SurfaceWeights(wx,wz);
                shoreBand[index]=new Vector3(ShoreAccess.ShoreBandWeight(wx,wz),coast.x,coast.y);
                if(x==nx||z==nz)continue;
                int b=index+nx+1;
                // A water fragment must not alternate between a real bed and an
                // absent bed at WPM/cell edges. Extend the existing visual mesh
                // through shallow water, without adding collision or deep seabed.
                if(HasVisualGroundCell(data,visual,ix,iz))AddQuad(triangles,index,b,index+1,b+1);
                var center=(data.TerrainVertex(ix,iz)+data.TerrainVertex(ix+1,iz)+data.TerrainVertex(ix,iz+1)+data.TerrainVertex(ix+1,iz+1))*.25f;
                if(data.HasSourcePathing?data.TerrainCellUsesCoarseNavigation(ix,iz):data.IsLand(center.x,center.y)&&data.InPlayable(center.x,center.y))
                    AddQuad(walkable,index,b,index+1,b+1);
            }
            var mesh=resources.Track(new Mesh{name="Imported land chunk",vertices=vertices,normals=normals,colors=colors});mesh.SetUVs(1,shoreBand);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            var go=new GameObject("Terrain "+sx+","+sz);go.layer=MapLayout.TerrainLayer;go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=ground;
            if(walkable.Count>0){var collision=resources.Track(new Mesh{name="Imported navigation chunk"});collision.vertices=vertices;collision.SetTriangles(walkable,0);collision.RecalculateBounds();go.AddComponent<MeshCollider>().sharedMesh=collision;}
        }

        /// <summary>
        /// Visual-only ring beyond the W3E grid. Each vertex extrudes the nearest
        /// playable edge sample; the shaders fade it into the horizon colour. No colliders.
        /// </summary>
        static void CreateHorizonSkirt(Transform root,GeneratedResourceOwner resources,ImportedMapData data,Material ground,Material water)
        {
            float step=data.cellSize;
            float gridMaxX=data.originX+(data.width-1)*step,gridMaxZ=data.originZ+(data.height-1)*step;
            // Snap the ring to the W3E lattice so its inner edge shares the grid border vertices.
            int left=Mathf.Max(0,Mathf.CeilToInt((data.originX-(data.PlayableMinX-ImportedMapSkirt.Reach))/step));
            int right=Mathf.Max(0,Mathf.CeilToInt((data.PlayableMaxX+ImportedMapSkirt.Reach-gridMaxX)/step));
            int back=Mathf.Max(0,Mathf.CeilToInt((data.originZ-(data.PlayableMinZ-ImportedMapSkirt.Reach))/step));
            int front=Mathf.Max(0,Mathf.CeilToInt((data.PlayableMaxZ+ImportedMapSkirt.Reach-gridMaxZ)/step));
            int minX=-left,maxX=data.width-1+right,minZ=-back,maxZ=data.height-1+front;
            var started=System.Diagnostics.Stopwatch.StartNew();
            var skirt=new GameObject("Imported horizon continuation · visual only");skirt.transform.SetParent(root,false);
            int chunks=0;const int chunk=48;int w=data.width-1,h=data.height-1;
            // Four strips around the grid: west and east span the full height.
            var strips=new[]{new RectInt(minX,minZ,left,maxZ-minZ),new RectInt(w,minZ,right,maxZ-minZ),new RectInt(0,minZ,w,back),new RectInt(0,h,w,front)};
            foreach(var strip in strips)
            {
                if(strip.width<=0||strip.height<=0)continue;
                for(int z0=strip.yMin;z0<strip.yMax;z0+=chunk)for(int x0=strip.xMin;x0<strip.xMax;x0+=chunk)
                    if(CreateSkirtChunk(skirt.transform,resources,data,x0,z0,Mathf.Min(strip.xMax,x0+chunk),Mathf.Min(strip.yMax,z0+chunk),ground,water))chunks++;
            }
            skirt.name+=" · "+chunks+" chunks";
            Debug.Log($"RISKAI_TERRAIN_SKIRT map={data.mapId} chunks={chunks} ms={started.Elapsed.TotalMilliseconds:F1}");
        }
        static bool CreateSkirtChunk(Transform root,GeneratedResourceOwner resources,ImportedMapData data,int x0,int z0,int x1,int z1,Material ground,Material water)
        {
            int nx=x1-x0,nz=z1-z0;float step=data.cellSize;
            var vertices=new Vector3[(nx+1)*(nz+1)];var normals=new Vector3[vertices.Length];var colors=new Color[vertices.Length];
            var shoreBand=new Vector3[vertices.Length];var surface=new Vector3[vertices.Length];var depth=new Color[vertices.Length];
            var land=new bool[vertices.Length];var bed=new bool[vertices.Length];
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int index=z*(nx+1)+x,gx=x0+x,gz=z0+z;float wx=data.originX+gx*step,wz=data.originZ+gz*step;
                // W3I bounds lie on the W3E lattice: every ring vertex extrudes the low-passed edge.
                int s=ImportedMapSkirt.SourceIndex(data,gx,gz);var e=ImportedMapSkirt.Extrude(data,gx,gz);
                float h=e.height,w=e.water;
                vertices[index]=new Vector3(wx,h,wz);
                normals[index]=new Vector3(ImportedMapSkirt.Extrude(data,gx-1,gz).height-ImportedMapSkirt.Extrude(data,gx+1,gz).height,2*step,
                    ImportedMapSkirt.Extrude(data,gx,gz-1).height-ImportedMapSkirt.Extrude(data,gx,gz+1).height).normalized;
                // Augmented ridges are inland; the ring never carries rock/snow ridge weight.
                colors[index]=GroundTint(data.tileSamples[s],wx,wz);colors[index].a=0;
                shoreBand[index]=ShoreAccess.ImportedSampleWeights(s);
                land[index]=e.land;bed[index]=land[index]||w-h<=VisibleBedDepth;
                surface[index]=new Vector3(wx,w,wz);depth[index]=new Color(1,1,1,Mathf.Clamp01((w-h)/3f));
            }
            var groundTriangles=new List<int>();var waterTriangles=new List<int>();
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                int a=z*(nx+1)+x,b=a+nx+1;
                if(bed[a]||bed[a+1]||bed[b]||bed[b+1])AddQuad(groundTriangles,a,b,a+1,b+1);
                if(!(land[a]&&land[a+1]&&land[b]&&land[b+1]))AddQuad(waterTriangles,a,b,a+1,b+1);
            }
            if(groundTriangles.Count==0&&waterTriangles.Count==0)return false;
            var go=new GameObject("Horizon "+x0+","+z0);go.transform.SetParent(root,false);
            if(groundTriangles.Count>0)
            {
                var mesh=resources.Track(new Mesh{name="Imported horizon ground",vertices=vertices,normals=normals,colors=colors});
                mesh.SetUVs(1,shoreBand);mesh.SetTriangles(groundTriangles,0);mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=ground;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            if(waterTriangles.Count>0)
            {
                var sea=resources.Track(new Mesh{name="Imported horizon water",vertices=surface,colors=depth});
                sea.SetTriangles(waterTriangles,0);sea.RecalculateNormals();sea.RecalculateBounds();
                var surfaceObject=new GameObject("Horizon water");surfaceObject.transform.SetParent(go.transform,false);
                surfaceObject.AddComponent<MeshFilter>().sharedMesh=sea;var renderer=surfaceObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=water;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            return true;
        }

        static void CreateWaterSurface(Transform root,GeneratedResourceOwner resources,ImportedMapData data,Material water)
        {
            int width=data.width,height=data.height;
            var vertices=new Vector3[width*height];var colors=new Color[vertices.Length];
            var triangles=new List<int>((width-1)*(height-1)*6);
            var visual=ImportedMapSkirt.For(data);
            for(int z=0;z<height;z++)for(int x=0;x<width;x++)
            {
                int index=z*width+x;var point=ImportedMapSkirt.Vertex(data,visual,x,z);
                vertices[index]=new Vector3(point.x,visual.water[index],point.y);
                colors[index]=new Color(1,1,1,Mathf.Clamp01((visual.water[index]-visual.height[index])/3f));
                if(x==width-1||z==height-1)continue;
                if(visual.land[index]+visual.land[index+1]+visual.land[index+width]+visual.land[index+width+1]<4)
                    AddQuad(triangles,index,index+width,index+1,index+width+1);
            }
            var sea=resources.Track(new Mesh{name="Imported water surface",indexFormat=IndexFormat.UInt32,vertices=vertices,colors=colors});
            sea.SetTriangles(triangles,0);sea.RecalculateNormals();sea.RecalculateBounds();
            var surface=new GameObject("Continuous imported water");surface.transform.SetParent(root,false);
            surface.AddComponent<MeshFilter>().sharedMesh=sea;
            var renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterial=water;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        static void CreateFineGroundNavigation(Transform root,GeneratedResourceOwner resources,ImportedMapData data,Material ground)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var shoreBand=new List<Vector3>();
            var collisionTriangles=new List<int>();var visualTriangles=new List<int>();int cells=0;
            for(int z=0;z<data.pathingHeight;z++)for(int x=0;x<data.pathingWidth;x++)
            {
                if(!data.PathingCellNeedsFineGround(x,z))continue;
                int first=vertices.Count;
                AddShallowVertex(data,x,z,vertices,normals,colors,shoreBand);
                AddShallowVertex(data,x,z+1,vertices,normals,colors,shoreBand);
                AddShallowVertex(data,x+1,z,vertices,normals,colors,shoreBand);
                AddShallowVertex(data,x+1,z+1,vertices,normals,colors,shoreBand);
                AddQuad(collisionTriangles,first,first+1,first+2,first+3);
                var center=data.PathingCellCenter(x,z);
                if(data.IsSharedPathingCell(x,z)&&!HasVisualGroundAt(data,center.x,center.y))
                    AddQuad(visualTriangles,first,first+1,first+2,first+3);
                cells++;
            }
            if(cells==0)return;
            var values=vertices.ToArray();
            var collision=resources.Track(new Mesh{name="Imported source fine navigation",indexFormat=IndexFormat.UInt32,vertices=values});
            collision.SetTriangles(collisionTriangles,0);collision.RecalculateBounds();
            var shallow=new GameObject("Imported fine navigation · "+cells);shallow.layer=MapLayout.TerrainLayer;shallow.transform.SetParent(root,false);
            shallow.AddComponent<MeshCollider>().sharedMesh=collision;
            if(visualTriangles.Count==0)return;
            var visible=resources.Track(new Mesh{name="Imported shared shallow ground",indexFormat=IndexFormat.UInt32,vertices=values,normals=normals.ToArray(),colors=colors.ToArray()});
            visible.SetUVs(1,shoreBand);visible.SetTriangles(visualTriangles,0);visible.RecalculateBounds();
            shallow.AddComponent<MeshFilter>().sharedMesh=visible;
            var renderer=shallow.AddComponent<MeshRenderer>();renderer.sharedMaterial=ground;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        static void AddShallowVertex(ImportedMapData data,int pathX,int pathZ,List<Vector3> vertices,List<Vector3> normals,List<Color> colors,List<Vector3> shoreBand)
        {
            Vector2 point=data.PathingVertex(pathX,pathZ);float step=data.pathingCellSize;
            float left=data.HeightAt(point.x-step,point.y),right=data.HeightAt(point.x+step,point.y);
            float back=data.HeightAt(point.x,point.y-step),forward=data.HeightAt(point.x,point.y+step);
            vertices.Add(new Vector3(point.x,data.HeightAt(point.x,point.y),point.y));
            normals.Add(new Vector3(left-right,2*step,back-forward).normalized);
            colors.Add(GroundTint(data.TileAt(point.x,point.y),point.x,point.y));
            var coast=ShoreAccess.SurfaceWeights(point.x,point.y);
            shoreBand.Add(new Vector3(ShoreAccess.ShoreBandWeight(point.x,point.y),coast.x,coast.y));
        }
        static void AddQuad(List<int> target,int a,int b,int c,int d){target.Add(a);target.Add(b);target.Add(c);target.Add(c);target.Add(b);target.Add(d);}
        public static bool HasVisualGroundCell(ImportedMapData data,int x,int z)
        {
            if(data==null)return false;
            return HasVisualGroundCell(data.width,data.height,data.landSamples,data.waterSamples,data.heightSamples,x,z);
        }
        static bool HasVisualGroundCell(ImportedMapData data,ImportedMapSkirt.Samples visual,int x,int z)=>
            HasVisualGroundCell(data.width,data.height,visual.land,visual.water,visual.height,x,z);
        static bool HasVisualGroundCell(int width,int height,int[] land,float[] water,float[] ground,int x,int z)
        {
            if(x<0||z<0||x>=width-1||z>=height-1)return false;
            int k=z*width+x;
            if(land[k]+land[k+1]+land[k+width]+land[k+width+1]>0)return true;
            return water[k]-ground[k]<=VisibleBedDepth||
                water[k+1]-ground[k+1]<=VisibleBedDepth||
                water[k+width]-ground[k+width]<=VisibleBedDepth||
                water[k+width+1]-ground[k+width+1]<=VisibleBedDepth;
        }
        static bool HasVisualGroundAt(ImportedMapData data,float x,float z)
        {
            Vector2 source=data.SourcePositionAt(x,z);
            int ix=Mathf.Clamp(Mathf.FloorToInt((source.x-data.originX)/data.cellSize),0,data.width-2);
            int iz=Mathf.Clamp(Mathf.FloorToInt((source.y-data.originZ)/data.cellSize),0,data.height-2);
            return HasVisualGroundCell(data,ix,iz);
        }
        static Vector3 TerrainNormal(ImportedMapData data,ImportedMapSkirt.Samples visual,int x,int z)
        {
            int left=Mathf.Max(0,x-1),right=Mathf.Min(data.width-1,x+1),back=Mathf.Max(0,z-1),forward=Mathf.Min(data.height-1,z+1);
            var lp=ImportedMapSkirt.Vertex(data,visual,left,z);var rp=ImportedMapSkirt.Vertex(data,visual,right,z);
            var bp=ImportedMapSkirt.Vertex(data,visual,x,back);var fp=ImportedMapSkirt.Vertex(data,visual,x,forward);
            var h=visual.height;
            var across=new Vector3(rp.x-lp.x,h[z*data.width+right]-h[z*data.width+left],rp.y-lp.y);
            var along=new Vector3(fp.x-bp.x,h[forward*data.width+x]-h[back*data.width+x],fp.y-bp.y);
            var normal=Vector3.Cross(along,across);return normal.sqrMagnitude>.000001f?normal.normalized:Vector3.up;
        }
        static Color GroundTint(int tile,float x,float z)
        {
            Color tint=GroundColors[Mathf.Min(ImportedMapData.GroundTileIndex(tile),GroundColors.Length-1)];
            float patch=Mathf.PerlinNoise(x*.022f+51,z*.022f+19);
            return tint*Mathf.Lerp(.88f,1.1f,patch);
        }
        static void CreateVegetation(Transform root,ImportedMapData data)
        {
            // Every candidate is a static DOO destructible exported at its authored
            // Warcraft coordinate.  The source X/Y ground axes map to Unity X/Z;
            // source Z is vertical and is preserved as scaleZ in the export.
            if(data.sourceTrees==null||data.sourceTrees.Length==0)return;
            var trees=new GameObject("Source tree destructibles");trees.transform.SetParent(root,false);
            int placed=0,dead=0,thinned=0,horizon=0;
            var bounds=ImportedMapSkirt.Bounds(data);bool skirt=bounds.z>bounds.x;
            for(int i=0;i<data.sourceTrees.Length;i++)
            {
                var tree=data.sourceTrees[i];
                if(tree.lifePercent<=0){dead++;continue;}
                // The flat source border is replaced by extruded edge terrain; its trees
                // give way to a thin band of copies of the trees along the playable edge.
                if(skirt&&ImportedMapSkirt.Outside(data,tree.x,tree.z))continue;
                float horizontalX=Mathf.Max(.08f,tree.scaleX),horizontalZ=Mathf.Max(.08f,tree.scaleY);
                float crownRadius=1.7f*Mathf.Max(horizontalX,horizontalZ);
                if(!data.IsLand(tree.x,tree.z)||!ClearOfPosts(tree.x,tree.z,crownRadius,data))continue;
                string sourceType=SourceTreeType(data,tree.species);
                int seed=tree.sourceRecord+tree.variation;
                if(!BiomeTree(data,TreeForm(sourceType),tree.x,tree.z,seed,out var form,out float heightScale)){thinned++;continue;}
                float height=4.2f*Mathf.Max(.08f,tree.scaleZ)*heightScale;
                if(PlaceTree(trees.transform,MapLayout.Point(tree.x,tree.z),height,seed,form,-tree.rotationDegrees,horizontalX,horizontalZ,
                    "Source "+sourceType+" · f"+tree.pathingFlags+" · "+tree.lifePercent+"% · #"+tree.sourceRecord))placed++;
                if(!skirt)continue;
                // Visual-only copies continue edge woods a few metres past the playable edge.
                const float band=7f;
                for(int axis=1;axis<4;axis++)
                {
                    float mx=tree.x,mz=tree.z;
                    if((axis&1)!=0){if(tree.x-bounds.x<band)mx=2*bounds.x-tree.x;else if(bounds.z-tree.x<band)mx=2*bounds.z-tree.x;else continue;}
                    if((axis&2)!=0){if(tree.z-bounds.y<band)mz=2*bounds.y-tree.z;else if(bounds.w-tree.z<band)mz=2*bounds.w-tree.z;else continue;}
                    var edge=ImportedMapSkirt.Clamp(data,mx,mz);
                    if(!data.IsLand(edge.x,edge.y))continue;
                    if(PlaceTree(trees.transform,new Vector3(mx,data.HeightAt(edge.x,edge.y),mz),height*.9f,seed+axis,form,tree.rotationDegrees,horizontalX,horizontalZ,"Horizon edge tree"))horizon++;
                }
            }
            trees.name="Source tree destructibles · "+placed+" visible · "+dead+" destroyed · "+thinned+" biome-thinned · "+horizon+" horizon";
            if(placed+horizon>0)GeneratedResourceOwner.CombineStaticBatches(trees.transform);
        }
        static bool PlaceTree(Transform trees,Vector3 point,float height,int seed,BiomeVegetation.ImportedTreeForm form,float yaw,float horizontalX,float horizontalZ,string label)
        {
            int before=trees.childCount;
            BiomeVegetation.ImportedTree(trees,point,height,seed,form);
            if(trees.childCount==before)return false;
            var instance=trees.GetChild(trees.childCount-1);
            instance.name=label;
            // x,y,z -> x,z,y reflects handedness, so authored yaw is negated.
            instance.localRotation=Quaternion.Euler(0,yaw,0);
            instance.localScale=new Vector3(horizontalX,1,horizontalZ);
            return true;
        }
        /// <summary>
        /// Adapts a source destructible to its geographic biome. Source trees never
        /// block WPM pathing, so thinning desert and ice trees is presentation only.
        /// </summary>
        static bool BiomeTree(ImportedMapData data,BiomeVegetation.ImportedTreeForm source,float x,float z,int seed,out BiomeVegetation.ImportedTreeForm form,out float heightScale)
        {
            form=source;heightScale=1;
            var biome=TerrainBiomes.Sample(x,z);
            float roll=(((uint)seed*2654435761u)>>8&1023)/1023f;
            if(biome.Cold>.9f)return false;
            if(biome.Arid>.82f)
            {
                // Sahara and Arabia: rare palm oases.
                if(roll>.16f)return false;
                form=BiomeVegetation.ImportedTreeForm.Palm;heightScale=.85f;return true;
            }
            if(biome.Cold>.72f)
            {
                if(roll>.45f)return false;
                form=BiomeVegetation.ImportedTreeForm.Fir;heightScale=.72f;return true;
            }
            if(biome.Arid>.45f)
            {
                // Mediterranean and semi-arid scrub: sparser olive and holm-oak crowns.
                if(roll>Mathf.Lerp(.85f,.45f,Mathf.InverseLerp(.45f,.82f,biome.Arid)))return false;
                form=roll<.08f&&biome.Arid>.6f?BiomeVegetation.ImportedTreeForm.Palm:BiomeVegetation.ImportedTreeForm.DryOak;
                heightScale=.86f;return true;
            }
            if(biome.Arid>.26f&&biome.Cold<.3f)
            {
                // Steppe: open grassland with scattered, drier groves.
                if(roll>.62f)return false;
                if(source!=BiomeVegetation.ImportedTreeForm.Fir&&roll<.35f)form=BiomeVegetation.ImportedTreeForm.DryOak;
                return true;
            }
            if(biome.Cold>.36f)
            {
                // Boreal taiga: mostly conifers.
                if(roll<.85f)form=BiomeVegetation.ImportedTreeForm.Fir;
                return true;
            }
            if(biome.Lush>.82f&&biome.Cold<.08f)
            {
                // Caribbean and Central American lowlands.
                if(roll<.5f)form=BiomeVegetation.ImportedTreeForm.Palm;
                return true;
            }
            return true;
        }
        static string SourceTreeType(ImportedMapData data,int species)
        {
            if(data.treeSpecies==null||species<0||species>=data.treeSpecies.Length)return "unknown";
            return data.treeSpecies[species];
        }
        static BiomeVegetation.ImportedTreeForm TreeForm(string sourceType)
        {
            int separator=sourceType.IndexOf(':');string code=separator>=0?sourceType.Substring(separator+1):sourceType;
            if(code=="WTst")return BiomeVegetation.ImportedTreeForm.Fir;
            if(code=="BTtc"||code=="BTtw")return BiomeVegetation.ImportedTreeForm.DryOak;
            return BiomeVegetation.ImportedTreeForm.Oak;
        }
        static bool ClearOfPosts(float x,float z,float crownRadius,ImportedMapData data)
        {
            foreach(var c in data.cities)
                if((new Vector2(x-c.x,z-c.z)).sqrMagnitude<Mathf.Max(110,(crownRadius+6)*(crownRadius+6))||
                   (new Vector2(x-c.claimX,z-c.claimZ)).sqrMagnitude<Mathf.Max(64,(crownRadius+5)*(crownRadius+5)))return false;
            return true;
        }
        static void CreatePortPlatforms(Transform root,ImportedMapData data)
        {
            foreach(var c in data.cities)
            {
                if(!c.port)continue;
                Vector3 city=new Vector3(c.x,.55f,c.z),claim=new Vector3(c.claimX,.55f,c.claimZ);
                var layout=ImportedPortLayout.Resolve(city,claim);
                string label=layout.Shape==ImportedPortLayout.PierShape.CliffRamp?"Cliff harbor ramp":"Harbor causeway";
                // One direct deck is shorter, clearer and cannot create a blocked
                // corner where two independently baked NavMesh strips overlap.
                Platform(root,layout.Shore,layout.Claim,ImportedPortLayout.WalkwayWidth,label);
            }
        }
        static void Platform(Transform root,Vector3 from,Vector3 to,float width,string label)
        {
            NavalArt.CreatePierDeck(root,from,to,width,label,true,true);
        }
    }
}
