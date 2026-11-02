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
        _SnowMap ("Pixel snow", 2D) = "white" {}
        _RockMap ("Pixel rock", 2D) = "white" {}
        _NoiseTex ("Snow breakup (red)", 2D) = "gray" {}
        [HideInInspector] _CloudBand ("Cloud band", Float) = 0
        [HideInInspector] _CloudColor ("Cloud tint", Color) = (0.65,0.72,0.80,0.05)
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _DepthWrite ("Depth write", Float) = 1
        [HideInInspector] _StencilComp ("Stencil compare", Float) = 8
        [HideInInspector] _StencilPass ("Stencil pass", Float) = 2
        _FogFloor ("Additional foot fog", Range(0,1)) = 0
        _Aerial ("Distance air", Range(0,1)) = 0.25
        _RockShade ("Rock shadow", Color) = (.105,.127,.162,1)
        _SnowShade ("Snow shadow", Color) = (.485,.578,.658,1)
        _AirColor ("Sky air", Color) = (.198,.309,.456,1)
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
            TEXTURE2D(_SnowMap); SAMPLER(sampler_SnowMap);
            TEXTURE2D(_RockMap); SAMPLER(sampler_RockMap);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorBottom, _ColorTop, _SnowColor, _CloudColor;
                float4 _RockShade, _SnowShade, _AirColor;
                float _Aerial;
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
                    float textureAlpha=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,float2(uv.x*3,uv.y)).a;
                    float alpha=edge*saturate(textureAlpha*3.0)*_CloudColor.a;
                    half3 tint=_CloudColor.rgb;
                    return half4(tint,alpha);
                }
                float3 n=normalize(input.normalWS);
                float face=saturate(dot(n,GetMainLight().direction));
                // Explicit warm/cool face colours in linear light, independent of exposure.
                float lightFace=smoothstep(.62,.94,face);
                float breakup=sin(input.positionWS.x*.22+sin(input.positionWS.z*.11)*2)*.012;
                // Existing .78 aerial identifies the third ridge; keep the other two unchanged.
                float farRidge = step(.7, _Aerial);
                float snowHeight = lerp(_SnowHeight, .88, farRidge);
                float snowWidth = lerp(_SnowSoftness, .003, farRidge);
                float snow=smoothstep(snowHeight-snowWidth, snowHeight+snowWidth, input.ridge.x+breakup);
                half3 rock=lerp(_RockShade.rgb,_ColorBottom.rgb,lightFace);
                rock = lerp(rock, lerp(half3(.156,.195,.246),half3(.34,.40,.48),lightFace),farRidge);
                half3 ice=lerp(_SnowShade.rgb,_SnowColor.rgb,lightFace);
                float2 pixelUV = input.positionWS.xy / 16.0;
                half3 snowTex = SAMPLE_TEXTURE2D(_SnowMap,sampler_SnowMap,pixelUV).rgb;
                // The snow-flecked top rows belong to rock caps, not vertical mountain faces.
                float2 rockUV=float2(pixelUV.x,frac(pixelUV.y)*.80);
                half3 rockTex = SAMPLE_TEXTURE2D(_RockMap,sampler_RockMap,rockUV).rgb;
                #if defined(UNITY_COLORSPACE_GAMMA)
                    snowTex=SRGBToLinear(snowTex); rockTex=SRGBToLinear(rockTex);
                #endif
                // Retain the existing face lighting and aerial perspective, replacing only albedo detail.
                half3 color=lerp(rock * lerp(1, rockTex / half3(.153,.125,.101), .20),
                    ice * lerp(1, snowTex / half3(.651,.744,.842), .20),snow);
                float foot=(1-smoothstep(0,15,input.positionWS.y))*step(0,input.positionWS.y)*.18;
                color=lerp(color,_AirColor.rgb,saturate(lerp(_Aerial,.24,farRidge)+_FogFloor+foot));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
