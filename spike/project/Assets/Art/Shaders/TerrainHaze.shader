Shader "Mistbell/TerrainHaze"
{
    Properties
    {
        [MainTexture] _BaseMap ("Grass", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _SnowAlbedo ("Snow linear albedo", Range(0,1)) = 0.86
        _SnowShadowColor ("Snow shadow tint", Color) = (0.472,0.584,0.716,1)
        _Footprint ("Footprint darkening", Range(0,1)) = 0
        _RockSnowCap ("Rock snow cap", Range(0,1)) = 0
        _RockColor ("Rock below snow cap", Color) = (0.1946,0.1683,0.1329,1)
        [Normal] _NormalMap ("Normal", 2D) = "bump" {}
        _NormalScale ("Normal strength", Float) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        _HazeColor ("Haze sky color", Color) = (0.654902,0.717647,0.796078,1)
        _HazeStart ("Haze start metres", Float) = 45
        _HazeEnd ("Haze end metres", Float) = 135
        _HazePower ("Haze power", Float) = 1.4
        _HazeStrength ("Haze strength", Range(0,1)) = 0.92
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _BaseColor, _HazeColor, _SnowShadowColor, _RockColor;
                float _SnowAlbedo, _RockSnowCap, _Footprint;
                float _NormalScale, _Metallic, _Smoothness;
                float _HazeStart, _HazeEnd, _HazePower, _HazeStrength;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half3 vertexLighting : TEXCOORD4;
                float capHeight : TEXCOORD5;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.capHeight = input.uv.y;
                output.vertexLighting = VertexLighting(output.positionWS, normals.normalWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_NORMALMAP)
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
                    half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
                        half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                #endif
                // Wind-packed snow: oblique ripples, broken by slower drift lobes (world metres).
                float2 p = input.positionWS.xz;
                float wave = p.x * 3.1 + p.y * 1.2 + sin(p.y * .31) * 1.4 + sin(p.x*.47+p.y*.19)*2.1;
                float ripple = sin(wave);
                normalWS = normalize(normalWS + half3(cos(wave)*.022, 0, cos(wave)*.009));
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normalWS;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(normalWS);
                lighting.vertexLighting = input.vertexLighting;
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                // Linear albedo .86, tinted #EDF3F8 (normalised by blue).
                half snowCoverage = lerp(1.0h, smoothstep(.70h, .76h, input.capHeight), _RockSnowCap);
                half3 snowAlbedo = _SnowAlbedo * _BaseColor.rgb / max(_BaseColor.b, .001);
                float detail = .975 + ripple*.009 + sin(p.x*.37+p.y*.24)*.012;
                surface.albedo = lerp(_RockColor.rgb, snowAlbedo * detail, snowCoverage);
                surface.emission = (1.0h - snowCoverage) * half3(.065h, .075h, .085h);
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;
                // URP Lit's Lambert diffuse + dielectric specular, SH and additional fill.
                // UniversalFragmentPBR calls GetMainLight and MainLightRealtimeShadow.
                half3 color = UniversalFragmentPBR(lighting, surface).rgb;
                // Cool skylight remains in the actual cast shadow; never fill it with white albedo.
                Light sun = GetMainLight(lighting.shadowCoord);
                half3 shadowTint = lerp(half3(1,1,1), _SnowShadowColor.rgb, snowCoverage); // #B7C9DC, material supplies linear color
                color *= lerp(shadowTint, half3(1,1,1), sun.shadowAttenuation);
                // Ground mist is bounded in WORLD height, so no layer can cross a mountainside.
                float d = distance(input.positionWS.xz, _WorldSpaceCameraPos.xz);
                float t = saturate((d - _HazeStart) / max(1e-3, _HazeEnd - _HazeStart));
                float low = (1-smoothstep(0,15,input.positionWS.y)) * step(0,input.positionWS.y);
                float f = pow(t, _HazePower) * _HazeStrength * low;
                color = max(0, color - _Footprint * .08);
                return half4(lerp(color, _HazeColor.rgb, f), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback Off
}
