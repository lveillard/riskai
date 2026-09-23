#ifndef RISK_NATURAL_NOISE
#define RISK_NATURAL_NOISE
float NaturalHash(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
float NaturalNoise(float2 p)
{
 float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
 return lerp(lerp(NaturalHash(a),NaturalHash(a+float2(1,0)),f.x),lerp(NaturalHash(a+float2(0,1)),NaturalHash(a+1),f.x),f.y);
}
// Organic low-frequency field: domain-warped value noise plus a rotated octave.
// Thresholding plain value noise exposes its square lattice as straight edges
// and corners, so every zone mask thresholds this instead.
float NaturalFbm(float2 p)
{
 p+=(float2(NaturalNoise(p*.43+3.1),NaturalNoise(p*.43+17.9))-.5)*2.4;
 float2 r=float2(p.x*.8-p.y*.6,p.x*.6+p.y*.8);
 return NaturalNoise(p)*.62+NaturalNoise(r*2.07+5.3)*.38;
}
float2 NaturalWarp(float2 p)
{
 return p+(float2(NaturalNoise(p*.041),NaturalNoise(p*.047+31.7))-.5)*5;
}
#endif
