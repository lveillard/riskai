using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI
{
    /// <summary>Chunked terrain rebuilt from numerical W3E geography using our materials.</summary>
    public static class ImportedTerrain
    {
        static readonly Color[] GroundColors={new Color(.69f,.76f,.58f),new Color(.50f,.67f,.35f),new Color(.66f,.63f,.38f),new Color(.56f,.63f,.46f),new Color(.72f,.67f,.42f),new Color(.78f,.83f,.77f),new Color(.50f,.59f,.42f),new Color(.60f,.66f,.53f),new Color(.57f,.66f,.47f)};
        public static void Create(Transform root)
        {
            var data=MapLayout.Imported;
            ShoreAccess.BakeSurface(root);
            var resources=root.gameObject.AddComponent<ImportedTerrainResources>();
            Material ground=Resources.Load<Material>("ImportedGround"),water=Resources.Load<Material>("ImportedWater");
            const int chunk=32;
            for(int z=0;z<data.height-1;z+=chunk)for(int x=0;x<data.width-1;x+=chunk)
                CreateChunk(root,resources,data,x,z,Mathf.Min(chunk,data.width-1-x),Mathf.Min(chunk,data.height-1-z),ground,water);
            CreatePortPlatforms(root,data);
            CreateVegetation(root,data);
        }
        static void CreateChunk(Transform root,ImportedTerrainResources resources,ImportedMapData data,int sx,int sz,int nx,int nz,Material ground,Material water)
        {
            var vertices=new Vector3[(nx+1)*(nz+1)];var normals=new Vector3[vertices.Length];var colors=new Color[vertices.Length];
            // All three meshes share the bounded coastal vertex deformation;
            // ImportedMapData resolves these same triangles for CPU queries.
            var shoreBand=new Vector3[vertices.Length];
            var triangles=new List<int>(nx*nz*6);var walkable=new List<int>(nx*nz*6);
            // Water uses one vertex per source grid point inside a chunk. Sharing the
            // diagonal and cardinal edges keeps every interpolant identical across
            // adjacent source tiles instead of submitting four private vertices each.
            var seaVertices=new Vector3[vertices.Length];var seaColors=new Color[vertices.Length];var seaTriangles=new List<int>();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int ix=sx+x,iz=sz+z,source=iz*data.width+ix,index=z*(nx+1)+x;
                var point=data.TerrainVertex(ix,iz);float wx=point.x,wz=point.y;
                vertices[index]=new Vector3(wx,data.heightSamples[source],wz);
                normals[index]=TerrainNormal(data,ix,iz);
                colors[index]=GroundTint(data.tileSamples[source],wx,wz);
                colors[index].a=ImportedLandscapeAugment.Enabled?ImportedLandscapeAugment.RockSnowWeightAt(data,wx,wz):0;
                seaVertices[index]=new Vector3(wx,data.waterSamples[source],wz);
                seaColors[index]=new Color(1,1,1,Mathf.Clamp01((data.waterSamples[source]-data.heightSamples[source])/3f));
                var coast=ShoreAccess.SurfaceWeights(wx,wz);
                shoreBand[index]=new Vector3(ShoreAccess.ShoreBandWeight(wx,wz),coast.x,coast.y);
                if(x==nx||z==nz)continue;
                int b=index+nx+1;
                AddQuad(triangles,index,b,index+1,b+1);
                var center=(point+data.TerrainVertex(ix+1,iz)+data.TerrainVertex(ix,iz+1)+data.TerrainVertex(ix+1,iz+1))*.25f;
                if(data.IsLand(center.x,center.y))AddQuad(walkable,index,b,index+1,b+1);
                if(data.landSamples[source]+data.landSamples[source+1]+data.landSamples[source+data.width]+data.landSamples[source+data.width+1]<4)
                    AddQuad(seaTriangles,index,b,index+1,b+1);
            }
            var mesh=new Mesh{name="Imported land chunk",vertices=vertices,normals=normals,colors=colors};mesh.SetUVs(1,shoreBand);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();resources.Meshes.Add(mesh);
            var go=new GameObject("Terrain "+sx+","+sz);go.layer=MapLayout.TerrainLayer;go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=ground;
            if(walkable.Count>0){var collision=new Mesh{name="Imported navigation chunk"};collision.vertices=vertices;collision.SetTriangles(walkable,0);collision.RecalculateBounds();resources.Meshes.Add(collision);go.AddComponent<MeshCollider>().sharedMesh=collision;}
            if(seaTriangles.Count==0)return;
            var sea=new Mesh{name="Imported water chunk",vertices=seaVertices,colors=seaColors};sea.SetTriangles(seaTriangles,0);sea.RecalculateNormals();sea.RecalculateBounds();resources.Meshes.Add(sea);
            var surface=new GameObject("Water "+sx+","+sz);surface.transform.SetParent(root,false);surface.AddComponent<MeshFilter>().sharedMesh=sea;
            var renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterial=water;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        static void AddQuad(List<int> target,int a,int b,int c,int d){target.Add(a);target.Add(b);target.Add(c);target.Add(c);target.Add(b);target.Add(d);}
        static Vector3 TerrainNormal(ImportedMapData data,int x,int z)
        {
            int left=Mathf.Max(0,x-1),right=Mathf.Min(data.width-1,x+1),back=Mathf.Max(0,z-1),forward=Mathf.Min(data.height-1,z+1);
            var lp=data.TerrainVertex(left,z);var rp=data.TerrainVertex(right,z);var bp=data.TerrainVertex(x,back);var fp=data.TerrainVertex(x,forward);
            var across=new Vector3(rp.x-lp.x,data.heightSamples[z*data.width+right]-data.heightSamples[z*data.width+left],rp.y-lp.y);
            var along=new Vector3(fp.x-bp.x,data.heightSamples[forward*data.width+x]-data.heightSamples[back*data.width+x],fp.y-bp.y);
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
            int placed=0,dead=0;
            for(int i=0;i<data.sourceTrees.Length;i++)
            {
                var tree=data.sourceTrees[i];
                if(tree.lifePercent<=0){dead++;continue;}
                float horizontalX=Mathf.Max(.08f,tree.scaleX),horizontalZ=Mathf.Max(.08f,tree.scaleY);
                float crownRadius=1.7f*Mathf.Max(horizontalX,horizontalZ);
                if(!data.IsLand(tree.x,tree.z)||!ClearOfPosts(tree.x,tree.z,crownRadius,data))continue;
                string sourceType=SourceTreeType(data,tree.species);
                int before=trees.transform.childCount;
                BiomeVegetation.ImportedTree(trees.transform,MapLayout.Point(tree.x,tree.z),
                    4.2f*Mathf.Max(.08f,tree.scaleZ),tree.sourceRecord+tree.variation,
                    TreeForm(sourceType));
                if(trees.transform.childCount>before)
                {
                    var instance=trees.transform.GetChild(trees.transform.childCount-1);
                    instance.name="Source "+sourceType+" · f"+tree.pathingFlags+" · "+tree.lifePercent+"% · #"+tree.sourceRecord;
                    // x,y,z -> x,z,y reflects handedness, so authored yaw is negated.
                    instance.localRotation=Quaternion.Euler(0,-tree.rotationDegrees,0);
                    instance.localScale=new Vector3(horizontalX,1,horizontalZ);
                    placed++;
                }
            }
            trees.name="Source tree destructibles · "+placed+" visible · "+dead+" destroyed";
            if(placed>0)StaticBatchingUtility.Combine(trees);
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
                // Direct posts need one clean quay. Elbow posts retain the exact
                // h00O coordinate as their bend, matching the source placement.
                if(layout.Shape==ImportedPortLayout.PierShape.Direct)
                    Platform(root,layout.Shore,layout.Claim,ImportedPortLayout.WalkwayWidth,label);
                else
                {
                    Platform(root,layout.Shore,layout.City,ImportedPortLayout.WalkwayWidth,label);
                    Platform(root,layout.City,layout.Claim,ImportedPortLayout.WalkwayWidth,"Circle platform");
                }
            }
        }
        static void Platform(Transform root,Vector3 from,Vector3 to,float width,string label)
        {
            NavalArt.CreatePierDeck(root,from,to,width,label,true,true,label=="Circle platform"?.02f:0);
        }
    }
    public sealed class ImportedTerrainResources:MonoBehaviour
    {
        public readonly List<Mesh> Meshes=new List<Mesh>();
        void OnDestroy(){foreach(var mesh in Meshes)if(mesh)Destroy(mesh);}
    }
}
