#ifndef RELIC_PROP_COMMON_INCLUDED
#define RELIC_PROP_COMMON_INCLUDED

// Shared vertex code for instanced props (trees, rocks, bushes, grass): distance shrink-fade,
// wind sway from the vertex color alpha, per-instance color variation. Drawn with
// Graphics.RenderMeshInstanced (GPU instancing: unity_ObjectToWorld is per instance).

#include "RelicToonCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    half _RimStrength;
    half4 _OutlineColor;
    float _OutlineWidth;
    float _FadeStart;   // m: instances start shrinking away here...
    float _FadeEnd;     // ...and are gone here (0 = never)
    float _WindStrength;
CBUFFER_END

float4 _RelicWind; // xz: wind direction, w: gust speed multiplier (set by PropRenderer)

float RelicInstanceHash()
{
    float3 o = TransformObjectToWorld(float3(0, 0, 0)) + _RelicWorldOffset.xyz;
    return frac(sin(dot(floor(o.xz * 4.0), float2(12.9898, 78.233))) * 43758.5453);
}

// World position of a prop vertex after shrink-fade and wind. sway = vertex color alpha.
float3 RelicPropPositionWS(float3 positionOS, half sway)
{
    float3 origin = TransformObjectToWorld(float3(0, 0, 0));
    float dist = distance(origin, _WorldSpaceCameraPos);
    float keep = _FadeEnd > 0.0 ? 1.0 - smoothstep(_FadeStart, _FadeEnd, dist) : 1.0;
    float3 positionWS = TransformObjectToWorld(positionOS * keep);

    float2 dir = dot(_RelicWind.xz, _RelicWind.xz) > 1e-4 ? _RelicWind.xz : float2(0.8, 0.6);
    float gustSpeed = _RelicWind.w > 0.0 ? _RelicWind.w : 1.0;
    float2 world = origin.xz + _RelicWorldOffset.xz;
    float phase = dot(world, float2(0.071, 0.053));
    float t = _Time.y * gustSpeed;
    float gust = 0.45 + 0.4 * sin(t * 1.1 + phase) + 0.25 * sin(t * 2.7 + phase * 1.9);
    float height = max(positionWS.y - origin.y, 0.0);
    float amount = sway * _WindStrength * (0.12 + 0.05 * height);
    positionWS.xz += dir * gust * amount;
    positionWS.y += sin(t * 6.0 + phase * 3.0 + positionOS.x * 4.0) * 0.015 * sway * _WindStrength;
    return positionWS;
}

half3 RelicPropAlbedo(half3 vertexColorSRGB)
{
    half3 albedo = SRGBToLinear(vertexColorSRGB);
    half h = (half)RelicInstanceHash();
    // A little brightness and hue drift per instance so a forest isn't one tree repeated.
    albedo *= lerp(0.88h, 1.1h, h);
    albedo.r *= lerp(0.94h, 1.08h, frac(h * 7.13h));
    return albedo;
}

#endif
