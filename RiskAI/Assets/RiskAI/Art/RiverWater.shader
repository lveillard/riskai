Shader "RiskAI/RiverWater"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5"}
  Pass
  {
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "MapSurface.hlsl"
   struct A {float4 p:POSITION;};struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;};
   V Vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.fog=ComputeFogFactor(o.p.z);return o;}
   float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float Noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);}
   half4 Frag(V i):SV_Target
   {
    float2 p=i.w.xz,b=p/max(1,_RiskMapScale);float time=_Time.y;
    float shore=max(0,RiskShore(b)*_RiskMapScale);
    float pond=min(length((b-float2(-11,-30))/float2(8,5)),length((b-float2(16,-4))/float2(3,2)));
    float isPond=1-smoothstep(1,1.15,pond);shore=lerp(shore,max(0,(1-pond)*7),isPond);
    // The analytical coast is interrupted by the carved estuary: suppress its old foam line here.
    float mouth=1-smoothstep(2,7,length(p-_RiskRiver[48].xz));
    shore=lerp(shore,max(shore,3.6),mouth);
    float w1=sin(p.x*1.1+p.y*.8-time*1.2),w2=sin(p.x*-.72+p.y*1.6+time*.8),w3=sin(p.x*3.1+p.y*2.4-time*1.6);
    float3 normal=normalize(float3(w1*.11+w3*.025,1,w2*.09+w3*.018));
    float3 view=GetWorldSpaceNormalizeViewDir(i.w);Light sun=GetMainLight();
    float fresnel=pow(1-saturate(dot(normal,view)),4);
    half3 color=lerp(half3(.035,.31,.32),half3(.012,.095,.225),smoothstep(.2,15,shore));
    color=lerp(color,half3(.025,.12,.115),isPond*.52);
    color=lerp(color,half3(.28,.44,.55)*(Noise(p*.035+float2(time*.002,0))*.22+.8),fresnel*.6);
    color+=(w1*w2*.5+.5)*half3(.008,.017,.023);
    color+=sun.color*pow(saturate(dot(normal,normalize(view+sun.direction))),180)*.28;
    float wave=shore*.95-time*.85+Noise(p*.65)*.75;
    float foam=pow(saturate(.5+.5*sin(wave*5)),13)*(1-smoothstep(.2,2.8,shore));
    foam*=.38+.62*Noise(p*2+time*.12);
    color=lerp(color,half3(.61,.79,.72),saturate(foam*.62+(1-smoothstep(.08,.36,shore))*.3)*(1-mouth));
    color+=pow(saturate((w1+w2+w3)/3),5)*(1-smoothstep(0,5,shore))*half3(.10,.16,.10);
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
 }
}
