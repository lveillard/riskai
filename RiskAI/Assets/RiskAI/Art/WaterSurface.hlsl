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
float2 WaterRotate(float2 p){return float2(p.x*.8-p.y*.6,p.x*.6+p.y*.8);}
float WaterFilteredNoise(float2 p)
{
 float footprint=max(length(ddx(p)),length(ddy(p)));
 return lerp(.5,WaterNoise(p),1-smoothstep(.32,1.05,footprint));
}
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
 float2 rotated=WaterRotate(p);
 float swell=WaterFilteredNoise(rotated*.105+_Time.y*float2(.011,-.007));
 float fineA=WaterFilteredNoise(rotated*1.35-drift*.45+_Time.y*float2(.12,-.08));
 float fineB=WaterFilteredNoise(WaterRotate(rotated)*2.15+drift*.31+_Time.y*float2(-.09,.14));
 float2 slope=float2(fineA-.5,fineB-.5);
 float3 normal=normalize(surfaceNormal+float3(slope.x*.18,0,slope.y*.16));
 float3 view=GetWorldSpaceNormalizeViewDir(world);
 Light sun=GetMainLight();
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
 half3 c=RiskWaterBodyColor(bottom,depth,opticalDepth,coastalWeight);
 c=lerp(c,half3(.22,.38,.48),fresnel*.45);
 float grain=WaterFilteredNoise(WaterRotate(rotated)*3.4-drift*.25+_Time.y*float2(.16,.11));
 c+=(grain-.5)*half3(.008,.017,.025)*(1-fresnel*.5);
 float rippleCrest=smoothstep(.68,.90,fineA*.55+fineB*.45);
 c+=rippleCrest*half3(.010,.025,.042)*(1-fresnel*.5);
 c=lerp(c,c*half3(.91,1.035,1.07),smoothstep(.34,.78,swell)*.22);
 // A continuous body tint for every sea/river. Shadow-cascade boundaries must
 // not print broad straight bands across the water; sun direction lights ripples.
 c*=.96;
 c+=sun.color*pow(saturate(dot(normal,normalize(view+sun.direction))),155)*.045*smoothstep(.38,.76,fineB);
 float broken=WaterFilteredNoise(WaterRotate(p)*2.1+_Time.y*float2(.13,.17));
 float shoreWave=WaterFilteredNoise(rotated*1.18-drift*.6+_Time.y*float2(-.15,.11));
 float foam=smoothstep(.78,.96,shoreWave)*(1-smoothstep(.08,.65,depth))*smoothstep(.005,.07,depth)*broken*.34;
 float rapidNoise=WaterFilteredNoise(WaterRotate(p*float2(3,1.4)-drift*2.5));
 foam+=rapids*pow(saturate(rapidNoise),5)*.28;
 c=lerp(c,half3(.62,.77,.70),saturate(foam));
 return half4(c,RiskWaterContactOpacity(depth));
}
// Existing authored rivers/sea keep their geographical optical-depth adapter.
half4 RiskWater(float3 world,float4 screen,float3 surfaceNormal,float2 riverFlow,float rapids)
{
 return RiskWater(world,screen,surfaceNormal,riverFlow,rapids,0);
}
#endif
