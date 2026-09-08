Shader "RiskAI/StrategicTerritory"
{
    Properties { _Regions("Regions",2D)="white"{} _Palette("Owners",2D)="white"{} _ZWrite("Depth",Float)=0 }
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
            CBUFFER_START(UnityPerMaterial)
            float4 _MapBounds,_Regions_TexelSize;float _PaletteWidth,_Overview,_SelectedCountry;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.world.y+=.035;
                o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normalOS);return o;
            }
            float CountryEdge(float2 uv,float country)
            {
                float4 adjacent=SAMPLE_TEXTURE2D(_Regions,sampler_Regions,uv);
                float other=round(adjacent.b*255);
                // Zero is ungrouped. Water and coast edges never become country borders.
                return step(.5,adjacent.a)*step(.5,other)*step(.5,abs(other-country));
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv=(i.world.xz-_MapBounds.xy)/_MapBounds.zw;
                clip(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                float4 cell=SAMPLE_TEXTURE2D(_Regions,sampler_Regions,uv);
                clip(cell.a-.5);
                float country=round(cell.b*255),id=round(cell.r*255)+round(cell.g*255)*256;
                float selected=1-step(.5,abs(country-_SelectedCountry));
                if(_Overview<.5){clip(selected-.5);return half4(1,.79,.28,.21);}
                half3 owner=SAMPLE_TEXTURE2D(_Palette,sampler_Palette,float2((id+.5)/_PaletteWidth,.5)).rgb;
                float relief=.80+.2*saturate(dot(normalize(i.normal),normalize(float3(-.4,1,.3))));
                half3 color=lerp(half3(.22,.29,.25),owner,.68)*relief;
                color=lerp(color,half3(1,.85,.45),selected*.38);
                // The immutable group channel joins all cities sharing one camp,
                // independently of the live ownership palette. Approximately one
                // screen pixel on each side stays legible at strategic zoom.
                float2 borderStep=max(_Regions_TexelSize.xy,fwidth(uv)*1.15);
                float2 withinCell=frac(uv/_Regions_TexelSize.xy);
                float2 feather=max(fwidth(uv)*1.8,float2(.000001,.000001));
                float2 positive=1-smoothstep(0,feather,(1-withinCell)*_Regions_TexelSize.xy);
                float2 negative=1-smoothstep(0,feather,withinCell*_Regions_TexelSize.xy);
                float border=max(max(CountryEdge(uv+float2(borderStep.x,0),country)*positive.x,CountryEdge(uv-float2(borderStep.x,0),country)*negative.x),
                                 max(CountryEdge(uv+float2(0,borderStep.y),country)*positive.y,CountryEdge(uv-float2(0,borderStep.y),country)*negative.y));
                border*=step(.5,country);
                color=lerp(color,half3(.075,.095,.09),border*.85);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
