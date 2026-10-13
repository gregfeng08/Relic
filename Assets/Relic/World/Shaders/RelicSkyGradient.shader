// Gradient skybox with a stylized sun disc and soft halo. Colors and sun direction are driven
// by Relic.World.Runtime.SkyController so fog and ambient always match the sky.
Shader "Relic/World/SkyGradient"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.22, 0.47, 0.88, 1)
        _HorizonColor ("Horizon", Color) = (0.74, 0.87, 0.98, 1)
        _GroundColor ("Below horizon", Color) = (0.62, 0.70, 0.78, 1)
        _SunColor ("Sun", Color) = (1, 0.96, 0.85, 1)
        _SunDirection ("Direction toward the sun", Vector) = (0.3, 0.6, 0.4, 0)
        _SunSize ("Sun disc size (cos)", Range(0.99, 1)) = 0.9993
        _HorizonSharpness ("Horizon sharpness", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _SunColor;
                float4 _SunDirection;
                float _SunSize;
                float _HorizonSharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half up = saturate(d.y);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 1.0 / _HorizonSharpness));
                half3 col = d.y >= 0 ? sky : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-d.y * 6.0));

                float3 s = normalize(_SunDirection.xyz);
                float cosAngle = dot(d, s);
                half disc = smoothstep(_SunSize - 0.00015, _SunSize, cosAngle);
                half halo = pow(saturate(cosAngle), 64.0) * 0.35 + pow(saturate(cosAngle), 8.0) * 0.12;
                col += _SunColor.rgb * halo;
                col = lerp(col, _SunColor.rgb * 2.5, disc);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
