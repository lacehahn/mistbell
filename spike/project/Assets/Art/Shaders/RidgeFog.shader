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
            float TriplanarNoise(float3 positionWS, float3 weights, float scale, float offset)
            {
                float3 p = positionWS / scale;
                return SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p.yz+offset).r * weights.x
                     + SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p.xz+offset).r * weights.y
                     + SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p.xy+offset).r * weights.z;
            }
            float2 RockUV(float2 projected)
            {
                // Keep the snow-flecked cap rows out of all three rock projections.
                return float2(projected.x, frac(projected.y*.35)*.80);
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
                // Existing .78 aerial identifies the third ridge; keep the other two unchanged.
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
                // A narrow exposed strip immediately below the snowline reads as cornice shadow.
                float edgeLocal=step(snowHeight-.022,input.ridge.x+breakup)*altitudeGate;
                float edgeAltitude=step(_SnowWorldFloor-2,input.positionWS.y)*step(_SnowWorldFloor,input.positionWS.y+2);
                float underEdge=(1-heightGate)*max(edgeLocal,edgeAltitude)*slopeGate;
                // Bedding carries the large-scale albedo; directional light only modulates it.
                float rockFace=.22+.62*lightFace;
                half3 rock=lerp(_RockShade.rgb,_ColorBottom.rgb,rockFace);
                rock=lerp(rock,half3(.55,.52,.47),lightFace*.35);
                rock = lerp(rock, lerp(half3(.156,.195,.246),half3(.34,.40,.48),rockFace),farRidge);
                // Snow receives broad sky light on shelves; rock retains directional cliff shadows.
                float snowLight=.72+.28*smoothstep(.1,.65,face);
                half3 ice=lerp(_SnowShade.rgb,_SnowColor.rgb,snowLight);
                // Cliff-facing projections retain detail on near-vertical rock.
                float bandA=TriplanarNoise(input.positionWS,weights,47.0,.13);
                float bandB=TriplanarNoise(input.positionWS,weights,129.0,.71);
                rock *= lerp(.80,1.06,bandA*.65+bandB*.35);
                // Height bedding stays subtle and local: thickness varies +/-35% along X.
                float layerThickness=14.0*(1.0+.35*sin(input.positionWS.x*.0063));
                float strataPhase=input.positionWS.y*(2.0*PI)/layerThickness;
                float strataJitter=bandB*2.0-1.0;
                // Multiplicative phase perturbation bounded to +/-18 degrees at every height.
                strataPhase*=1.0+strataJitter*(18.0*PI/180.0)/max(abs(strataPhase),1e-3);
                float strata=sin(strataPhase)*(.35+.65*bandA);
                rock *= 1.0+.035*strata;
                rock *= lerp(1,.38,farRidge);
                rock *= lerp(1,.78,underEdge);
                float2 pixelUV = input.positionWS.xy / 6.0;
                half3 snowTex = SAMPLE_TEXTURE2D(_SnowMap,sampler_SnowMap,pixelUV).rgb;
                // The snow-flecked top rows belong to rock caps, not vertical mountain faces.
                float3 rockPosition=input.positionWS/8.0;
                half3 rockTex = SAMPLE_TEXTURE2D(_RockMap,sampler_RockMap,RockUV(rockPosition.yz)).rgb*weights.x
                    + SAMPLE_TEXTURE2D(_RockMap,sampler_RockMap,RockUV(rockPosition.xz)).rgb*weights.y
                    + SAMPLE_TEXTURE2D(_RockMap,sampler_RockMap,RockUV(rockPosition.xy)).rgb*weights.z;
                #if defined(UNITY_COLORSPACE_GAMMA)
                    snowTex=SRGBToLinear(snowTex); rockTex=SRGBToLinear(rockTex);
                #endif
                // Retain the existing face lighting and aerial perspective, replacing only albedo detail.
                // Compensate the brighter three-axis rock average at the valley silhouette.
                half3 color=lerp(.88 * rock * lerp(1, rockTex / half3(.153,.125,.101), .35),
                    ice * lerp(1, snowTex / half3(.651,.744,.842), .38),snow);
                float foot=(1-smoothstep(0,15,input.positionWS.y))*step(0,input.positionWS.y)*.18;
                color=lerp(color,_AirColor.rgb,saturate(lerp(_Aerial,.24,farRidge)+_FogFloor+foot));
                color=lerp(color,_AirColor.rgb,.15*farRidge);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
