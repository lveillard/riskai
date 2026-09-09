#ifndef RISK_COAST_SURFACE
#define RISK_COAST_SURFACE
// Linear RGBA32, no mipmaps: CPU samples these same quantized texels bilinearly.
TEXTURE2D(_RiskCoastField); SAMPLER(sampler_RiskCoastField);
float4 _RiskCoastGrid;
float4 _RiskCoastSize;
float _RiskSandThreshold;
float3 RiskCoastSurface(float2 world)
{
 float2 texel=(world-_RiskCoastGrid.xy)*_RiskCoastGrid.z;
 float2 uv=(texel+.5)*_RiskCoastSize.zw;
 return SAMPLE_TEXTURE2D_LOD(_RiskCoastField,sampler_RiskCoastField,uv,0).rgb;
}
float RiskSandBlend(float sand)
{
 return smoothstep(_RiskSandThreshold-.05,_RiskSandThreshold+.05,sand);
}
#endif
