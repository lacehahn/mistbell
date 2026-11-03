Shader "Mistbell/TerrainHaze"
{
    Properties
    {
        [MainTexture] _BaseMap ("Grass", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _ForgeSurface ("Forge sample: 0 off / 1 snow / 2 stone / 3 wood", Float) = 0
        _ForgeEmission ("Preserved interior stone emission", Color) = (0,0,0,0)
        _ForgeTopGain ("Secondary step top polish", Float) = 1
        _ForgeCopingPlane ("Secondary coping top plane; 0 disables", Vector) = (0,0,0,0)
        _ForgeRoughness ("Forge roughness", 2D) = "white" {}
        _SnowMedium ("Snow 256px middle", 2D) = "gray" {}
        _SnowLow ("Snow 1024px low", 2D) = "gray" {}
        _SnowRelief ("Snowfield relief only", Float) = 0
        _StoneSurface ("Stone surface", Float) = 0
        _RockOnly ("World projected rock texture", Float) = 0
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
        _FarHazeStrength ("Far snow haze (terrain only)", Range(0,1)) = 0
        _FarHazeColor ("Sampled sky horizon", Color) = (1,1,1,1)
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
            TEXTURE2D(_ForgeRoughness); SAMPLER(sampler_ForgeRoughness);
            TEXTURE2D(_SnowMedium); SAMPLER(sampler_SnowMedium);
            TEXTURE2D(_SnowLow); SAMPLER(sampler_SnowLow);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_TexelSize, _ForgeCopingPlane;
                float4 _ForgeEmission;
                float4 _BaseMap_ST, _BaseColor, _HazeColor, _SnowShadowColor, _RockColor, _FarHazeColor;
                float _FarHazeStrength;
                float _SnowRelief, _ForgeSurface, _ForgeTopGain;
                float _StoneSurface, _SnowAlbedo, _RockSnowCap, _Footprint, _RockOnly;
                float _NormalScale, _Metallic, _Smoothness;
                float _HazeStart, _HazeEnd, _HazePower, _HazeStrength;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 relief : TEXCOORD1;
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
                float2 relief : TEXCOORD6;
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
                output.relief = input.relief;
                output.vertexLighting = VertexLighting(output.positionWS, normals.normalWS);
                return output;
            }
            float HashChunk(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float SnowMound(float2 p)
            {
                float2 c = floor(p), f = frac(p);
                f = f*f*(3.0-2.0*f);
                return lerp(lerp(HashChunk(c), HashChunk(c+float2(1,0)), f.x),
                            lerp(HashChunk(c+float2(0,1)), HashChunk(c+1), f.x), f.y);
            }
            float2 ChunkUV(float2 p, float2 cell, float seed)
            {
                float angle = HashChunk(cell+seed)*1.570796327;
                float sn,cs; sincos(angle,sn,cs);
                float2 q=p-cell;
                return float2(cs*q.x-sn*q.y,sn*q.x+cs*q.y)
                    + float2(HashChunk(cell+seed+19),HashChunk(cell+seed+73))*13;
            }
            // Four overlapping independently rotated chunks, continuous across every boundary.
            half3 SnowLayer(float2 p, int layer)
            {
                float2 cell=floor(p), f=smoothstep(0,1,frac(p));
                half3 result=0;
                [unroll] for(int y=0;y<2;y++) [unroll] for(int x=0;x<2;x++)
                {
                    float2 c=cell+float2(x,y);
                    float2 uv=ChunkUV(p,c,layer*29.7);
                    float w=(x?f.x:1-f.x)*(y?f.y:1-f.y);
                    half3 v;
                    if(layer==0) v=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                    else if(layer==1) v=SAMPLE_TEXTURE2D(_SnowMedium,sampler_SnowMedium,uv).rgb;
                    else v=SAMPLE_TEXTURE2D(_SnowLow,sampler_SnowLow,uv).rgb;
                    result+=v*w;
                }
                return result;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Opt-in samples only. World metre projection avoids stretched imported FBX UVs.
                // One axis (no triplanar blend) preserves palette hard edges; no procedural noise.
                if (_ForgeSurface > .5)
                {
                    half3 n = NormalizeNormalPerPixel(input.normalWS);
                    float3 projectionNormal = normalize(cross(ddy(input.positionWS),ddx(input.positionWS)));
                    float3 an = abs(projectionNormal);
                    float2 uv = _ForgeSurface < 1.5 ? input.positionWS.xz :
                        (an.y > max(an.x,an.z) ? input.positionWS.xz :
                        (an.x > an.z ? input.positionWS.zy : input.positionWS.xy));
                    if (_ForgeSurface > 2.5)
                    {
                        // Door grain follows the leaf/hinge instead of swimming when the door opens.
                        float4x4 m = GetObjectToWorldMatrix();
                        float3 scale = float3(length(m._m00_m10_m20),length(m._m01_m11_m21),length(m._m02_m12_m22));
                        float3 local = TransformWorldToObject(input.positionWS) * scale;
                        float3 localNormal = abs(mul(projectionNormal,(float3x3)m));
                        uv = localNormal.y > max(localNormal.x,localNormal.z) ? local.xz :
                            (localNormal.x > localNormal.z ? local.zy : local.xy);
                    }
                    // A shared world origin and exact texel centres keep terrace/wall faces
                    // on one metre grid, independent of their mesh UVs and object scales.
                    uv = (floor((uv * _BaseMap_ST.xy + _BaseMap_ST.zw) * _BaseMap_TexelSize.zw) + .5) / _BaseMap_TexelSize.zw;
                    half3 albedo = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb * _BaseColor.rgb;
                    if (_ForgeSurface > 1.5 && _ForgeSurface < 2.5)
                    {
                        float capDepth=-dot(float4(input.positionWS,1),_ForgeCopingPlane);
                        if (_ForgeCopingPlane.y > .5 && capDepth >= -.02 && capDepth <= .25)
                            albedo=half3(.144128,.144128,.144128)*_BaseColor.rgb; // sRGB #6A6A6A, one tier brighter
                        if(n.y > .9) albedo *= _ForgeTopGain;
                    }
                    half rough = SAMPLE_TEXTURE2D(_ForgeRoughness,sampler_ForgeRoughness,uv).r;
                    Light key = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                    // Diffuse only: the roughness map bounds diffuse energy; no smooth PBR highlight.
                    half3 irradiance = SampleSH(n) + key.color * saturate(dot(n,key.direction)) * key.shadowAttenuation;
                    #if defined(_ADDITIONAL_LIGHTS)
                        uint count = GetAdditionalLightsCount();
                        for(uint li=0;li<count;li++)
                        {
                            Light light = GetAdditionalLight(li,input.positionWS,half4(1,1,1,1));
                            irradiance += light.color * saturate(dot(n,light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                        }
                    #endif
                    // Preserve broad lighting and real cast shadows; quantize illumination to three steps.
                    half value = dot(irradiance,half3(.299,.587,.114));
                    half shade = value < .55 ? .66 : (value < 1.2 ? .86 : 1.0);
                    // Snow's dark facet tier carries a restrained surface-occlusion step.
                    // It survives the existing depth of field without adding fine noise.
                    half occlusion = _ForgeSurface < 1.5 && rough > .97 ? .73 : 1.0;
                    half3 color = albedo * shade * lerp(.98,1.0,rough) * occlusion + _ForgeEmission.rgb;
                    float d=distance(input.positionWS.xz,_WorldSpaceCameraPos.xz);
                    float low=(1-smoothstep(0,15,input.positionWS.y))*step(0,input.positionWS.y);
                    float mist=pow(saturate((d-_HazeStart)/max(.001,_HazeEnd-_HazeStart)),_HazePower)*_HazeStrength*low;
                    color=lerp(color,_HazeColor.rgb,mist);
                    float far=smoothstep(600,1100,d)*_FarHazeStrength;
                    float elevation=normalize(input.positionWS-_WorldSpaceCameraPos).y;
                    float crest=smoothstep(55,85,d)*(1-smoothstep(.10,.24,elevation))*saturate(_FarHazeStrength/.85);
                    return half4(lerp(color,_FarHazeColor.rgb,max(far,crest)),1);
                }
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_NORMALMAP)
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
                    half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
                        half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                #endif
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
                // Pixel-painted albedo in world metres; no UV edits required on rocks.
                half snowCoverage = 1.0h - _RockOnly;
                float3 weights = abs(input.normalWS);
                weights = pow(weights, 8); weights /= max(dot(weights, 1.0), .0001);
                float3 p = input.positionWS * _BaseMap_ST.x;
                // Group the 40 texels/m world grid into hard 2.3-texel clusters (5.75 cm; about 3-4 screen pixels nearby).
                // No interpolated noise in the fine palette: each cell has one flat value.
                float2 snowXZ = input.positionWS.xz;
                float grain = HashChunk(floor(snowXZ * (40.0 / 3.4)));
                half detail = grain < .333333 ? .92h : (grain < .666667 ? .99h : 1.06h);
                // Isotropic 0.5 / 0.75 / 1 m value fields, bounded albedo relief +/-4%.
                float2 rotatedXZ = float2(.8*snowXZ.x-.6*snowXZ.y, .6*snowXZ.x+.8*snowXZ.y);
                float mound = .5*SnowMound(snowXZ/.75+17.3)
                            + .3*SnowMound(rotatedXZ/.5+43.1)
                            + .2*SnowMound(snowXZ+91.7);
                detail *= 1.0 + (mound*2.0-1.0)*.07;
                half3 texel = half3(.651, .744, .842) * detail;
                if (_RockOnly > .5)
                    texel = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb*weights.x
                          + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*weights.y
                          + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*weights.z;
                // Normalize the procedural palette to the previous snow's average albedo.
                half3 snowAlbedo = _SnowAlbedo * _BaseColor.rgb / max(_BaseColor.b, .001);
                surface.albedo = lerp(texel * _BaseColor.rgb,
                    snowAlbedo * texel / half3(.651, .744, .842), snowCoverage);
                if (_SnowRelief > .5)
                {
                    float dist = distance(input.positionWS, _WorldSpaceCameraPos);
                    if (_SnowRelief > 1.5 && dist > 45.0) clip(-1);
                    // Metres per detail cell; smooth transitions prevent visible distance rings.
                    float scale = lerp(.6, 1.6, smoothstep(23,27,dist));
                    scale = lerp(scale, 3.0, smoothstep(57,63,dist));
                    float field = SnowMound(snowXZ/scale + 17.3);
                    float fine = HashChunk(floor(snowXZ * (10.0/scale)));
                    float fade = 1.0-smoothstep(25,65,dist);
                    float grainValue = fine < .3333 ? -.055 : (fine < .6667 ? 0 : .055);
                    float reliefHeight = input.relief.x;
                    float slope = length(input.normalWS.xz - float2(0,-.104528));
                    float crest = max(smoothstep(.06,.25,reliefHeight), smoothstep(.045,.12,input.relief.y));
                    half3 hollow = half3(.725490,.796078,.870588); // #B9CBDE
                    half3 face = half3(.894118,.929412,.960784);   // #E4EDF5
                    half3 top = half3(.964706,.980392,.992157);    // #F6FAFD
                    half3 palette = lerp(hollow,face,smoothstep(-.15,.06,reliefHeight));
                    palette = lerp(palette,top,crest*(1-saturate(slope*2.2)));
                    palette *= 1.0 - saturate(slope)*.35;
                    surface.albedo = palette * (1 + (field-.5)*.065 + grainValue*fade);
                }
                // Broad snow mounds modulate the three hard grain levels, without waves.
                if(_StoneSurface>.5)
                {
                    half3 stone=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                    half baseSnow=(1-smoothstep(0,.8,input.positionWS.y))*step(1.5,_StoneSurface);
                    surface.albedo=lerp(stone*_BaseColor.rgb,half3(.80,.84,.88),baseSnow);
                    snowCoverage=0;
                }
                surface.emission = 0;
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
                // Snow keeps cool skylight but loses bounce in occluded hollows.
                // Local terrain only: shared church/path/rock materials stay unchanged.
                if (_SnowRelief > .5)
                    color *= lerp(.90h,1.0h,sun.shadowAttenuation);
                // Ground mist is bounded in WORLD height, so no layer can cross a mountainside.
                float d = distance(input.positionWS.xz, _WorldSpaceCameraPos.xz);
                float t = saturate((d - _HazeStart) / max(1e-3, _HazeEnd - _HazeStart));
                float low = (1-smoothstep(0,15,input.positionWS.y)) * step(0,input.positionWS.y);
                float f = pow(t, _HazePower) * _HazeStrength * low;
                color = max(0, color - _Footprint * .08);
                color = lerp(color, _HazeColor.rgb, f);
                // Far snow falls below world Y=0: the old low-height gate misses it.
                // Opt-in on the terrain material; church, props and paving stay intact.
                float farHaze = smoothstep(600.0, 1100.0, d) * _FarHazeStrength;
                // The visible horizon is a nearer snow crest, not the mesh's far edge.
                // Match its atmosphere with the foot of the depthless mountain cards.
                float elevation = normalize(input.positionWS - _WorldSpaceCameraPos).y;
                float crestHaze = smoothstep(55.0, 85.0, d)
                    * (1.0 - smoothstep(.10, .24, elevation)) * saturate(_FarHazeStrength/.85);
                return half4(lerp(color, _FarHazeColor.rgb, max(farHaze, crestHaze)), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback Off
}
