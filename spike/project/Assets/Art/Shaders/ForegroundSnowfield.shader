Shader "Mistbell/ForegroundSnowfield"
{
    Properties {
        _BaseColor ("Tint",Color) = (1,1,1,1)
        _BaseMap ("STYLE6 albedo",2D) = "white" {}
        _Style6Density ("Texels per metre",Float) = 24
        _Style6Mode ("0 original / 1 stems / 2 bare rock",Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Style6Density, _Style6Mode;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; };
            V Vert(A a)
            {
                V o; o.positionWS=TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;return o;
            }
            half4 Frag(V i):SV_Target
            {
                InputData d=(InputData)0;
                d.positionWS=i.positionWS;d.normalWS=normalize(i.normalWS);
                d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                d.bakedGI=SampleSH(d.normalWS);d.shadowMask=half4(1,1,1,1);
                d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                SurfaceData s=(SurfaceData)0;s.albedo=i.color.rgb*_BaseColor.rgb;
                // Existing vertex snow and all mesh vertices/triangles stay untouched.
                if(_Style6Mode > .5 && (_Style6Mode < 1.5 || i.color.b < .5))
                {
                    float3 an=abs(normalize(cross(ddy(i.positionWS),ddx(i.positionWS))));
                    float2 p=an.y>max(an.x,an.z)?i.positionWS.xz:(an.x>an.z?i.positionWS.zy:i.positionWS.xy);
                    float2 uv=(floor(p*_Style6Density)+.5)/256;
                    s.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb*_BaseColor.rgb;
                }
                s.smoothness=.02;s.normalTS=half3(0,0,1);s.occlusion=1;s.alpha=1;
                return UniversalFragmentPBR(d,s);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
