// Instanced toon props (trees, bushes, rocks, shrubs): vertex-color albedo, the shared cel ramp,
// wind sway from vertex alpha, distance shrink-fade, inverted-hull outline, real shadows.
Shader "Relic/World/ToonProp"
{
    Properties
    {
        _RimStrength ("Rim strength", Range(0, 2)) = 0.35
        _OutlineColor ("Outline color", Color) = (0.1, 0.09, 0.1, 1)
        _OutlineWidth ("Outline width (px at 1080p)", Range(0, 8)) = 1.4
        _FadeStart ("Shrink-fade start (m)", Float) = 0
        _FadeEnd ("Shrink-fade end (m, 0 = off)", Float) = 0
        _WindStrength ("Wind strength", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "RelicPropCommon.hlsl"

        struct PropAttributes
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
            Cull Back

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
                half3 normalWS : TEXCOORD1;
                half3 albedo : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(PropAttributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = RelicPropPositionWS(input.positionOS.xyz, input.color.a);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.albedo = RelicPropAlbedo(input.color.rgb);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half3 color = RelicToonLight(i.albedo, i.positionWS, n, v, _RimStrength);
                return half4(MixFog(color, i.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; };

            Varyings OutlineVert(PropAttributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float4 positionCS = TransformWorldToHClip(RelicPropPositionWS(input.positionOS.xyz, input.color.a));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float len = max(length(normalCS), 1e-5);
                float pixels = _OutlineWidth * (_ScreenParams.y / 1080.0);
                // Thin out with distance so far forests don't turn into outline.
                float fade = saturate(30.0 / max(positionCS.w, 1e-3));
                positionCS.xy += (normalCS / len) * (pixels * 2.0 / _ScreenParams.xy) * positionCS.w * fade;
                o.positionCS = positionCS;
                o.fogFactor = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_Target { return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1.0h); }
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

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(PropAttributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = RelicShadowPositionCS(RelicPropPositionWS(input.positionOS.xyz, input.color.a), TransformObjectToWorldNormal(input.normalOS));
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

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(PropAttributes input)
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
