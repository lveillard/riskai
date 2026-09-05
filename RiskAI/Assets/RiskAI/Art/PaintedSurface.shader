Shader "RiskAI/PaintedSurface"
{
 Properties { _Atlas("Atlas",2D)="white"{} _Tint("Tint",Color)=(1,1,1,1) _Tile("Tile",Vector)=(0,.5,0,0) _Scale("World scale",Float)=.33 _Recolor("Recolor",Float)=0 }
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint,_Tile; float _Scale,_Recolor;
 CBUFFER_END
 ENDHLSL
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;half fog:TEXCOORD2;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.fog=ComputeFogFactor(o.p.z);return o;}
   half3 Tile(float2 uv){return SAMPLE_TEXTURE2D_GRAD(_Atlas,sampler_Atlas,frac(uv)*.47+_Tile.xy+.015,ddx(uv)*.47,ddy(uv)*.47).rgb;}
   half4 Frag(V i):SV_Target
   {
    float3 n=normalize(i.n);float3 weights=pow(abs(n),8);weights/=max(dot(weights,float3(1,1,1)),.0001);
    half3 c=Tile(i.w.zy*_Scale)*weights.x+Tile(i.w.xz*_Scale)*weights.y+Tile(i.w.xy*_Scale)*weights.z;
    c=lerp(c*_Tint.rgb,dot(c,half3(.2126,.7152,.0722))*_Tint.rgb*2.7,_Recolor);
    float4 shadowCoord=TransformWorldToShadowCoord(i.w);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.w));
    #endif
    Light sun=GetMainLight(shadowCoord);
    c*=half3(.40,.45,.48)+sun.color*saturate(dot(n,sun.direction))*lerp(.27,1,sun.shadowAttenuation)*.65;
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowPassVertex
   #pragma fragment ShadowPassFragment
   #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex DepthVert
   #pragma fragment DepthFrag
   float4 DepthVert(float4 p:POSITION):SV_POSITION{return TransformObjectToHClip(p.xyz);}
   half4 DepthFrag():SV_Target{return 0;}
   ENDHLSL
  }
 }
}
