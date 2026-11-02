Shader "Mistbell/SkyGradient"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.32,0.44,0.60,1)
        _MidColor ("Mid sky", Color) = (0.55,0.66,0.79,1)
        _HorizonColor ("Horizon", Color) = (0.86,0.90,0.94,1)
        _CloudColor ("Cloud", Color) = (0.95,0.96,0.98,1)
        _CloudShadow ("Cloud shade", Color) = (0.66,0.71,0.78,1)
        _CloudAmount ("Cloud amount", Range(0,1)) = 0.45
        _CloudHeight ("Cloud band height", Range(0,1)) = 0.24
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor, _MidColor, _HorizonColor, _CloudColor, _CloudShadow;
                float _CloudAmount, _CloudHeight;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }
            // 2D value noise，用于云带，不需要纹理
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1,0));
                float c = Hash21(i + float2(0,1)), d = Hash21(i + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float v = 0.0, amp = 0.5;
                for (int i = 0; i < 4; i++) { v += amp * ValueNoise(p); p *= 2.03; amp *= 0.5; }
                return v;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);
                float h = saturate(d.y * 0.5 + 0.5);           // 0 底部 1 顶部
                half3 sky = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.42, 0.62, h));
                sky = lerp(sky, _TopColor.rgb, smoothstep(0.55, 0.95, h));
                // 云带：地平线上方一条，边缘柔化
                // 云必须落在 y>0 的天顶一侧；用 h(=y*.5+.5) 会在 h<0.5 处被山挡住而完全看不见。
                float band = smoothstep(0.015, 0.10, d.y) * (1.0 - smoothstep(0.28, 0.72, d.y));
                float2 uv = float2(atan2(d.z, d.x) * 1.1, d.y * 3.2);
                float n = Fbm(uv * 1.6 + float2(3.1, 0.7));
                float mask = smoothstep(0.42, 0.62, n) * band * _CloudAmount;
                half3 cloud = lerp(_CloudShadow.rgb, _CloudColor.rgb, smoothstep(0.55, 0.9, n));
                return half4(lerp(sky, cloud, mask), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
