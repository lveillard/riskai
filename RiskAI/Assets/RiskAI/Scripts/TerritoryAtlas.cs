using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>One map-derived Voronoi atlas shared by country inspection and strategic ownership.</summary>
    public sealed class TerritoryAtlas
    {
        public readonly struct Site
        {
            public readonly Vector2 Point;
            public readonly int Country;
            public readonly Settlement Town;
            public readonly Harbor Port;
            public Site(Vector3 point, int country, Settlement town, Harbor port = null)
            { Point = new Vector2(point.x, point.z); Country = country; Town = town; Port = port; }
            public int Owner => Port ? Port.Owner : Town ? Town.State.Owner : -1;
        }
        public readonly List<Site> Sites = new List<Site>();
        public readonly Texture2D Regions, Palette;
        public readonly Vector4 Bounds;
        readonly Color32[] palette;
        const int Resolution = 1024;

        public TerritoryAtlas(BattleSession session)
        {
            foreach (var town in session.Towns)
                Sites.Add(new Site(town.ClaimPoint, town.State.Country, town));
            if (session.Naval) foreach (var port in session.Naval.Harbors)
            {
                int country = port.LinkedTown ? port.LinkedTown.State.Country : port.State.Country;
                // Both shore and berth belong to the port's group, even when its city is inland.
                Sites.Add(new Site(port.Landing, country, port.LinkedTown, port));
                Sites.Add(new Site(port.Berth, country, port.LinkedTown, port));
            }
            Vector2 min = MapLayout.PlayableMin, max = MapLayout.PlayableMax;
            Bounds = new Vector4(min.x, min.y, max.x - min.x, max.y - min.y);
            var pixels = new Color32[Resolution * Resolution];
            var polygon = new List<Vector2>(32); var scratch = new List<Vector2>(32);
            for (int site = 0; site < Sites.Count; site++)
            {
                polygon.Clear(); polygon.Add(min); polygon.Add(new Vector2(max.x,min.y));
                polygon.Add(max); polygon.Add(new Vector2(min.x,max.y));
                for (int other = 0; other < Sites.Count && polygon.Count > 0; other++)
                {
                    if (other == site) continue;
                    Vector2 normal = Sites[other].Point - Sites[site].Point;
                    if (normal.sqrMagnitude < .00001f) { if(other < site) polygon.Clear(); continue; }
                    float edge = Vector2.Dot((Sites[other].Point + Sites[site].Point) * .5f, normal);
                    Clip(polygon, scratch, normal, edge);
                    var swap = polygon; polygon = scratch; scratch = swap;
                }
                Rasterize(polygon, site, pixels);
            }
            // Match the actual terrain/water classification at sub-metre resolution.
            for(int z=0;z<Resolution;z++)for(int x=0;x<Resolution;x++)
            {
                int i=z*Resolution+x;
                float wx=min.x+(x+.5f)/Resolution*Bounds.z,wz=min.y+(z+.5f)/Resolution*Bounds.w;
                // Resolve rare scanline edge round-off without assigning a gap to city zero.
                if(pixels[i].a==0&&Sites.Count>0)
                {
                    int nearest=0;float distance=float.MaxValue;var point=new Vector2(wx,wz);
                    for(int j=0;j<Sites.Count;j++){float d=(Sites[j].Point-point).sqrMagnitude;if(d<distance){distance=d;nearest=j;}}
                    pixels[i]=Encode(nearest);
                }
                pixels[i].a=Sites.Count>0&&MapLayout.IsLand(wx,wz)?(byte)255:(byte)0;
            }
            Regions = new Texture2D(Resolution,Resolution,TextureFormat.RGBA32,false,true)
                { name="Territory IDs and coast", filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            Regions.SetPixels32(pixels); Regions.Apply(false,true);
            palette = new Color32[Mathf.NextPowerOfTwo(Mathf.Max(2,Sites.Count))];
            Palette = new Texture2D(palette.Length,1,TextureFormat.RGBA32,false,true)
                { name="Live territory ownership", filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            RefreshOwners();
        }
        public void RefreshOwners()
        {
            bool changed=false;
            for(int i=0;i<Sites.Count;i++)
            {
                Color32 next=VisualFactory.TeamColor(Sites[i].Owner);
                if(!palette[i].Equals(next)){palette[i]=next;changed=true;}
            }
            if(changed){Palette.SetPixels32(palette);Palette.Apply(false,false);}
        }
        static void Clip(List<Vector2> input,List<Vector2> output,Vector2 normal,float edge)
        {
            output.Clear(); if(input.Count==0)return;
            Vector2 a=input[input.Count-1];float da=Vector2.Dot(a,normal)-edge;
            foreach(var b in input)
            {
                float db=Vector2.Dot(b,normal)-edge;
                if((da<=0)!=(db<=0))output.Add(Vector2.LerpUnclamped(a,b,da/(da-db)));
                if(db<=0)output.Add(b);
                a=b;da=db;
            }
        }
        Color32 Encode(int site) => new Color32((byte)(site&255),(byte)(site>>8),(byte)(Sites[site].Country+1),255);
        void Rasterize(List<Vector2> polygon,int site,Color32[] pixels)
        {
            if(polygon.Count<3)return;
            float low=float.MaxValue,high=float.MinValue;
            foreach(var p in polygon){low=Mathf.Min(low,p.y);high=Mathf.Max(high,p.y);}
            int z0=Mathf.Clamp(Mathf.FloorToInt((low-Bounds.y)/Bounds.w*Resolution),0,Resolution-1);
            int z1=Mathf.Clamp(Mathf.CeilToInt((high-Bounds.y)/Bounds.w*Resolution),0,Resolution-1);
            var encoded=Encode(site);
            for(int z=z0;z<=z1;z++)
            {
                float y=Bounds.y+(z+.5f)/Resolution*Bounds.w,l=float.MaxValue,r=float.MinValue;
                for(int j=0;j<polygon.Count;j++)
                {
                    var a=polygon[j];var b=polygon[(j+1)%polygon.Count];
                    if((a.y>y)==(b.y>y))continue;
                    float x=Mathf.LerpUnclamped(a.x,b.x,(y-a.y)/(b.y-a.y));l=Mathf.Min(l,x);r=Mathf.Max(r,x);
                }
                if(l>r)continue;
                int x0=Mathf.Clamp(Mathf.CeilToInt((l-Bounds.x)/Bounds.z*Resolution-.5f),0,Resolution-1);
                int x1=Mathf.Clamp(Mathf.FloorToInt((r-Bounds.x)/Bounds.z*Resolution-.5f),0,Resolution-1);
                for(int x=x0;x<=x1;x++)pixels[z*Resolution+x]=encoded;
            }
        }
        public void Dispose(){Object.Destroy(Regions);Object.Destroy(Palette);}
    }
}
