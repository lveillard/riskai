Shader "RiskAI/ImportedGround"
{
 Properties { _Atlas("Painted ground",2D)="white"{} _Biomes("Biomes",2D)="white"{} _Cliffs("Rock",2D)="white"{} }
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
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "NaturalNoise.hlsl"
   TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);TEXTURE2D(_Cliffs);SAMPLER(sampler_Cliffs);
   struct A {float4 p:POSITION;float3 n:NORMAL;half4 color:COLOR;float3 shore:TEXCOORD1;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;half fog:TEXCOORD2;half3 shore:TEXCOORD3;half4 color:COLOR;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.fog=ComputeFogFactor(o.p.z);o.shore=a.shore;o.color=a.color;return o;}
   half4 Frag(V i):SV_Target
   {
    float2 warped=NaturalWarp(i.w.xz);float2 uv=warped*.25;float3 n=normalize(i.n);
    float2 rotated=float2(uv.x*.73+uv.y*.68,-uv.x*.68+uv.y*.73)*.637+17.3;
    half patch=NaturalNoise(warped*.055);
    half3 grassA=SAMPLE_TEXTURE2D_GRAD(_Atlas,sampler_Atlas,frac(uv)*.46+float2(.02,.52),ddx(uv)*.46,ddy(uv)*.46).rgb;
    half3 grassB=SAMPLE_TEXTURE2D_GRAD(_Atlas,sampler_Atlas,frac(rotated)*.46+float2(.02,.52),ddx(rotated)*.46,ddy(rotated)*.46).rgb;
    half3 grass=lerp(grassA,grassB,.30+patch*.4);
    half3 rock=SAMPLE_TEXTURE2D_GRAD(_Cliffs,sampler_Cliffs,frac(uv*.8)*.46+float2(.02,.02),ddx(uv)*.368,ddy(uv)*.368).rgb;
    half3 color=lerp(grass*1.65,rock*1.25,1-smoothstep(.6,.96,n.y))*i.color.rgb;
    half ridge=saturate(i.color.a);
    color=lerp(color,rock*half3(.88,.91,.92),ridge*.75);
    half snow=smoothstep(.78,.98,ridge)*smoothstep(.6,.96,n.y);
    half3 snowRock=lerp(rock*half3(1.25,1.35,1.4),half3(.78,.82,.84),.25);
    color=lerp(color,snowRock,snow*.45);
    // Source water flags supply a one-cell static bank band. Existing noise
    // breaks its interpolation without moving land, water, or navigation.
    half shore=saturate(i.shore.x+(patch-.5)*.10);
    half sand=smoothstep(.25,.75,i.shore.y);
    half3 bank=lerp(color*half3(.90,1.04,.91),rock*half3(.83,.88,.91),i.shore.z);
    bank=lerp(bank,rock*half3(1.15,1.03,.72),sand);
    color=lerp(color,bank,shore);
    // Overlapping irregular patches break the square repetition without changing source biomes.
    half meadow=NaturalNoise(warped*.18+patch*3);
    color*=lerp(half3(.84,.92,.80),half3(1.10,1.07,.96),patch);
    color*=.90+meadow*.17;
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w),i.w,half4(1,1,1,1));
    color*=half3(.36,.41,.43)+sun.color*saturate(dot(n,sun.direction))*lerp(.24,1,sun.shadowAttenuation)*.78;
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
