// Toon terrain for Relic: cel-banded main light with crisp shadows, slope-based rock with
// strata, biome splat from vertex color (ground) and UV0 (rock tint), rim light and fog.
// Vertex colors come from Relic.World.Logic and are authored in sRGB.
Shader "Relic/World/ToonTerrain"
{
    Properties
    {
        _RockSlopeStart ("Rock slope start (1 - normal.y)", Range(0, 1)) = 0.2
        _RockSlopeEnd ("Rock slope end", Range(0, 1)) = 0.3
        _StrataScale ("Rock strata height (m)", Float) = 2.6
        _StrataStrength ("Rock strata strength", Range(0, 1)) = 0.22
        _GroundVariation ("Ground color variation", Range(0, 0.5)) = 0.12
        _RimStrength ("Rim strength", Range(0, 2)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "RelicToonCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _RockSlopeStart;
            half _RockSlopeEnd;
            float _StrataScale;
            half _StrataStrength;
            half _GroundVariation;
            half _RimStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float3 rock : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 ground : TEXCOORD2;
                half3 rock : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.ground = SRGBToLinear(input.color.rgb);
                o.rock = SRGBToLinear(input.rock);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));

                float nBig = RelicWorldNoise(i.positionWS, 8.0);
                float nSmall = RelicWorldNoise(i.positionWS, 2.0);

                // Slope rock, with noisy edges so the transition reads as broken rock, not a line.
                half slope = 1.0h - n.y;
                half rockMask = smoothstep(_RockSlopeStart, _RockSlopeEnd, slope + (nBig - 0.5h) * 0.10h + (nSmall - 0.5h) * 0.04h);

                // Horizontal strata so cliffs read as layered rock.
                float strataCoord = i.positionWS.y / _StrataScale + nBig * 0.8;
                half strata = smoothstep(0.35h, 0.5h, abs(frac(strataCoord) - 0.5h) * 2.0h);
                // Fade strata out with distance: thin stripes alias into moire far away.
                half strataFade = saturate(1.0h - (half)(distance(_WorldSpaceCameraPos, i.positionWS) / 220.0));
                half3 rock = i.rock * (1.0h - _StrataStrength * strata * strataFade) * (0.92h + 0.16h * nSmall);

                half3 ground = i.ground * (1.0h - _GroundVariation * 0.5h + _GroundVariation * nBig);

                half3 albedo = lerp(ground, rock, rockMask);
                half3 color = RelicToonLight(albedo, i.positionWS, n, v, _RimStrength);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0h);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = RelicShadowPositionCS(positionWS, normalWS);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings DNVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DNFrag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0.0h); }
            ENDHLSL
        }
    }
}
