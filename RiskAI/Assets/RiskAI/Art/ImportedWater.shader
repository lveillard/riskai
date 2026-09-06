Shader "RiskAI/ImportedWater"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10"}
  Pass
  {
   Tags {"LightMode"="UniversalForwardOnly"} ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #define RISK_IMPORTED_WATER 1
   #include "WaterSurface.hlsl"
   struct A {float4 p:POSITION;half4 color:COLOR;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;half depth:TEXCOORD2;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.fog=ComputeFogFactor(o.p.z);o.depth=a.color.a;return o;}
   half4 Frag(V i):SV_Target
   {
    half4 color=RiskWater(i.w,i.p,float3(0,1,0),0,0);
    color.rgb=MixFog(color.rgb,i.fog);return color;
   }
   ENDHLSL
  }
 }
}
