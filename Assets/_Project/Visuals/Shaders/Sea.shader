// Moving sea for the campus world (Role 3). One flat plane, no wave geometry, no depth or opaque texture:
//  - two copies of one tiling wave normal map scroll in different directions, sizes and speeds: moving sun glints;
//  - a baked shore-distance map, read by world position, gives turquoise shallows, deeper blue further out, and a
//    white foam band at the water's edge that slides in and out; outside the map a straight coast line takes over,
//    which turns north-west west of _CoastWest.x (Carcavelos beach beyond the world edge, env_backdrop.py);
//  - lit by the main light, the sky light and the sky reflection, with the same fog as URP Lit.
//  - V17 "AAA" pass: physically shaped reflection (Schlick fresnel: 2 % straight down, a mirror at grazing angles),
//    a sharp sun glint plus a broad warm sheen around it, light shining through the wave crests towards the sun
//    (sea-green glow), a third small ripple layer near the camera, and the sun shadows of the pier and the beach
//    buildings on the shallows.
// Cost per pixel: 6 texture reads (3 x wave normal, shore map, sky reflection, sun shadow), no loops, no branches.
// Made by CampusWorldBuilder.BuildMaterials from the "water" block in Data/materials.json; if this shader is
// missing, the sea stays the flat URP Lit material.
Shader "CarcavelosCircuit/Sea"
{
    Properties
    {
        _BaseColor ("Sea colour", Color) = (0.08, 0.56, 0.86, 1)
        _ShallowColor ("Shallow water (near the shore)", Color) = (0.16, 0.86, 0.80, 1)
        _DeepColor ("Deep water (open sea)", Color) = (0.03, 0.27, 0.62, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        [NoScaleOffset] [Normal] _WaveNormal ("Wave normal map (tiling)", 2D) = "bump" {}
        [NoScaleOffset] _ShoreMap ("Shore distance map (R, linear)", 2D) = "white" {}
        _ShoreRect ("Shore map: world x0, z0, 1 / width, 1 / depth", Vector) = (0, 0, 0.001, 0.001)
        _ShoreRange ("Shore map: metres at value 1", Float) = 60
        _CoastZ ("Coast line outside the map (world z)", Float) = -100
        _CoastWest ("Far coast west of x: x, its z there, metres north per metre west (off by default)", Vector) = (-100000, 0, 0, 0)
        _WaveA ("Waves A: size m, direction x, direction z, speed m/s", Vector) = (13, 1, 0.35, 0.9)
        _WaveB ("Waves B: size m, direction x, direction z, speed m/s", Vector) = (21, -0.45, 1, 0.6)
        _NormalStrength ("Wave strength", Range(0, 1)) = 0.3
        _Glint ("Sun glint: sharpness, strength", Vector) = (260, 2.4, 0, 0)
        _Reflection ("Sky reflection", Range(0, 1)) = 0.45
        _Foam ("Foam: width m, slide m, speed, breakup", Vector) = (2.2, 1.6, 0.7, 2.5)
        _ScatterColor ("Light through the waves (sea green)", Color) = (0.05, 0.68, 0.58, 1)
        _Sheen ("Sun sheen: sharpness, strength; ripples near the camera: size m, strength", Vector) = (18, 0.35, 3.2, 0.5)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShallowColor, _DeepColor, _FoamColor, _ScatterColor, _Sheen;
            float4 _ShoreRect, _WaveA, _WaveB, _CoastWest;
            half4 _Glint, _Foam;
            float _ShoreRange, _CoastZ;
            half _NormalStrength, _Reflection;
        CBUFFER_END
        float _SeaTimeOffset;             // global, 0 in the game; the editor's shot renderer steps it to show motion
        float _ShotTimeFreeze;            // global, 0 in the game; 1 = ignore the running clock (repeatable shots)
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
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WaveNormal); SAMPLER(sampler_WaveNormal);
            TEXTURE2D(_ShoreMap); SAMPLER(sampler_ShoreMap);

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 pw = i.positionWS;
                float t = _Time.y * (1.0 - _ShotTimeFreeze) + _SeaTimeOffset;
                float3 toCam = _WorldSpaceCameraPos - pw;
                float dist = length(toCam);
                half3 V = toCam / dist;

                // two scrolling wave layers (the plane is level: tangent = world x, bitangent = world z)
                float2 uvA = pw.xz / _WaveA.x + normalize(_WaveA.yz) * (t * _WaveA.w / _WaveA.x);
                float2 uvB = pw.zx / _WaveB.x + normalize(_WaveB.zy) * (t * _WaveB.w / _WaveB.x);
                half3 nA = UnpackNormal(SAMPLE_TEXTURE2D(_WaveNormal, sampler_WaveNormal, uvA));
                half3 nB = UnpackNormal(SAMPLE_TEXTURE2D(_WaveNormal, sampler_WaveNormal, uvB));
                half2 ripple = nA.xy + nB.yx;
                // V17: small fast ripples near the camera (fade out by 60 m, so far water stays calm and does not shimmer)
                half nearFade = 1.0 - saturate(dist / 60.0);
                float2 uvC = pw.xz / _Sheen.z + float2(0.6, -0.8) * (t * 0.35 / _Sheen.z);
                half3 nC = UnpackNormal(SAMPLE_TEXTURE2D(_WaveNormal, sampler_WaveNormal, uvC));
                ripple += nC.xy * (_Sheen.w * nearFade);
                half calm = 1.0 - 0.6 * saturate(dist / 700.0);                  // far water: calmer, no shimmer
                half3 N = normalize(half3(ripple.x * _NormalStrength * calm, 1.0, ripple.y * _NormalStrength * calm));

                // distance to the shore in metres: baked map near the world, a straight coast line far outside it
                float2 suv = (pw.xz - _ShoreRect.xy) * _ShoreRect.zw;
                half mapped = SAMPLE_TEXTURE2D(_ShoreMap, sampler_ShoreMap, suv).r * _ShoreRange;
                float2 edge = abs(suv - 0.5) * 2.0;
                half outside = smoothstep(0.92, 1.0, max(edge.x, edge.y));
                // straight coast z = _CoastZ; west of _CoastWest.x the coast turns north-west (the long beach beyond
                // the world edge); the vertical gap there is scaled to the distance across the slanted line
                half west = step(pw.x, _CoastWest.x);
                float coastZ = lerp(_CoastZ, _CoastWest.y + (_CoastWest.x - pw.x) * _CoastWest.z, west);
                float open = max(coastZ - pw.z, 0.0) * lerp(1.0, rsqrt(1.0 + _CoastWest.z * _CoastWest.z), west);
                half shore = lerp(mapped, min(open, _ShoreRange), outside);

                // colour: turquoise shallows -> sea blue -> deep blue towards the open sea
                half near = saturate(shore / _ShoreRange);
                half3 albedo = lerp(_ShallowColor.rgb, _BaseColor.rgb, smoothstep(0.0, 0.6, near));
                half deep = saturate(max(open - 60.0, 0.0) / 500.0) * smoothstep(0.7, 1.0, near);
                albedo = lerp(albedo, _DeepColor.rgb, deep);

                // light: sun (with the shadows of piers and beach buildings) + sky light, sky reflection, sun glint
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = float4(GetNormalizedScreenSpaceUV(i.positionCS), 0.0, 1.0);
                #else
                float4 shadowCoord = TransformWorldToShadowCoord(pw);
                #endif
                Light sun = GetMainLight(shadowCoord, pw, half4(1, 1, 1, 1));
                half shade = lerp(0.55, 1.0, sun.shadowAttenuation);            // water in shade still shows the sky
                half3 sky = SampleSH(N);
                half3 color = albedo * (sky + sun.color * (saturate(dot(N, sun.direction)) * shade));
                // V17: light through the wave crests when looking towards the sun: the glowing sea green of a low sun
                half towardsSun = pow(saturate(dot(-V, sun.direction)), 3.0);
                half crest = saturate(0.35 + (ripple.x * sun.direction.x + ripple.y * sun.direction.z) * 0.8);
                color += _ScatterColor.rgb * sun.color * (towardsSun * crest * shade * (1.0 - 0.7 * near));
                // V17: Schlick fresnel (water reflects 2 % straight down, almost everything at grazing angles)
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(N, V)), 5.0);
                half3 reflection = GlossyEnvironmentReflection(reflect(-V, N), 0.08, 1.0);
                color = lerp(color, reflection, saturate(fresnel * _Reflection * 1.6));
                half3 H = normalize(sun.direction + V);
                half nh = saturate(dot(N, H));
                color += sun.color * (shade * (pow(nh, _Glint.x) * _Glint.y + pow(nh, _Sheen.x) * _Sheen.y * fresnel * 4.0));

                // foam at the water's edge: a soft band that slides in and out, broken up by the waves,
                // and one thin line that travels towards the shore and fades
                half breakup = (ripple.x + ripple.y) * _Foam.w;
                half slide = sin(t * _Foam.z + pw.x * 0.031 + pw.z * 0.017);
                half width = max(_Foam.x + _Foam.y * slide + breakup, 0.3);
                half foam = 1.0 - smoothstep(width * 0.35, width, shore);
                half phase = frac(t * 0.11 + pw.x * 0.004);
                half lineAt = lerp(11.0, 2.5, phase) + breakup;
                half travelling = (1.0 - smoothstep(0.0, 0.8, abs(shore - lineAt))) * (1.0 - phase) * 0.55;
                foam = saturate(max(foam, travelling)) * step(0.001, 1.0 - near);
                half3 foamLit = _FoamColor.rgb * (SampleSH(half3(0, 1, 0)) + sun.color * (saturate(sun.direction.y + 0.25) * shade));
                color = lerp(color, foamLit, foam);

                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass                               // depth for renderers that run a depth prepass
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
    }
}
