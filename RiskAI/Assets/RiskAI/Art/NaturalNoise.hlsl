#ifndef RISK_NATURAL_NOISE
#define RISK_NATURAL_NOISE
float NaturalHash(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
float NaturalNoise(float2 p)
{
 float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
 return lerp(lerp(NaturalHash(a),NaturalHash(a+float2(1,0)),f.x),lerp(NaturalHash(a+float2(0,1)),NaturalHash(a+1),f.x),f.y);
}
float2 NaturalWarp(float2 p)
{
 return p+(float2(NaturalNoise(p*.041),NaturalNoise(p*.047+31.7))-.5)*5;
}
#endif
