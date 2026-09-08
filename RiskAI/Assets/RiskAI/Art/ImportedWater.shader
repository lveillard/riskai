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
   #include "CoastSurface.hlsl"
   struct A {float4 p:POSITION;half4 color:COLOR;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;half depth:TEXCOORD2;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.fog=ComputeFogFactor(o.p.z);o.depth=a.color.a;return o;}
   half4 Frag(V i):SV_Target
   {
    float3 coast=RiskCoastSurface(i.w.xz);
    half4 color=RiskWater(i.w,i.p,float3(0,1,0),0,0,coast.b);
    // The source coast field also softens the water side of the bank. This
    // touches existing water fragments only: no new water coverage or depth.
    float2 warped=NaturalWarp(i.w.xz);
    half washNoise=NaturalNoise(warped*.67+float2(_Time.y*.018,-_Time.y*.025));
    half shallow=coast.b*(1-smoothstep(.15,.95,i.depth));
    half sand=RiskSandBlend(coast.r);
    half3 shallows=lerp(half3(.065,.215,.205),half3(.20,.32,.255),sand)*.88;
    color.rgb=lerp(color.rgb,shallows,shallow*(.46+washNoise*.22));
    // A sparse wash on the water side breaks long straight-looking optical
    // edges without a solid white contour or foam across open/deep water.
    half wash=sin(coast.b*15.0+washNoise*5.0-_Time.y*.85)*.5+.5;
    half foam=pow(wash,9)*smoothstep(.30,.82,coast.b)*shallow;
    foam*=smoothstep(.38,.78,washNoise)*.30;
    color.rgb=lerp(color.rgb,half3(.57,.69,.59),foam);
    color.rgb=MixFog(color.rgb,i.fog);return color;
   }
   ENDHLSL
  }
 }
}
