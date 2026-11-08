// Instanced grass and flowers: double-sided blades (both windings in the mesh), up-facing normals (they shade like the
// ground they grow from), strong wind sway, shrink-fade near the draw distance, no outline and no
// shadow casting (they still receive shadows).
Shader "Relic/World/ToonGrass"
{
    Properties
    {
        _RimStrength ("Rim strength", Range(0, 2)) = 0.1
        _OutlineColor ("Unused", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Unused", Float) = 0
        _FadeStart ("Shrink-fade start (m)", Float) = 50
        _FadeEnd ("Shrink-fade end (m)", Float) = 70
        _WindStrength ("Wind strength", Range(0, 3)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "RelicPropCommon.hlsl"

        struct GrassAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back // blades carry both windings

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 albedo : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(GrassAttributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = RelicPropPositionWS(input.positionOS.xyz, input.color.a);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.albedo = RelicPropAlbedo(input.color.rgb);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half3 color = RelicToonLight(i.albedo, i.positionWS, half3(0, 1, 0), v, _RimStrength);
                return half4(MixFog(color, i.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back // blades carry both windings

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(GrassAttributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformWorldToHClip(RelicPropPositionWS(input.positionOS.xyz, input.color.a));
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}
