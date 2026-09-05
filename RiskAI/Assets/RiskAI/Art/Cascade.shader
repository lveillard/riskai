Shader "RiskAI/Cascade"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Pass
  {
   Tags {"LightMode"="UniversalForwardOnly"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #include "WaterSurface.hlsl"
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float2 flow:TEXCOORD1;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;float3 w:TEXCOORD2;float3 n:TEXCOORD3;float2 flow:TEXCOORD4;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;o.flow=a.flow;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 Frag(V i):SV_Target
   {
    float inland=1-smoothstep(.70,.87,i.flow.y);
    half4 c=RiskWater(i.w,i.p,normalize(lerp(float3(0,1,0),i.n,inland)),float2(-.65,.8)*inland*(.8+i.flow.x*2),smoothstep(.18,.55,i.flow.x));
    c.a*=smoothstep(0,.02,i.flow.y)*(1-smoothstep(.74,.835,i.flow.y));
    c.rgb=MixFog(c.rgb,i.fog);return c;
   }
   ENDHLSL
  }
 }
}
