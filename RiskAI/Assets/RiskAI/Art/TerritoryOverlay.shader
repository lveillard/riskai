Shader "RiskAI/TerritoryOverlay"
{
 Properties { _Tint("Territory tint",Color)=(.8,.64,.2,.12) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   CBUFFER_END
   struct A { float4 position:POSITION; half4 color:COLOR; };
   struct V { float4 position:SV_POSITION; half weight:TEXCOORD0; };
   V Vert(A input) { V output;output.position=TransformObjectToHClip(input.position.xyz);output.weight=input.color.a;return output; }
   half4 Frag(V input):SV_Target { return half4(_Tint.rgb,_Tint.a*input.weight); }
   ENDHLSL
  }
 }
}
