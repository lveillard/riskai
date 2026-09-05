#ifndef RISK_WATER_SURFACE
#define RISK_WATER_SURFACE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "MapSurface.hlsl"
float WaterHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float WaterNoise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(WaterHash(a),WaterHash(a+float2(1,0)),f.x),lerp(WaterHash(a+float2(0,1)),WaterHash(a+1),f.x),f.y);}
// Shared opaque bed and optics for river and sea; no painted coastline or mouth disc.
half4 RiskWater(float3 world,float4 screen,float3 surfaceNormal,float2 riverFlow,float rapids)
{
 float2 uv=screen.xy/_ScaledScreenParams.xy;
 float raw=SampleSceneDepth(uv);
 #if !UNITY_REVERSED_Z
 raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
 #endif
 float3 bed=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
 float depth=clamp(world.y-bed.y,0,24);
 float2 p=world.xz,drift=riverFlow*_Time.y;
 float a=sin(p.x*1.7+p.y*.93-_Time.y*1.3-drift.x);
 float b=sin(p.x*-.82+p.y*2.1+_Time.y*.87-drift.y);
 float small=WaterNoise(p*3.2-drift*.6+_Time.y*.15);
 float3 normal=normalize(surfaceNormal+float3(a*.09+(small-.5)*.065,0,b*.075));
 float3 view=GetWorldSpaceNormalizeViewDir(world);
 Light sun=GetMainLight(TransformWorldToShadowCoord(world));
 float fresnel=pow(1-saturate(dot(normal,view)),4);
 // A continuous geographical tint also covers the far sea beyond the rendered bed.
 // Actual scene depth still controls transmission, contact foam and water/land intersections.
 float coast=max(0,RiskShore(p/max(1,_RiskMapScale))*_RiskMapScale);
 float channel=max(0,-RiskRiverDistance(p));
 float opticalDepth=.4+max(coast*.65,channel*.50);
 half3 tint=lerp(half3(.024,.235,.225),half3(.012,.073,.19),1-exp(-opticalDepth*.24));
 half3 bottom=SampleSceneColor(uv);
 half3 c=lerp(bottom,tint,1-exp(-depth*2.3));
 c=lerp(c,half3(.22,.38,.48),fresnel*.45);
 c+=(a*b*.5+.5)*half3(.003,.009,.014);
 c+=pow(saturate((a+b)*.5),7)*exp(-depth*.8)*half3(.065,.095,.058);
 c*=.76+.24*sun.shadowAttenuation;
 c+=sun.color*pow(saturate(dot(normal,normalize(view+sun.direction))),155)*.085;
 float broken=WaterNoise(p*2.1+_Time.y*.17);
 float wave=.5+.5*sin(depth*13-_Time.y*1.8+WaterNoise(p*.8)*2.4);
 float foam=pow(wave,12)*(1-smoothstep(.08,.65,depth))*smoothstep(.005,.07,depth)*broken*.58;
 foam+=rapids*pow(WaterNoise(p*float2(3,1.4)-drift*2.5),5)*.28;
 c=lerp(c,half3(.62,.77,.70),saturate(foam));
 return half4(c,smoothstep(0,.18,depth));
}
#endif
