using UnityEngine;
using UnityEngine.Rendering;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Sun, gradient sky, fog and toon ambient, kept consistent with each other: fog fades to the
    /// sky's horizon color, so far terrain melts into the sky instead of popping at the view edge.
    /// Optional slow day cycle (off by default for M1).
    /// </summary>
    public sealed class SkyController : MonoBehaviour
    {
        public Material SkyMaterial;
        public Light Sun;

        [Header("Sun")]
        [Range(-10f, 90f)] public float SunElevation = 40f;
        [Range(0f, 360f)] public float SunAzimuth = 215f;
        public Color SunColor = new Color(1.0f, 0.95f, 0.86f);
        public float SunIntensity = 1.25f;
        public bool DayCycle;
        [Tooltip("Seconds for a full day when DayCycle is on.")]
        public float DayLength = 600f;

        [Header("Sky")]
        public Color Zenith = new Color(0.20f, 0.45f, 0.88f);
        public Color Horizon = new Color(0.72f, 0.86f, 0.98f);
        public Color BelowHorizon = new Color(0.60f, 0.68f, 0.78f);

        [Header("Toon ambient")]
        public Color AmbientSky = new Color(0.55f, 0.66f, 0.85f);
        public Color AmbientGround = new Color(0.38f, 0.36f, 0.33f);
        [Tooltip("Multiplier for the shaded band: cool and fairly bright, like Genshin's shadows.")]
        public Color ShadeColor = new Color(0.60f, 0.60f, 0.78f);

        [Header("Fog")]
        public float FogStart = 380f;
        public float FogEnd = 1500f;

        public void Apply()
        {
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Euler(SunElevation, SunAzimuth, 0f);
                float day = Mathf.Clamp01(SunElevation / 15f);
                Sun.color = Color.Lerp(new Color(1f, 0.6f, 0.4f), SunColor, day);
                Sun.intensity = SunIntensity * Mathf.Lerp(0.15f, 1f, day);
                Sun.shadows = LightShadows.Hard; // crisp shadows: players dodge by them
                Sun.shadowStrength = 1f;
                RenderSettings.sun = Sun;
            }

            Vector3 toSun = Sun != null ? -Sun.transform.forward : Quaternion.Euler(SunElevation, SunAzimuth, 0f) * Vector3.back;
            if (SkyMaterial != null)
            {
                SkyMaterial.SetColor(ShaderIds.ZenithColor, Zenith);
                SkyMaterial.SetColor(ShaderIds.HorizonColor, Horizon);
                SkyMaterial.SetColor(ShaderIds.GroundColor, BelowHorizon);
                SkyMaterial.SetColor(ShaderIds.SunColor, SunColor);
                SkyMaterial.SetVector(ShaderIds.SunDirection, toSun);
                RenderSettings.skybox = SkyMaterial;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.fogColor = Horizon;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = Color.Lerp(AmbientSky, AmbientGround, 0.5f);
            RenderSettings.ambientGroundColor = AmbientGround;

            Shader.SetGlobalVector(ShaderIds.AmbientSky, (Vector4)AmbientSky.linear); // vectors are never color-converted
            Shader.SetGlobalVector(ShaderIds.AmbientGround, (Vector4)AmbientGround.linear); // vectors are never color-converted
            Shader.SetGlobalVector(ShaderIds.ShadeColor, (Vector4)ShadeColor.linear); // vectors are never color-converted
        }

        void OnEnable() => Apply();

        void OnValidate() { if (Application.isPlaying) Apply(); }

        void Update()
        {
            if (DayCycle && Application.isPlaying)
            {
                SunElevation = Mathf.Sin(Time.time / DayLength * Mathf.PI * 2f) * 70f + 10f;
                Apply();
            }
        }
    }
}
