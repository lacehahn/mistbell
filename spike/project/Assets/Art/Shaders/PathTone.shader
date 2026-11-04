Shader "Mistbell/PathTone" {
 Properties {
  [MainTexture] _BaseMap("Bare paving",2D)="white"{}
  _SnowMap("Snow paving",2D)="white"{}
  [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  _ScaleM("Metres per tile",Float)=2.4
  _SnowLine("Blend lower world altitude",Float)=-10
  _BlendBand("Blend width metres",Float)=8
  _Steps("Tread UV only",Float)=0
  _ToneGain("Working-space brightness gain",Float)=1.32
  _CoolTint("Linear cool tint",Color)=(.90,.99,1.12,1)
  _EdgeColor("Snow shoulder",Color)=(.9098,.9373,.9647,1)
  _Cutoff("Cutoff",Float)=.5
  _Cull("Cull",Float)=2
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_SnowMap); SAMPLER(sampler_SnowMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor,_CoolTint,_EdgeColor;
   float _ToneGain;
   float _ScaleM,_SnowLine,_BlendBand,_Steps,_Cutoff,_Cull;
   CBUFFER_END
   struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
   V Vert(A a){V o;o.world=TransformObjectToWorld(a.vertex.xyz);o.pos=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normal);o.uv=a.uv;o.fog=ComputeFogFactor(o.pos.z);return o;}
   half4 Frag(V i):SV_Target {
    half3 albedo;
    if(_Steps>.5) albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    else {
     // Metre projection, independent of every box UV. Two incommensurate scales
     // and opposite rotations spread the residual source seams over different directions.
     float2 p=i.world.xz/_ScaleM;
     float2 a=float2(.7986355*p.x-.6018150*p.y,.6018150*p.x+.7986355*p.y);
     float2 b=float2(.7986355*p.x+.6018150*p.y,-.6018150*p.x+.7986355*p.y)*.713+float2(.317,.613);
     half3 bare=lerp(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,a).rgb,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,b).rgb,.32);
     half3 snow=lerp(SAMPLE_TEXTURE2D(_SnowMap,sampler_SnowMap,a).rgb,SAMPLE_TEXTURE2D(_SnowMap,sampler_SnowMap,b).rgb,.32);
     albedo=lerp(bare,snow,saturate((i.world.y-_SnowLine)/max(_BlendBand,.001)));
    }
    half3 n=normalize(i.normal);Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
    half3 light=max(SampleSH(n),half3(.20,.20,.20))+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
    half3 road=albedo*_BaseColor.rgb*_CoolTint.rgb*_ToneGain;
    half3 surface=lerp(road,SRGBToLinear(_EdgeColor.rgb),smoothstep(0,1,i.uv.x));
    return half4(MixFog(surface*light,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
