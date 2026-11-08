using System;
using System.Collections.Generic;
using Relic.World.Regions;
using Relic.World.Runtime;
using Relic.World.Terrain;
using UnityEngine;

namespace Relic.World.Editor
{
    /// <summary>
    /// Extra capture sets for SandboxCapture: close-ups that show terrain artifacts (peaks, mesas,
    /// cliff bands, region borders at several LODs), diving and props. Viewpoints are found by
    /// searching the height function, so they follow the seed.
    /// </summary>
    internal static partial class CaptureViews
    {
        static readonly int[] RidgeLods = { 0, 1, 2 };

        public static void AddRidgeViews(TerrainGenerator gen, List<CaptureView> views)
        {
            // 1. The highest peak within 2.5 km.
            FindBest(gen, 2500.0, 24.0, (x, z) => gen.HeightAt(x, z), out double px, out double pz);
            AddCloseUps(gen, views, "r1_peak", px, pz, 170.0, 35f, -25f);

            // 2. A badlands mesa: steepest terrace riser in a badlands region.
            if (FindBest(gen, 4000.0, 16.0, (x, z) =>
                {
                    RegionSample r = gen.Regions.Sample(x, z);
                    if (r.Family != RegionFamily.Badlands || r.W0 < 0.85f) return double.MinValue;
                    gen.HeightAndWater(x, z, out float h, out float w);
                    if (h < w + 8f) return double.MinValue;
                    return Slope(gen, x, z) - Math.Abs(Slope(gen, x + 12, z) - Slope(gen, x - 12, z)) * 0.1;
                }, out double mx, out double mz))
                AddCloseUps(gen, views, "r2_mesa", mx, mz, 70.0, 18f, 0f, Uphill(gen, mx, mz));

            // 3. A cliff band (steepest non-badlands slope near spawn).
            if (FindBest(gen, 2000.0, 12.0, (x, z) =>
                {
                    RegionSample r = gen.Regions.Sample(x, z);
                    if (r.Family == RegionFamily.Badlands) return double.MinValue;
                    gen.HeightAndWater(x, z, out float h, out float w);
                    if (h < w + 3f) return double.MinValue;
                    return Slope(gen, x, z);
                }, out double cx, out double cz))
                AddCloseUps(gen, views, "r3_cliff", cx, cz, 60.0, 6f, 6f, Uphill(gen, cx, cz));

            // 4. A region border between two different families, with relief.
            if (FindBest(gen, 3000.0, 20.0, (x, z) =>
                {
                    RegionSample r = gen.Regions.Sample(x, z);
                    if (r.W1 < 0.3f) return double.MinValue;
                    if (gen.Regions.ParamsOf(r.Id1).Family == r.Family) return double.MinValue;
                    gen.HeightAndWater(x, z, out float h, out float w);
                    if (h < w + 4f) return double.MinValue;
                    return h * 0.05 + Slope(gen, x, z);
                }, out double bx, out double bz))
                AddCloseUps(gen, views, "r4_border", bx, bz, 110.0, 25f, 0f);
        }

        static void AddCloseUps(TerrainGenerator gen, List<CaptureView> views, string name, double x, double z, double back, float camHeight, float aim, double ang = 0.6)
        {
            // Look at the spot from the south-west (sun comes from the south-west at azimuth 215,
            // so this shows the lit side; the low-sun variant rakes across the slope).

            double camX = x - Math.Sin(ang) * back, camZ = z - Math.Cos(ang) * back;
            foreach (int lod in RidgeLods)
                views.Add(new CaptureView
                {
                    Name = name + "_lod" + lod, X = x, Z = z, LookAt = true, CamX = camX, CamZ = camZ,
                    CamHeight = camHeight, Height = aim, ForceLod = lod, ViewDistance = 700f,
                });
            views.Add(new CaptureView
            {
                Name = name + "_lowsun", X = x, Z = z, LookAt = true, CamX = camX, CamZ = camZ,
                CamHeight = camHeight, Height = aim, ViewDistance = 1000f, SunElevation = 14f, SunAzimuth = 300f,
            });
        }

        /// <summary>Angle (atan2(x, z)) of the uphill direction: a camera placed opposite it looks at the slope's face.</summary>
        static double Uphill(TerrainGenerator gen, double x, double z)
        {
            double gx = gen.HeightAt(x + 3.0, z) - gen.HeightAt(x - 3.0, z);
            double gz = gen.HeightAt(x, z + 3.0) - gen.HeightAt(x, z - 3.0);
            return Math.Atan2(gx, gz);
        }

        static double Slope(TerrainGenerator gen, double x, double z)
        {
            float h = gen.HeightAt(x, z);
            float hx = gen.HeightAt(x + 2.0, z), hz = gen.HeightAt(x, z + 2.0);
            return Math.Sqrt((hx - h) * (hx - h) + (hz - h) * (hz - h)) / 2.0;
        }

        /// <summary>Grid search for the highest score within a radius of the origin. False if nothing scored.</summary>
        static bool FindBest(TerrainGenerator gen, double radius, double step, Func<double, double, double> score, out double bestX, out double bestZ)
        {
            double best = double.MinValue;
            bestX = 0; bestZ = 0;
            int n = (int)(radius / step);
            for (int j = -n; j <= n; j++)
            for (int i = -n; i <= n; i++)
            {
                double x = i * step, z = j * step;
                if (x * x + z * z > radius * radius) continue;
                double s = score(x, z);
                if (s > best) { best = s; bestX = x; bestZ = z; }
            }
            return best > double.MinValue;
        }

        /// <summary>The tallest peaks outside Frost regions (snow caps must show in every climate).</summary>
        public static void AddSnowViews(TerrainGenerator gen, List<CaptureView> views)
        {
            AddTallest(gen, views, "s1_peak_nonfrost", f => f != RegionFamily.Frost, 480.0, 100f);
            AddTallest(gen, views, "s2_peak_badlands", f => f == RegionFamily.Badlands, 320.0, 70f);
            AddTallest(gen, views, "s3_peak_forest", f => f == RegionFamily.Forest || f == RegionFamily.Meadow, 320.0, 70f);
        }

        static void AddTallest(TerrainGenerator gen, List<CaptureView> views, string name, Func<RegionFamily, bool> family, double back, float camHeight)
        {
            if (!FindBest(gen, 5000.0, 32.0, (x, z) =>
                {
                    RegionSample r = gen.Regions.Sample(x, z);
                    return r.W0 > 0.6f && family(r.Family) ? gen.HeightAt(x, z) : double.MinValue;
                }, out double px, out double pz)) return;
            double ang = Uphill(gen, px - 60.0, pz) + Math.PI * 0.15;
            views.Add(new CaptureView
            {
                Name = name, X = px, Z = pz, LookAt = true, CamX = px - Math.Sin(ang) * back, CamZ = pz - Math.Cos(ang) * back,
                CamHeight = camHeight, Height = -40f, ViewDistance = 1300f,
            });
        }

        /// <summary>Underwater: a deep spot near spawn, looking along the bed and up at the surface.</summary>
        public static void AddDiveViews(TerrainGenerator gen, List<CaptureView> views)
        {
            if (!FindBest(gen, 3000.0, 16.0, (x, z) =>
                {
                    gen.HeightAndWater(x, z, out float h, out float w);
                    float d = w - h;
                    if (d < 10f || d > 40f) return double.MinValue;
                    for (int k = 0; k < 6; k++) // open water around, not a pit
                    {
                        double a = k * Math.PI / 3.0;
                        gen.HeightAndWater(x + 35.0 * Math.Cos(a), z + 35.0 * Math.Sin(a), out float h2, out float w2);
                        if (w2 - h2 < 6f) return double.MinValue;
                    }
                    return -Math.Sqrt(x * x + z * z);
                }, out double cx, out double cz)) return;
            gen.HeightAndWater(cx, cz, out float g, out float wl);
            float depth = wl - g;
            views.Add(new CaptureView { Name = "d1_dive_bed", X = cx + 25.0, Z = cz + 10.0, LookAt = true, CamX = cx, CamZ = cz, CamHeight = depth * 0.5f, Height = 0.5f, Underwater = true, ViewDistance = 400f });
            views.Add(new CaptureView { Name = "d2_dive_up", X = cx + 6.0, Z = cz + 2.0, LookAt = true, CamX = cx, CamZ = cz, CamHeight = depth * 0.35f, Height = depth + 6f, Underwater = true, ViewDistance = 400f });
            views.Add(new CaptureView { Name = "d3_surface_above", X = cx + 30.0, Z = cz + 12.0, LookAt = true, CamX = cx, CamZ = cz, CamHeight = 2.5f, Height = depth - 1f, ViewDistance = 600f });
        }

        public static void AddPropViews(TerrainGenerator gen, List<CaptureView> views) { }

        internal static void AfterChunksBuilt(SandboxCapture.CaptureEnv env, CaptureView v, ChunkMeshData[] chunks, Transform root, double originX, double originZ) { }

        internal static void BeforeRender(SandboxCapture.CaptureEnv env, CaptureView v, Camera cam)
        {
            if (!v.Underwater) return;
            float water = env.Gen.WaterLevelAt(v.CamX, v.CamZ);
            float depth = Mathf.Max(0.5f, water - cam.transform.position.y);
            env.Sky.ApplyUnderwater(1f, depth);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
        }

        internal static void AfterRender(SandboxCapture.CaptureEnv env, CaptureView v)
        {
            if (v.Underwater) env.Sky.ApplyUnderwater(0f, 0f);
        }
    }
}
