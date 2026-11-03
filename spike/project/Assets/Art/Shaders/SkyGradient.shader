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
        _CloudScale ("Cloud size multiplier", Float) = 0.6
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
                float _CloudAmount, _CloudHeight, _CloudScale;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = TransformObjectToWorld(input.positionOS.xyz) - _WorldSpaceCameraPos;
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
            // Direction-space triplanar noise is continuous over the entire sphere.
            // Unlike atan2, it has no +/-pi meridian or UV wrap discontinuity.
            float Clouds(float3 d)
            {
                // Preserve the original 1.1*1.6 angular / 3.2*1.6 vertical
                // frequencies, then reduce cloud size to 60 percent.
                float3 p = d * float3(1.76,5.12,1.76) / max(_CloudScale, .01);
                float3 w = pow(abs(d), 4.0);
                w /= max(dot(w, float3(1,1,1)), .0001);
                return Fbm(p.yz + float2(3.1,.7))*w.x
                     + Fbm(p.zx + float2(7.3,2.1))*w.y
                     + Fbm(p.xy + float2(1.7,5.2))*w.z;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);
                float h = saturate(d.y * 0.5 + 0.5);           // 0 底部 1 顶部
                // Y=0 (h=.5) must actually evaluate to the authored horizon color.
                half3 sky = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.50, 0.70, h));
                sky = lerp(sky, _TopColor.rgb, smoothstep(0.55, 0.95, h));
                // 云带：地平线上方一条，边缘柔化
                // 云必须落在 y>0 的天顶一侧；用 h(=y*.5+.5) 会在 h<0.5 处被山挡住而完全看不见。
                float band = smoothstep(0.015, 0.10, d.y) * (1.0 - smoothstep(0.28, 0.72, d.y));
                float n = Clouds(d);
                float mask = smoothstep(0.42, 0.62, n) * band * _CloudAmount;
                half3 cloud = lerp(_CloudShadow.rgb, _CloudColor.rgb, smoothstep(0.55, 0.9, n));
                sky = lerp(sky, cloud, mask);
                // Apply after clouds too: the cards' transparent feet must expose
                // the same horizon color, not a darker low cloud or sky fragment.
                sky = lerp(sky, _HorizonColor.rgb, 1.0-smoothstep(.10,.24,d.y));
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
