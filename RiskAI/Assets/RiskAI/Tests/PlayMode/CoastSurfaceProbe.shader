Shader "Hidden/RiskAI/Tests/CoastSurfaceProbe"
{
 Properties { _WorldPoint("World sample",Vector)=(0,0,0,0) }
 SubShader
 {
  Pass
  {
   ZTest Always ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "../../Art/CoastSurface.hlsl"
   float4 _WorldPoint;
   float4 Vert(float4 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
   float4 Frag():SV_Target
   {
    float3 coast=RiskCoastSurface(_WorldPoint.xy);
    return float4(coast.rg,RiskSandBlend(coast.r),1);
   }
   ENDHLSL
  }
 }
}
