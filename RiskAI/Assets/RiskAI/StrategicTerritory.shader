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
                // Sample by screen footprint instead of gating the atlas-cell edge.
                // This produces a stable two-pixel country outline at every zoom and
                // catches diagonal borders without outlining water or individual cities.
                float2 pixel=max(_Regions_TexelSize.xy,fwidth(uv));
                float2 coreStep=pixel*.9,outerStep=pixel*2.15;
                float core=max(max(CountryEdge(uv+float2(coreStep.x,0),country),CountryEdge(uv-float2(coreStep.x,0),country)),
                               max(CountryEdge(uv+float2(0,coreStep.y),country),CountryEdge(uv-float2(0,coreStep.y),country)));
                core=max(core,max(CountryEdge(uv+coreStep,country),CountryEdge(uv-coreStep,country)));
                core=max(core,max(CountryEdge(uv+float2(coreStep.x,-coreStep.y),country),CountryEdge(uv+float2(-coreStep.x,coreStep.y),country)));
                float outer=max(max(CountryEdge(uv+float2(outerStep.x,0),country),CountryEdge(uv-float2(outerStep.x,0),country)),
                                max(CountryEdge(uv+float2(0,outerStep.y),country),CountryEdge(uv-float2(0,outerStep.y),country)));
                core*=step(.5,country);outer*=step(.5,country);
                color=lerp(color,half3(.105,.125,.105),outer*.48);
                color=lerp(color,selected>.5?half3(.34,.245,.075):half3(.035,.047,.043),core*.94);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
