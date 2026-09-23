#ifndef RISK_COAST_SURFACE
#define RISK_COAST_SURFACE
// Linear RGBA32, no mipmaps: CPU samples these same quantized texels bilinearly.
TEXTURE2D(_RiskCoastField); SAMPLER(sampler_RiskCoastField);
float4 _RiskCoastGrid;
float4 _RiskCoastSize;
float _RiskSandThreshold;
float _RiskCoastDepthRange;
// Imported playable rectangle (min.xy, max.xy); zero size disables the clamp.
float4 _RiskPlayableBounds;
// Must match ImportedMapSkirt.Clamp: art beyond the playable edge extrudes the
// nearest edge sample, while every point inside the rectangle maps to itself.
float2 RiskClampPlayable(float2 p)
{
 float2 lo=_RiskPlayableBounds.xy,hi=_RiskPlayableBounds.zw;
 if(hi.x<=lo.x||hi.y<=lo.y)return p;
 return clamp(p,lo,hi);
}
float4 RiskCoastSurfaceData(float2 world)
{
 world=RiskClampPlayable(world);
 float2 texel=(world-_RiskCoastGrid.xy)*_RiskCoastGrid.z;
 float2 uv=(texel+.5)*_RiskCoastSize.zw;
 return SAMPLE_TEXTURE2D_LOD(_RiskCoastField,sampler_RiskCoastField,uv,0);
}
float3 RiskCoastSurface(float2 world){return RiskCoastSurfaceData(world).rgb;}
float RiskSandBlend(float sand)
{
 return smoothstep(_RiskSandThreshold-.05,_RiskSandThreshold+.05,sand);
}
#endif
