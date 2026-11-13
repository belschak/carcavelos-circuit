// Buildings, paving and props of the campus world with depth (Role 3, V16h). URP Lit's lighting (same BRDF, same sky
// reflections, same maps: base, normal, metallic/smoothness), plus two things URP Lit cannot do without a light bake:
//  - AMBIENT OCCLUSION FROM VERTEX COLOURS (tools/world_gen/env_shade.py bakes them into the FBX files): inner
//    corners, wall feet, the underside of canopies and bridges and the tunnel mouth get less sky light and a little
//    less sun. Meshes without vertex colours read white (no change). No texture, no extra material, static batching
//    keeps the colours;
//  - the BAKED SUN SHADOW on walkable surfaces (materials.json "world_lit.ground_shadow": paving, road, streets): the
//    same two maps Ground.shader uses (alpha of the world and the near ground colour map, read by world position),
//    faded in beyond the realtime shadow range and combined with the realtime shadow by min(), so nothing is ever
//    darker than one shadow. Only faces that look up take it; _Shade.x = 1 marks materials that are always in the
//    shade far away (the tunnel's inside).
// Meshes without UVs (keyword _WORLD_UV, materials.json "world_lit.world_uv"): the textures are read by world
// position, walls along their longer horizontal axis and by height, faces that look up from above (or one plain
// texel when _WorldUV.z = 0); the normal map uses the same axes as its tangent frame.
// Additional lights are added per pixel in Forward (not in Forward+). No lightmaps, no light probes beyond the sky's SH.
// Made by CampusWorldBuilder.BuildMaterials from the "world_lit" block in Data/materials.json; if this shader is
// missing, the material stays URP Lit.
Shader "CarcavelosCircuit/WorldLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        [NoScaleOffset] [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        [NoScaleOffset] _MetallicGlossMap ("Metallic (R) smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _Occlusion ("Vertex occlusion: on the sky light, on the sun, -, -", Vector) = (1, 0.35, 0, 0)
        [NoScaleOffset] _GroundWorldMap ("Ground colour map (alpha = baked sun shadow)", 2D) = "white" {}
        [NoScaleOffset] _GroundNearMap ("Fine ground colour map (alpha = baked sun shadow)", 2D) = "white" {}
        _WorldRect ("World map: world x0, z0, 1 / width, 1 / depth", Vector) = (0, 0, 0, 0)
        _NearRect ("Fine map: world x0, z0, 1 / width, 1 / depth (0 = none)", Vector) = (0, 0, 0, 0)
        _BakedShadow ("Baked sun shadow: fade in from m, to m, strength, -", Vector) = (30, 42, 0.85, 0)
        _WorldUV ("World UVs: 1 / tile m across, 1 / tile m up, roof scale per m (0 = one texel), -", Vector) = (0.3333, 0.3333, 0, 0)
        _Shade ("x: 1 = always in the shade far away; y: 1 = takes the baked ground shadow", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale, _Metallic, _Smoothness;
            half4 _Occlusion, _Shade;
            float4 _WorldRect, _NearRect, _BakedShadow, _WorldUV;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _WORLD_UV
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_GroundWorldMap); SAMPLER(sampler_GroundWorldMap);
            TEXTURE2D(_GroundNearMap); SAMPLER(sampler_GroundNearMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;               // rgb = ambient occlusion (env_shade.py); white if the mesh has none
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 fogAndOcclusion : TEXCOORD3;      // x fog, y vertex occlusion
                #if defined(_NORMALMAP)
                half4 tangentWS : TEXCOORD4;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_NORMALMAP)
                o.tangentWS = half4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogAndOcclusion = half4(ComputeFogFactor(o.positionCS.z), v.color.r, 0, 0);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 pw = i.positionWS;
                half3 geoN = normalize(i.normalWS);
                half3 N = geoN;
                float2 uv = i.uv;
                #if defined(_WORLD_UV)
                half3 absN = abs(geoN);
                half3 axisU = absN.x > absN.z ? half3(0, 0, 1) : half3(1, 0, 0);
                half3 axisV = half3(0, 1, 0);
                uv = float2(dot(pw, axisU) * _WorldUV.x, pw.y * _WorldUV.y);
                if (absN.y > 0.7)
                {
                    axisU = half3(1, 0, 0);
                    axisV = half3(0, 0, 1);
                    uv = pw.xz * _WorldUV.z;
                }
                #endif
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                #if defined(_WORLD_UV)
                // V18l: walls without UVs repeat their 3 m tile; a second, 6.3x larger read of the same texture
                // (rotated, so its joints never line up) breaks the repeat into big soft patches of the render.
                // Two such reads compared with each other: their mean is 1 whatever the texture's brightness.
                half m1 = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(uv.y, -uv.x) * 0.159 + 0.37).rgb, half3(0.3, 0.59, 0.11));
                half m2 = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(-uv.y, uv.x) * 0.113 + 0.71).rgb, half3(0.3, 0.59, 0.11));
                base.rgb *= 1.0 + 0.7 * step(absN.y, 0.7) * (m1 - m2) / max(m1 + m2, 0.02);
                #endif
                #if defined(_NORMALMAP)
                half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                #if defined(_WORLD_UV)
                N = normalize(nt.x * axisU + nt.y * axisV + nt.z * geoN);
                #else
                half3 bitangent = i.tangentWS.w * cross(geoN, i.tangentWS.xyz);
                N = normalize(mul(nt, half3x3(i.tangentWS.xyz, bitangent, geoN)));
                #endif
                #endif
                half metallic = _Metallic, smoothness = _Smoothness;
                #if defined(_METALLICSPECGLOSSMAP)
                half4 ms = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
                metallic = ms.r;
                smoothness = ms.a * _Smoothness;
                #endif

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = base.rgb;
                surface.metallic = metallic;
                surface.smoothness = smoothness;
                surface.occlusion = 1.0;
                surface.alpha = 1.0;
                BRDFData brdf;
                InitializeBRDFData(surface, brdf);

                InputData inputData = (InputData)0;
                inputData.positionWS = pw;
                inputData.normalWS = N;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(pw);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                inputData.shadowCoord = float4(inputData.normalizedScreenSpaceUV, 0.0, 1.0);
                #else
                inputData.shadowCoord = TransformWorldToShadowCoord(pw);
                #endif

                // vertex ambient occlusion: fully on the sky light, partly on the sun
                // (glossy surfaces take less of it: glass set back in a reveal stays light and mirrors the sky)
                half matt = 1.0 - 0.75 * smoothness;
                half vertexAO = lerp(1.0, i.fogAndOcclusion.y, _Occlusion.x * matt);
                half sunAO = lerp(1.0, i.fogAndOcclusion.y, _Occlusion.y * matt);
                AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData, surface);

                Light sun = GetMainLight(inputData.shadowCoord, pw, half4(1, 1, 1, 1));
                // baked sun shadow far away: the ground maps' alpha for faces that look up, or "always shaded"
                half camDist = distance(pw, _WorldSpaceCameraPos);
                half far = smoothstep(_BakedShadow.x, _BakedShadow.y, camDist);
                float2 nearUV = (pw.xz - _NearRect.xy) * _NearRect.zw;
                float2 nearEdge = min(nearUV, 1.0 - nearUV) / max(_NearRect.zw * 10.0, 1e-6);
                half nearShare = saturate(min(nearEdge.x, nearEdge.y)) * step(1e-6, _NearRect.z);
                half lit = SAMPLE_TEXTURE2D(_GroundWorldMap, sampler_GroundWorldMap, (pw.xz - _WorldRect.xy) * _WorldRect.zw).a;
                lit = lerp(lit, SAMPLE_TEXTURE2D(_GroundNearMap, sampler_GroundNearMap, nearUV).a, nearShare);
                lit = lerp(1.0, lit, _Shade.y * saturate(geoN.y * 4.0 - 2.0));
                lit = lerp(lit, 0.0, _Shade.x);
                sun.shadowAttenuation = min(sun.shadowAttenuation, lerp(1.0, lerp(1.0 - _BakedShadow.z, 1.0, lit), far));
                sun.color *= sunAO * ao.directAmbientOcclusion;

                // the sky light takes the full vertex occlusion; reflections only a third of it (glass set back in a
                // reveal still mirrors the sky)
                half3 color = GlobalIllumination(brdf, SampleSH(N) * vertexAO, lerp(1.0, vertexAO, 0.3) * ao.indirectAmbientOcclusion,
                                                 pw, N, inputData.viewDirectionWS);
                color += LightingPhysicallyBased(brdf, sun, N, inputData.viewDirectionWS);
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; ++li)
                {
                    Light light = GetAdditionalLight(li, pw, half4(1, 1, 1, 1));
                    color += LightingPhysicallyBased(brdf, light, N, inputData.viewDirectionWS) * vertexAO;
                }
                #endif
                return half4(MixFog(color, i.fogAndOcclusion.x), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - pw);
                #else
                float3 lightDir = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(pw, TransformObjectToWorldNormal(v.normalOS), lightDir));
                #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 ShadowFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half4 DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass                               // depth + normals for renderers that need them (screen-space ambient occlusion)
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings NormalsVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 NormalsFrag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
}
