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
            struct Attributes { float4 positionOS : POSITION; float2 slopeCrest : TEXCOORD0; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 ridge : TEXCOORD1; float3 normalWS : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.ridge = float3(input.slopeCrest, input.positionOS.y);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_CloudBand > 0.5)
                {
                    float2 uv=input.ridge.xy;
                    float edge=pow(saturate(sin(uv.y*PI)),2.0);
                    float noise=.72+.28*sin(uv.x*65+sin(uv.x*127)*.7+uv.y*4);
                    float alpha=edge*noise*_CloudColor.a;
                    half3 tint=lerp(half3(.83,.79,.73),half3(.64,.74,.84),uv.y);
                    return half4(tint,alpha);
                }
                float3 n=normalize(input.normalWS);
                float face=saturate(dot(n,GetMainLight().direction));
                // 22% light/back contrast from actual triangle normals.
                float shade=.78+.22*face;
                float snow=smoothstep(_SnowHeight-_SnowSoftness, _SnowHeight+_SnowSoftness, input.ridge.x);
                half3 color=lerp(_ColorBottom.rgb, _SnowColor.rgb, snow)*shade;
                float grain=frac(sin(dot(floor(input.positionWS.xz*.4),float2(127.1,311.7)))*43758.5453)-.5;
                color*=1+grain*.025;
                // Mountains are atmospheric scenery beyond the local 80..120m fog volume.
                float foot=(1-smoothstep(-10,30,input.positionWS.y))*.20;
                color=lerp(color,half3(.81,.85,.89),saturate(_FogFloor+foot));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
