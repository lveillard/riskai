Shader "RiskAI/UnitTeam"
{
    Properties
    {
        [MainTexture] _BaseMap("Original texture",2D)="white"{}
        [MainColor] _BaseColor("Original tint",Color)=(1,1,1,1)
        _TeamColor("Team colour",Color)=(.17,.55,.95,1)
        _SourceHue("Cloth source hue",Range(0,1))=.42
        _HueWidth("Cloth hue width",Range(.01,.35))=.14
        _ForceTeam("Force team cloth",Range(0,1))=0
        _MedicUniform("Healer cream tunic",Range(0,1))=0
        _Cutoff("Alpha cutoff",Range(0,1))=.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex UnitTeamVertex
            #pragma fragment UnitTeamFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD1;
                half3 normalWS:TEXCOORD2;
                float2 uv:TEXCOORD0;
                float fogFactor:TEXCOORD3;
                float4 shadowCoord:TEXCOORD4;
                float bodyHeight:TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _TeamColor;
                half _SourceHue;
                half _HueWidth;
                half _ForceTeam;
                half _Cutoff;
                half _MedicUniform;
            CBUFFER_END

            Varyings UnitTeamVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal=GetVertexNormalInputs(input.normalOS);
                output.positionCS=position.positionCS;
                output.positionWS=position.positionWS;
                output.normalWS=normal.normalWS;
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                output.fogFactor=ComputeFogFactor(position.positionCS.z);
                output.shadowCoord=GetShadowCoord(position);
                output.bodyHeight=input.positionOS.y;
                return output;
            }

            half3 RgbToHsv(half3 color)
            {
                half4 K=half4(0,-1.0/3.0,2.0/3.0,-1);
                half4 p=lerp(half4(color.bg,K.wz),half4(color.gb,K.xy),step(color.b,color.g));
                half4 q=lerp(half4(p.xyw,color.r),half4(color.r,p.yzx),step(p.x,color.r));
                half d=q.x-min(q.w,q.y);half e=.0001;
                return half3(abs(q.z+(q.w-q.y)/(6*d+e)),d/(q.x+e),q.x);
            }
            half3 HsvToRgb(half3 color)
            {
                half3 p=abs(frac(color.xxx+half3(0,2.0/3.0,1.0/3.0))*6-3);
                return color.z*lerp(half3(1,1,1),saturate(p-1),color.y);
            }
            half HueDistance(half a,half b) { return abs(frac(a-b+.5)-.5); }

            half3 TeamSurface(half3 source,float height)
            {
                half3 hsv=RgbToHsv(source);
                half3 team=RgbToHsv(_TeamColor.rgb);
                half mask=1-smoothstep(_HueWidth*.25,_HueWidth,HueDistance(hsv.x,_SourceHue));
                mask*=smoothstep(.16,.42,hsv.y);
                mask=lerp(mask,1,_ForceTeam);
                half3 recolored=HsvToRgb(half3(team.x,max(team.y,hsv.y*.8),hsv.z));
                recolored=lerp(recolored,hsv.z*half3(1.05,1.01,.84),_MedicUniform*(1-_ForceTeam)*(1-smoothstep(1.35,1.52,height)));
                return lerp(source,recolored,mask);
            }

            half4 UnitTeamFragment(Varyings input):SV_Target
            {
                half4 source=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv)*_BaseColor;
                half3 albedo=TeamSurface(source.rgb,input.bodyHeight);
                half3 normal=normalize(input.normalWS);
                Light mainLight=GetMainLight(input.shadowCoord);
                half ndl=saturate(dot(normal,mainLight.direction));
                half3 lighting=SampleSH(normal)+mainLight.color*(ndl*mainLight.distanceAttenuation*mainLight.shadowAttenuation);
                half3 color=albedo*(lighting+.18);
                color=MixFog(color,input.fogFactor);
                return half4(color,source.a);
            }
            ENDHLSL
        }
        // Keep the original textured silhouette in the depth and shadow maps.
        // The URP Lit passes use the compatible _BaseMap/_BaseColor properties above.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}
