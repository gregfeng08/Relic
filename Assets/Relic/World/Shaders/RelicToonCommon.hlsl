#ifndef RELIC_TOON_COMMON_INCLUDED
#define RELIC_TOON_COMMON_INCLUDED

// Shared toon (cel) lighting for Relic's world: terrain, characters and props.
// Globals are set by Relic.World.Runtime.SkyController.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float4 _RelicAmbientSky;     // rgb: ambient from above
float4 _RelicAmbientGround;  // rgb: ambient from below
float4 _RelicShadeColor;     // rgb: multiplier for the shaded band (cool, Genshin-like)
float4 _RelicWorldOffset;    // xz: floating-origin offset modulo RELIC_NOISE_PERIOD (keeps world noise stable when the origin moves)

#define RELIC_NOISE_PERIOD 4096.0

// Three bands: shade (0), mid (~0.55), lit (1), with narrow smooth edges against aliasing.
half RelicCelRamp(half ndl)
{
    half a = smoothstep(0.00h, 0.05h, ndl);
    half b = smoothstep(0.50h, 0.55h, ndl);
    return a * 0.55h + b * 0.45h;
}

half3 RelicAmbient(half3 normalWS)
{
    return lerp(_RelicAmbientGround.rgb, _RelicAmbientSky.rgb, saturate(normalWS.y * 0.5h + 0.5h));
}

// Cel-shaded main light with real (hard) shadows. Shadowed areas drop to the shade band,
// so shadows read as crisp shapes on the ground (combat dodges by shadows).
half3 RelicToonLight(half3 albedo, float3 positionWS, half3 normalWS, half3 viewDirWS, half rimStrength)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1));
    half ndl = dot(normalWS, mainLight.direction);
    half shadow = smoothstep(0.35h, 0.65h, mainLight.shadowAttenuation);
    half lit = RelicCelRamp(ndl) * lerp(0.0h, 1.0h, shadow);
    // A shadowed surface that faces the sun keeps a hint of the mid band so form stays readable.
    lit = max(lit, RelicCelRamp(ndl) * 0.18h);

    half3 ambient = RelicAmbient(normalWS);
    half3 shade = albedo * (_RelicShadeColor.rgb + ambient * 0.35h);
    half3 bright = albedo * (mainLight.color * 0.92h + ambient * 0.3h);
    half3 color = lerp(shade, bright, lit);

    half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 4.0h) * rimStrength;
    color += rim * lerp(_RelicAmbientSky.rgb, mainLight.color, 0.5h) * (0.35h + 0.65h * lit);
    return color;
}

// Cheap periodic value noise for breaking up splat edges. Period RELIC_NOISE_PERIOD meters,
// evaluated on (scene position + world offset) so it doesn't jump on origin shifts.
float RelicHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float RelicValueNoise(float2 p, float cellsPerPeriod)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float2 i0 = fmod(fmod(i, cellsPerPeriod) + cellsPerPeriod, cellsPerPeriod);
    float2 i1 = fmod(i0 + 1.0, cellsPerPeriod);
    float a = RelicHash21(i0);
    float b = RelicHash21(float2(i1.x, i0.y));
    float c = RelicHash21(float2(i0.x, i1.y));
    float d = RelicHash21(i1);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// World-stable noise in [0,1] at the given cell size (meters). cellSize must divide RELIC_NOISE_PERIOD.
float RelicWorldNoise(float3 positionWS, float cellSize)
{
    float2 p = (positionWS.xz + _RelicWorldOffset.xz) / cellSize;
    return RelicValueNoise(p, RELIC_NOISE_PERIOD / cellSize);
}

// --- Shared shadow-caster helpers -------------------------------------------------------
float3 _LightDirection;
float3 _LightPosition;

float4 RelicShadowPositionCS(float3 positionWS, float3 normalWS)
{
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    return ApplyShadowClamping(positionCS);
}

#endif
