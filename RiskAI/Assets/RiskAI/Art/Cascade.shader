Shader "RiskAI/Cascade"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Pass
  {
   Tags {"LightMode"="UniversalForwardOnly"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float2 flow:TEXCOORD1;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;float3 w:TEXCOORD2;float3 n:TEXCOORD3;float2 flow:TEXCOORD4;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;o.flow=a.flow;o.fog=ComputeFogFactor(o.p.z);return o;}
   float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float Noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);}
   half4 Frag(V i):SV_Target
   {
    float speed=1.1+i.flow.x*2.2,travel=i.uv.y-_Time.y*speed;
    float ripple=sin(travel*4.7+i.uv.x*8)*.025+sin(travel*2.1-i.uv.x*13)*.018;
    float3 n=normalize(i.n+float3(ripple,0,ripple*.65));
    float3 view=GetWorldSpaceNormalizeViewDir(i.w);
    float fresnel=pow(1-saturate(dot(n,view)),4);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    float bank=abs(i.uv.x*2-1),deep=1-smoothstep(.32,.96,bank);
    half3 c=lerp(half3(.048,.14,.11),half3(.009,.073,.091),deep);
    c=lerp(c,half3(.14,.25,.29),fresnel*.35);
    float streak=Noise(float2(i.uv.x*19,travel*1.8));
    float rapids=smoothstep(.18,.55,i.flow.x)*pow(streak,5);
    float margin=smoothstep(.75,.98,bank)*pow(Noise(float2(i.uv.x*9,travel*3)),7)*.18;
    c=lerp(c,half3(.46,.59,.53),saturate(rapids*.45+margin));
    c*=.65+.35*sun.shadowAttenuation;
    c+=sun.color*pow(saturate(dot(n,normalize(sun.direction+view))),120)*.10;
    float edge=smoothstep(0,.16,1-bank);
    float ends=smoothstep(0,.035,i.flow.y)*(1-smoothstep(.88,1,i.flow.y));
    return half4(MixFog(c,i.fog),edge*ends*.99);
   }
   ENDHLSL
  }
 }
}
