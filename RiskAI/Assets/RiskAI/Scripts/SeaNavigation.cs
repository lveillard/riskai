using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Deterministic coarse-grid routing. Grid edges are validated once; returned segments are always rechecked.</summary>
    public static class SeaNavigation
    {
        const float ClassicCellSize=2.8f,ImportedCellSize=4f,SegmentStep=1.25f;
        const int MaxSearchStates=160000;
        public const float HullClearance=1.3f;
        static readonly int[] Dx={-1,0,1,-1,1,-1,0,1};
        static readonly int[] Dz={-1,-1,-1,0,0,1,1,1};
        static readonly float[] StepCost={1.4142135f,1,1.4142135f,1,1,1.4142135f,1,1.4142135f};
        const int SmoothingLookaheadCells=32;
        static readonly Vector3[] ClearanceDirections=BuildClearanceDirections();

        static Vector3[] BuildClearanceDirections()
        {
            var directions=new Vector3[8];
            for(int i=0;i<directions.Length;i++)
            {
                float angle=i*Mathf.PI*.25f;
                directions[i]=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            }
            return directions;
        }

        sealed class Grid
        {
            public readonly int Width,Height; public readonly float CellSize; public readonly Vector2 Origin;
            readonly bool[] ocean; readonly byte[] edges; readonly int[] components,queue;
            public readonly SearchScratch Search=new SearchScratch();
            public int ComponentCount { get; private set; }
            public Grid(int width,int height,Vector2 origin,float cellSize)
            {
                Width=width;Height=height;Origin=origin;CellSize=cellSize;
                int size=width*height;ocean=new bool[size];edges=new byte[size];components=new int[size];queue=new int[size];
                for(int i=0;i<size;i++)ocean[i]=HasClearance(Point(i));
                // Validate each undirected pair once, then store its result at
                // both endpoints. Components must not depend on reverse floating
                // interpolation producing exactly the same answer.
                for(int i=0;i<size;i++)if(ocean[i])for(int d=4;d<8;d++)
                    if(CanConnect(i,d,out int next))
                    {
                        edges[i]|=(byte)(1<<d);
                        edges[next]|=(byte)(1<<Reverse(d));
                    }
                BuildComponents();
            }
            public bool Ocean(int index)=>index>=0&&index<ocean.Length&&ocean[index];
            public Vector3 Point(int index)=>new Vector3(Origin.x+(index%Width)*CellSize,-.24f,Origin.y+(index/Width)*CellSize);
            public int Index(Vector3 point)
            {
                int x=Mathf.Clamp(Mathf.RoundToInt((point.x-Origin.x)/CellSize),0,Width-1),z=Mathf.Clamp(Mathf.RoundToInt((point.z-Origin.y)/CellSize),0,Height-1);
                return z*Width+x;
            }
            public bool Edge(int index,int direction)=>(edges[index]&(1<<direction))!=0;
            public int Neighbor(int index,int direction)=>index+Dz[direction]*Width+Dx[direction];
            public int Component(int index)=>Ocean(index)?components[index]:-1;
            bool CanConnect(int index,int direction,out int next)
            {
                int x=index%Width,z=index/Width,nx=x+Dx[direction],nz=z+Dz[direction];
                next=-1;if(nx<0||nx>=Width||nz<0||nz>=Height)return false;
                next=nz*Width+nx;if(!ocean[next])return false;
                if(Dx[direction]!=0&&Dz[direction]!=0&&(!ocean[z*Width+nx]||!ocean[nz*Width+x]))return false;
                return ClearSegment(Point(index),Point(next));
            }
            void BuildComponents()
            {
                for(int i=0;i<components.Length;i++)components[i]=-1;
                int component=0;
                for(int start=0;start<components.Length;start++)
                {
                    if(!ocean[start]||components[start]>=0)continue;
                    int head=0,tail=0;queue[tail++]=start;components[start]=component;
                    while(head<tail)
                    {
                        int current=queue[head++];
                        for(int d=0;d<8;d++)
                        {
                            if(!Edge(current,d))continue;int next=Neighbor(current,d);
                            if(components[next]>=0)continue;components[next]=component;queue[tail++]=next;
                        }
                    }
                    component++;
                }
                ComponentCount=component;
            }
        }
        struct SearchNode { public float Cost; public int Came; public bool Closed; }
        sealed class SearchScratch
        {
            public readonly MinHeap Open=new MinHeap();
            public readonly Dictionary<int,SearchNode> Nodes=new Dictionary<int,SearchNode>(2048);
            public readonly List<int> Cells=new List<int>(1024);
            public readonly List<Vector3> RawPath=new List<Vector3>(1024);

            public void Reset()
            {
                Open.Clear();Nodes.Clear();Cells.Clear();RawPath.Clear();
            }
        }
        sealed class MinHeap
        {
            struct Item { public int Index; public float Score; }
            readonly List<Item> items=new List<Item>(1024); public int Count=>items.Count;
            public void Clear()=>items.Clear();
            public void Push(int index,float score)
            {
                items.Add(new Item{Index=index,Score=score});int child=items.Count-1;
                while(child>0){int parent=(child-1)/2;if(items[parent].Score<=score)break;items[child]=items[parent];child=parent;}
                items[child]=new Item{Index=index,Score=score};
            }
            public int Pop()
            {
                var result=items[0].Index;var last=items[items.Count-1];items.RemoveAt(items.Count-1);if(items.Count==0)return result;
                int parent=0;while(true){int left=parent*2+1;if(left>=items.Count)break;int right=left+1,child=right<items.Count&&items[right].Score<items[left].Score?right:left;if(items[child].Score>=last.Score)break;items[parent]=items[child];parent=child;}items[parent]=last;return result;
            }
        }
        static Grid cached;
        static ImportedMapData cachedImportedData;
        static int cachedWidth,cachedHeight,cachedTownCount;
        static float cachedCellSize;
        static string cachedMapId;
        static bool cachedImported,cachedExpanded;
        static SeaNavigationTelemetry telemetry;
        public static int GridBuildCount { get; private set; }
        public static int GridComponentCount=>cached==null?0:cached.ComponentCount;
        public static double GridBuildMilliseconds { get; private set; }
        public static int LastSearchExpanded { get; private set; }
        public static double LastSearchMilliseconds { get; private set; }
        public static bool LastSearchUsedDirectSegment { get; private set; }
        public static bool LastSearchRejectedDisconnected { get; private set; }

        /// <summary>Low-frequency aggregate for runtime diagnostics; consuming it
        /// never changes route state or allocates.</summary>
        public static SeaNavigationTelemetry ConsumeTelemetry()
        {
            var result=telemetry;telemetry=default;return result;
        }

        public static bool IsOcean(Vector3 point)=>MapLayout.IsOcean(point.x,point.z);
        static int Reverse(int direction)=>7-direction;
        public static bool HasClearance(Vector3 point,float clearance=HullClearance)
        {
            if(!IsOcean(point))return false;
            for(int i=0;i<ClearanceDirections.Length;i++)if(!IsOcean(point+ClearanceDirections[i]*clearance))return false;
            return true;
        }
        /// <summary>Builds static clearance edges and ocean components during map setup.</summary>
        public static void Prepare()=>CurrentGrid();
        static Grid CurrentGrid()
        {
            bool imported=MapLayout.IsImported;float cellSize=imported?ImportedCellSize:ClassicCellSize;
            int width=imported?Mathf.CeilToInt((MapLayout.Imported.width-1)*MapLayout.Imported.cellSize/cellSize)+1:Mathf.CeilToInt(MapLayout.HalfWidth*2/cellSize)+1;
            int height=imported?Mathf.CeilToInt((MapLayout.Imported.height-1)*MapLayout.Imported.cellSize/cellSize)+1:Mathf.CeilToInt(MapLayout.HalfDepth*2/cellSize)+1;
            string mapId=imported?MapLayout.Imported.mapId:MapLayout.Scenario.ToString();
            if(cached!=null&&cachedWidth==width&&cachedHeight==height&&cachedCellSize==cellSize&&cachedTownCount==MapLayout.Towns.Length&&cachedImported==imported&&cachedExpanded==MapLayout.IsExpanded&&cachedMapId==mapId&&cachedImportedData==MapLayout.Imported)return cached;
            cachedWidth=width;cachedHeight=height;cachedCellSize=cellSize;cachedTownCount=MapLayout.Towns.Length;cachedImported=imported;cachedExpanded=MapLayout.IsExpanded;cachedMapId=mapId;cachedImportedData=MapLayout.Imported;
            var origin=imported?new Vector2(MapLayout.Imported.originX,MapLayout.Imported.originZ):new Vector2(-MapLayout.HalfWidth,-MapLayout.HalfDepth);
            long started=Stopwatch.GetTimestamp();cached=new Grid(width,height,origin,cellSize);GridBuildCount++;GridBuildMilliseconds=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;return cached;
        }
        public static bool TryBuildPath(Vector3 from,Vector3 to,out List<Vector3> path)
        {
            long started=Stopwatch.GetTimestamp();LastSearchExpanded=0;LastSearchUsedDirectSegment=false;LastSearchRejectedDisconnected=false;
            bool result=TryBuildPathCore(from,to,out path);LastSearchMilliseconds=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
            telemetry.SearchCount++;telemetry.TotalMilliseconds+=LastSearchMilliseconds;telemetry.MaxMilliseconds=System.Math.Max(telemetry.MaxMilliseconds,LastSearchMilliseconds);
            telemetry.ExpandedTotal+=LastSearchExpanded;telemetry.ExpandedMax=System.Math.Max(telemetry.ExpandedMax,LastSearchExpanded);
            if(LastSearchUsedDirectSegment)telemetry.DirectCount++;
            if(LastSearchRejectedDisconnected)telemetry.DisconnectedCount++;
            return result;
        }
        static bool TryBuildPathCore(Vector3 from,Vector3 to,out List<Vector3> path)
        {
            path=null;if(!HasClearance(from)||!HasClearance(to))return false;
            if(ClearSegment(from,to)){path=new List<Vector3>{to};LastSearchUsedDirectSegment=true;return true;}
            var grid=CurrentGrid();int start=NearestOcean(from,grid),goal=NearestOcean(to,grid);if(start<0||goal<0)return false;
            if(grid.Component(start)!=grid.Component(goal)){LastSearchRejectedDisconnected=true;return false;}
            // Equal snapped cells do not prove the two exact endpoints have line
            // of sight. Let the normal reconstruction retain from→cell→to when
            // the direct fast path above was blocked by a small coast feature.
            // This scratch state belongs to the cached grid. Its backing storage
            // stays allocated between searches; the returned route never aliases it.
            var scratch=grid.Search;scratch.Reset();var open=scratch.Open;var nodes=scratch.Nodes;
            nodes[start]=new SearchNode{Cost=0,Came=-1};open.Push(start,Heuristic(start,goal,grid.Width));int found=-1;
            while(open.Count>0)
            {
                int current=open.Pop();if(!nodes.TryGetValue(current,out var currentNode)||currentNode.Closed)continue;LastSearchExpanded++;
                if(current==goal){found=current;break;}currentNode.Closed=true;nodes[current]=currentNode;
                for(int d=0;d<8;d++)
                {
                    if(!grid.Edge(current,d))continue;int next=grid.Neighbor(current,d);if(nodes.TryGetValue(next,out var nextNode)&&nextNode.Closed)continue;
                    float candidate=currentNode.Cost+StepCost[d];if(nodes.TryGetValue(next,out nextNode)&&candidate>=nextNode.Cost)continue;
                    if(!nodes.ContainsKey(next)&&nodes.Count>=MaxSearchStates)return false;nodes[next]=new SearchNode{Cost=candidate,Came=current};open.Push(next,candidate+Heuristic(next,goal,grid.Width));
                }
            }
            if(found<0)return false;
            var cells=scratch.Cells;for(int current=found;current>=0;current=nodes[current].Came)cells.Add(current);cells.Reverse();
            var rawPath=scratch.RawPath;for(int i=0;i<cells.Count;i++)rawPath.Add(grid.Point(cells[i]));rawPath.Add(to);
            Vector3 previous=from;for(int i=0;i<rawPath.Count;i++){if(!HasClearance(rawPath[i])||!ClearSegment(previous,rawPath[i]))return false;previous=rawPath[i];}
            // A bounded lookahead avoids quadratic rechecking on long routes. Every
            // selected segment remains validated against exact hull clearance.
            path=new List<Vector3>(rawPath.Count);Vector3 anchor=from;int cursor=0;
            while(cursor<rawPath.Count)
            {
                int far=cursor,limit=Mathf.Min(rawPath.Count-1,cursor+SmoothingLookaheadCells);
                for(int n=limit;n>cursor;n--)if(ClearSegment(anchor,rawPath[n])){far=n;break;}
                path.Add(rawPath[far]);anchor=rawPath[far];cursor=far+1;
            }
            return true;
        }
        public static bool TryNearestOcean(Vector3 point,float radius,out Vector3 result)
        {
            if(HasClearance(point)){result=new Vector3(point.x,-.24f,point.z);return true;}if(!MapLayout.IsImported)return TryNearestClassicOcean(point,radius,out result);
            var grid=CurrentGrid();int center=grid.Index(point),cx=center%grid.Width,cz=center/grid.Width,cells=Mathf.CeilToInt(radius/grid.CellSize)+1;float best=float.MaxValue;result=default;
            for(int dz=-cells;dz<=cells;dz++)for(int dx=-cells;dx<=cells;dx++){int x=cx+dx,z=cz+dz;if(x<0||x>=grid.Width||z<0||z>=grid.Height)continue;int index=z*grid.Width+x;if(!grid.Ocean(index))continue;var candidate=grid.Point(index);float distance=(new Vector2(candidate.x-point.x,candidate.z-point.z)).sqrMagnitude;if(distance<best&&distance<=radius*radius){best=distance;result=candidate;}}
            return best<float.MaxValue;
        }
        static bool TryNearestClassicOcean(Vector3 point,float radius,out Vector3 result)
        {
            float step=ClassicCellSize*.5f,best=float.MaxValue;result=default;for(float x=point.x-radius;x<=point.x+radius;x+=step)for(float z=point.z-radius;z<=point.z+radius;z+=step){var candidate=new Vector3(x,-.24f,z);float dx=x-point.x,dz=z-point.z,distance=dx*dx+dz*dz;if(distance<best&&distance<=radius*radius&&HasClearance(candidate)){best=distance;result=candidate;}}return best<float.MaxValue;
        }
        static int NearestOcean(Vector3 point,Grid grid)
        {
            int center=grid.Index(point),cx=center%grid.Width,cz=center/grid.Width;for(int radius=0;radius<8;radius++){int best=-1;float distance=float.MaxValue;for(int dz=-radius;dz<=radius;dz++)for(int dx=-radius;dx<=radius;dx++){int x=cx+dx,z=cz+dz;if(x<0||x>=grid.Width||z<0||z>=grid.Height)continue;int index=z*grid.Width+x;if(!grid.Ocean(index))continue;var cell=grid.Point(index);if(!ClearSegment(point,cell))continue;float d=dx*dx+dz*dz;if(d<distance){distance=d;best=index;}}if(best>=0)return best;}return -1;
        }
        static float Heuristic(int a,int b,int width){int ax=a%width,az=a/width,bx=b%width,bz=b/width,dx=Mathf.Abs(ax-bx),dz=Mathf.Abs(az-bz);return Mathf.Max(dx,dz)+.4142135f*Mathf.Min(dx,dz);}
        public static bool ClearSegment(Vector3 from,Vector3 to)
        {
            float distance=Vector2.Distance(new Vector2(from.x,from.z),new Vector2(to.x,to.z));int count=Mathf.Max(1,Mathf.CeilToInt(distance/SegmentStep));for(int i=0;i<=count;i++){float t=i/(float)count;var point=Vector3.Lerp(from,to,t);if(!HasClearance(point))return false;}return true;
        }
    }

    public struct SeaNavigationTelemetry
    {
        public int SearchCount,ExpandedTotal,ExpandedMax,DirectCount,DisconnectedCount;
        public double TotalMilliseconds,MaxMilliseconds;
    }
}
