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
