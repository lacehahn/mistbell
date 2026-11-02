Shader "Mistbell/RidgeFog"
{
    Properties
    {
        _ColorBottom ("Foot", Color) = (0.235,0.290,0.361,1)
        _ColorTop ("Crest", Color) = (0.424,0.498,0.573,1)
        _GradLow ("Gradient bottom world Y", Float) = -300
        _GradHigh ("Gradient top world Y", Float) = 100
        _GradPow ("Crest concentration", Float) = 1.65
        _SnowHeight ("Snow local height fraction", Float) = 250
        _SnowSoftness ("Snow transition half width", Float) = 55
        _SnowWorldFloor ("Minimum snow altitude", Float) = 125
        _SnowColor ("Snow", Color) = (0.835,0.882,0.914,1)
        _SnowMap ("Pixel snow", 2D) = "white" {}
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
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorBottom, _ColorTop, _SnowColor, _CloudColor;
                float4 _RockShade, _SnowShade, _AirColor;
                float _Aerial;
                float _CloudBand;
                float _GradLow, _GradHigh, _GradPow;
                float _SnowHeight, _SnowSoftness, _SnowWorldFloor, _FogFloor;
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
            float RockNoise(float2 p)
            {
                // Coherent value noise, no texture crop/repeated cap rows on cliff faces.
                float2 cell=floor(p), f=frac(p);
                f=f*f*(3.0-2.0*f);
                float a=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                float b=frac(sin(dot(cell+float2(1,0),float2(127.1,311.7)))*43758.5453);
                float c=frac(sin(dot(cell+float2(0,1),float2(127.1,311.7)))*43758.5453);
                float d=frac(sin(dot(cell+float2(1,1),float2(127.1,311.7)))*43758.5453);
                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }
            float TriplanarNoise(float3 positionWS, float3 weights, float scale, float offset)
            {
                float3 p=positionWS/scale+offset;
                return RockNoise(p.yz)*weights.x + RockNoise(p.xz)*weights.y + RockNoise(p.xy)*weights.z;
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
                float3 weights=pow(abs(n),4.0);
                weights/=max(dot(weights,float3(1,1,1)),1e-5);
                float micro=TriplanarNoise(input.positionWS,weights,11.0,0.0);
                float face=saturate(dot(n,GetMainLight().direction))*(.95+.10*micro);
                // Explicit warm/cool face colours in linear light, independent of exposure.
                float lightFace=smoothstep(.62,.94,face);
                float breakup=sin(input.positionWS.x*.22+sin(input.positionWS.z*.11)*2)*.012;
                // Only the third ridge receives the additional 20% contrast reduction.
                float farRidge = step(.7, _Aerial);
                float snowHeight = lerp(_SnowHeight, .88, farRidge);
                float snowWidth = min(lerp(_SnowSoftness, .003, farRidge), .004);
                float heightGate=smoothstep(snowHeight-snowWidth, snowHeight+snowWidth, input.ridge.x+breakup);
                // Low saddles share a climatic snow floor; local crest UV still controls higher peaks.
                float wobble=6.0*sin(input.positionWS.x*.0043)+4.0*sin(input.positionWS.x*.0117+2.1);
                float altitudeGate=smoothstep(_SnowWorldFloor-.4,_SnowWorldFloor+.4,
                    input.positionWS.y+wobble+breakup*8);
                heightGate=min(heightGate,altitudeGate);
                float slopeGate=smoothstep(.55,.86,n.y);
                float snow=heightGate*slopeGate;
                // Broad cliff shading follows the geometric normals.
                float rockFace=.22+.62*lightFace;
                half3 rock=lerp(_RockShade.rgb,_ColorBottom.rgb,rockFace);
                rock=lerp(rock,half3(.55,.52,.47),lightFace*.35);
                rock = lerp(rock, lerp(half3(.156,.195,.246),half3(.34,.40,.48),rockFace),farRidge);
                // Snow receives broad sky light on shelves; rock retains directional cliff shadows.
                float snowLight=.72+.28*smoothstep(.1,.65,face);
                half3 ice=lerp(_SnowShade.rgb,_SnowColor.rgb,snowLight);
                // Vertical drainage ribs: broad folds plus finer paired creases (7.32/100m).
                // Phase warp is low frequency and bounded; no periodic function of world Y.
                float broad=TriplanarNoise(input.positionWS,weights,47.0,.13);
                float ribPhase=(broad*2.0-1.0)*.8;
                float x=input.positionWS.x;
                float rib=.20*sin(x*.09+ribPhase)
                         +.25*sin(x*.23+ribPhase)
                         +.55*sin(x*.46+ribPhase);
                rock *= 1.0+.12*rib;
                // Correlated mineral grain in three projections, bounded to +/-8%.
                float grain=.55*TriplanarNoise(input.positionWS,weights,.75,.71)
                           +.30*TriplanarNoise(input.positionWS,weights,2.8,.37)
                           +.15*TriplanarNoise(input.positionWS,weights,9.0,.19);
                rock *= 1.0+.08*(grain*2.0-1.0);
                float2 pixelUV = input.positionWS.xy / 6.0;
                half3 snowTex = SAMPLE_TEXTURE2D(_SnowMap,sampler_SnowMap,pixelUV).rgb;
                #if defined(UNITY_COLORSPACE_GAMMA)
                    snowTex=SRGBToLinear(snowTex);
                #endif
                half3 color=lerp(.88*rock,
                    ice*lerp(1,snowTex/half3(.651,.744,.842),.38),snow);
                color=lerp(color,lerp(_RockShade.rgb,_ColorBottom.rgb,.5),.20*farRidge);
                float foot=(1-smoothstep(0,15,input.positionWS.y))*step(0,input.positionWS.y)*.18;
                color=lerp(color,_AirColor.rgb,saturate(_Aerial+_FogFloor+foot));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
