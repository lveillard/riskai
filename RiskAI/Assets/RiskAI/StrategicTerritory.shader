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
            float4 _MapBounds;float _PaletteWidth,_Overview,_SelectedCountry;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.world.y+=.035;
                o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normalOS);return o;
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
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
