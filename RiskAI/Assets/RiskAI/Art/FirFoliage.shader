Shader "RiskAI/FirFoliage"
{
 Properties { _Atlas("Fir bough",2D)="white"{} }
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);
 struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
 struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float2 uv:TEXCOORD1;half4 color:TEXCOORD2;half fog:TEXCOORD3;float3 n:TEXCOORD4;};
 V Vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.uv=a.uv;o.color=a.color;o.n=TransformObjectToWorldNormal(a.n);o.fog=ComputeFogFactor(o.p.z);return o;}
 half3 Bough(float2 uv)
 {
  half4 c=SAMPLE_TEXTURE2D(_Atlas,sampler_Atlas,uv);
  clip(min(c.a,max(c.r,c.g))-.012);
  return c.rgb*half3(.95,1.15,1.05);
 }
 ENDHLSL
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
  Cull Off
  Pass
  {
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   half4 Frag(V i):SV_Target
   {
    half3 c=Bough(i.uv)*i.color.rgb;
    float4 sc=TransformWorldToShadowCoord(i.w);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    sc=ComputeScreenPos(TransformWorldToHClip(i.w));
    #endif
    Light sun=GetMainLight(sc);
    half diffuse=.6+.4*abs(dot(normalize(i.n),sun.direction));
    c*=half3(.38,.45,.46)+sun.color*diffuse*lerp(.14,1,sun.shadowAttenuation)*.8;
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment DepthFrag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
   float3 _LightDirection;
   V ShadowVert(A a){V o=Vert(a);o.p=TransformWorldToHClip(ApplyShadowBias(o.w,normalize(o.n),_LightDirection));return o;}
   half4 DepthFrag(V i):SV_Target{Bough(i.uv);return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment DepthFrag
   half4 DepthFrag(V i):SV_Target{Bough(i.uv);return 0;}
   ENDHLSL
  }
 }
}
