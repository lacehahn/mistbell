Shader "Mistbell/PewContact"
{
 Properties { _BaseColor("Shadow",Color)=(0,0,0,.20) }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
 struct V {float4 p:SV_POSITION;};
 float4 _BaseColor;
 V Vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);return v;}
 half4 Frag(V v):SV_Target{return _BaseColor;}
 ENDHLSL
 } }
}
