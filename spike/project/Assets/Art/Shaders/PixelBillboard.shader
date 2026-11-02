// Mistbell HD-2D 像素 Billboard 着色器（Y 轴锁定，URP 17 / Unity 6.3）
// 用途：3D 场景中的像素角色/植被贴片，始终绕 Y 轴面向相机，保持像素边缘清晰。
Shader "Mistbell/PixelBillboard"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _Cols ("Atlas Columns", Float) = 6
        _Rows ("Atlas Rows", Float) = 4
        _Frame ("Animation Frame", Float) = 0
        _Row ("Direction Row (down/left/right/up)", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _PivotY ("Pivot Y (0=脚底, 0.5=中心)", Range(0,1)) = 0.0
        [HideInInspector] _BottomOrigin ("Bottom-origin mesh", Float) = 0
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

        HLSLINCLUDE
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
                float _BottomOrigin;
                float _Cols;
                float _Rows;
                float _Frame;
                float _Row;
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

            float3 BillboardPosition(Attributes input)
            {
                float3 center = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float3 toCam = GetCameraPositionWS() - center;
                toCam.y = 0;
                toCam = normalize(toCam + 1e-5);
                float3 rightAxis = cross(float3(0,1,0), toCam);
                float2 quad = input.positionOS.xy + 0.5;
                quad.y -= _BottomOrigin * .5;
                return center + rightAxis * ((quad.x-.5)*_ScaleX)
                    + float3(0,1,0) * ((quad.y-_PivotY)*_ScaleY);
            }

            float2 AtlasUV(float2 uv)
            {
                float2 cells = max(float2(_Cols, _Rows), 1);
                float2 cell = float2(clamp(floor(_Frame), 0, cells.x-1), cells.y-1-clamp(floor(_Row), 0, cells.y-1));
                return (saturate(uv) + cell) / cells;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 worldPos = BillboardPosition(input);

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = AtlasUV(input.uv);
                return output;
            }

            half4 ClipDepth(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a - _Cutoff);
                return input.positionCS.z;
            }
        ENDHLSL

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
            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(c.a - _Cutoff);
                return c;
            }
            ENDHLSL
        }
        // URP ShadowCasterPass 的 bias / punctual / reversed-Z 逻辑；顶点替换为同一 BillboardPosition。
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ClipDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 worldPos = BillboardPosition(input);
                float3 normalWS = normalize(GetCameraPositionWS() - float3(unity_ObjectToWorld._m03, worldPos.y, unity_ObjectToWorld._m23) + 1e-5);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - worldPos);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                output.uv = AtlasUV(input.uv);
                return output;
            }
            ENDHLSL
        }
        // URP DepthOnlyPass 的同等 alpha clip，复用 Forward 的位置变换。
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ClipDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        // SSAO 配置为 DepthNormals 时同样覆盖；法线仅供遮蔽，Forward 仍完全 unlit。
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 NormalFrag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a - _Cutoff);
                float3 n = normalize(float3(GetCameraPositionWS().x-unity_ObjectToWorld._m03, 0, GetCameraPositionWS().z-unity_ObjectToWorld._m23) + 1e-5);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * .5 + .5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }

    Fallback Off
}
