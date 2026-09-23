#ifndef RISK_WATER_OPTICS
#define RISK_WATER_OPTICS
// Open sea color follows the continuous coast field, not individual bed triangles.
float RiskImportedOpticalDepth(float coastalWeight,float swell)
{
 return .4+lerp(10.0,1.3,saturate(coastalWeight))*(.9+swell*.2);
}
half3 RiskWaterBodyColor(half3 bottom,float depth,float opticalDepth,float coastalWeight)
{
 half3 tint=lerp(half3(.025,.13,.285),half3(.018,.072,.28),1-exp(-opticalDepth*.24));
 // Open sea keeps its seam-proof transmission. Source-authored shared
 // shallows may reveal their continuous submerged bed, then close before the
 // first deep-water band so coarse terrain tiles cannot print through offshore.
 float openSeaTransmission=exp(-depth*4.8)*(1-smoothstep(.10,.45,depth));
 float shallowBase=lerp(.80,.60,smoothstep(.20,.80,depth));
 float sharedTransmission=shallowBase*(1-smoothstep(.75,1.45,depth));
 float transmission=lerp(openSeaTransmission,max(openSeaTransmission,sharedTransmission),saturate(coastalWeight));
 return lerp(tint,bottom,transmission);
}
half3 RiskWaterBodyColor(half3 bottom,float depth,float opticalDepth)
{
 return RiskWaterBodyColor(bottom,depth,opticalDepth,0);
}
half RiskWaterContactOpacity(float depth) { return smoothstep(0,.18,depth); }
#endif
