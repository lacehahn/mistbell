Shader "Mistbell/RevealMistDepth"
{
    Properties { _Color ("Mist", Color) = (0.86,0.89,0.92,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float4 _Color;
            struct A { float4 positionOS:POSITION; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float4 color:COLOR; };
            V Vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.color=v.color; return o; }
            half4 Frag(V i):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float depth=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                // The reveal bank belongs to the church approach. Background
                // cards do not write depth and already carry their own layer haze.
                float localBank=1-smoothstep(250,500,depth);
                return half4(_Color.rgb,_Color.a*i.color.a*localBank);
            }
            ENDHLSL
        }
    }
}
