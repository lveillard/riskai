using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Continuous ground-zone weights for the authored maps (Las Marcas, Cuatro Riberas).
    /// Zones used to be assigned by hard rectangles and by raw terrain height, so the
    /// polygonal cliff outlines and straight ramp edges of the relief leaked into the
    /// material as straight lines. Every weight here is a smooth field: noise-warped
    /// regional masks plus terrain-driven terms computed on a blurred height, sampled
    /// bilinearly by the Meadow shader and read back by CPU art from the same texels.
    /// Presentation only: navigation, pads and shore rules never read it.
    /// </summary>
    public static class FictionalGround
    {
        public const float Texel=2.8f;   // two map units
        public const float Reach=120f;   // covers the continental backdrop
        static Color32[] field;
        static int width,height;
        static float originX,originZ;
        static ScenarioMap fieldScenario=(ScenarioMap)(-1);

        public readonly struct Zones
        {
            /// <summary>Dry Las Marcas south-west, arid east, autumn north-west, exposed stone.</summary>
            public readonly float Dry,Arid,Autumn,Stone;
            public Zones(float dry,float arid,float autumn,float stone){Dry=dry;Arid=arid;Autumn=autumn;Stone=stone;}
        }

        public static void Bake(Transform root)
        {
            Ensure();
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true)
            {name="Authored ground zones",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(field);texture.Apply(false,true);
            GeneratedResourceOwner.For(root).Track(texture);
            Shader.SetGlobalTexture("_RiskGroundZones",texture);
            Shader.SetGlobalVector("_RiskGroundZonesGrid",new Vector4(originX,originZ,1/Texel,1));
            Shader.SetGlobalVector("_RiskGroundZonesSize",new Vector4(width,height,1f/width,1f/height));
        }

        public static Zones Sample(float x,float z)
        {
            if(MapLayout.IsImported)return default;
            Ensure();
            float gx=Mathf.Clamp((x-originX)/Texel,0,width-1),gz=Mathf.Clamp((z-originZ)/Texel,0,height-1);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),width-2),iz=Mathf.Min(Mathf.FloorToInt(gz),height-2),k=iz*width+ix;
            Color c=Color.Lerp(Color.Lerp(field[k],field[k+1],gx-ix),Color.Lerp(field[k+width],field[k+width+1],gx-ix),gz-iz);
            return new Zones(c.r,c.g,c.b,c.a);
        }

        /// <summary>The field for the configured authored map (row-major, origin at the minimum corner).</summary>
        public static Color32[] Field(out int fieldWidth,out int fieldHeight){Ensure();fieldWidth=width;fieldHeight=height;return field;}

        /// <summary>
        /// Dry riverbed (rambla) crossing the Las Marcas secano: 0..1 bed weight. The
        /// Meadow shader evaluates the same curve; it is purely visual.
        /// </summary>
        public static float Riverbed(float x,float z)
        {
            if(MapLayout.IsExpanded||MapLayout.IsImported)return 0;
            float bx=x/MapLayout.Spacing,bz=z/MapLayout.Spacing;
            float centre=-73-.1f*(bx+36)+1.5f*Mathf.Sin(bx*.15f)+.8f*Mathf.Sin(bx*.41f+1.7f);
            float ends=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-74,-66,bx))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-8,2,bx)));
            return (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,2.1f,Mathf.Abs(bz-centre))))*ends;
        }

        static void Ensure()
        {
            if(field!=null&&fieldScenario==MapLayout.Scenario)return;
            var started=System.Diagnostics.Stopwatch.StartNew();
            fieldScenario=MapLayout.Scenario;
            originX=-MapLayout.HalfWidth-Reach;originZ=-MapLayout.HalfDepth-Reach;
            width=Mathf.CeilToInt(2*(MapLayout.HalfWidth+Reach)/Texel)+1;
            height=Mathf.CeilToInt(2*(MapLayout.HalfDepth+Reach)/Texel)+1;
            int size=width*height;
            var relief=new float[size];
            for(int z=0;z<height;z++)for(int x=0;x<width;x++)
            {
                float wx=originX+x*Texel,wz=originZ+z*Texel;
                // Beyond the board the backdrop continues the same height function.
                relief[z*width+x]=Mathf.Max(0,MapLayout.IsImported?0:MapLayout.Height(wx,wz));
            }
            // Plateau membership follows a blurred height: its contours are smooth curves,
            // never the polygon edges of the cliff outlines or the straight ramp sides.
            var blurred=Gaussian(relief,width,height,3.2f);
            // Cliff proximity: relief against a narrow blur, so only the rims light up.
            var local=Gaussian(relief,width,height,1.1f);
            var edge=new float[size];
            for(int i=0;i<size;i++)edge[i]=Mathf.Abs(relief[i]-local[i]);
            edge=Gaussian(edge,width,height,.9f);
            field=new Color32[size];
            for(int z=0;z<height;z++)for(int x=0;x<width;x++)
            {
                int i=z*width+x;float wx=originX+x*Texel,wz=originZ+z*Texel;
                float bx=wx/MapLayout.Spacing,bz=wz/MapLayout.Spacing;
                // Domain warp at the regional scale: zone borders meander instead of running straight.
                float ux=bx+(Fbm(bx*.038f+3.7f,bz*.038f+1.3f)-.5f)*22,uz=bz+(Fbm(bx*.038f+8.1f,bz*.038f+5.9f)-.5f)*22;
                float autumn=Smooth(8,25,uz)*(1-Smooth(-32,-14,ux));
                float arid=Smooth(20,43,ux)*(1-Smooth(-29,-12,uz));
                // The northern archipelago of Las Marcas is sandy; weight it by the island itself.
                if(!MapLayout.IsExpanded)for(int k=0;k<MapLayout.Islands.Length;k++)
                    if(MapLayout.Islands[k].y>60)arid=Mathf.Max(arid,Smooth(-6,4,MapLayout.IslandDistance(wx,wz,k)));
                float dry=MapLayout.IsExpanded?0:(1-Smooth(-53,-37,uz))*(1-Smooth(-5,15,ux));
                // Exposed stone: rocky rims along cliffs and slopes of raised ground, with
                // scattered outcrops; plateau tops stay mostly grass.
                float plateau=Smooth(2.4f,4.8f,blurred[i]+(Fbm(bx*.07f+21,bz*.07f+4)-.5f)*1.6f);
                // Cliff outlines are straight polygon segments, so a rim of constant width reads as
                // a ruler band: its reach and presence vary along the cliff with warped noise.
                float rim=Smooth(.18f,.7f,edge[i]+(Fbm(bx*.21f+2,bz*.21f+6)-.5f)*.25f);
                rim*=Smooth(.3f,.62f,Fbm(bx*.085f+12.7f,bz*.085f+3.9f));
                float outcrop=Smooth(.70f,.84f,Fbm(bx*.11f+44,bz*.11f+17));
                float stone=Mathf.Clamp01(Mathf.Max(rim*(.3f+.7f*plateau),plateau*outcrop*.6f));
                field[i]=new Color32(Byte(dry),Byte(arid),Byte(autumn),Byte(stone));
            }
            Debug.Log($"RISKAI_GROUND_ZONES map={MapLayout.Scenario} size={width}x{height} ms={started.Elapsed.TotalMilliseconds:F1}");
        }

        static float Smooth(float from,float to,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));
        static byte Byte(float value)=>(byte)Mathf.RoundToInt(Mathf.Clamp01(value)*255);
        /// <summary>Two octaves of gradient noise on a rotated lattice.</summary>
        public static float Fbm(float x,float z)
        {
            float a=Mathf.PerlinNoise(x,z);
            float rx=x*.8f-z*.6f,rz=x*.6f+z*.8f;
            return a*.62f+Mathf.PerlinNoise(rx*2.07f+5.3f,rz*2.07f+9.1f)*.38f;
        }
        static float[] Gaussian(float[] source,int w,int h,float sigma)
        {
            int radius=Mathf.CeilToInt(sigma*2.5f);var kernel=new float[radius*2+1];float sum=0;
            for(int i=-radius;i<=radius;i++){kernel[i+radius]=Mathf.Exp(-.5f*i*i/(sigma*sigma));sum+=kernel[i+radius];}
            for(int i=0;i<kernel.Length;i++)kernel[i]/=sum;
            var temp=new float[source.Length];var result=new float[source.Length];
            for(int z=0;z<h;z++)for(int x=0;x<w;x++)
            {
                float acc=0;for(int k=-radius;k<=radius;k++)acc+=kernel[k+radius]*source[z*w+Mathf.Clamp(x+k,0,w-1)];temp[z*w+x]=acc;
            }
            for(int z=0;z<h;z++)for(int x=0;x<w;x++)
            {
                float acc=0;for(int k=-radius;k<=radius;k++)acc+=kernel[k+radius]*temp[Mathf.Clamp(z+k,0,h-1)*w+x];result[z*w+x]=acc;
            }
            return result;
        }
    }
}
