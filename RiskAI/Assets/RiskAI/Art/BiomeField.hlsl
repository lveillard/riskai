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
half4 RiskBiomeData(float2 world)
{
 if(_RiskBiomeGrid.w<=0)return half4(0,0,.45,0);
 float2 texel=(RiskClampPlayable(world)-_RiskBiomeGrid.xy)*_RiskBiomeGrid.z;
 return SAMPLE_TEXTURE2D_LOD(_RiskBiomeField,sampler_RiskBiomeField,(texel+.5)*_RiskBiomeSize.zw,0);
}
half RiskHorizonFade(float2 world)
{
 float2 lo=_RiskHorizonBounds.xy,hi=_RiskHorizonBounds.zw;
 if(_RiskHorizonFade.z<=0||hi.x<=lo.x)return 0;
 float2 outside=max(max(lo-world,world-hi),0);
 return smoothstep(_RiskHorizonFade.x,_RiskHorizonFade.y,length(outside))*_RiskHorizonFade.z;
}
#endif
