#ifndef RISK_MAP_SURFACE
#define RISK_MAP_SURFACE
float _RiskMapScale;
float4 _RiskRiver[57];
float RiskCoast(float x){return 40+.26*x+4*sin(x*.09)+3*sin(x*.2);}
float RiskIsland(float2 b,float4 island,float index)
{
 float2 q=(b-island.xy)/island.zw;float a=atan2(q.y,q.x);
 return (1-length(q)/(1+.075*sin(a*3+index)+.045*cos(a*5)))*min(island.z,island.w);
}
float RiskShore(float2 b)
{
 return min(b.y-RiskCoast(b.x),min(-RiskIsland(b,float4(-47,53,12,8),0),-RiskIsland(b,float4(-8,69,13,9),1)));
}
float RiskRiverDistance(float2 p)
{
 float d=1000;
 if(p.x<27*_RiskMapScale||p.x>58*_RiskMapScale||p.y<25*_RiskMapScale||p.y>58*_RiskMapScale)return d;
 for(int i=0;i<56;i++){float2 a=_RiskRiver[i].xz,e=_RiskRiver[i+1].xz-a;float t=saturate(dot(p-a,e)/max(dot(e,e),.01));d=min(d,length(p-a-e*t)-lerp(_RiskRiver[i].w,_RiskRiver[i+1].w,t));}return d;
}
#endif
