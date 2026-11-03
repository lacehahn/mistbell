Shader "Mistbell/TiltShift"
{
    Properties
    {
        _DitherStrength ("Ordered dither strength", Range(0,1)) = 0.5
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _DitherStrength;
                float _FocusCenter;
                float _FocusBand;
                float _Falloff;
                float _MaskPower;
                float _MaxRadius;
            CBUFFER_END

            half4 Palette(float4 color, float2 positionCS, float2 uv)
            {
                const uint bayer[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
                const uint bayer8[64]={
                    0,32,8,40,2,34,10,42, 48,16,56,24,50,18,58,26,
                    12,44,4,36,14,46,6,38, 60,28,52,20,62,30,54,22,
                    3,35,11,43,1,33,9,41, 51,19,59,27,49,17,57,25,
                    15,47,7,39,13,45,5,37, 63,31,55,23,61,29,53,21};
                uint2 pixel=(uint2)positionCS;
                float threshold=(bayer[(pixel.y&3)*4+(pixel.x&3)]+.5)/16.0-.5;
                #if defined(UNITY_COLORSPACE_GAMMA)
                    float3 srgb=saturate(color.rgb);
                #else
                    float3 srgb=LinearToSRGB(saturate(color.rgb));
                #endif
                // MTN15: a 5-bit green dither step alone exceeds the foot's
                // 0.02 luma budget. Preserve continuous color only on the distant
                // backdrop at the mountain foot; opaque church/ground/interior
                // depth keeps the original palette and dithering unchanged.
                float rawDepth=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float backdrop=rawDepth<0.00001?1.0:0.0;
                #else
                    float backdrop=rawDepth>0.99999?1.0:0.0;
                #endif
                float screenY=1.0-positionCS.y/_ScreenParams.y;
                float footBand=smoothstep(.52,.58,screenY)*(1.0-smoothstep(.74,.80,screenY));
                float footMask=backdrop*footBand;
                float ditherStrength=_DitherStrength*(1.0-footMask);
                // The sky and depthless backdrop receive 6-bit / 8x8 at 0.35.
                // Preserve the authored palette of church, gameplay and interiors.
                float steps=lerp(31.0,63.0,backdrop);
                threshold=lerp(threshold,(bayer8[(pixel.y&7)*8+(pixel.x&7)]+.5)/64.0-.5,backdrop);
                ditherStrength=lerp(_DitherStrength,.35,backdrop)*(1.0-footMask);
                float3 continuous=srgb;
                srgb=floor(saturate(srgb+threshold*ditherStrength/steps)*steps+.5)/steps;
                srgb=lerp(srgb,continuous,footMask);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return half4(srgb,color.a);
                #else
                    return half4(SRGBToLinear(srgb),color.a);
                #endif
            }

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
                    return Palette(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv), input.positionCS.xy, uv);

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
                return Palette(color / 24.0, input.positionCS.xy, uv);
            }
            ENDHLSL
        }
    }
}
