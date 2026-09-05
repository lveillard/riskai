Shader "RiskAI/GroundShade"
{
 Properties {_Strength("Strength",Range(0,1))=.5}
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent"}
  Pass
  {
   Blend DstColor Zero ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _Strength;
   CBUFFER_END
   struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
   half4 Frag(V i):SV_Target{float2 p=i.uv*2-1;half shade=1-(1-smoothstep(.08,1,dot(p,p)))*_Strength;return half4(shade,shade,shade,1);}
   ENDHLSL
  }
 }
}
