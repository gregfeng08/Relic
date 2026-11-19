using System;
using System.Collections.Generic;
using Relic.World.Regions;
using Relic.World.Terrain;

namespace Relic.World.Editor
{
    /// <summary>
    /// The "shapes" capture set (RLC-069): mountain and mesa silhouettes from a flying height, for
    /// checking stair-stepped peaks and paper-thin walls. Every seed gets its tallest peak, a
    /// badlands mesa and a mountain range vista; seed 1338 also gets the two playtest spots.
    /// File names start with the seed so several seeds can share one folder (-captureSeeds).
    /// </summary>
    internal static partial class CaptureViews
    {
        public static void AddShapeViews(TerrainGenerator gen, List<CaptureView> views)
        {
            string s = gen.Seed + "_";
            if (gen.Seed == 1338)
            {
                // Playtest: stair-stepped Badlands peak at (1917.7, 440.7); the peaks rise to its north-east.
                Look(views, s + "g1_stairs", 2010.0, 530.0, 1780.0, 300.0, 70f, 40f);
                Look(views, s + "g1_stairs_far", 2010.0, 530.0, 1600.0, 120.0, 140f, 30f);
                // Playtest: paper-thin Highlands wall near (1351.4, 1238.9), running north-south at x ~1310.
                Look(views, s + "g2_slice", 1320.0, 1290.0, 1530.0, 1120.0, 60f, 40f);
                Look(views, s + "g2_slice_side", 1320.0, 1290.0, 1320.0, 1000.0, 80f, 40f);
            }

            // The tallest peak within 3 km, from 450 m away and 100 m up.
            if (FindBest(gen, 3000.0, 32.0, (x, z) => gen.HeightAt(x, z), out double px, out double pz))
            {
                double a = Uphill(gen, px - 80.0, pz) + Math.PI * 0.15;
                Look(views, s + "s1_peak", px, pz, px - Math.Sin(a) * 450.0, pz - Math.Cos(a) * 450.0, 100f, -60f);
            }

            // A badlands mesa: ground in the mesa band with relief around it.
            if (FindBest(gen, 4000.0, 24.0, (x, z) =>
                {
                    RegionSample r = gen.Regions.Sample(x, z);
                    if (r.Family != RegionFamily.Badlands || r.W0 < 0.8f) return double.MinValue;
                    gen.HeightAndWater(x, z, out float h, out float w);
                    double above = h - w;
                    if (above < 25.0 || above > 70.0) return double.MinValue;
                    return Slope(gen, x, z) - Math.Sqrt(x * x + z * z) * 1e-4;
                }, out double mx, out double mz))
            {
                double a = Uphill(gen, mx, mz);
                Look(views, s + "s2_mesa", mx, mz, mx - Math.Sin(a) * 260.0, mz - Math.Cos(a) * 260.0, 45f, 5f);
            }

            // A mountain range (Highlands or Frost) seen across from a ridge-top height.
            if (FindBest(gen, 4000.0, 48.0, (x, z) =>
                {
                    RegionSample r = gen.Regions.Sample(x, z);
                    if ((r.Family != RegionFamily.Highlands && r.Family != RegionFamily.Frost) || r.W0 < 0.7f) return double.MinValue;
                    return gen.HeightAt(x, z) - Math.Sqrt(x * x + z * z) * 0.01;
                }, out double rx, out double rz))
            {
                double a = Uphill(gen, rx - 60.0, rz) + Math.PI * 0.6;
                Look(views, s + "s3_range", rx, rz, rx - Math.Sin(a) * 900.0, rz - Math.Cos(a) * 900.0, 160f, -80f);
            }
        }

        static void Look(List<CaptureView> views, string name, double x, double z, double camX, double camZ, float camHeight, float aim)
        {
            views.Add(new CaptureView
            {
                Name = name, X = x, Z = z, LookAt = true, CamX = camX, CamZ = camZ,
                CamHeight = camHeight, Height = aim, ViewDistance = 1500f,
            });
        }
    }
}
