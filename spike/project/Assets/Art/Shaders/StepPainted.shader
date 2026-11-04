Shader "Mistbell/StepPainted" {
 Properties {
  [MainTexture] _BaseMap("Stone",2D)="white"{}
  _SnowMap("Snow edge",2D)="white"{}
  [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  _Smoothness("Smoothness",Range(0,1))=.08
  _Specular("Specular",Range(0,1))=0
  _SnowAlpha("Snow alpha",Range(0,1))=.65
  _SnowWidth("Snow edge width metres",Float)=.08
  _SnowEnabled("Snow enabled",Float)=1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(_SnowMap); SAMPLER(sampler_SnowMap);
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseColor;
  float _Smoothness,_Specular,_SnowAlpha,_SnowWidth,_SnowEnabled;
  CBUFFER_END
  struct A {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float4 edge:TEXCOORD1;};
  struct V {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float4 edge:TEXCOORD3;half fog:TEXCOORD4;};
  V Vert(A a){V o;o.world=TransformObjectToWorld(a.vertex.xyz);o.pos=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normal);o.uv=a.uv;o.edge=a.edge;o.fog=ComputeFogFactor(o.pos.z);return o;}
  half3 Diffuse(V i){Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));return max(SampleSH(normalize(i.normal)),half3(.2,.2,.2))+sun.color*saturate(dot(normalize(i.normal),sun.direction))*sun.shadowAttenuation;}
  ENDHLSL
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog
   half4 Frag(V i):SV_Target {
    half3 light=Diffuse(i);
    half3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb*light;
    half3 snow=SAMPLE_TEXTURE2D(_SnowMap,sampler_SnowMap,i.edge.xy).rgb;
    // Additive layer: same source*alpha + destination equation as Blend SrcAlpha One.
    // Resolve in one pass so URP cannot skip a second forward pass on this opaque material.
    half mask=smoothstep(.52,.78,min(snow.r,min(snow.g,snow.b)));
    half band=step(0,i.edge.z)*step(i.edge.z,_SnowWidth)*i.edge.w*_SnowEnabled;
    half fade=1-smoothstep(_SnowWidth*.65,_SnowWidth,i.edge.z);
    base+=snow*light*(_SnowAlpha*mask*band*fade);
    return half4(MixFog(base,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
