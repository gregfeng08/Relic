using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Underwater look: when the view camera dips below the water surface, fog closes in to a
    /// murky teal (darker with depth) and the camera clears to the fog color instead of the sky,
    /// so open water fades into murk rather than showing sky. Blended over ~0.15 s.
    /// </summary>
    public sealed partial class SkyController
    {
        /// <summary>0 above water, 1 fully underwater (smoothed).</summary>
        public float UnderwaterAmount { get; private set; }

        float _appliedUnderwater = -1f;
        float _appliedDepth = -1f;

        void LateUpdate()
        {
            if (View == null) return;
            float target = View.Underwater ? 1f : 0f;
            // Snap on the way in (no sky flash below the surface), ease on the way out.
            UnderwaterAmount = target > UnderwaterAmount ? target : Mathf.MoveTowards(UnderwaterAmount, target, Time.deltaTime / 0.15f);
            float depth = View.CameraWaterDepth;
            if (Mathf.Abs(UnderwaterAmount - _appliedUnderwater) < 1e-3f && Mathf.Abs(depth - _appliedDepth) < 0.05f) return;
            _appliedUnderwater = UnderwaterAmount;
            _appliedDepth = depth;
            ApplyUnderwater(UnderwaterAmount, depth);
        }

        /// <summary>Sets fog (and the view camera's clear) for an underwater amount 0..1 at a depth (m). Also used by captures.</summary>
        internal void ApplyUnderwater(float amount, float depth)
        {
            float deep = Mathf.Clamp01(depth / 30f);
            Color water = Color.Lerp(UnderwaterFog, UnderwaterDeepFog, deep);
            float fogEnd = Mathf.Lerp(UnderwaterFogEnd, UnderwaterFogEnd * 0.5f, deep);
            RenderSettings.fogColor = Color.Lerp(Horizon, water, amount);
            RenderSettings.fogStartDistance = Mathf.Lerp(FogStart, 0f, amount);
            RenderSettings.fogEndDistance = Mathf.Lerp(FogEnd, fogEnd, amount);
            Camera cam = View != null ? View.Cam : null;
            if (cam != null)
            {
                bool under = amount > 0.5f;
                cam.clearFlags = under ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                cam.backgroundColor = water;
            }
        }
    }
}
