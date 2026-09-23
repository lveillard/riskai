#ifndef RISK_BIOME_FIELD
#define RISK_BIOME_FIELD
#include "CoastSurface.hlsl"
// Linear RGBA32 geographic field (aridity, cold, lushness, rocky highland) over the
// imported playable rectangle; TerrainBiomes.Sample reads the same texels on the CPU.
TEXTURE2D(_RiskBiomeField); SAMPLER(sampler_RiskBiomeField);
float4 _RiskBiomeGrid;   // origin.xy, 1/texel metres, strength (0 = legacy look)
float4 _RiskBiomeSize;
float4 _RiskHorizonFade; // start, end metres beyond the playable edge, strength
float4 _RiskHorizonBounds; // playable rectangle (min.xy, max.xy) the horizon fade measures from
half4 _RiskHorizonColor;
// Beyond the playable edge the clamped lookup would copy one edge row outward;
// a mip level that grows with distance blurs it along the edge instead.
float RiskSkirtLod(float2 world){return log2(1+RiskOutsidePlayable(world)/5);}
half4 RiskBiomeData(float2 world)
{
 if(_RiskBiomeGrid.w<=0)return half4(0,0,.45,0);
 float2 texel=(RiskClampPlayable(world)-_RiskBiomeGrid.xy)*_RiskBiomeGrid.z;
 return SAMPLE_TEXTURE2D_LOD(_RiskBiomeField,sampler_RiskBiomeField,(texel+.5)*_RiskBiomeSize.zw,RiskSkirtLod(world)*.7);
}
// Satellite ground colour (sRGB RGB, aridity in A) over the same playable rectangle.
TEXTURE2D(_RiskGroundColor); SAMPLER(sampler_RiskGroundColor);
float4 _RiskGroundGrid;  // origin.xy, gain, strength (0 = no satellite grading)
float4 _RiskGroundSize;  // 1/width, 1/height, texel metres, unused
half4 RiskGroundColor(float2 world)
{
 if(_RiskGroundGrid.w<=0)return half4(0,0,0,0);
 float2 texel=(RiskClampPlayable(world)-_RiskGroundGrid.xy)/_RiskGroundSize.z;
 return SAMPLE_TEXTURE2D_LOD(_RiskGroundColor,sampler_RiskGroundColor,(texel+.5)*_RiskGroundSize.xy,RiskSkirtLod(world));
}
half RiskHorizonFade(float2 world)
{
 float2 lo=_RiskHorizonBounds.xy,hi=_RiskHorizonBounds.zw;
 if(_RiskHorizonFade.z<=0||hi.x<=lo.x)return 0;
 float2 outside=max(max(lo-world,world-hi),0);
 return smoothstep(_RiskHorizonFade.x,_RiskHorizonFade.y,length(outside))*_RiskHorizonFade.z;
}
#endif
