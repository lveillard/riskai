Shader "Hidden/RiskAI/Tests/WaterOpticsProbe"
{
 Properties { _Optics("Depth, coast, swell",Vector)=(1,0,.5,0) _Bottom("Opaque background",Vector)=(0,0,0,1) }
 SubShader
 {
  Pass
  {
   ZTest Always ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "../../Art/WaterOptics.hlsl"
   float4 _Optics,_Bottom;
   float4 Vert(float4 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
   half4 Frag():SV_Target
   {
    float opticalDepth=RiskImportedOpticalDepth(_Optics.y,_Optics.z);
    return half4(RiskWaterBodyColor(_Bottom.rgb,_Optics.x,opticalDepth),RiskWaterContactOpacity(_Optics.x));
   }
   ENDHLSL
  }
 }
}
