#ifndef RISK_WATER_SURFACE
#define RISK_WATER_SURFACE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "MapSurface.hlsl"
#include "NaturalNoise.hlsl"
#include "WaterOptics.hlsl"
float WaterHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float WaterNoise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(WaterHash(a),WaterHash(a+float2(1,0)),f.x),lerp(WaterHash(a+float2(0,1)),WaterHash(a+1),f.x),f.y);}
// Shared opaque bed and optics for river and sea; no painted coastline or mouth disc.
half4 RiskWater(float3 world,float4 screen,float3 surfaceNormal,float2 riverFlow,float rapids,float coastalWeight)
{
 float2 uv=screen.xy/_ScaledScreenParams.xy;
 float raw=SampleSceneDepth(uv);
 #if !UNITY_REVERSED_Z
 raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
 #endif
 float3 bed=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
 float depth=clamp(world.y-bed.y,0,24);
 float2 p=NaturalWarp(world.xz),drift=riverFlow*_Time.y;
 float swell=NaturalNoise(p*.039+_Time.y*float2(.011,-.007));
 float phaseA=NaturalNoise(p*.16+float2(_Time.y*.027,-_Time.y*.019));
 float phaseB=NaturalNoise(p*.27+float2(-_Time.y*.022,_Time.y*.031));
 float a=sin(p.x*1.17+p.y*.73-_Time.y*1.3-drift.x+swell*8+phaseA*13);
 float b=sin(p.x*-.62+p.y*1.51+_Time.y*.87-drift.y+phaseB*17);
 float small=WaterNoise(p*3.2-drift*.6+_Time.y*.15);
 float3 normal=normalize(surfaceNormal+float3(a*.09+(small-.5)*.065,0,b*.075));
 float3 view=GetWorldSpaceNormalizeViewDir(world);
 Light sun=GetMainLight(TransformWorldToShadowCoord(world),world,half4(1,1,1,1));
 float fresnel=pow(1-saturate(dot(normal,view)),4);
 // A continuous geographical tint also covers the far sea beyond the rendered bed.
 // Actual scene depth still controls transmission, contact foam and water/land intersections.
 #if defined(RISK_IMPORTED_WATER)
 float opticalDepth=RiskImportedOpticalDepth(coastalWeight,swell);
 #else
 float coast=max(0,RiskShore(p/max(1,_RiskMapScale))*_RiskMapScale);
 float channel=max(0,-RiskRiverDistance(p));
 float opticalDepth=.4+max(coast*.65,channel*.50);
 #endif
 half3 bottom=SampleSceneColor(uv);
 half3 c=RiskWaterBodyColor(bottom,depth,opticalDepth);
 c=lerp(c,half3(.22,.38,.48),fresnel*.45);
 c+=(a*b*.5+.5)*half3(.003,.009,.014);
 c+=pow(saturate((a+b)*.5),7)*exp(-depth*.8)*half3(.065,.095,.058);
 // A second, broad world-space ripple keeps the water alive at map scale while
 // avoiding geometry, tessellation, or an additional render pass.
 float broadRipple=NaturalNoise(p*.071+float2(_Time.y*.013,-_Time.y*.009));
 c+=(broadRipple-.5)*half3(.018,.053,.046);
 c=lerp(c,c*half3(.80,1.10,1.13),smoothstep(.30,.82,swell)*.55);
 float rippleCrest=pow(saturate(a*.6+b*.4),12)*(.35+.65*WaterNoise(p*.39+17));
 c+=rippleCrest*half3(.017,.030,.035)*(1-fresnel*.5);
 c*=.82+.18*saturate(sun.shadowAttenuation);
 c+=sun.color*pow(saturate(dot(normal,normalize(view+sun.direction))),155)*.055*smoothstep(.3,.7,phaseA);
 float broken=WaterNoise(p*2.1+_Time.y*.17);
 float wave=.5+.5*sin(depth*13-_Time.y*1.8+WaterNoise(p*.8)*2.4);
 float foam=pow(wave,12)*(1-smoothstep(.08,.65,depth))*smoothstep(.005,.07,depth)*broken*.58;
 foam+=rapids*pow(WaterNoise(p*float2(3,1.4)-drift*2.5),5)*.28;
 c=lerp(c,half3(.62,.77,.70),saturate(foam));
 return half4(c,RiskWaterContactOpacity(depth));
}
// Existing authored rivers/sea keep their geographical optical-depth adapter.
half4 RiskWater(float3 world,float4 screen,float3 surfaceNormal,float2 riverFlow,float rapids)
{
 return RiskWater(world,screen,surfaceNormal,riverFlow,rapids,0);
}
#endif
