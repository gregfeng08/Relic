// The hero's single material: the World toon look (same cel ramp, rim, shadows and fog, via
// RelicToonCommon.hlsl) driven per vertex, so the whole hero is one draw plus one outline pass.
//   COLOR.rgb     albedo
//   TEXCOORD0.x   outline width (px at 1080p); 0 = none
//   TEXCOORD0.y   emission: 0 = lit toon, > 0 = unlit albedo * emission (face paint, glow)
//   TEXCOORD0.z   rim strength (TEXCOORD0.w unused)
//   TEXCOORD1     shade tint rgb, a = how much it replaces the world's cool shade (hair, skin)
//   TANGENT.xyz   the smooth surface normal, for the outline (NORMAL may be bent, e.g. the face)
// The outline color is a darker, more saturated version of the albedo (softer than black).
Shader "Relic/Character/Toon"
{
    Properties
    {
        _OutlineDarken ("Outline darkness", Range(0, 1)) = 0.38
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Relic/World/Shaders/RelicToonCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _OutlineDarken;
        CBUFFER_END
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; half4 color : COLOR; float4 data : TEXCOORD0; float4 shade : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half3 data : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half4 shade : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                o.data = input.data.yzw;
                o.shade = input.shade;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 albedo = i.color.rgb;
                if (i.data.x > 0.0h) return half4(MixFog(albedo * i.data.x, i.fogFactor), 1.0h);

                half3 n = normalize(i.normalWS);
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half ndl = dot(n, mainLight.direction);
                half shadow = smoothstep(0.35h, 0.65h, mainLight.shadowAttenuation);
                // Two tones with a soft edge (Genshin-like), a hint of light kept in cast shadow.
                half ramp = smoothstep(-0.04h, 0.16h, ndl);
                half lit = ramp * shadow;
                lit = max(lit, ramp * 0.15h);

                half3 ambient = RelicAmbient(n);
                // Hair and skin carry their own (warm) shade tint instead of the world's cool blue.
                half3 shadeTint = lerp(_RelicShadeColor.rgb, i.shade.rgb, i.shade.a);
                half3 shade = albedo * (shadeTint * 0.92h + ambient * 0.22h);
                half3 bright = albedo * (mainLight.color * 0.92h + ambient * 0.3h);
                half3 color = lerp(shade, bright, lit);
                half rim = pow(1.0h - saturate(dot(n, v)), 4.0h) * i.data.y;
                color += rim * lerp(_RelicAmbientSky.rgb, mainLight.color, 0.5h) * (0.35h + 0.65h * lit);
                return half4(MixFog(color, i.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; half4 color : COLOR; float4 data : TEXCOORD0; float4 shade : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 color : TEXCOORD0; half fogFactor : TEXCOORD1; };

            Varyings OutlineVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.tangentOS.xyz);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float len = max(length(normalCS), 1e-5);
                float pixels = input.data.x * (_ScreenParams.y / 1080.0);
                float distanceFade = saturate(12.0 / max(positionCS.w, 1e-3));
                positionCS.xy += (normalCS / len) * (pixels * 2.0 / _ScreenParams.xy) * positionCS.w * lerp(0.4, 1.0, distanceFade);
                // Zero-width outlines (face paint, glow) collapse to a point: no area, never drawn.
                if (input.data.x <= 0.0) positionCS = float4(0.0, 0.0, 0.0, 1.0);
                o.positionCS = positionCS;
                half3 c = input.color.rgb;
                o.color = c * c * _OutlineDarken + c * 0.08h;
                o.fogFactor = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_Target { return half4(MixFog(i.color, i.fogFactor), 1.0h); }
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
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                o.positionCS = RelicShadowPositionCS(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS));
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
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
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
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            Varyings DNVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 DNFrag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0.0h); }
            ENDHLSL
        }
    }
}
