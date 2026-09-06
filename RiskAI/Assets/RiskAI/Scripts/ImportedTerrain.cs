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
            var vertices=new Vector3[(nx+1)*(nz+1)];var colors=new Color[vertices.Length];
            // This is a static material-only shore band.  It preserves the W3E
            // mesh, collision and land flags while avoiding a hard tile tint at
            // a one-cell water boundary.
            var shoreBand=new Vector2[vertices.Length];
            var triangles=new List<int>(nx*nz*6);var walkable=new List<int>(nx*nz*6);
            var seaVertices=new List<Vector3>();var seaColors=new List<Color>();var seaTriangles=new List<int>();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int ix=sx+x,iz=sz+z,source=iz*data.width+ix,index=z*(nx+1)+x;
                float wx=data.originX+ix*data.cellSize,wz=data.originZ+iz*data.cellSize;
                vertices[index]=new Vector3(wx,data.heightSamples[source],wz);
                colors[index]=GroundTint(data.tileSamples[source],wx,wz);
                colors[index].a=ImportedLandscapeAugment.Enabled?ImportedLandscapeAugment.RockSnowWeightAt(data,wx,wz):0;
                shoreBand[index]=new Vector2(ShoreBand(data,ix,iz),0);
                if(x==nx||z==nz)continue;
                int b=index+nx+1;
                AddQuad(triangles,index,b,index+1,b+1);
                if(data.IsLand(wx+data.cellSize*.5f,wz+data.cellSize*.5f))AddQuad(walkable,index,b,index+1,b+1);
                if(data.landSamples[source]+data.landSamples[source+1]+data.landSamples[source+data.width]+data.landSamples[source+data.width+1]<4)
                {
                    int n=seaVertices.Count;
                    for(int corner=0;corner<4;corner++)
                    {
                        int offset=(corner%2)*data.width+corner/2;
                        int k=source+offset,cx=k%data.width,cz=k/data.width;
                        seaVertices.Add(new Vector3(data.originX+cx*data.cellSize,data.waterSamples[k],data.originZ+cz*data.cellSize));
                        seaColors.Add(new Color(1,1,1,Mathf.Clamp01((data.waterSamples[k]-data.heightSamples[k])/3f)));
                    }
                    AddQuad(seaTriangles,n,n+1,n+2,n+3);
                }
            }
            var mesh=new Mesh{name="Imported land chunk"};mesh.vertices=vertices;mesh.colors=colors;mesh.SetUVs(1,shoreBand);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Meshes.Add(mesh);
            var go=new GameObject("Terrain "+sx+","+sz);go.layer=MapLayout.TerrainLayer;go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=ground;
            if(walkable.Count>0){var collision=new Mesh{name="Imported navigation chunk"};collision.vertices=vertices;collision.SetTriangles(walkable,0);collision.RecalculateBounds();resources.Meshes.Add(collision);go.AddComponent<MeshCollider>().sharedMesh=collision;}
            if(seaTriangles.Count==0)return;
            var sea=new Mesh{name="Imported water chunk"};sea.SetVertices(seaVertices);sea.SetColors(seaColors);sea.SetTriangles(seaTriangles,0);sea.RecalculateNormals();sea.RecalculateBounds();resources.Meshes.Add(sea);
            var surface=new GameObject("Water "+sx+","+sz);surface.transform.SetParent(root,false);surface.AddComponent<MeshFilter>().sharedMesh=sea;
            var renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterial=water;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        static void AddQuad(List<int> target,int a,int b,int c,int d){target.Add(a);target.Add(b);target.Add(c);target.Add(c);target.Add(b);target.Add(d);}
        static float ShoreBand(ImportedMapData data,int x,int z)
        {
            int index=z*data.width+x;
            if(data.landSamples[index]==0)return 0;
            // A direct (including diagonal) water neighbour gives one source
            // cell of blended bank.  It does not infer or move a coastline.
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                if(dx==0&&dz==0)continue;
                int nx=x+dx,nz=z+dz;
                if(nx>=0&&nx<data.width&&nz>=0&&nz<data.height&&data.landSamples[nz*data.width+nx]==0)return 1;
            }
            return 0;
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
                Platform(root,city,city+Vector3.forward*.01f,9,"Shipyard footing");
                Platform(root,city,claim,4.5f,"Guard pier");
                Vector3 shore=default;float nearest=float.MaxValue;
                for(float dz=-26;dz<=26;dz+=data.cellSize)for(float dx=-26;dx<=26;dx+=data.cellSize)
                {
                    float x=c.x+dx,z=c.z+dz;if(!data.IsLand(x,z))continue;
                    float distance=dx*dx+dz*dz;
                    if(distance<nearest){nearest=distance;shore=MapLayout.Point(x,z)+Vector3.up*.08f;}
                }
                if(nearest<float.MaxValue)Platform(root,city,shore,3.4f,"Shore gangway");
                else Debug.LogError("RISKAI_PORT_SHORE_MISSING: "+c.id);
            }
        }
        static void Platform(Transform root,Vector3 from,Vector3 to,float width,string label)
        {
            var middle=(from+to)*.5f;var direction=to-from;
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=label;go.layer=MapLayout.TerrainLayer;go.transform.SetParent(root,false);
            go.transform.position=middle-Vector3.up*.16f;
            go.transform.rotation=direction.sqrMagnitude>.02f?Quaternion.LookRotation(direction):Quaternion.identity;
            go.transform.localScale=new Vector3(width,.32f,Mathf.Max(width,(to-from).magnitude+2));
            go.GetComponent<Renderer>().sharedMaterial=WorldArt.Painted(2,new Color(.92f,.83f,.69f),.8f);
        }
    }
    public sealed class ImportedTerrainResources:MonoBehaviour
    {
        public readonly List<Mesh> Meshes=new List<Mesh>();
        void OnDestroy(){foreach(var mesh in Meshes)if(mesh)Destroy(mesh);}
    }
}
