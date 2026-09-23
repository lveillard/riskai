using System.Runtime.CompilerServices;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Presentation continuation beyond the W3I playable rectangle. The source
    /// boundary is a flat green placeholder; art instead extrudes the nearest
    /// playable edge sample outward (sea stays sea, an edge coast or land runs
    /// straight out as that terrain) and fades into the horizon. Nothing outside
    /// the rectangle is walkable or navigable (ImportedMapData.InPlayable).
    /// </summary>
    public static class ImportedMapSkirt
    {
        /// <summary>Review/setup code may disable the continuation to reproduce the legacy border.</summary>
        public static bool Enabled { get; set; } = true;
        /// <summary>Outer visual ring beyond the playable rectangle, in metres.</summary>
        public const float Reach = 232f;

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

        /// <summary>Visual sample arrays: the source inside the playable rectangle, extruded edge samples outside it.</summary>
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
                samples.height=new float[size];samples.water=new float[size];samples.land=new int[size];samples.tile=new int[size];
                for(int i=0;i<size;i++)
                {
                    int s=source[i];
                    samples.height[i]=data.heightSamples[s];samples.water[i]=data.waterSamples[s];
                    samples.land[i]=data.landSamples[s];samples.tile[i]=data.tileSamples[s];
                }
            }
            cache.Add(data,samples);return samples;
        }

        /// <summary>
        /// Edge sample extruded to any W3E lattice vertex, including beyond the grid.
        /// Low-frequency noise bends the extrusion along the edge so land does not
        /// read as perfect stripes; the offset grows with distance and never reaches inside.
        /// </summary>
        public static int SourceIndex(ImportedMapData data,int x,int z)
        {
            var b=Bounds(data);
            if(b.z<=b.x)return Mathf.Clamp(z,0,data.height-1)*data.width+Mathf.Clamp(x,0,data.width-1);
            // W3I bounds fall on W3E vertices; rounding keeps any other value stable.
            int minX=Mathf.Clamp(Mathf.CeilToInt((b.x-data.originX)/data.cellSize-.001f),0,data.width-1);
            int maxX=Mathf.Clamp(Mathf.FloorToInt((b.z-data.originX)/data.cellSize+.001f),0,data.width-1);
            int minZ=Mathf.Clamp(Mathf.CeilToInt((b.y-data.originZ)/data.cellSize-.001f),0,data.height-1);
            int maxZ=Mathf.Clamp(Mathf.FloorToInt((b.w-data.originZ)/data.cellSize+.001f),0,data.height-1);
            // Source posts just past the rectangle keep their own ground (they stay playable).
            if(x>=0&&z>=0&&x<data.width&&z<data.height&&data.NearOutsideAnchor(data.originX+x*data.cellSize,data.originZ+z*data.cellSize,1.5f*data.cellSize))
                return z*data.width+x;
            int cx=Mathf.Clamp(x,minX,maxX),cz=Mathf.Clamp(z,minZ,maxZ);
            int dx=Mathf.Abs(x-cx),dz=Mathf.Abs(z-cz);
            if(dx>0&&dz==0)cz=Mathf.Clamp(z+Mathf.RoundToInt(Bend(x,z)*dx),minZ,maxZ);
            else if(dz>0&&dx==0)cx=Mathf.Clamp(x+Mathf.RoundToInt(Bend(x,z)*dz),minX,maxX);
            return cz*data.width+cx;
        }
        static float Bend(int x,int z)=>(Mathf.PerlinNoise(x*.043f+5.3f,z*.043f+17.1f)-.5f)*.9f;

        /// <summary>Visual vertex position: deformed source geometry inside, the plain lattice outside.</summary>
        public static Vector2 Vertex(ImportedMapData data,Samples samples,int x,int z)
        {
            int index=z*data.width+x;
            if(samples.source==null||samples.source[index]==index)return data.TerrainVertex(x,z);
            return new Vector2(data.originX+x*data.cellSize,data.originZ+z*data.cellSize);
        }
    }
}
