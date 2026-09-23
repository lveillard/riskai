using System;
using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>
    /// Guard against geometric ground zones. Every zone weight field (authored-map
    /// dry/arid/autumn/stone zones, imported-map biome field) is thresholded and its
    /// boundary is scanned along eight lattice directions: a boundary that runs
    /// perfectly straight for many texels comes from a rectangle, polygon edge or
    /// value-noise lattice, never from a noise-warped organic field.
    /// </summary>
    public sealed class GroundZoneShapeTests
    {
        // A smooth curve of radius r has boundary runs of about 2*sqrt(2r) texels along its
        // flattest direction; organic zones measure 6-13, straight edges far above 16.
        const int MaximumStraightRun = 16;

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        public void AuthoredGroundZonesHaveNoStraightBoundaries(ScenarioMap map)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(map);
                var field=FictionalGround.Field(out int width,out int height);
                string[] names={"dry","arid","autumn","stone"};
                for(int channel=0;channel<4;channel++)
                {
                    var mask=Threshold(field,width,height,channel,.5f,out int covered);
                    int run=MaxStraightBoundaryRun(mask,width,height,out var at);
                    Debug.Log($"RISKAI_ZONE_SHAPE map={map} zone={names[channel]} texels={covered} maxStraightRun={run} at={at}");
                    Assert.That(run,Is.LessThanOrEqualTo(MaximumStraightRun),$"{map} {names[channel]} zone boundary runs straight for {run} texels near {at}");
                }
            }
            finally{MapLayout.Configure(previous);}
        }

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        public void AuthoredTreeDensityHasNoStraightBands(ScenarioMap map)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(map);
                const float step=1.4f;
                int width=Mathf.FloorToInt(2*MapLayout.HalfWidth/step),height=Mathf.FloorToInt(2*MapLayout.HalfDepth/step);
                var chance=new float[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    chance[y*width+x]=StrategicTerrain.TreeChance(-MapLayout.HalfWidth+(x+.5f)*step,-MapLayout.HalfDepth+(y+.5f)*step);
                foreach(float threshold in new[]{.25f,.5f})
                {
                    var mask=new bool[chance.Length];for(int i=0;i<mask.Length;i++)mask[i]=chance[i]>=threshold;
                    int run=MaxStraightBoundaryRun(mask,width,height,out var at);
                    Debug.Log($"RISKAI_ZONE_SHAPE map={map} zone=trees>{threshold} maxStraightRun={run} at={at} dir={LastDirection} world=({-MapLayout.HalfWidth+(at.x+.5f)*step:F1},{-MapLayout.HalfDepth+(at.y+.5f)*step:F1})");
                    Assert.That(run,Is.LessThanOrEqualTo(MaximumStraightRun+4),$"{map} tree density>{threshold} has a straight band of {run} cells near {at}");
                }
            }
            finally{MapLayout.Configure(previous);}
        }

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        public void AuthoredSandFieldHasNoStraightBoundaries(ScenarioMap map)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(map);
                const float step=1.4f;
                int width=Mathf.FloorToInt(2*MapLayout.HalfWidth/step),height=Mathf.FloorToInt(2*MapLayout.HalfDepth/step);
                var mask=new bool[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    mask[y*width+x]=ShoreAccess.IsSandySurface(-MapLayout.HalfWidth+(x+.5f)*step,-MapLayout.HalfDepth+(y+.5f)*step);
                // Only sand boundaries inside land count: where sand meets water the edge is
                // the coastline itself (terrain), which may legitimately run straight.
                var land=new bool[mask.Length];var visible=new bool[mask.Length];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)land[y*width+x]=MapLayout.IsLand(-MapLayout.HalfWidth+(x+.5f)*step,-MapLayout.HalfDepth+(y+.5f)*step);
                // A beach runs parallel to its coast, so keep a 4 m margin from any water.
                const int margin=3;
                for(int y=margin;y<height-margin;y++)for(int x=margin;x<width-margin;x++)
                {
                    bool inland=true;
                    for(int dy=-margin;dy<=margin&&inland;dy++)for(int dx=-margin;dx<=margin;dx++)if(!land[(y+dy)*width+x+dx]){inland=false;break;}
                    visible[y*width+x]=inland;
                }
                int run=MaxStraightBoundaryRun(mask,width,height,out var at,visible);
                Debug.Log($"RISKAI_ZONE_SHAPE map={map} zone=sand maxStraightRun={run} at={at} dir={LastDirection}");
                Assert.That(run,Is.LessThanOrEqualTo(MaximumStraightRun+4),$"{map} sand boundary runs straight for {run} cells near {at}");
            }
            finally{MapLayout.Configure(previous);}
        }

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        public void SeabedShelfStaysBelowIslandLand(ScenarioMap map)
        {
            // A shelf that rises through the coarser island mesh draws straight intersection lines.
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(map);
                int checkedPoints=0;
                for(float x=-MapLayout.HalfWidth;x<=MapLayout.HalfWidth;x+=.7f)for(float z=0;z<=MapLayout.HalfDepth+16;z+=.7f)
                {
                    float island=-1;for(int i=0;i<MapLayout.Islands.Length;i++)island=Mathf.Max(island,MapLayout.IslandDistance(x,z,i));
                    if(island<.6f)continue;checkedPoints++;
                    Assert.That(StrategicTerrain.SeabedHeight(x,z),Is.LessThanOrEqualTo(MapLayout.Height(x,z)-.4f),$"{map} shelf at ({x:F1},{z:F1}) rises into island land");
                }
                Assert.That(checkedPoints,Is.GreaterThan(100));
            }
            finally{MapLayout.Configure(previous);}
        }

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        public void AuthoredStoneAsRenderedHasNoStraightBands(ScenarioMap map)
        {
            // The shader interpolates the stone channel; sample it finely, as drawn.
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(map);
                const float step=.7f;
                int width=Mathf.FloorToInt(2*MapLayout.HalfWidth/step),height=Mathf.FloorToInt(2*MapLayout.HalfDepth/step);
                var mask=new bool[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    mask[y*width+x]=FictionalGround.Sample(-MapLayout.HalfWidth+(x+.5f)*step,-MapLayout.HalfDepth+(y+.5f)*step).Stone>=.5f;
                int run=MaxStraightBoundaryRun(mask,width,height,out var at);
                Debug.Log($"RISKAI_ZONE_SHAPE map={map} zone=stone-rendered maxStraightRun={run} at={at} dir={LastDirection}");
                // Measured: 20 before the rim breakup (Las Marcas), 17 after.
                Assert.That(run,Is.LessThanOrEqualTo(18),$"{map} rendered stone runs straight for {run*step:F1} m near {at}");
            }
            finally{MapLayout.Configure(previous);}
        }

        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void ImportedSandAsRenderedHasNoStraightBoundaries(ScenarioMap map)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(map);
                var data=MapLayout.Imported;const float step=.64f;
                float minX=data.PlayableMinX,minZ=data.PlayableMinZ;
                int width=Mathf.FloorToInt((data.PlayableMaxX-minX)/step),height=Mathf.FloorToInt((data.PlayableMaxZ-minZ)/step);
                var mask=new bool[width*height];var land=new bool[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    float wx=minX+(x+.5f)*step,wz=minZ+(y+.5f)*step;
                    mask[y*width+x]=ShoreAccess.IsSandySurface(wx,wz);land[y*width+x]=data.IsLand(wx,wz);
                }
                // Sand meeting water is the coastline (terrain); judge only sand edges on dry land.
                var visible=new bool[mask.Length];const int margin=3;
                for(int y=margin;y<height-margin;y++)for(int x=margin;x<width-margin;x++)
                {
                    bool inland=true;
                    for(int dy=-margin;dy<=margin&&inland;dy++)for(int dx=-margin;dx<=margin;dx++)if(!land[(y+dy)*width+x+dx]){inland=false;break;}
                    visible[y*width+x]=inland;
                }
                int run=MaxStraightBoundaryRun(mask,width,height,out var at,visible);
                float fraction=StraightFraction(mask,width,height,6,visible);
                Debug.Log($"RISKAI_ZONE_SHAPE map={map} zone=sand-rendered maxStraightRun={run} straightFraction={fraction:F3} at={at} dir={LastDirection} world=({minX+at.x*step:F1},{minZ+at.y*step:F1})");
                // Measured on cell-aligned sand: Europe 12, New World 16; smoothed: 9 and 10.
                Assert.That(run,Is.LessThanOrEqualTo(11),$"{map} sand runs straight for {run*step:F1} m near {at}");
            }
            finally{MapLayout.Configure(previous);}
        }

        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void ImportedBiomeZonesHaveNoStraightBoundaries(ScenarioMap map)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(map);
                var field=TerrainBiomes.Field(out int width,out int height);
                Assert.That(field,Is.Not.Null);
                // Only visible ground matters: the field also spans open sea between anchors.
                var data=MapLayout.Imported;var land=new bool[width*height];float step=data.cellSize*2;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)land[y*width+x]=data.IsLand(data.PlayableMinX+x*step,data.PlayableMinZ+y*step);
                string[] names={"arid","cold","lush","rock"};
                foreach(int channel in new[]{0,1,3})
                foreach(float threshold in new[]{.35f,.6f,.85f})
                {
                    var mask=Threshold(field,width,height,channel,threshold,out _);
                    int run=MaxStraightBoundaryRun(mask,width,height,out var at,land);
                    Debug.Log($"RISKAI_ZONE_SHAPE map={map} zone={names[channel]}>{threshold} maxStraightRun={run} at={at}");
                    Assert.That(run,Is.LessThanOrEqualTo(MaximumStraightRun),$"{map} {names[channel]}>{threshold} boundary runs straight for {run} texels near {at}");
                }
            }
            finally{MapLayout.Configure(previous);}
        }

        [Test]
        public void DetectorFlagsRectanglesAndDiagonalsButAcceptsCurves()
        {
            const int size=128;
            var rectangle=new bool[size*size];var diagonal=new bool[size*size];var disc=new bool[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                rectangle[y*size+x]=x>20&&x<90&&y>30&&y<100;
                diagonal[y*size+x]=x+y*.5f<90;
                disc[y*size+x]=(x-64)*(x-64)+(y-64)*(y-64)<24*24;
            }
            Assert.That(MaxStraightBoundaryRun(rectangle,size,size,out _),Is.GreaterThan(MaximumStraightRun));
            Assert.That(MaxStraightBoundaryRun(diagonal,size,size,out _),Is.GreaterThan(MaximumStraightRun));
            Assert.That(MaxStraightBoundaryRun(disc,size,size,out _),Is.LessThanOrEqualTo(MaximumStraightRun));
        }

        static bool[] Threshold(Color32[] field,int width,int height,int channel,float threshold,out int covered)
        {
            var mask=new bool[width*height];covered=0;byte cut=(byte)Mathf.RoundToInt(threshold*255);
            for(int i=0;i<mask.Length;i++)
            {
                var c=field[i];byte v=channel==0?c.r:channel==1?c.g:channel==2?c.b:c.a;
                mask[i]=v>=cut;if(mask[i])covered++;
            }
            return mask;
        }

        /// <summary>
        /// Share of boundary texels lying on straight lattice runs of at least minRun texels.
        /// A polygonal outline (cell-aligned sand) is mostly such runs; an organic one is not.
        /// </summary>
        public static float StraightFraction(bool[] mask,int width,int height,int minRun,bool[] visible=null)
        {
            var boundary=new bool[mask.Length];int total=0;
            for(int y=2;y<height-3;y++)for(int x=2;x<width-3;x++)
            {
                int i=y*width+x;
                boundary[i]=(visible==null||visible[i])&&(mask[i]!=mask[i+1]||mask[i]!=mask[i+width]);
                if(boundary[i])total++;
            }
            if(total==0)return 0;
            var straight=new bool[mask.Length];
            foreach(var d in Directions)
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    if(!boundary[y*width+x])continue;
                    int px=x-d.x,py=y-d.y;
                    if(px>=0&&py>=0&&px<width&&py<height&&boundary[py*width+px])continue;
                    int run=0,cx=x,cy=y;
                    while(cx>=0&&cy>=0&&cx<width&&cy<height&&boundary[cy*width+cx]){run++;cx+=d.x;cy+=d.y;}
                    if(run<minRun)continue;
                    for(int k=0,ax=x,ay=y;k<run;k++,ax+=d.x,ay+=d.y)straight[ay*width+ax]=true;
                }
            int count=0;for(int i=0;i<straight.Length;i++)if(straight[i])count++;
            return count/(float)total;
        }
        public static Vector2Int LastDirection;
        static readonly Vector2Int[] Directions={new(1,0),new(0,1),new(1,1),new(1,-1),new(2,1),new(1,2),new(2,-1),new(1,-2)};
        /// <summary>Longest run of boundary texels along a single straight lattice direction.</summary>
        public static int MaxStraightBoundaryRun(bool[] mask,int width,int height,out Vector2Int at,bool[] visible=null)
        {
            var boundary=new bool[mask.Length];
            // The outermost texels are the field's own rectangle, not a zone boundary.
            for(int y=2;y<height-3;y++)for(int x=2;x<width-3;x++)
            {
                int i=y*width+x;
                boundary[i]=(visible==null||visible[i])&&(mask[i]!=mask[i+1]||mask[i]!=mask[i+width]);
            }
            int best=0;at=default;
            foreach(var d in Directions)
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    if(!boundary[y*width+x])continue;
                    int px=x-d.x,py=y-d.y;
                    if(px>=0&&py>=0&&px<width&&py<height&&boundary[py*width+px])continue;
                    int run=0,cx=x,cy=y;
                    while(cx>=0&&cy>=0&&cx<width&&cy<height&&boundary[cy*width+cx]){run++;cx+=d.x;cy+=d.y;}
                    if(run>best){best=run;at=new Vector2Int(x,y);LastDirection=d;}
                }
            return best;
        }
    }
}
