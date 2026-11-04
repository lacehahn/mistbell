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
        _MistContrast ("Relief retained through mist", Range(0,1)) = 0.18
        _MistBrightness ("Mist silhouette brightness", Range(0,1)) = 0.84
        _RidgeSkyRatio ("Crest light retained against local sky", Range(0,1)) = 0.95
        _SkyTopColor ("Read-only sky zenith sample", Color) = (0.26,0.35,0.48,1)
        _SkyMidColor ("Read-only sky middle sample", Color) = (0.54,0.63,0.73,1)
        _SkyCloudColor ("Read-only sky cloud sample", Color) = (0.95,0.96,0.98,1)
        _SkyCloudShadow ("Read-only sky cloud shade sample", Color) = (0.66,0.71,0.78,1)
        _SkyCloudAmount ("Read-only cloud coverage", Float) = 0.7
        _SkyCloudScale ("Read-only cloud scale", Float) = 0.6
        _HorizonFogColor ("Sampled sky horizon", Color) = (1,1,1,1)
        _HorizonFog ("Vertical horizon veil", Range(0,1)) = 0
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
                float4 _HorizonFogColor;
                float _HorizonFog;
                float _Haze, _HazeBottom;
                float _MistContrast, _MistBrightness;
                float _RidgeSkyRatio;
                float4 _SkyTopColor, _SkyMidColor;
                float4 _SkyCloudColor, _SkyCloudShadow;
                float _SkyCloudAmount, _SkyCloudScale;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 direction:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.direction=TransformObjectToWorld(v.positionOS.xyz)-_WorldSpaceCameraPos;
                float3 view=TransformWorldToView(TransformObjectToWorld(v.positionOS.xyz));
                o.positionCS=mul(UNITY_MATRIX_P,float4(view,1));
                // Keep the physical card and projected XY at 1800m while putting
                // its depth just inside the unchanged 1500m far plane.
                float depth=min(-view.z,_ProjectionParams.z*.995);
                float4 depthCS=mul(UNITY_MATRIX_P,float4(0,0,-depth,1));
                o.positionCS.z=depthCS.z/depthCS.w*o.positionCS.w;
                o.uv=v.uv; return o;
            }
            // Mirror the existing SkyGradient evaluator without changing its
            // shader/material. Mist sees the same local cloud-lit sky as the ridge.
            float SkyHash(float2 p)
            {
                p=frac(p*float2(123.34,456.21));
                p+=dot(p,p+45.32); return frac(p.x*p.y);
            }
            float SkyNoise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(SkyHash(i),SkyHash(i+float2(1,0)),f.x),
                    lerp(SkyHash(i+float2(0,1)),SkyHash(i+float2(1,1)),f.x),f.y);
            }
            float SkyFbm(float2 p)
            {
                float value=0,amp=.5;
                for(int k=0;k<4;k++) { value+=amp*SkyNoise(p);p*=2.03;amp*=.5; }
                return value;
            }
            half3 LocalSky(float3 d)
            {
                float h=saturate(d.y*.5+.5);
                half3 sky=lerp(_HazeColor.rgb,_SkyMidColor.rgb,smoothstep(.50,.70,h));
                sky=lerp(sky,_SkyTopColor.rgb,smoothstep(.55,.95,h));
                float band=smoothstep(.015,.10,d.y)*(1-smoothstep(.28,.72,d.y));
                float3 p=d*float3(1.76,5.12,1.76)/max(_SkyCloudScale,.01);
                float3 w=pow(abs(d),4); w/=max(dot(w,float3(1,1,1)),.0001);
                float n=SkyFbm(p.yz+float2(3.1,.7))*w.x
                    +SkyFbm(p.zx+float2(7.3,2.1))*w.y
                    +SkyFbm(p.xy+float2(1.7,5.2))*w.z;
                float mask=smoothstep(.42,.62,n)*band*_SkyCloudAmount;
                half3 cloud=lerp(_SkyCloudShadow.rgb,_SkyCloudColor.rgb,smoothstep(.55,.9,n));
                sky=lerp(sky,cloud,mask);
                return lerp(sky,_HazeColor.rgb,1-smoothstep(.10,.24,d.y));
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                clip(c.a-.001);
                // 底部 12% 再加一层更浓的地雾，避免山与雪原之间出现硬切线。
                float footHaze=saturate(1.0-i.uv.y/.12) * _HazeBottom;
                half amount=saturate(_Haze+footHaze);
                c.rgb=lerp(c.rgb,_HazeColor.rgb,amount);
                // Compress snow, rock and relief together: no bright cap band
                // survives above the atmospheric silhouette. Alpha keeps the ridge.
                c.rgb=lerp(_HazeColor.rgb*_MistBrightness,c.rgb,_MistContrast);
                float verticalFog=(1.0-smoothstep(.10,.24,normalize(i.direction).y))*_HorizonFog;
                c.rgb=lerp(c.rgb,_HorizonFogColor.rgb,verticalFog);
                // A distant ridge stays just below the local sky gradient instead
                // of becoming a white cutout against its darker upper sky.
                half3 sky=LocalSky(normalize(i.direction));
                // Blend the high ridge into its local sky over a broad vertical
                // interval. Keep the lower body lit by the horizon/foot mist;
                // clamping the entire card to the upper sky made it too dark.
                float ridgeVeil=smoothstep(.50,.82,i.uv.y);
                c.rgb=lerp(c.rgb,min(c.rgb,sky*_RidgeSkyRatio),ridgeVeil);
                return c;
            }
            ENDHLSL
        }
    }
}
