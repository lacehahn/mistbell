Shader "Mistbell/PaintedMountain"
{
    Properties
    {
        _MainTex ("Painted mountain RGBA", 2D) = "white" {}
        // 大气透视：按层把整张山体往天空色里推，越远越淡。
        // 只影响 RGB，不动 alpha，避免改变轮廓覆盖。
        _HazeColor ("Haze color", Color) = (0.79, 0.84, 0.89, 1)
        _Haze ("Haze amount", Range(0,1)) = 0
        _HazeBottom ("Extra haze at foot", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _HazeColor;
                float _Haze, _HazeBottom;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 view=TransformWorldToView(TransformObjectToWorld(v.positionOS.xyz));
                o.positionCS=mul(UNITY_MATRIX_P,float4(view,1));
                // Keep the physical card and projected XY at 1800m while putting
                // its depth just inside the unchanged 1500m far plane.
                float depth=min(-view.z,_ProjectionParams.z*.995);
                float4 depthCS=mul(UNITY_MATRIX_P,float4(0,0,-depth,1));
                o.positionCS.z=depthCS.z/depthCS.w*o.positionCS.w;
                o.uv=v.uv; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                clip(c.a-.001);
                // 底部 12% 再加一层更浓的地雾，避免山与雪原之间出现硬切线。
                float footHaze=saturate(1.0-i.uv.y/.12) * _HazeBottom;
                half amount=saturate(_Haze+footHaze);
                c.rgb=lerp(c.rgb,_HazeColor.rgb,amount);
                return c;
            }
            ENDHLSL
        }
    }
}
