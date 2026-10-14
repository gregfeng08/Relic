// Stylized water: depth-tinted (vertex depth from the generator), shoreline foam from the
// scene depth texture, toon sun glint, fresnel toward the sky, receives the main light's shadow.
Shader "Relic/World/Water"
{
    Properties
    {
        _ShallowColor ("Shallow color", Color) = (0.35, 0.82, 0.86, 0.55)
        _DeepColor ("Deep color", Color) = (0.06, 0.28, 0.52, 0.92)
        _FoamColor ("Foam color", Color) = (0.95, 0.98, 1.0, 1)
        _FoamDistance ("Foam distance (m)", Float) = 0.7
        _DepthRange ("Depth for full deep color (m)", Float) = 12
        _GlintSize ("Sun glint size", Range(0.9, 1)) = 0.985
        _WaveScale ("Ripple cell (m)", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog

            #include "RelicToonCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _FoamDistance;
                float _DepthRange;
                half _GlintSize;
                float _WaveScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half depth : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.depth = input.color.r;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Water depth along the view ray from the depth texture (falls back to vertex depth).
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float surfaceEye = i.screenPos.w;
                float viewDepth = max(sceneEye - surfaceEye, 0.0);

                half depth01 = saturate(max(i.depth, viewDepth / _DepthRange));
                half4 col = lerp(_ShallowColor, _DeepColor, depth01);

                // Ripple normal from two drifting noise fields.
                float t = _Time.y;
                float3 wp = i.positionWS;
                float n1 = RelicWorldNoise(wp + float3(t * 0.6, 0, t * 0.4), _WaveScale);
                float n2 = RelicWorldNoise(wp + float3(-t * 0.5, 0, t * 0.7), _WaveScale * 0.5);
                half3 n = normalize(half3((n1 - 0.5h) * 0.25h, 1.0h, (n2 - 0.5h) * 0.25h));
                half3 v = SafeNormalize(GetWorldSpaceViewDir(wp));

                float4 shadowCoord = TransformWorldToShadowCoord(wp);
                Light mainLight = GetMainLight(shadowCoord, wp, half4(1, 1, 1, 1));
                half shadow = lerp(0.65h, 1.0h, smoothstep(0.35h, 0.65h, mainLight.shadowAttenuation));

                half fresnel = pow(1.0h - saturate(dot(n, v)), 5.0h);
                col.rgb = lerp(col.rgb, _RelicAmbientSky.rgb * 1.4h, fresnel * 0.6h);
                col.rgb *= shadow;

                half3 h = normalize(mainLight.direction + v);
                half glint = step(_GlintSize, dot(n, h)) * shadow;
                col.rgb += glint * mainLight.color;
                col.a = saturate(col.a + glint + fresnel * 0.3h);

                half foam = 1.0h - smoothstep(_FoamDistance * 0.6h, _FoamDistance, viewDepth + (n1 - 0.5h) * 0.4h);
                col = lerp(col, _FoamColor, foam * 0.9h);

                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
}
