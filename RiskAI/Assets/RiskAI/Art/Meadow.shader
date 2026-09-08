Shader "RiskAI/Meadow"
{
 Properties { _Atlas("Painted ground",2D)="white"{} _Biomes("Sand, mud, meadow, cliff",2D)="white"{} _Cliffs("Slate, limestone, gravel, moss",2D)="white"{} }
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);
   TEXTURE2D(_Biomes); SAMPLER(sampler_Biomes);
   TEXTURE2D(_Cliffs); SAMPLER(sampler_Cliffs);
   float4 _RiskCities[32];
   int _RiskCityCount;
   #include "MapSurface.hlsl"
   #include "NaturalNoise.hlsl"
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 coast:TEXCOORD1;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;float3 n:TEXCOORD2;float river:TEXCOORD3;float2 coast:TEXCOORD4;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.fog=ComputeFogFactor(o.p.z);o.river=RiskRiverDistance(o.w.xz);o.coast=a.coast;return o;}
   float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
   half3 Tile(float2 uv,float2 tile){return SAMPLE_TEXTURE2D_GRAD(_Atlas,sampler_Atlas,frac(uv)*.46+tile+.02,ddx(uv)*.46,ddy(uv)*.46).rgb;}
   half3 Biome(float2 uv,float2 tile){return SAMPLE_TEXTURE2D_GRAD(_Biomes,sampler_Biomes,frac(uv)*.46+tile+.02,ddx(uv)*.46,ddy(uv)*.46).rgb;}
   half3 RockTile(float2 uv,float2 tile){return SAMPLE_TEXTURE2D_GRAD(_Cliffs,sampler_Cliffs,frac(uv)*.46+tile+.02,ddx(uv)*.46,ddy(uv)*.46).rgb;}
   half4 Frag(V i):SV_Target
   {
    float2 p=i.w.xz,b=p/max(1,_RiskMapScale);float3 n=normalize(i.n);
    float noise=Noise(p*.42);
    float2 warped=NaturalWarp(p)*.26;
    float2 alternate=float2(warped.x*.73+warped.y*.68,-warped.x*.68+warped.y*.73)*.637+17.3;
    half3 grass=lerp(Tile(warped,float2(0,.5)),Tile(alternate,float2(0,.5)),.3+NaturalNoise(p*.055)*.4)*half3(1.20,1.95,.98);
    // Keep the variation in world space so it stays anchored while the camera pans.
    float meadowMacro=Noise(p*.055);
    grass*=lerp(.91,1.16,meadowMacro);
    grass*=lerp(.978,1.022,.5+.5*sin(p.x*.023+p.y*.031));
    half3 dry=Biome(p*.21,float2(0,0))*half3(.74,.89,.59);
    float meadow=smoothstep(.59,.8,Noise(b*.061+13))*.42;
    half3 color=lerp(grass,dry,meadow);
    float autumn=smoothstep(8,25,b.y)*(1-smoothstep(-32,-14,b.x));
    float arid=smoothstep(20,43,b.x)*(1-smoothstep(-29,-12,b.y));
    arid=max(arid,smoothstep(59,64,b.y));
    color=lerp(color,dry*half3(1.18,1.03,.78),autumn*.55);
    color=lerp(color,Biome(p*.16,float2(0,.5))*half3(1.09,.98,.69),arid*.88);
    // Las Marcas continues into a drier, olive-toned southwest.  Keep this
    // local to the classic layout so Cuatro Riberas retains its own materials.
    float classicSouthwest=(1-step(.5,_RiskExpandedMap))*(1-smoothstep(-48,-42,b.y))*(1-smoothstep(2,10,b.x));
    float dryPatch=smoothstep(.42,.78,Noise(b*.18+float2(31,47)));
    half3 oliveGround=Biome(p*.18,float2(.5,0))*half3(1.04,.87,.54);
    color=lerp(color,oliveGround,classicSouthwest*(.44+.34*dryPatch));
    half3 dirt=Biome(p*.22,float2(.5,.5))*half3(.78,.89,.82);
    float pond=min(length((b-float2(-11,-30))/float2(8,5)),length((b-float2(16,-4))/float2(3,2)));
    float wet=(1-smoothstep(1.02,1.65,pond+(noise-.5)*.2));
    float woodland=1-smoothstep(3,8,min(abs(b.x-(-10+5*sin(b.y*.095))),abs(b.x-(29+6*sin(b.y*.12)))));
    color=lerp(color,dirt,wet*.82);
    color=lerp(color,RockTile(p*.25,float2(.5,0))*half3(.78,1.02,.70),woodland*.64);
    float court=0;
    int cityCount=_RiskCityCount>0?min(_RiskCityCount,32):12;
    for(int c=0;c<32;c++){if(c>=cityCount)break;float d=length((p-_RiskCities[c].xy)*float2(1,.94));court=max(court,1-smoothstep(2.0,4.6,d+(noise-.5)*1.6));}
    color=lerp(color,dirt,court*.75);
    float highland=smoothstep(2.9,6,i.w.y)*smoothstep(5,25,b.x);
    float bare=highland*smoothstep(.41,.7,Noise(b*.09+22));
    color=lerp(color,RockTile(p*.19,float2(.5,.5))*half3(.83,.91,.91),bare*.85);
    float foot=(1-smoothstep(.65,1.9,i.w.y))*smoothstep(.04,.28,i.w.y)*(1-smoothstep(.75,.96,n.y));
    color=lerp(color,RockTile(p*.28,float2(0,0))*half3(.9,.94,.87),max(foot,court*.22));
    float beach=1-smoothstep(.2,3.3,-RiskShore(b)+(noise-.5)*.7);
    half3 bank=lerp(color*half3(.9,1.04,.91),RockTile(p*.22,float2(0,0))*half3(.90,.97,.99),i.coast.y);
    bank=lerp(bank,Biome(p*.19,float2(0,.5))*half3(1.04,1.02,.83),i.coast.x);
    color=lerp(color,bank,beach);
    float rock=1-smoothstep(12,19,length((b-float2(57,-40))*float2(1,.95))+(noise-.5)*5);
    color=lerp(color,Tile(p*.13,float2(0,0))*half3(.72,.88,1.05),rock);
    float river=1-smoothstep(-.3,2.2,i.river+(noise-.5)*.65);
    color=lerp(color,RockTile(p*.24,float2(0,0))*half3(.74,.83,.73),river*.85);
    color=lerp(color,RockTile(p*.18,float2(.5,.5))*half3(1.02,1.07,1.06),smoothstep(9,16,i.w.y));
    // Layered rock is projected vertically across the exposed escarpments.
    float cliff=1-smoothstep(.60,.97,n.y+(noise-.5)*.09);
    float2 weights=pow(abs(n.xz),4);weights/=max(weights.x+weights.y,.001);
    half3 wall=RockTile(i.w.zy*.13,float2(0,.5))*weights.x+RockTile(i.w.xy*.13,float2(0,.5))*weights.y;
    color=lerp(color,wall*half3(.93,.98,1.02),cliff);
    // Let the installed URP helper select the screen/cascade representation so
    // terrain receivers use the same coordinate path as the shadow pass.
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w),i.w,half4(1,1,1,1));
    float terrainShadow=lerp(.24,1,saturate(sun.shadowAttenuation));
    color*=half3(.36,.41,.43)+sun.color*saturate(dot(n,sun.direction))*terrainShadow*.78;
    return half4(MixFog(color,i.fog),1);
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
  Pass
  {
   Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"} ZWrite On
   HLSLPROGRAM
   #pragma vertex DepthNormalsVertex
   #pragma fragment DepthNormalsFragment
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
   ENDHLSL
  }
 }
}
