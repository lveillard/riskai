using System.Runtime.CompilerServices;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Presentation continuation beyond the W3I playable rectangle. The source
    /// boundary is a flat green placeholder; art instead extrudes the playable
    /// edge outward (sea stays sea, land stays land), low-passed along the edge
    /// as distance grows, and fades into the horizon. Nothing outside
    /// the rectangle is walkable or navigable (ImportedMapData.InPlayable).
    /// </summary>
    public static class ImportedMapSkirt
    {
        /// <summary>Review/setup code may disable the continuation to reproduce the legacy border.</summary>
        public static bool Enabled { get; set; } = true;
        /// <summary>Outer visual ring beyond the playable rectangle, in metres.</summary>
        public const float Reach = 185f;

        public sealed class Samples
        {
            public float[] height, water;
            public int[] land, tile;
            // Grid index each presentation sample copies from (null when identical to the source).
            public int[] source;
        }
        static readonly ConditionalWeakTable<ImportedMapData,Samples> cache=new ConditionalWeakTable<ImportedMapData,Samples>();

        /// <summary>Playable rectangle used for the extrusion, or zero when disabled.</summary>
        public static Vector4 Bounds(ImportedMapData data)=>Enabled&&data!=null?data.PlayableBounds:Vector4.zero;

        /// <summary>Nearest point of the playable rectangle. Must match RiskClampPlayable in CoastSurface.hlsl.</summary>
        public static Vector2 Clamp(Vector4 bounds,float x,float z)
        {
            if(bounds.z<=bounds.x||bounds.w<=bounds.y)return new Vector2(x,z);
            return new Vector2(Mathf.Clamp(x,bounds.x,bounds.z),Mathf.Clamp(z,bounds.y,bounds.w));
        }
        public static Vector2 Clamp(ImportedMapData data,float x,float z)=>Clamp(Bounds(data),x,z);
        public static bool Outside(ImportedMapData data,float x,float z,float margin=0)
        {
            var b=Bounds(data);if(b.z<=b.x)return false;
            return x<b.x-margin||x>b.z+margin||z<b.y-margin||z>b.w+margin;
        }

        /// <summary>Visual sample arrays: the source inside the playable rectangle, extruded edge terrain outside it.</summary>
        public static Samples For(ImportedMapData data)
        {
            if(cache.TryGetValue(data,out var found))return found;
            var samples=new Samples{height=data.heightSamples,water=data.waterSamples,land=data.landSamples,tile=data.tileSamples};
            int size=data.width*data.height;var source=new int[size];bool changed=false;
            for(int z=0;z<data.height;z++)for(int x=0;x<data.width;x++)
            {
                int i=z*data.width+x;source[i]=SourceIndex(data,x,z);changed|=source[i]!=i;
            }
            if(changed)
            {
                samples.source=source;
                samples.height=(float[])data.heightSamples.Clone();samples.water=(float[])data.waterSamples.Clone();
                samples.land=(int[])data.landSamples.Clone();samples.tile=(int[])data.tileSamples.Clone();
                for(int z=0;z<data.height;z++)for(int x=0;x<data.width;x++)
                {
                    int i=z*data.width+x;if(source[i]==i)continue;
                    var e=Extrude(data,x,z);
                    samples.height[i]=e.height;samples.water[i]=e.water;samples.land[i]=e.land?1:0;samples.tile[i]=data.tileSamples[source[i]];
                }
            }
            cache.Add(data,samples);return samples;
        }

        /// <summary>Nearest playable edge sample for any W3E lattice vertex, including beyond the grid.</summary>
        public static int SourceIndex(ImportedMapData data,int x,int z)
        {
            var b=Bounds(data);
            if(b.z<=b.x)return Mathf.Clamp(z,0,data.height-1)*data.width+Mathf.Clamp(x,0,data.width-1);
            // Source posts just past the rectangle keep their own ground (they stay playable).
            if(x>=0&&z>=0&&x<data.width&&z<data.height&&data.NearOutsideAnchor(data.originX+x*data.cellSize,data.originZ+z*data.cellSize,1.5f*data.cellSize))
                return z*data.width+x;
            var e=Edges(data);
            return Mathf.Clamp(z,e.minZ,e.maxZ)*data.width+Mathf.Clamp(x,e.minX,e.maxX);
        }

        public readonly struct Extruded
        {
            public readonly float height,water,landFraction;public readonly bool land;
            public Extruded(float height,float water,bool land,float landFraction=-1)
            {this.height=height;this.water=water;this.land=land;this.landFraction=landFraction<0?(land?1:0):landFraction;}
        }
        /// <summary>
        /// Terrain beyond the playable edge: the edge profile, low-passed along the edge
        /// with a window that widens with distance, so the skirt reads as soft terrain
        /// receding into the horizon rather than rows copied outward (no stripes, and
        /// narrow edge channels such as rivers close after a few metres).
        /// </summary>
        public static Extruded Extrude(ImportedMapData data,int x,int z)
        {
            int self=SourceIndex(data,x,z);
            var e=Edges(data);
            int cx=Mathf.Clamp(x,e.minX,e.maxX),cz=Mathf.Clamp(z,e.minZ,e.maxZ);
            if(e.minX>e.maxX||self==z*data.width+x&&x>=0&&z>=0&&x<data.width&&z<data.height)
                return new Extruded(data.heightSamples[self],data.waterSamples[self],data.landSamples[self]!=0);
            int dx=Mathf.Abs(x-cx),dz=Mathf.Abs(z-cz);
            // Rows (south/north edges) run along X; columns (west/east edges) along Z.
            int radius=Mathf.Clamp(Mathf.RoundToInt((Mathf.Max(dx,dz)-4)*.8f),0,64);
            Extruded Along(bool row)
            {
                Profile profile=row?(z<cz?e.south:e.north):(x<cx?e.west:e.east);
                int along=row?cx-e.minX:cz-e.minZ;
                return profile.Average(along-radius,along+radius);
            }
            if(dx==0)return Wavy(Along(true),x,z);
            if(dz==0)return Wavy(Along(false),x,z);
            // Corners blend both edge profiles by direction, so no diagonal seam appears.
            var a=Along(true);var b=Along(false);float t=dz/(float)(dx+dz);
            float fraction=Mathf.Lerp(b.landFraction,a.landFraction,t),level=Mathf.Lerp(b.water,a.water,t);
            // Low-frequency noise keeps the corner land/sea boundary from reading as a straight ray.
            bool isLand=fraction+(Mathf.PerlinNoise(x*.06f+3.3f,z*.06f+9.1f)-.5f)*.6f>.5f;float height=Mathf.Lerp(b.height,a.height,t);
            height=isLand?Mathf.Max(height,level+.05f):Mathf.Min(height,level-.2f);
            return new Extruded(height,level,isLand,fraction);
        }

        // The widening window makes the land fraction change gradually across an edge
        // coast; low-frequency noise on that fraction turns the extruded coastline into
        // an irregular shore instead of a straight line running to the horizon.
        static Extruded Wavy(Extruded e,int x,int z)
        {
            bool isLand=e.landFraction+(Mathf.PerlinNoise(x*.05f+13.7f,z*.05f+2.9f)-.5f)*.7f>.5f;
            if(isLand==e.land)return e;
            float height=isLand?e.water+.05f:e.water-.2f;
            return new Extruded(height,e.water,isLand,e.landFraction);
        }
        sealed class Profile
        {
            // Prefix sums along one edge: land count, land height, sea-bed height, water level.
            readonly float[] land,landHeight,seaHeight,level;
            public Profile(int count){land=new float[count+1];landHeight=new float[count+1];seaHeight=new float[count+1];level=new float[count+1];}
            public void Set(int i,bool isLand,float height,float water)
            {
                land[i+1]=land[i]+(isLand?1:0);landHeight[i+1]=landHeight[i]+(isLand?height:0);
                seaHeight[i+1]=seaHeight[i]+(isLand?0:height);level[i+1]=level[i]+water;
            }
            public Extruded Average(int from,int to)
            {
                int count=land.Length-1;from=Mathf.Clamp(from,0,count-1);to=Mathf.Clamp(to,0,count-1);
                float n=to-from+1,landCount=land[to+1]-land[from],seaCount=n-landCount;
                float water=(level[to+1]-level[from])/n;
                bool isLand=landCount*2>n;
                float height=isLand?(landHeight[to+1]-landHeight[from])/Mathf.Max(1,landCount):(seaHeight[to+1]-seaHeight[from])/Mathf.Max(1,seaCount);
                // A water cell must stay under the surface, a land cell above it.
                height=isLand?Mathf.Max(height,water+.05f):Mathf.Min(height,water-.2f);
                return new Extruded(height,water,isLand,landCount/n);
            }
        }
        sealed class EdgeSet { public int minX,maxX,minZ,maxZ;public Profile west,east,south,north; }
        static readonly ConditionalWeakTable<ImportedMapData,EdgeSet> edges=new ConditionalWeakTable<ImportedMapData,EdgeSet>();
        static EdgeSet Edges(ImportedMapData data)
        {
            if(edges.TryGetValue(data,out var found))return found;
            var b=Bounds(data);var set=new EdgeSet();
            // W3I bounds fall on W3E vertices; rounding keeps any other value stable.
            set.minX=Mathf.Clamp(Mathf.CeilToInt((b.x-data.originX)/data.cellSize-.001f),0,data.width-1);
            set.maxX=Mathf.Clamp(Mathf.FloorToInt((b.z-data.originX)/data.cellSize+.001f),0,data.width-1);
            set.minZ=Mathf.Clamp(Mathf.CeilToInt((b.y-data.originZ)/data.cellSize-.001f),0,data.height-1);
            set.maxZ=Mathf.Clamp(Mathf.FloorToInt((b.w-data.originZ)/data.cellSize+.001f),0,data.height-1);
            Profile Build(bool row,int fixedIndex)
            {
                int count=row?set.maxX-set.minX+1:set.maxZ-set.minZ+1;var profile=new Profile(count);
                for(int i=0;i<count;i++)
                {
                    int k=row?fixedIndex*data.width+set.minX+i:(set.minZ+i)*data.width+fixedIndex;
                    profile.Set(i,data.landSamples[k]!=0,data.heightSamples[k],data.waterSamples[k]);
                }
                return profile;
            }
            set.south=Build(true,set.minZ);set.north=Build(true,set.maxZ);set.west=Build(false,set.minX);set.east=Build(false,set.maxX);
            edges.Add(data,set);return set;
        }

        /// <summary>Visual vertex position: deformed source geometry inside, the plain lattice outside.</summary>
        public static Vector2 Vertex(ImportedMapData data,Samples samples,int x,int z)
        {
            int index=z*data.width+x;
            if(samples.source==null||samples.source[index]==index)return data.TerrainVertex(x,z);
            return new Vector2(data.originX+x*data.cellSize,data.originZ+z*data.cellSize);
        }
    }
}
