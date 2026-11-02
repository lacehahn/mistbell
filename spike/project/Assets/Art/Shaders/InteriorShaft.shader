Shader "Mistbell/InteriorShaft"
{
    Properties { _BaseColor("Tint", Color) = (.45,.64,.9,.08) _DstBlend("Destination blend", Float) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 Frag(Varyings v):SV_Target
            {
                float radius=abs(v.uv.x*2-1)/lerp(1,.23,v.uv.y);
                float radial=saturate(1-radius*radius);
                float ends=smoothstep(0,.12,v.uv.y)*smoothstep(0,.12,1-v.uv.y);
                return half4(_BaseColor.rgb,_BaseColor.a*radial*radial*ends);
            }
            ENDHLSL
        }
    }
}
