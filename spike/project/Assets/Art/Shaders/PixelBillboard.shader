// Mistbell HD-2D 像素 Billboard 着色器（Y 轴锁定，URP 17 / Unity 6.3）
// 用途：3D 场景中的像素角色/植被贴片，始终绕 Y 轴面向相机，保持像素边缘清晰。
Shader "Mistbell/PixelBillboard"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _PivotY ("Pivot Y (0=脚底, 0.5=中心)", Range(0,1)) = 0.0
        _ScaleX ("World Width (m)", Float) = 1.0
        _ScaleY ("World Height (m)", Float) = 1.75
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Cutoff;
                float _PivotY;
                float _ScaleX;
                float _ScaleY;
                float _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                // 物体中心（用模型矩阵平移部分，单位四边形顶点在 [-0.5, 0.5]）
                float3 center = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);

                // 只绕 Y 轴朝向相机
                float3 toCam = GetCameraPositionWS() - center;
                toCam.y = 0.0;
                toCam = normalize(toCam + 1e-5);
                float3 rightAxis = cross(float3(0, 1, 0), toCam);

                float2 quad = input.positionOS.xy + 0.5;   // 0..1
                float3 worldPos = center
                    + rightAxis * ((quad.x - 0.5) * _ScaleX)
                    + float3(0, 1, 0) * ((quad.y - _PivotY) * _ScaleY);

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(c.a - _Cutoff);
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
