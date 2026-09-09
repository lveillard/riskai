#ifndef RISK_WATER_OPTICS
#define RISK_WATER_OPTICS
// Open sea color follows the continuous coast field, not individual bed triangles.
float RiskImportedOpticalDepth(float coastalWeight,float swell)
{
 return .4+lerp(10.0,1.3,saturate(coastalWeight))*(.9+swell*.2);
}
half3 RiskWaterBodyColor(half3 bottom,float depth,float opticalDepth)
{
 half3 tint=lerp(half3(.021,.207,.198),half3(.0106,.0642,.1672),1-exp(-opticalDepth*.24));
 // Only the contact shallows transmit the opaque terrain. Beyond 45 cm,
 // differences between source bed tiles or chunk lighting cannot print seams.
 float transmission=exp(-depth*4.8)*(1-smoothstep(.10,.45,depth));
 return lerp(tint,bottom,transmission);
}
half RiskWaterContactOpacity(float depth) { return smoothstep(0,.18,depth); }
#endif
