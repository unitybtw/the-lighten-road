Shader "TheLightenRoad/Archive Book Surface"
{
 Properties {
  _BaseColor("Surface",Color)=(0.8,0.7,0.5,1)
  _InkTex("Page ink",2D)="black"{}
  _HasInk("Use page ink",Float)=0
  _Paper("Paper",Float)=0
  _Metal("Metal",Float)=0
  _TurnShade("Turning page shadow",Vector)=(0,1,0,0)
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass {
  Tags {"LightMode"="SRPDefaultUnlit"}
  Cull Off ZWrite On ZTest LEqual
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  TEXTURE2D(_InkTex); SAMPLER(sampler_InkTex);
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseColor, _TurnShade;float _HasInk,_Paper,_Metal;
  CBUFFER_END
  struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
  struct V {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;float2 uv:TEXCOORD2;float3 local:TEXCOORD3;};
  V Vert(A i){V o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;o.local=i.positionOS.xyz;return o;}
  float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  half4 Frag(V i,bool front:SV_IsFrontFace):SV_Target {
   float3 n=normalize(i.normalWS)*(front?1:-1);
   float3 light=normalize(float3(-.42,.72,-.8));
   float diffuse=floor(saturate(dot(n,light))*5+.5)/5;
   float grain=Hash(floor(i.local.xy*16));
   float3 base=_BaseColor.rgb*(.94+.07*floor(grain*3)/2);
   if(_Paper>.5)
   {
    float margin=step(5.1,abs(i.local.x))+step(3.0,abs(i.local.y));
    base*=1-.045*step(.92,grain)*saturate(margin);
    base*=.99+.014*step(.5,frac(i.local.z*80));
   }
   if(_HasInk>.5){float2 uv=i.uv;if(!front)uv.x=1-uv.x;float4 ink=SAMPLE_TEXTURE2D(_InkTex,sampler_InkTex,uv);base=ink.rgb+base*(1-ink.a);}
   float projected=.5+.24*cos(_TurnShade.x*PI)*_TurnShade.y;
   float shadow=sin(_TurnShade.x*PI)*exp(-pow((i.uv.x-projected)*9,2))*.20*_Paper;
   float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
   float spec=pow(saturate(dot(n,normalize(light+view))),lerp(28,85,_Metal));
   float3 shaded=base*(.69+.34*diffuse-shadow)+step(.55,spec)*lerp(.018,.17,_Metal);
   return half4(floor(saturate(shaded)*31+.5)/31,1);
  }
  ENDHLSL
 }
 }
}
