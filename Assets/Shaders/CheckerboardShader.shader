Shader "Custom/FullCheckerboardShader"
{
    Properties
    {
        _Color1 ("Color1", Color) = (1, 1, 1, 1)
        _Color2 ("Color2", Color) = (0, 0, 0, 1)
        _Size ("Size", Range(1, 100)) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            float _Size;
            float4 _Color1;
            float4 _Color2;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Adjust UV coordinates based on the size
                o.uv = v.uv * _Size;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Compute the checkerboard pattern
                float checker = fmod(floor(i.uv.x) + floor(i.uv.y), 2.0);
                return lerp(_Color1, _Color2, checker);
            }
            ENDCG
        }
    }
}
