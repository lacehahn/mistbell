Shader "Mistbell/CandleGlow"
{
    Properties { _BaseColor("Flame tint", Color) = (1,.788,.478,.75) _Halo("Halo",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float3 positionOS:POSITION; float2 uv:TEXCOORD0; float3 center:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _Halo;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o;float3 c=TransformObjectToWorld(v.center);
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up=UNITY_MATRIX_I_V._m01_m11_m21;
                o.positionCS=TransformWorldToHClip(c+right*v.positionOS.x+up*v.positionOS.y);
                o.uv=v.uv;return o;
            }
            half4 Frag(Varyings v):SV_Target
            {
                float2 q=v.uv*2-1;
                float halo=pow(saturate(1-dot(q,q)),2);
                float width=lerp(.85,.07,v.uv.y);
                float flame=saturate(1-pow(q.x/width,2))*smoothstep(0,.15,v.uv.y)*smoothstep(1,.65,v.uv.y);
                return half4(_BaseColor.rgb,_BaseColor.a*lerp(flame,halo,_Halo));
            }
            ENDHLSL
        }
    }
}
