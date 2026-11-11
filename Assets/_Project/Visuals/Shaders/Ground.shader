// Ground of the campus world with detail (Role 3): lawn that reads as grass and sand with grain and wind ripples,
// on top of the painted ground colour map. Replaces URP Lit on W_ground_map (terrain, track corridor, shoulders):
//  - colour = ground colour map (one picture over the whole world, mesh UV) x detail;
//  - a mask in the same UVs says where lawn (R) and sand (G) are and how far up the beach a point is (B);
//  - one tiling detail texture, read by world position at a small and a large scale: blade strokes and soft
//    patches on the lawn, ripples and grain on the sand; the small scale fades out with distance, the large one
//    stays, so nothing tiles or stripes far away; a matching detail normal gives the low sun something to catch;
//  - sand: the colour map paints the dry beach, the darker wet band and the cove (make_groundmap.py); the shader adds
//    a soft sheen on the wet band (sun highlight and sky at flat angles) that fades up the beach;
//  - rock shelves (mask alpha = 1 - rock): grain, blotches and thin strata lines from the detail texture read at a
//    third, stretched scale, and a rougher normal.
// Lit like URP Lit without the specular term (except the wet sand's sheen): main light with received shadows, sky
// light, fog, screen-space ambient occlusion when the renderer has it. Cost per pixel: 6 texture reads, no loops,
// no branches.
// World-space detail UVs, so static batching changes nothing. Made by CampusWorldBuilder.BuildMaterials from the
// "ground" block in Data/materials.json; if this shader is missing, the ground stays URP Lit with the colour map.
Shader "CarcavelosCircuit/Ground"
{
    Properties
    {
        [MainTexture] [NoScaleOffset] _BaseMap ("Ground colour map", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _GroundMask ("Mask: R lawn, G sand, B beach height, A 1 - rock (linear)", 2D) = "black" {}
        [NoScaleOffset] _DetailMap ("Detail: R blades, G patches, B ripples, A grain (linear)", 2D) = "gray" {}
        [NoScaleOffset] _DetailNormal ("Detail normal: RG lawn, BA sand (linear)", 2D) = "gray" {}
        _Detail ("Detail: small tile m, large tile m, fade from m, fade to m", Vector) = (6, 47, 30, 120)
        _Grass ("Lawn: blades, patches, patch tint, normal", Vector) = (0.42, 0.34, 1, 0.6)
        _Sand ("Sand: ripples, grain, patches, normal", Vector) = (0.22, 0.26, 0.15, 1.0)
        _WetColor ("Wet sand (multiplies the painted wet band)", Color) = (0.95, 0.94, 0.92, 1)
        _WetSheen ("Wet sand sheen: sharpness, sun highlight, sky at flat angles, reach (beach height 0..1)", Vector) = (60, 0.35, 0.3, 0.5)
        _Rock ("Rock: grain, blotches, strata lines, normal", Vector) = (0.4, 0.3, 0.35, 0.9)
        _StrataTile ("Rock strata: tile m along x, along z", Vector) = (15, 5, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _WetColor;
            float4 _Detail, _StrataTile;
            half4 _Grass, _Sand, _WetSheen, _Rock;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GroundMask); SAMPLER(sampler_GroundMask);
            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
            TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
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
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 pw = i.positionWS;
                half3 macro = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half4 mask = SAMPLE_TEXTURE2D(_GroundMask, sampler_GroundMask, i.uv);
                half rock = 1.0 - mask.a;
                float2 smallUV = pw.xz / _Detail.x;
                half4 fine = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, smallUV);
                half patch = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, pw.zx / _Detail.y + 0.37).g - 0.5;
                half4 bump = SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, smallUV) * 2.0 - 1.0;
                half strata = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, pw.xz / _StrataTile.xy + 0.61).b;
                half fade = 1.0 - smoothstep(_Detail.z, _Detail.w, distance(pw, _WorldSpaceCameraPos));

                // lawn: blade strokes up close, soft lighter (yellower) and darker (cooler) patches at any distance
                half3 lawn = macro * (1.0 + (fine.r - 0.5) * _Grass.x * fade + patch * _Grass.y);
                lawn *= 1.0 + patch * _Grass.z * half3(0.10, 0.02, -0.14);

                // sand: ripples and grain up close, soft patches; the wet band is painted, here only a slight darkening
                half wet = 1.0 - smoothstep(0.1, 0.55, mask.b);
                half3 sand = macro * (1.0 + ((fine.b - 0.5) * _Sand.x + (fine.a - 0.5) * _Sand.y) * fade + patch * _Sand.z);
                sand *= lerp(half3(1, 1, 1), _WetColor.rgb, wet) * (1.0 + 0.03 * smoothstep(0.35, 1.0, mask.b));

                // rock: coarse grain and blotches, thin dark strata lines (ridges of the ripple channel, stretched)
                half strataLine = smoothstep(0.8, 0.96, strata) * fade;
                half3 stone = macro * (1.0 + (fine.a - 0.5) * _Rock.x * fade + patch * _Rock.y) * (1.0 - _Rock.z * strataLine);

                half3 albedo = lerp(lerp(lerp(macro, lawn, mask.r), sand, mask.g), stone, rock);
                half2 tilt = (bump.rg * (mask.r * _Grass.w + rock * _Rock.w) + bump.ba * (mask.g * _Sand.w * (1.0 - 0.6 * wet))) * fade;
                half3 N = normalize(normalize(i.normalWS) + half3(tilt.x, 0.0, tilt.y));

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = float4(GetNormalizedScreenSpaceUV(i.positionCS), 0.0, 1.0);
                #else
                float4 shadowCoord = TransformWorldToShadowCoord(pw);
                #endif
                Light sun = GetMainLight(shadowCoord, pw, half4(1, 1, 1, 1));
                half3 direct = sun.color * (sun.shadowAttenuation * saturate(dot(N, sun.direction)));
                half3 sky = SampleSH(N);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                direct *= ao.directAmbientOcclusion;
                sky *= ao.indirectAmbientOcclusion;
                #endif
                // wet sand sheen: a soft sun highlight and a little sky at flat angles, fading up the beach
                half sheen = mask.g * (1.0 - smoothstep(0.0, _WetSheen.w, mask.b)) * (1.0 - rock);
                half3 V = normalize(_WorldSpaceCameraPos - pw);
                half3 Hv = normalize(sun.direction + V);
                half3 gloss = sun.color * (sun.shadowAttenuation * pow(saturate(dot(N, Hv)), _WetSheen.x) * _WetSheen.y);
                half grazing = pow(1.0 - saturate(dot(N, V)), 4.0) * _WetSheen.z;
                half3 color = albedo * (sky + direct);
                color = lerp(color, SampleSH(reflect(-V, N)), grazing * sheen) + gloss * sheen;
                return half4(MixFog(color, i.fogFactor), 1.0);
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
