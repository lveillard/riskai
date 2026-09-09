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
   #include "CoastSurface.hlsl"
   TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);TEXTURE2D(_Cliffs);SAMPLER(sampler_Cliffs);
   TEXTURE2D(_Biomes);SAMPLER(sampler_Biomes);
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
    // Source water flags seed a continuous bank band through the waterline.
    // The shared field changes materials, never land, water or navigation.
    float3 coast=RiskCoastSurface(i.w.xz);
    half shore=saturate(coast.b);
    half sand=RiskSandBlend(coast.r);
    // Keep beach identity in the shared field. Grain and broken stone alter
    // only its surface texture, so noise cannot move the boarding cutoff.
    half grain=NaturalNoise(i.w.xz*3.8);
    half pebbles=NaturalNoise(warped*1.3);
    half strata=.5+.5*sin(i.w.y*8.0+NaturalNoise(warped*.19)*6.0);
    half rockLuma=dot(rock,half3(.30,.59,.11));
    half3 shoreRock=lerp(rock,rockLuma.xxx,.72)*half3(.91,1.00,1.06);
    shoreRock*=.82+strata*.24+pebbles*.19;
    half3 sandTile=SAMPLE_TEXTURE2D_GRAD(_Biomes,sampler_Biomes,frac(warped*.19)*.46+float2(.02,.52),ddx(warped*.19)*.46,ddy(warped*.19)*.46).rgb;
    half3 beachSand=lerp(sandTile*half3(1.08,1.02,.83),half3(.70,.60,.39),.24);
    beachSand*=.88+grain*.18+pebbles*.10;
    half fringe=saturate(shore+(pebbles-.5)*.72*shore*(1-shore));
    half3 dampGreen=color*half3(.70,.88,.77);
    half3 bank=lerp(dampGreen,shoreRock,smoothstep(.15,.8,coast.g));
    bank=lerp(bank,beachSand,sand);
    color=lerp(color,bank,fringe);
    // Beach material eligibility never paints a steep face as a usable landing.
    // Physical NavMesh/slope/ocean checks remain authoritative for safe access.
    half coastalCliff=shore*(1-smoothstep(.75,.86,n.y));
    color=lerp(color,shoreRock,coastalCliff);
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
