// Moving sea for the campus world (Role 3). One flat plane, no wave geometry, no depth or opaque texture:
//  - two copies of one tiling wave normal map scroll in different directions, sizes and speeds: moving sun glints;
//  - a baked shore-distance map, read by world position, gives turquoise shallows, deeper blue further out, and a
//    white foam band at the water's edge that slides in and out;
//  - lit by the main light, the sky light and the sky reflection, with the same fog as URP Lit.
// Cost per pixel: 4 texture reads (2 x wave normal, shore map, sky reflection), no loops, no branches.
// Made by CampusWorldBuilder.BuildMaterials from the "water" block in Data/materials.json; if this shader is
// missing, the sea stays the flat URP Lit material.
Shader "CarcavelosCircuit/Sea"
{
    Properties
    {
        _BaseColor ("Sea colour", Color) = (0.19, 0.54, 0.83, 1)
        _ShallowColor ("Shallow water (near the shore)", Color) = (0.33, 0.80, 0.78, 1)
        _DeepColor ("Deep water (open sea)", Color) = (0.07, 0.30, 0.62, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        [NoScaleOffset] [Normal] _WaveNormal ("Wave normal map (tiling)", 2D) = "bump" {}
        [NoScaleOffset] _ShoreMap ("Shore distance map (R, linear)", 2D) = "white" {}
        _ShoreRect ("Shore map: world x0, z0, 1 / width, 1 / depth", Vector) = (0, 0, 0.001, 0.001)
        _ShoreRange ("Shore map: metres at value 1", Float) = 60
        _CoastZ ("Coast line outside the map (world z)", Float) = -100
        _WaveA ("Waves A: size m, direction x, direction z, speed m/s", Vector) = (13, 1, 0.35, 0.9)
        _WaveB ("Waves B: size m, direction x, direction z, speed m/s", Vector) = (21, -0.45, 1, 0.6)
        _NormalStrength ("Wave strength", Range(0, 1)) = 0.3
        _Glint ("Sun glint: sharpness, strength", Vector) = (260, 2.4, 0, 0)
        _Reflection ("Sky reflection", Range(0, 1)) = 0.45
        _Foam ("Foam: width m, slide m, speed, breakup", Vector) = (2.2, 1.6, 0.7, 2.5)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShallowColor, _DeepColor, _FoamColor;
            float4 _ShoreRect, _WaveA, _WaveB;
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
                half calm = 1.0 - 0.6 * saturate(dist / 700.0);                  // far water: calmer, no shimmer
                half3 N = normalize(half3(ripple.x * _NormalStrength * calm, 1.0, ripple.y * _NormalStrength * calm));

                // distance to the shore in metres: baked map near the world, a straight coast line far outside it
                float2 suv = (pw.xz - _ShoreRect.xy) * _ShoreRect.zw;
                half mapped = SAMPLE_TEXTURE2D(_ShoreMap, sampler_ShoreMap, suv).r * _ShoreRange;
                float2 edge = abs(suv - 0.5) * 2.0;
                half outside = smoothstep(0.92, 1.0, max(edge.x, edge.y));
                float open = max(_CoastZ - pw.z, 0.0);                           // metres south of the coast line
                half shore = lerp(mapped, min(open, _ShoreRange), outside);

                // colour: turquoise shallows -> sea blue -> deep blue towards the open sea
                half near = saturate(shore / _ShoreRange);
                half3 albedo = lerp(_ShallowColor.rgb, _BaseColor.rgb, smoothstep(0.0, 0.6, near));
                half deep = saturate(max(open - 60.0, 0.0) / 500.0) * smoothstep(0.7, 1.0, near);
                albedo = lerp(albedo, _DeepColor.rgb, deep);

                // light: sun + sky light, sky reflection at flat angles, a sharp sun glint
                Light sun = GetMainLight();
                half3 sky = SampleSH(N);
                half3 color = albedo * (sky + sun.color * saturate(dot(N, sun.direction)));
                half fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);
                half3 reflection = GlossyEnvironmentReflection(reflect(-V, N), 0.12, 1.0);
                color = lerp(color, reflection, fresnel * _Reflection);
                half3 H = normalize(sun.direction + V);
                color += sun.color * (pow(saturate(dot(N, H)), _Glint.x) * _Glint.y);

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
                half3 foamLit = _FoamColor.rgb * (SampleSH(half3(0, 1, 0)) + sun.color * saturate(sun.direction.y + 0.25));
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
