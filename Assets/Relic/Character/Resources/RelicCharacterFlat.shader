// Unlit color for the hero's painted details (eyes, mouth) and glowing parts (relic amulet,
// weapon orbs and runes). No outline pass: these sit on top of outlined surfaces.
// Color is _BaseColor * _Intensity, so values above 1 read as glow (and feed bloom if enabled).
Shader "Relic/Character/Flat"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Flat"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_BaseColor.rgb * _Intensity, i.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}
