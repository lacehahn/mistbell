Shader "Mistbell/RidgeFog"
{
    Properties
    {
        _ColorBottom ("Foot", Color) = (0.235,0.290,0.361,1)
        _ColorTop ("Crest", Color) = (0.424,0.498,0.573,1)
        _GradLow ("Gradient bottom world Y", Float) = -300
        _GradHigh ("Gradient top world Y", Float) = 100
        _GradPow ("Crest concentration", Float) = 1.65
        _SnowHeight ("Snow world Y", Float) = 250
        _SnowSoftness ("Snow transition metres", Float) = 55
        _SnowColor ("Snow", Color) = (0.835,0.882,0.914,1)
        _NoiseTex ("Snow breakup (alpha)", 2D) = "gray" {}
        _FogFloor ("Additional foot fog", Range(0,1)) = 0.58
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorBottom, _ColorTop, _SnowColor;
                float _GradLow, _GradHigh, _GradPow;
                float _SnowHeight, _SnowSoftness, _FogFloor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float t = saturate((input.positionWS.y - _GradLow) / max(1, _GradHigh - _GradLow));
                half3 color = lerp(_ColorBottom.rgb, _ColorTop.rgb, pow(t, _GradPow));
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.positionWS.xy / float2(310, 145)).a;
                float snow = smoothstep(_SnowHeight - _SnowSoftness * 0.5,
                                        _SnowHeight + _SnowSoftness * 0.5,
                                        input.positionWS.y + (noise - 0.35) * _SnowSoftness * 0.3);
                color = lerp(color, _SnowColor.rgb, snow);
                // URP fog variants use FOG_LINEAR / FOG_EXP / FOG_EXP2.
                float fog = ComputeFogFactor(TransformWorldToHClip(input.positionWS).z);
                color = MixFog(color, fog);
                // 额外雾化只覆盖渐变高度范围底部 1/3。
                float footFog = 1 - smoothstep(0, 1.0 / 3.0, t);
                color = lerp(color, unity_FogColor.rgb, _FogFloor * footFog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
