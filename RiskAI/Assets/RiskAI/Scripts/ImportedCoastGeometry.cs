using UnityEngine;

namespace RiskAI
{
    /// <summary>A bounded, invertible horizontal rounding of source water-cell corners.
    /// Source heights, water flags and triangle connectivity remain unchanged.</summary>
    public sealed class ImportedCoastGeometry
    {
        public const float MaximumCellDisplacement=.20f;
        public const float CityProtectionRadius=9f;
        public const float PortProtectionRadius=24f;
        readonly ImportedMapData data;
        readonly Vector2[] offsets;
        public int MovedVertexCount { get; private set; }

        public ImportedCoastGeometry(ImportedMapData source)
        {
            data=source;offsets=new Vector2[data.width*data.height];
            var boundary=new bool[offsets.Length];
            // The rendered water footprint is the union of cells with at least
            // one enabled source-water corner. Round that footprint, not a new mask.
            for(int z=1;z<data.height-1;z++)for(int x=1;x<data.width-1;x++)
            {
                int wet=0;
                for(int dz=-1;dz<=0;dz++)for(int dx=-1;dx<=0;dx++)
                    if(WaterCell(x+dx,z+dz))wet++;
                boundary[z*data.width+x]=wet>0&&wet<4;
            }
            for(int z=2;z<data.height-2;z++)for(int x=2;x<data.width-2;x++)
            {
                int k=z*data.width+x;if(!boundary[k])continue;
                var point=new Vector2(data.originX+x*data.cellSize,data.originZ+z*data.cellSize);
                if(Protected(point))continue;
                Vector2 sum=Vector2.zero;int count=0;
                if(boundary[k-1]){sum+=Vector2.left;count++;}
                if(boundary[k+1]){sum+=Vector2.right;count++;}
                if(boundary[k-data.width]){sum+=Vector2.down;count++;}
                if(boundary[k+data.width]){sum+=Vector2.up;count++;}
                // Junctions and isolated points can describe narrow channels or
                // tiny islands: retain them, and keep a straight coast stationary.
                if(count!=2||sum.sqrMagnitude<.01f)continue;
                offsets[k]=sum.normalized*(MaximumCellDisplacement*data.cellSize);
                MovedVertexCount++;
            }
        }
        bool WaterCell(int x,int z)
        {
            int k=z*data.width+x;
            return data.landSamples[k]+data.landSamples[k+1]+data.landSamples[k+data.width]+data.landSamples[k+data.width+1]<4;
        }
        bool Protected(Vector2 point)
        {
            if(data.cities==null)return false;
            foreach(var city in data.cities)
            {
                float radius=city.port?PortProtectionRadius:CityProtectionRadius;
                if((point-new Vector2(city.x,city.z)).sqrMagnitude<radius*radius||
                    (point-new Vector2(city.claimX,city.claimZ)).sqrMagnitude<radius*radius)return true;
            }
            return false;
        }
        public Vector2 Vertex(int x,int z)=>new Vector2(data.originX+x*data.cellSize,data.originZ+z*data.cellSize)+offsets[z*data.width+x];
        public Vector2 Offset(int x,int z)=>offsets[z*data.width+x];

        // Most terrain uses the unchanged source cell, without triangle searches.
        // A displaced point can cross only one cell edge (movement < 0.2 cell).
        public bool Resolve(float x,float z,ref int ix,ref int iz,out float u,out float v)
        {
            int k=iz*data.width+ix;
            u=(x-data.originX)/data.cellSize-ix;v=(z-data.originZ)/data.cellSize-iz;
            if(offsets[k]==Vector2.zero&&offsets[k+1]==Vector2.zero&&
                offsets[k+data.width]==Vector2.zero&&offsets[k+data.width+1]==Vector2.zero)return true;
            var point=new Vector2(x,z);
            if(InCell(ix,iz,point,out u,out v))return true;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                int cx=ix+dx,cz=iz+dz;
                if((dx==0&&dz==0)||cx<0||cz<0||cx>=data.width-1||cz>=data.height-1)continue;
                if(!InCell(cx,cz,point,out u,out v))continue;
                ix=cx;iz=cz;return true;
            }
            return false;
        }
        bool InCell(int x,int z,Vector2 point,out float u,out float v)
        {
            var a=Vertex(x,z);var b=Vertex(x+1,z);var c=Vertex(x,z+1);var d=Vertex(x+1,z+1);
            if(InTriangle(point,a,b,c,out u,out v))return true;
            if(!InTriangle(point,d,c,b,out float beta,out float gamma))return false;
            u=1-beta;v=1-gamma;return true;
        }
        static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c,out float u,out float v)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;
            float determinant=ab.x*ac.y-ab.y*ac.x;
            u=(ap.x*ac.y-ap.y*ac.x)/determinant;
            v=(ab.x*ap.y-ab.y*ap.x)/determinant;
            const float tolerance=.00001f;
            return u>=-tolerance&&v>=-tolerance&&u+v<=1+tolerance;
        }
    }
}
