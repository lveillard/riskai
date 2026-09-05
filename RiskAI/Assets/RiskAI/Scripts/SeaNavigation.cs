using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Deterministic coarse-grid routing for ships. Every accepted segment is rechecked against IsOcean.</summary>
    public static class SeaNavigation
    {
        const float CellSize=2.8f,SegmentStep=1.25f;
        public const float HullClearance=1.3f;

        public static bool IsOcean(Vector3 point) => MapLayout.IsOcean(point.x,point.z);
        public static bool HasClearance(Vector3 point,float clearance=HullClearance)
        {
            if(!IsOcean(point))return false;
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI*.25f;var offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*clearance;
                if(!IsOcean(point+offset))return false;
            }
            return true;
        }

        public static bool TryBuildPath(Vector3 from,Vector3 to,out List<Vector3> path)
        {
            path=null;
            if(!HasClearance(from)||!HasClearance(to))return false;
            int width=Mathf.CeilToInt(MapLayout.HalfWidth*2/CellSize)+1;
            int height=Mathf.CeilToInt(MapLayout.HalfDepth*2/CellSize)+1;
            Vector2 origin=new Vector2(-MapLayout.HalfWidth,-MapLayout.HalfDepth);
            int start=NearestOcean(from,width,height,origin),goal=NearestOcean(to,width,height,origin);
            if(start<0||goal<0)return false;
            if(start==goal){if(!ClearSegment(from,to))return false;path=new List<Vector3>{to};return true;}
            var open=new List<int>{start};var closed=new bool[width*height];var came=new int[width*height];var g=new float[width*height];var f=new float[width*height];
            for(int i=0;i<came.Length;i++){came[i]=-1;g[i]=float.MaxValue;f[i]=float.MaxValue;}
            g[start]=0;f[start]=Heuristic(start,goal,width,origin);
            int found=-1;
            while(open.Count>0)
            {
                int bestIndex=0;
                for(int i=1;i<open.Count;i++)if(f[open[i]]<f[open[bestIndex]])bestIndex=i;
                int current=open[bestIndex];open.RemoveAt(bestIndex);
                if(current==goal){found=current;break;}
                if(closed[current])continue;closed[current]=true;
                int cx=current%width,cz=current/width;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dz==0)continue;
                    int nx=cx+dx,nz=cz+dz;if(nx<0||nx>=width||nz<0||nz>=height)continue;
                    int next=nz*width+nx;if(closed[next]||!OceanCell(next,width,origin))continue;
                    if(dx!=0&&dz!=0&&(!OceanCell(cz*width+nx,width,origin)||!OceanCell(nz*width+cx,width,origin)))continue;
                    Vector3 a=CellPoint(current,width,origin),b=CellPoint(next,width,origin);
                    if(!ClearSegment(a,b))continue;
                    float candidate=g[current]+(dx!=0&&dz!=0?1.4142135f:1);
                    if(candidate>=g[next])continue;
                    came[next]=current;g[next]=candidate;f[next]=candidate+Heuristic(next,goal,width,origin);
                    if(!open.Contains(next))open.Add(next);
                }
            }
            if(found<0)return false;
            var cells=new List<int>();for(int current=found;current>=0;current=came[current])cells.Add(current);cells.Reverse();
            path=new List<Vector3>();
            for(int i=0;i<cells.Count;i++)path.Add(CellPoint(cells[i],width,origin));
            path.Add(to);
            for(int i=0;i<path.Count;i++)if(!IsOcean(path[i])||(i>0&&!ClearSegment(path[i-1],path[i]))) {path=null;return false;}
            // Pull a string through visible waypoints so ships follow broad reaches, not a grid staircase.
            var smooth=new List<Vector3>();Vector3 anchor=from;int cursor=0;
            while(cursor<path.Count){int far=cursor;for(int n=path.Count-1;n>cursor;n--)if(ClearSegment(anchor,path[n])){far=n;break;}smooth.Add(path[far]);anchor=path[far];cursor=far+1;}
            path=smooth;
            return true;
        }

        public static bool TryNearestOcean(Vector3 point,float radius,out Vector3 result)
        {
            if(HasClearance(point)){result=new Vector3(point.x,-.24f,point.z);return true;}
            float step=CellSize*.5f,best=float.MaxValue;result=default(Vector3);
            for(float x=point.x-radius;x<=point.x+radius;x+=step)for(float z=point.z-radius;z<=point.z+radius;z+=step)
            {
                var candidate=new Vector3(x,-.24f,z);float dx=x-point.x,dz=z-point.z,distance=dx*dx+dz*dz;
                if(distance<best&&distance<=radius*radius&&HasClearance(candidate)){best=distance;result=candidate;}
            }
            return best<float.MaxValue;
        }

        static int NearestOcean(Vector3 point,int width,int height,Vector2 origin)
        {
            int cx=Mathf.Clamp(Mathf.RoundToInt((point.x-origin.x)/CellSize),0,width-1),cz=Mathf.Clamp(Mathf.RoundToInt((point.z-origin.y)/CellSize),0,height-1);
            for(int radius=0;radius<8;radius++)
            {
                int best=-1;float distance=float.MaxValue;
                for(int dz=-radius;dz<=radius;dz++)for(int dx=-radius;dx<=radius;dx++)
                {
                    int x=cx+dx,z=cz+dz;if(x<0||x>=width||z<0||z>=height)continue;int index=z*width+x;
                    if(!OceanCell(index,width,origin))continue;var cell=CellPoint(index,width,origin);if(!ClearSegment(point,cell))continue;float d=dx*dx+dz*dz;if(d<distance){distance=d;best=index;}
                }
                if(best>=0)return best;
            }
            return -1;
        }
        static bool OceanCell(int index,int width,Vector2 origin)=>HasClearance(CellPoint(index,width,origin));
        static Vector3 CellPoint(int index,int width,Vector2 origin)=>new Vector3(origin.x+(index%width)*CellSize,-.24f,origin.y+(index/width)*CellSize);
        static float Heuristic(int a,int b,int width,Vector2 origin)
        {
            int ax=a%width,az=a/width,bx=b%width,bz=b/width;int dx=Mathf.Abs(ax-bx),dz=Mathf.Abs(az-bz);return Mathf.Max(dx,dz)+.4142135f*Mathf.Min(dx,dz);
        }
        public static bool ClearSegment(Vector3 from,Vector3 to)
        {
            float distance=Vector2.Distance(new Vector2(from.x,from.z),new Vector2(to.x,to.z));int count=Mathf.Max(1,Mathf.CeilToInt(distance/SegmentStep));
            for(int i=0;i<=count;i++){float t=i/(float)count;var point=Vector3.Lerp(from,to,t);if(!HasClearance(point))return false;}
            return true;
        }
    }
}
