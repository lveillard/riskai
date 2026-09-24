using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Presentation-only geographic biome field for the imported maps. One small
    /// linear RGBA texture (aridity, cold, lushness, rocky highland) covers the
    /// playable rectangle; the ground shader samples it and CPU art (trees,
    /// ground cover, minimap) reads the same quantized texels. Navigation,
    /// speed, shore and landing rules never read it.
    /// </summary>
    public static class TerrainBiomes
    {
        // Two source cells per texel: regions span tens of metres, the shader adds fine noise.
        const int CellsPerTexel = 2;
        // Horizon fade outside the playable rectangle, in metres.
        public const float HorizonFadeStart = 12f, HorizonFadeEnd = 165f;
        public static readonly Color HorizonColor = new Color(.035f, .075f, .13f);
        static Color32[] field;
        static ImportedMapData fieldMap;
        static int fieldWidth, fieldHeight;
        static float fieldX, fieldZ, fieldStep;

        public readonly struct Biome
        {
            public readonly float Arid, Cold, Lush, Rock;
            public Biome(float arid, float cold, float lush, float rock) { Arid = arid; Cold = cold; Lush = lush; Rock = rock; }
        }

        // Anchor values: aridity, cold, lushness, rocky highland. Country anchors use
        // the imported country positions, so both Europe and New World share them.
        static readonly (string name, float arid, float cold, float lush, float rock)[] CountryBiomes =
        {
            ("Germany",.08f,.08f,.50f,.05f),("Poland",.12f,.14f,.45f,0),("Slovakia",.08f,.16f,.45f,.35f),
            ("Czech Republic",.08f,.10f,.50f,.12f),("Belarus",.10f,.24f,.42f,0),("Slovenia",.05f,.14f,.55f,.40f),
            ("Hungary",.32f,.08f,.28f,0),("Austria",.02f,.32f,.50f,.60f),("Estonia",.04f,.36f,.45f,0),
            ("Latvia",.05f,.30f,.45f,0),("Lithuania",.06f,.25f,.45f,0),("Kaliningrad",.06f,.20f,.45f,0),
            ("Northwestern District (Russia)",.02f,.62f,.30f,.05f),("Ukraine",.44f,.12f,.14f,0),
            ("Serbia",.18f,.08f,.42f,.15f),("North Macedonia",.40f,.05f,.24f,.40f),("Romania",.22f,.10f,.40f,.15f),
            ("Bulgaria",.32f,.06f,.30f,.22f),("Albania",.44f,.03f,.24f,.45f),("Croatia",.28f,.05f,.40f,.28f),
            ("Bosnia-Herzegovina",.12f,.10f,.50f,.45f),("Montenegro",.32f,.05f,.34f,.55f),("Greece",.60f,0,.12f,.38f),
            ("Moldova",.34f,.10f,.28f,0),("Turkey",.56f,.05f,.10f,.34f),("Syria",.84f,0,.02f,.12f),
            ("Lebanon",.55f,0,.20f,.32f),("Israel",.74f,0,.06f,.15f),("Jordan",.94f,0,0,.15f),("Egypt",.98f,0,0,.02f),
            ("Lybia",.92f,0,0,.05f),("Tunisia",.68f,0,.06f,.12f),("Algeria",.72f,0,.05f,.22f),("Morocco",.64f,0,.08f,.40f),
            ("Crete",.62f,0,.10f,.38f),("Sardinia",.56f,0,.14f,.32f),("Cyprus",.68f,0,.06f,.26f),("Malta",.64f,0,.08f,.20f),
            ("Italy",.36f,.02f,.32f,.20f),("Netherlands",0,.05f,.78f,0),("Denmark",.02f,.12f,.62f,0),("Belgium",0,.05f,.70f,0),
            ("France",.10f,.04f,.55f,.05f),("Spain",.64f,.02f,.08f,.22f),("Portugal",.46f,0,.24f,.14f),
            ("England",0,.06f,.80f,0),("Ireland",0,.08f,.95f,0),("Wales",0,.10f,.80f,.32f),("Scotland",.02f,.24f,.55f,.48f),
            ("Iceland",.02f,.78f,.06f,.55f),("Greenland",0,1,0,.35f),("East Greenland",0,1,0,.35f),("West Greenland",0,1,0,.35f),
            ("Disko Bay",0,1,0,.35f),("National Park",0,1,0,.40f),("Avannaata",0,1,0,.35f),("Svalbard",0,.98f,0,.42f),
            ("Norway",0,.50f,.35f,.52f),("Sweden",.02f,.50f,.35f,.12f),("Finland",.02f,.56f,.35f,.05f),
            ("Switzerland",0,.46f,.45f,.78f),("Sami",.02f,.80f,.08f,.25f),("Sicily",.60f,0,.12f,.25f),
            ("Novaya Zemlya",0,.98f,0,.38f),("Crimea",.46f,.05f,.14f,.16f),("Palestine",.82f,0,.04f,.12f),
            ("Armenia",.50f,.14f,.12f,.62f),("Azerbaijan",.60f,.05f,.10f,.30f),("Southern District (Russia)",.46f,.10f,.12f,.05f),
            ("Central District (Russia)",.10f,.36f,.40f,0),("Volga District (Russia)",.24f,.30f,.22f,0),
            ("Siberia",.02f,.80f,.08f,.10f),("Moscov (Russia)",.06f,.40f,.40f,0),
            // New World: Caribbean, the eastern United States and Canada.
            ("Haiti",.10f,0,.95f,.32f),("Dominican Republic",.08f,0,1,.26f),("Belize",0,0,1,.05f),("Yucatan",.16f,0,.94f,0),
            ("Jamaica",.05f,0,1,.20f),("Cuba",.10f,0,.95f,.05f),("Florida",.05f,0,.90f,0),("Alabama",.05f,.03f,.75f,0),
            ("South Carolina",.05f,.04f,.75f,0),("Bermuda",.05f,0,.80f,.10f),("Tennessee",.05f,.08f,.66f,.22f),
            ("North Carolina",.04f,.06f,.70f,.14f),("Kentucky",.04f,.10f,.62f,.16f),("Virginia",.04f,.10f,.62f,.16f),
            ("Delaware",.02f,.12f,.60f,0),("Maryland",.02f,.12f,.62f,0),("West Virginia",.02f,.15f,.60f,.52f),
            ("Ohio",.05f,.18f,.55f,0),("Pennsylvania",.02f,.20f,.58f,.32f),("New York",.02f,.28f,.55f,.22f),
            ("Vermont",0,.40f,.50f,.42f),("Michigan",.02f,.35f,.50f,0),("Maine",0,.46f,.45f,.16f),("Nova Scotia",0,.42f,.45f,.22f),
            ("Quebec",0,.62f,.30f,.15f),("Ontario",.02f,.50f,.35f,.08f),("Newfoundland",0,.64f,.24f,.38f),("Nunavut",0,.88f,.02f,.30f),
        };
        // Named countries that exist twice in New World resolve by side of the Atlantic.
        static readonly Biome GeorgiaCaucasus = new Biome(.14f,.14f,.55f,.48f), GeorgiaUnitedStates = new Biome(.05f,.04f,.74f,0);

        // Extra regional anchors in Europe-map metres (New World shifts them east).
        static readonly (float x, float z, float arid, float cold, float lush, float rock)[] EuropeRegions =
        {
            (-125,-112,.44f,0,.20f,.28f),   // Provence and the French Mediterranean
            (-190,-133,.22f,.38f,.22f,.82f), // Pyrenees
            (-240,-128,.18f,.06f,.55f,.30f), // Galicia and Cantabria
            (-222,-203,.74f,0,.04f,.20f),   // Andalusia
            (-55,-86,0,.48f,.35f,.80f),     // Eastern Alps
            (52,-60,.10f,.22f,.45f,.52f),   // Carpathians
            (282,-58,.10f,.48f,.30f,.78f),  // Caucasus
            (232,-152,.62f,.08f,.04f,.32f), // Anatolian plateau
            (-28,-162,.56f,0,.14f,.28f),    // Southern Italy
            (305,-282,1,0,0,.05f),          // Arabian desert
            (305,-222,.95f,0,0,.08f),       // Syrian desert
            (-20,222,0,.70f,.14f,.52f),     // Northern Norway
            (80,248,0,.80f,.05f,.16f),      // Kola and the White Sea
        };
        static readonly (float x, float z, float arid, float cold, float lush, float rock)[] AmericaRegions =
        {
            (-440,20,.02f,.15f,.55f,.52f),  // Appalachians
            (-420,262,0,.80f,.08f,.22f),    // Northern Quebec and Hudson coast
            (-330,228,0,.66f,.20f,.35f),    // Labrador
        };

        public static void Bake(Transform root)
        {
            var data=MapLayout.IsImported?MapLayout.Imported:null;
            if(data==null)
            {
                Shader.SetGlobalVector("_RiskBiomeGrid",Vector4.zero);
                Shader.SetGlobalVector("_RiskHorizonFade",Vector4.zero);
                return;
            }
            Horizon(data.PlayableBounds,HorizonFadeStart,HorizonFadeEnd);
            EnsureField(data);
            // Mips only serve the blurred skirt beyond the playable edge (level 0 inside).
            var texture=GeneratedResourceOwner.For(root).Track(new Texture2D(fieldWidth,fieldHeight,TextureFormat.RGBA32,true,true)
            {name="Geographic biome field",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp});
            texture.SetPixels32(field);texture.Apply(true,true);
            Shader.SetGlobalTexture("_RiskBiomeField",texture);
            Shader.SetGlobalVector("_RiskBiomeGrid",new Vector4(fieldX,fieldZ,1/fieldStep,1));
            Shader.SetGlobalVector("_RiskBiomeSize",new Vector4(fieldWidth,fieldHeight,1f/fieldWidth,1f/fieldHeight));
            if(ground==null){Shader.SetGlobalVector("_RiskGroundGrid",Vector4.zero);return;}
            // sRGB colour so the shader receives linear albedo; alpha (aridity) stays linear.
            var colour=GeneratedResourceOwner.For(root).Track(new Texture2D(groundWidth,groundHeight,TextureFormat.RGBA32,true,false)
            {name="Satellite ground colour",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp});
            colour.SetPixels32(ground);colour.Apply(true,true);
            Shader.SetGlobalTexture("_RiskGroundColor",colour);
            Shader.SetGlobalVector("_RiskGroundGrid",new Vector4(data.PlayableMinX,data.PlayableMinZ,GroundGain,1));
            Shader.SetGlobalVector("_RiskGroundSize",new Vector4(1f/groundWidth,1f/groundHeight,GroundTexel,0));
        }

        // Satellite ground colour baked by scripts/bake_ground_colors.py (NASA Blue Marble NG,
        // public domain). One texel per W3E cell over the playable rectangle, origin at its minimum.
        const float GroundTexel=2.56f,GroundGain=.68f;
        static Color32[] ground;
        static int groundWidth,groundHeight;
        static void LoadGround(ImportedMapData data)
        {
            ground=null;
            var asset=Resources.Load<TextAsset>("Maps/"+(string.Equals(data.mapId,"NewWorld",StringComparison.OrdinalIgnoreCase)?"NewWorld":"Europe")+"Ground");
            if(!asset)return;
            // RISKAI_SHARED_ASSET: decode scratch, zero lifetime — released in the finally below.
            var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
            try
            {
                if(!decoded.LoadImage(asset.bytes,false))return;
                int expectedWidth=Mathf.RoundToInt((data.PlayableMaxX-data.PlayableMinX)/GroundTexel)+1;
                int expectedHeight=Mathf.RoundToInt((data.PlayableMaxZ-data.PlayableMinZ)/GroundTexel)+1;
                if(decoded.width!=expectedWidth||decoded.height!=expectedHeight)
                {Debug.LogWarning($"RISKAI_GROUND_COLOR size {decoded.width}x{decoded.height} does not match {expectedWidth}x{expectedHeight}; rebake.");return;}
                ground=decoded.GetPixels32();groundWidth=decoded.width;groundHeight=decoded.height;
            }
            finally{if(Application.isPlaying)UnityEngine.Object.Destroy(decoded);else UnityEngine.Object.DestroyImmediate(decoded);}
        }
        static Color SampleGround(float x,float z)
        {
            float gx=Mathf.Clamp((x-fieldMap.PlayableMinX)/GroundTexel,0,groundWidth-1),gz=Mathf.Clamp((z-fieldMap.PlayableMinZ)/GroundTexel,0,groundHeight-1);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),groundWidth-2),iz=Mathf.Min(Mathf.FloorToInt(gz),groundHeight-2),k=iz*groundWidth+ix;
            return Color.Lerp(Color.Lerp(ground[k],ground[k+1],gx-ix),Color.Lerp(ground[k+groundWidth],ground[k+groundWidth+1],gx-ix),gz-iz);
        }

        /// <summary>Fades ground and water beyond a playable rectangle into the camera background.</summary>
        public static void Horizon(Vector4 bounds,float start,float end)
        {
            Shader.SetGlobalColor("_RiskHorizonColor",HorizonColor);
            Shader.SetGlobalVector("_RiskHorizonBounds",bounds);
            Shader.SetGlobalVector("_RiskHorizonFade",new Vector4(start,end,1,0));
        }

        /// <summary>Aridity, cold, lushness and rock at a world point (zero for authored maps).</summary>
        public static Biome Sample(float x,float z)
        {
            var data=MapLayout.IsImported?MapLayout.Imported:null;
            if(data==null)return default;
            EnsureField(data);
            var p=ImportedMapSkirt.Clamp(data,x,z);
            float gx=Mathf.Clamp((p.x-fieldX)/fieldStep,0,fieldWidth-1),gz=Mathf.Clamp((p.y-fieldZ)/fieldStep,0,fieldHeight-1);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),fieldWidth-2),iz=Mathf.Min(Mathf.FloorToInt(gz),fieldHeight-2),k=iz*fieldWidth+ix;
            Color c=Color.Lerp(Color.Lerp(field[k],field[k+1],gx-ix),Color.Lerp(field[k+fieldWidth],field[k+fieldWidth+1],gx-ix),gz-iz);
            return new Biome(c.r,c.g,c.b,c.a);
        }

        /// <summary>The biome field for the configured imported map (row-major from the playable minimum).</summary>
        public static Color32[] Field(out int width,out int height)
        {
            var data=MapLayout.IsImported?MapLayout.Imported:null;
            if(data==null){width=height=0;return null;}
            EnsureField(data);width=fieldWidth;height=fieldHeight;return field;
        }

        /// <summary>Minimap land colour: satellite colour when baked, else the relief ramp recoloured by the same biome texels.</summary>
        public static Color MinimapLand(float x,float z,float height)
        {
            var biome=Sample(x,z);
            if(ground!=null&&MapLayout.IsImported)
            {
                // The same satellite colour as the ground, brightened slightly by relief.
                var p=ImportedMapSkirt.Clamp(MapLayout.Imported,x,z);var satellite=SampleGround(p.x,p.y);satellite.a=1;
                return satellite*Mathf.Lerp(.92f,1.12f,Mathf.Clamp01(height/6.2f));
            }
            var color=Color.Lerp(new Color(.24f,.38f,.20f),new Color(.56f,.63f,.30f),Mathf.Clamp01(height/6.2f));
            color=Color.Lerp(color,new Color(.55f,.52f,.30f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.16f,.5f,biome.Arid)));
            color=Color.Lerp(color,new Color(.80f,.66f,.40f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,.94f,biome.Arid)));
            color=Color.Lerp(color,new Color(.20f,.33f,.25f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.55f,biome.Cold))*(1-Mathf.InverseLerp(.62f,.8f,biome.Cold)));
            color=Color.Lerp(color,new Color(.50f,.52f,.46f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,.8f,biome.Cold)));
            color=Color.Lerp(color,new Color(.86f,.89f,.92f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.84f,.97f,biome.Cold)));
            return Color.Lerp(color,new Color(.52f,.50f,.46f),biome.Rock*.35f);
        }

        static void EnsureField(ImportedMapData data)
        {
            if(field!=null&&fieldMap==data)return;
            var started=System.Diagnostics.Stopwatch.StartNew();
            fieldMap=data;fieldStep=data.cellSize*CellsPerTexel;
            fieldX=data.PlayableMinX;fieldZ=data.PlayableMinZ;
            fieldWidth=Mathf.Max(2,Mathf.CeilToInt((data.PlayableMaxX-fieldX)/fieldStep)+1);
            fieldHeight=Mathf.Max(2,Mathf.CeilToInt((data.PlayableMaxZ-fieldZ)/fieldStep)+1);
            var anchors=Anchors(data,out float europeOffset);
            LoadGround(data);
            field=new Color32[fieldWidth*fieldHeight];
            for(int z=0;z<fieldHeight;z++)for(int x=0;x<fieldWidth;x++)
            {
                float wx=fieldX+x*fieldStep,wz=fieldZ+z*fieldStep;
                // A broad warp makes regional borders irregular instead of circular.
                float px=wx+(Mathf.PerlinNoise(wx*.013f+3.1f,wz*.013f+7.7f)-.5f)*44;
                float pz=wz+(Mathf.PerlinNoise(wx*.013f+41.3f,wz*.013f+19.9f)-.5f)*44;
                var value=Blend(anchors,px,pz);
                float arid=value.x,cold=value.y,lush=value.z,rock=value.w;
                // Latitude cues in Europe coordinates: the Sahara begins just south of the
                // imported North African coast and the far north is always tundra or ice.
                float ex=wx-europeOffset;
                // Satellite aridity (same bake as the ground colour) is authoritative where present.
                // Sampled at the warped point too, so aridity isolines meander instead of
                // following the smooth, nearly straight latitude lines of the georeference.
                if(ground!=null)
                {
                    var satellite=SampleGround(Mathf.Clamp(px,data.PlayableMinX,data.PlayableMaxX),Mathf.Clamp(pz,data.PlayableMinZ,data.PlayableMaxZ));
                    arid=Mathf.Lerp(arid,satellite.a,.85f);
                    // Imaged ice and snow (bright, unsaturated) is always cold ground.
                    float lowest=Mathf.Min(satellite.r,Mathf.Min(satellite.g,satellite.b));
                    cold=Mathf.Max(cold,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.58f,.78f,lowest)));
                }
                else if(ex>-335)arid=Mathf.Lerp(arid,1,.9f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-266,-312,pz)));
                cold=Mathf.Max(cold,.86f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(262,322,pz)));
                float grain=Mathf.PerlinNoise(wx*.05f+11,wz*.05f+5)-.5f;
                arid=Mathf.Clamp01(arid+grain*.10f*(1-arid));lush=Mathf.Clamp01(lush-grain*.12f);
                cold=Mathf.Clamp01(cold+(Mathf.PerlinNoise(wx*.031f+27,wz*.031f+3)-.5f)*.18f*cold*(1-cold)*4);
                field[z*fieldWidth+x]=new Color32(Byte(arid),Byte(cold),Byte(lush),Byte(rock));
            }
            Debug.Log($"RISKAI_BIOME_FIELD map={data.mapId} size={fieldWidth}x{fieldHeight} anchors={anchors.Count} satellite={(ground!=null?groundWidth+"x"+groundHeight:"none")} ms={started.Elapsed.TotalMilliseconds:F1}");
        }
        static byte Byte(float value)=>(byte)Mathf.RoundToInt(Mathf.Clamp01(value)*255);

        struct Anchor { public float x,z,strength; public Vector4 value; }
        static List<Anchor> Anchors(ImportedMapData data,out float europeOffset)
        {
            bool newWorld=string.Equals(data.mapId,"NewWorld",StringComparison.OrdinalIgnoreCase);
            europeOffset=newWorld?163.84f:0;
            var byName=new Dictionary<string,Biome>();
            foreach(var entry in CountryBiomes)byName[entry.name]=new Biome(entry.arid,entry.cold,entry.lush,entry.rock);
            var anchors=new List<Anchor>();
            foreach(var country in data.countries)
            {
                Biome biome;
                if(country.name=="Georgia")biome=newWorld&&country.x<-150?GeorgiaUnitedStates:GeorgiaCaucasus;
                else if(!byName.TryGetValue(country.name,out biome))continue;
                var value=new Vector4(biome.Arid,biome.Cold,biome.Lush,biome.Rock);
                anchors.Add(new Anchor{x=country.x,z=country.z,strength=1,value=value});
            }
            foreach(var region in EuropeRegions)
                anchors.Add(new Anchor{x=region.x+europeOffset,z=region.z,strength=.8f,value=new Vector4(region.arid,region.cold,region.lush,region.rock)});
            if(newWorld)foreach(var region in AmericaRegions)
                anchors.Add(new Anchor{x=region.x,z=region.z,strength=.8f,value=new Vector4(region.arid,region.cold,region.lush,region.rock)});
            return anchors;
        }
        static Vector4 Blend(List<Anchor> anchors,float x,float z)
        {
            // Softened inverse-square weights: each anchor leads its own country while
            // neighbours meet in a wide gradient. Steeper powers approach a Voronoi
            // diagram whose straight bisectors showed up as straight zone borders.
            const float core=34f*34f;
            Vector4 sum=Vector4.zero;float total=0;
            for(int i=0;i<anchors.Count;i++)
            {
                float dx=x-anchors[i].x,dz=z-anchors[i].z,d=dx*dx+dz*dz+core;
                float w=anchors[i].strength/(d*Mathf.Sqrt(d));
                sum+=anchors[i].value*w;total+=w;
            }
            return total>0?sum/total:new Vector4(.1f,.1f,.45f,0);
        }
    }
}
