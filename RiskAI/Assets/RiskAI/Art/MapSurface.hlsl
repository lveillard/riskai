#ifndef RISK_MAP_SURFACE
#define RISK_MAP_SURFACE
float _RiskMapScale;
float _RiskExpandedMap;
int _RiskIslandCount;
float4 _RiskIslands[8];
float4 _RiskCoastParams0;
float4 _RiskCoastParams1;
// World-space x/z bounds uploaded by TerrainHydrology with maxWidth + 6 margin.
// A zero-sized value keeps the legacy producer compatible until it uploads bounds.
float4 _RiskRiverBounds;
float4 _RiskRiver[57];
float4 RiskLegacyIsland(int index){return index==0?float4(-47,53,12,8):float4(-8,69,13,9);}
float4 RiskIslandData(int index){return _RiskExpandedMap>.5?_RiskIslands[index]:RiskLegacyIsland(index);}
float RiskCoast(float x)
{
 float4 p0=_RiskExpandedMap>.5?_RiskCoastParams0:float4(40,.26,4,3);
 float4 p1=_RiskExpandedMap>.5?_RiskCoastParams1:float4(.09,.2,0,0);
 return p0.x+p0.y*x+p0.z*sin(x*p1.x+p1.z)+p0.w*sin(x*p1.y+p1.w);
}
float RiskIsland(float2 b,float4 island,float index)
{
 float2 q=(b-island.xy)/island.zw;float a=atan2(q.y,q.x);
 return (1-length(q)/(1+.075*sin(a*3+index)+.045*cos(a*5)))*min(island.z,island.w);
}
float RiskShore(float2 b)
{
 int count=_RiskExpandedMap>.5?clamp(_RiskIslandCount,0,8):2;
 float shore=b.y-RiskCoast(b.x);
 for(int i=0;i<8;i++)
 {
  if(i>=count)break;
  shore=min(shore,-RiskIsland(b,RiskIslandData(i),i));
 }
 return shore;
}
float RiskRiverDistance(float2 p)
{
 float2 size=_RiskRiverBounds.zw-_RiskRiverBounds.xy;
 if(size.x>.001&&size.y>.001&&(p.x<_RiskRiverBounds.x||p.x>_RiskRiverBounds.z||p.y<_RiskRiverBounds.y||p.y>_RiskRiverBounds.w))return 1e5;
 float d=1e5;
 for(int i=0;i<56;i++){float2 a=_RiskRiver[i].xz,e=_RiskRiver[i+1].xz-a;float t=saturate(dot(p-a,e)/max(dot(e,e),.01));d=min(d,length(p-a-e*t)-lerp(_RiskRiver[i].w,_RiskRiver[i+1].w,t));}return d;
}
#endif
