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
        _NoiseTex ("Snow breakup (red)", 2D) = "gray" {}
        [HideInInspector] _CloudBand ("Cloud band", Float) = 0
        [HideInInspector] _CloudColor ("Cloud tint", Color) = (0.65,0.72,0.80,0.05)
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _DepthWrite ("Depth write", Float) = 1
        [HideInInspector] _StencilComp ("Stencil compare", Float) = 8
        [HideInInspector] _StencilPass ("Stencil pass", Float) = 2
        _FogFloor ("Additional foot fog", Range(0,1)) = 0.58
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite [_DepthWrite]
            Blend [_SrcBlend] [_DstBlend]
            // 云带只覆盖已绘制的山，避免加色把天空提亮。
            Stencil { Ref 1 Comp [_StencilComp] Pass [_StencilPass] }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorBottom, _ColorTop, _SnowColor, _CloudColor;
                float _CloudBand;
                float _GradLow, _GradHigh, _GradPow;
                float _SnowHeight, _SnowSoftness, _FogFloor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 slopeCrest : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 ridge : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.ridge = float3(input.slopeCrest, input.positionOS.y);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_CloudBand > 0.5)
                {
                    float alpha = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,
                        input.ridge.xy * float2(3, 1)).a;
                    return half4(_CloudColor.rgb, alpha * _CloudColor.a);
                }
                float t = saturate((input.positionWS.y - _GradLow) / max(1, _GradHigh - _GradLow));
                half3 color = lerp(_ColorBottom.rgb, _ColorTop.rgb, pow(t, _GradPow));
                // 两段邻域斜率已在 CPU 平滑；插值后再 smoothstep，形成宽块面。
                float slope = input.ridge.x;
                float3 normal = normalize(float3(slope, 0.65, -0.75));
                float ndotl = saturate(dot(normal, GetMainLight().direction));
                float face = smoothstep(0.25, 0.85, ndotl);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,
                    input.positionWS.xy / float2(115, 85)).r;
                float fine = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,
                    input.positionWS.xy / float2(28, 43)).r;
                float crestDepth = input.ridge.y - input.ridge.z;
                float snowLine = smoothstep(_SnowHeight - _SnowSoftness * 0.5,
                    _SnowHeight + _SnowSoftness * 0.5,
                    input.positionWS.y + (noise - 0.5) * 125 + (fine - 0.5) * 28);
                float crest = 1 - smoothstep(20, 120, crestDepth);
                float snow = snowLine * lerp(0.20, 1.0, face) * lerp(0.25, 1.0, crest);
                color = lerp(color, _SnowColor.rgb, snow);
                color *= 1 + (fine - 0.5) * 0.08;
                // URP fog variants use FOG_LINEAR / FOG_EXP / FOG_EXP2.
                float fog = ComputeFogFactor(TransformWorldToHClip(input.positionWS).z);
                color = MixFog(color, fog);
                // 额外雾化只覆盖渐变高度范围底部 1/3。
                float footFog = 1 - smoothstep(0, 1.0 / 3.0, t);
                color = lerp(color, unity_FogColor.rgb, _FogFloor * footFog);
                // 色彩上限低于天空，雪保持灰蓝而非发光白。
                color *= lerp(0.84, 1.0, face);
                color = min(color, unity_FogColor.rgb * 0.94);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
