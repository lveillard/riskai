Shader "RiskAI/RiverWater"
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
   #include "WaterSurface.hlsl"
   struct A {float4 p:POSITION;};struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 Frag(V i):SV_Target{half4 c=RiskWater(i.w,i.p,float3(0,1,0),0,0);c.rgb=MixFog(c.rgb,i.fog);return c;}
   ENDHLSL
  }
 }
}
