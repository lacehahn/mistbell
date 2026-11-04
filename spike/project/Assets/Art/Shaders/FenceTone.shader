Shader "Mistbell/FenceTone"
{
    Properties {
        [MainColor] _BaseColor ("Wood/snow color", Color) = (.29,.23,.18,1)
        _ShadowCoverage ("Shadow coverage", Range(0,1)) = .625
        _Wood ("Wood grain", Float) = 1
        _Roughness ("Roughness (diffuse only)", Float) = .85
        _BaseMap ("Shadow compatibility", 2D) = "white" {}
        _Cutoff ("Cutoff", Float) = .5
        _Cull ("Cull", Float) = 2
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _BaseMap_ST;
            float _ShadowCoverage, _Wood, _Cutoff, _Cull, _Roughness;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
            V Vert(A a) {V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;return o;}
            half4 Frag(V i):SV_Target {
                half3 n=normalize(i.normal);Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 light=max(SampleSH(n),.20)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                float grain=lerp(1,.94+.06*sin(i.uv.x*125+sin(i.uv.y*5)),_Wood);
                // Unity uploads material colors in the working space. Diffuse only.
                half3 albedo=_BaseColor.rgb;
                half3 lit=albedo*grain*light;
                // Cap sunlit wood only; snow stays white. Preserve hue and shading.
                float luminance=dot(lit,float3(.2126,.7152,.0722));
                lit*=lerp(1,min(1,.22/max(luminance,.0001)),_Wood);
                return half4(lit,1);
            }
            ENDHLSL
        }
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment FenceShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _BaseMap_ST;
            float _ShadowCoverage, _Wood, _Cutoff, _Cull, _Roughness;
            CBUFFER_END
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            half4 FenceShadow(Varyings i):SV_TARGET {
                // 10/16 shadow texels survive; URP shadow filtering integrates the
                // coverage. Only this fence changes; all scene lights stay authored.
                uint2 p=(uint2)i.positionCS.xy & 3;
                const float bayer[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
                clip(_ShadowCoverage-(bayer[p.y*4+p.x]+.5)/16);
                return 0;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
