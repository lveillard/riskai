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
   #include "BiomeField.hlsl"
   struct A {float4 p:POSITION;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 Frag(V i):SV_Target
   {
    float3 coast=RiskCoastSurface(i.w.xz)*(1-smoothstep(4,24,RiskOutsidePlayable(i.w.xz)));
    half4 color=RiskWater(i.w,i.p,float3(0,1,0),0,0,coast.b);
    // Fine blue grain keeps source shallows alive without tinting whole W3E
    // quads or drawing a sine ridge along quantized coast-field contours.
    float2 grainPoint=WaterRotate(NaturalWarp(i.w.xz))*1.7+_Time.y*float2(.025,-.031);
    half grain=WaterFilteredNoise(grainPoint);
    half shallow=smoothstep(.08,.82,coast.b);
    color.rgb+=smoothstep(.70,.91,grain)*shallow*half3(.004,.012,.022);
    half horizon=RiskHorizonFade(i.w.xz);
    color.rgb=lerp(color.rgb,_RiskHorizonColor.rgb,horizon);color.a=max(color.a,horizon);
    color.rgb=MixFog(color.rgb,i.fog);return color;
   }
   ENDHLSL
  }
 }
}
