Shader "RiskAI/StrategicTerritory"
{
    Properties { _Regions("Regions",2D)="white"{} _Palette("Owners",2D)="white"{} _Borders("Border distances",2D)="white"{} _BorderRange("Border range (texels)",Float)=16 _ZWrite("Depth",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Regions); SAMPLER(sampler_Regions);
            TEXTURE2D(_Palette); SAMPLER(sampler_Palette);
            TEXTURE2D(_Borders); SAMPLER(sampler_Borders);
            CBUFFER_START(UnityPerMaterial)
            float4 _MapBounds,_Regions_TexelSize;float _PaletteWidth,_Overview,_SelectedCountry,_BorderRange;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.world.y+=.035;
                o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normalOS);return o;
            }
            // Band of a distance given in screen pixels: 1 inside halfWidth, anti-aliased over one pixel.
            float Band(float pixels,float halfWidth){return 1-smoothstep(halfWidth-.5,halfWidth+.5,pixels);}
            // Signed bilinear reconstruction of the border distances (texels): a neighbour texel
            // across a border counts negative, so the zero crossing lands on the sub-texel border
            // instead of flattening into a one-texel plateau. Water texels never make a border.
            float2 BorderDistance(float2 uv,float4 cell)
            {
                float2 size=_Regions_TexelSize.zw,t=uv*size-.5,f=frac(t);
                int2 base=(int2)floor(t);float2 result=0;
                [unroll] for(int k=0;k<4;k++)
                {
                    int2 offset=int2(k&1,k>>1);
                    int2 at=clamp(base+offset,int2(0,0),(int2)size-1);
                    float4 other=LOAD_TEXTURE2D(_Regions,at);
                    float2 d=LOAD_TEXTURE2D(_Borders,at).rg*_BorderRange;
                    float land=step(.5,other.a);
                    float sameCountry=1-step(.5/255,abs(other.b-cell.b));
                    float sameSite=sameCountry*(1-step(.5/255,abs(other.r-cell.r)+abs(other.g-cell.g)));
                    float2 signedDistance=lerp(float2(_BorderRange,_BorderRange),float2(lerp(-d.x,d.x,sameCountry),lerp(-d.y,d.y,sameSite)),land);
                    float weight=(offset.x?f.x:1-f.x)*(offset.y?f.y:1-f.y);
                    result+=signedDistance*weight;
                }
                return abs(result);
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv=(i.world.xz-_MapBounds.xy)/_MapBounds.zw;
                clip(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                float4 cell=SAMPLE_TEXTURE2D(_Regions,sampler_Regions,uv);
                clip(cell.a-.5);
                float country=round(cell.b*255),id=round(cell.r*255)+round(cell.g*255)*256;
                float selected=1-step(.5,abs(country-_SelectedCountry));
                // Texel distances become screen pixels, so lines keep one width at every zoom
                // and follow the sub-texel border without stair-steps.
                float2 distance=BorderDistance(uv,cell);
                float texelWorld=_MapBounds.z*_Regions_TexelSize.x;
                float pixelWorld=max(length(fwidth(i.world.xz))*.70710678,1e-5);
                float2 pixels=distance*texelWorld/pixelWorld;
                float outline=Band(pixels.x,3.2)*step(.5,country);
                float inner=Band(pixels.x,.8)*step(.5,country);
                float cityLine=Band(pixels.y,.6)*(1-outline);
                if(_Overview<.5)
                {
                    clip(selected-.5);
                    float border=saturate(inner+outline*.72);
                    return half4(lerp(half3(1,.79,.28),half3(1,.97,.79),border),lerp(.16,.9,border));
                }
                half4 site=SAMPLE_TEXTURE2D(_Palette,sampler_Palette,float2((id+.5)/_PaletteWidth,.5));
                // Neutral sites (alpha 0) share one muted fill, varied a little per country so
                // unowned neighbours still read as separate countries.
                half3 owner=site.rgb*lerp(.9+.2*frac(country*.618034),1,step(.5,site.a));
                float relief=.84+.16*saturate(dot(normalize(i.normal),normalize(float3(-.4,1,.3))));
                // Canonical owner hue as a flat country fill (neutral countries arrive muted).
                half3 color=owner*relief;
                color=lerp(color,half3(1,.85,.45),selected*.34);
                // Cities of one country: a thin, subtle seam.
                color=lerp(color,color*.62,cityLine*.8);
                // Between countries: dark outline with a thin light core.
                color=lerp(color,half3(.02,.024,.022),outline);
                color=lerp(color,selected>.5?half3(1,.86,.42):half3(.93,.9,.8),inner*.9);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
