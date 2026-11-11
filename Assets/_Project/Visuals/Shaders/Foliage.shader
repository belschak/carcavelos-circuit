// Trees that sway in a calm sea breeze (Role 3). Replaces URP Lit on the soft trees: same kind of light (main
// light with received shadows, sky light, fog, screen-space ambient occlusion when the renderer has it), optional
// alpha clipping (palm fronds) and both faces drawn, plus a vertex sway:
//  - a slow bend of the whole tree, weighted by UV2.x (0 at the trunk base, 1 at the top);
//  - a faster flutter, weighted by UV2.y (0 at a frond's base, 1 at its tip; small on the clumps of pine and olive).
// Both weights are baked into the meshes by env_vegetation.py, and the phase comes from the world position, so the
// sway is the same whether the trees are drawn one by one or merged by static batching (the dressing is marked
// static, and a static batch has no object space and no per-tree data left). Neighbouring trees are out of step.
// Shadows move with the leaves (ShadowCaster uses the same displacement and alpha clip).
// V17 "AAA" pass: soft "wrapped" sun light (leaves are thin, the light wraps round a canopy instead of cutting off
// hard), a warm yellow-green glow when you look at a tree against the low sun (light through the leaves), and the
// inside and the lower part of a crown darker than its sunny top (the sway weights double as a height in the tree).
// Cost: one texture read per pixel, a handful of sines per vertex, no specular term (cheaper than URP Lit).
// Made by CampusWorldBuilder.BuildMaterials from the "wind" block in Data/materials.json; if this shader is
// missing, the trees stay URP Lit and stand still.
Shader "CarcavelosCircuit/Foliage"
{
    Properties
    {
        [MainTexture] [NoScaleOffset] _BaseMap ("Colour (alpha = leaf outline)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha clipping", Float) = 0
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _Wind ("Wind: direction x, direction z, bend m, flutter m", Vector) = (0.9, 0.42, 0.1, 0.1)
        _WindSpeed ("Wind: bend speed, flutter speed, phase per m, unused", Vector) = (0.9, 2.6, 0.07, 0)
        _BackShade ("Back faces: brightness", Range(0, 1)) = 0.78
        [Toggle] _FlipBack ("Back faces: normal turned over (leaves); off for upright grass cards", Float) = 1
        _Shrink ("Grass tufts sink away: from m, to m, 1 / (to - from) or 0 = off, unused", Vector) = (0, 0, 0, 0)
        _Translucency ("V17 light through the leaves (linear rgb)", Color) = (0.62, 0.66, 0.16, 1)
        _Leaf ("V17: wrap 0..1, glow strength, crown base darkness, -", Vector) = (0.4, 0.9, 0.4, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _Translucency, _Leaf;
            float4 _Wind, _WindSpeed, _Shrink;
            half _Cutoff, _BackShade, _AlphaClip, _Cull, _FlipBack;
        CBUFFER_END
        float _WindTimeOffset;            // global, 0 in the game; the editor's shot renderer steps it to show motion
        float _ShotTimeFreeze;            // global, 0 in the game; 1 = ignore the running clock (repeatable shots)
        float _WindCalm;                  // global, 0 = wind as set on the material, 1 = no wind
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        // World position after the wind. weights = UV2 (bend, flutter). Works on world positions only.
        // height = vertex colour alpha: on grass tufts the height of the card in metres. With _Shrink set, the tops
        // sink into the ground between the two distances, so tufts do not pop when their cell is hidden further out.
        float3 Sway(float3 pw, float2 weights, float height)
        {
            pw.y -= height * weights.y * saturate((distance(pw, _WorldSpaceCameraPos) - _Shrink.x) * _Shrink.z);
            float t = _Time.y * (1.0 - _ShotTimeFreeze) + _WindTimeOffset;
            float2 dir = normalize(_Wind.xy);
            float phase = (dot(pw.xz, dir) + 0.6 * dot(pw.xz, float2(-dir.y, dir.x))) * _WindSpeed.z;
            float gust = 0.35 + 0.6 * sin(t * _WindSpeed.x + phase) + 0.4 * sin(t * _WindSpeed.x * 0.43 + phase * 1.7);
            float bend = gust * _Wind.z * weights.x;                              // leans downwind, swings slowly
            float flutterA = sin(t * _WindSpeed.y + phase * 3.1 + pw.y * 0.9);
            float flutterB = cos(t * _WindSpeed.y * 0.77 + phase * 2.3 + pw.x * 0.7);
            float flutter = _Wind.w * weights.y;
            float strength = 1.0 - _WindCalm;
            pw.xz += dir * ((bend + flutterA * flutter * 0.6) * strength);
            pw.y += (flutterB * flutter * 0.7 - abs(bend) * 0.15 * weights.x) * strength;
            return pw;
        }

        void Clip(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 sway : TEXCOORD1;
                half4 color : COLOR;              // rgb = tint (grass tufts: the lawn colour under them), white if the mesh has none
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half3 tint : TEXCOORD4;
                half height : TEXCOORD5;          // V17: 0 at the trunk base / blade root, 1 at the top or a frond tip
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = Sway(TransformObjectToWorld(v.positionOS.xyz), v.sway, v.color.a);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.tint = v.color.rgb;
                o.height = max(v.sway.x, v.sway.y);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                #if defined(_ALPHATEST_ON)
                clip(tex.a - _Cutoff);
                #endif
                half front = IS_FRONT_VFACE(face, 1.0, 0.0);
                half3 N = normalize(i.normalWS) * lerp(1.0, front * 2.0 - 1.0, _FlipBack);   // the underside looks down
                half3 albedo = tex.rgb * _BaseColor.rgb * i.tint * lerp(_BackShade, 1.0, front);

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = float4(GetNormalizedScreenSpaceUV(i.positionCS), 0.0, 1.0);
                #else
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light sun = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                // V17: wrapped light: thin leaves stay lit a little past the terminator, so a crown is round, not cut
                half lit = saturate((dot(N, sun.direction) + _Leaf.x) / (1.0 + _Leaf.x));
                half through = saturate(dot(-N, sun.direction)) * 0.5;            // a leaf lit from behind glows a little
                half3 direct = sun.color * (sun.shadowAttenuation * lerp(lit, max(lit, through), 1.0 - front));
                // V17: looking at the tree against the low sun: warm light shines through the leaves
                half3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
                half againstSun = pow(saturate(dot(-V, sun.direction)), 4.0);
                direct += _Translucency.rgb * sun.color * (againstSun * sun.shadowAttenuation * _Leaf.y * (0.4 + 0.6 * i.height));
                // V17: less sky light low in the crown and near the trunk (the crown shades itself)
                half3 sky = SampleSH(N) * lerp(1.0 - _Leaf.z, 1.0, saturate(i.height * 1.4));
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                direct *= ao.directAmbientOcclusion;
                sky *= ao.indirectAmbientOcclusion;
                #endif
                half3 color = MixFog(albedo * (sky + direct), i.fogFactor);
                return half4(color, 1.0);
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
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 sway : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pw = Sway(TransformObjectToWorld(v.positionOS.xyz), v.sway, v.color.a);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - pw);
                #else
                float3 lightDir = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(pw, nw, lightDir));
                #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv = v.uv;
                return o;
            }
            half4 ShadowFrag(Varyings i) : SV_Target { Clip(i.uv); return 0; }
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
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 sway : TEXCOORD1; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(Sway(TransformObjectToWorld(v.positionOS.xyz), v.sway, v.color.a));
                o.uv = v.uv;
                return o;
            }
            half4 DepthFrag(Varyings i) : SV_Target { Clip(i.uv); return i.positionCS.z; }
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
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 sway : TEXCOORD1; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings NormalsVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(Sway(TransformObjectToWorld(v.positionOS.xyz), v.sway, v.color.a));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }
            half4 NormalsFrag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                Clip(i.uv);
                return half4(normalize(i.normalWS) * lerp(1.0, IS_FRONT_VFACE(face, 1.0, -1.0), _FlipBack), 0.0);
            }
            ENDHLSL
        }
    }
}
