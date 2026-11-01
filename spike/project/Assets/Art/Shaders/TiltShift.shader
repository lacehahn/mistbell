Shader "Mistbell/TiltShift"
{
    Properties
    {
        _FocusCenter ("Focus Center", Float) = 0.5
        _FocusBand ("Focus Band", Float) = 0.26
        _Falloff ("Falloff", Float) = 0.24
        _MaskPower ("Mask Power", Float) = 1.2
        _MaxRadius ("Maximum Radius (pixels)", Float) = 10.0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "TiltShift"
            ZTest Always
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _FocusCenter;
                float _FocusBand;
                float _Falloff;
                float _MaskPower;
                float _MaxRadius;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Vert already applies _BlitScaleBias through DYNAMIC_SCALING_APPLY_SCALEBIAS.
                float2 uv = input.texcoord;
                float d = abs(uv.y - _FocusCenter);
                float m = saturate(smoothstep(_FocusBand, _FocusBand + _Falloff, d));
                float band = pow(m, _MaskPower);
                float radiusPixels = _MaxRadius * band;
                [branch]
                if (radiusPixels < 0.01)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // _ScreenParams.zw contains 1 + reciprocal size, not texel size.
                float2 pixelSize = rcp(_ScreenParams.xy);
                float4 color = 0;
                [unroll]
                for (int i = 0; i < 24; i++)
                {
                    float a = i * 2.39996323;
                    float r = sqrt((i + 0.5) / 24.0);
                    float2 offset = float2(cos(a), sin(a)) * r * pixelSize * radiusPixels;
                    color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset);
                }
                return color / 24.0;
            }
            ENDHLSL
        }
    }
}
