using System;
using System.Collections.Generic;
using NUnit.Framework;
using Relic.Core.Random;
using Relic.World.Regions;
using Relic.World.Terrain;

namespace Relic.World.Tests
{
    /// <summary>
    /// Terrain shape checks (RLC-069): mountains are massifs and ridges, not paper-thin fins, and
    /// mesa terraces stay in the mesa band instead of stair-stepping tall peaks. Pure geometry
    /// on the height function, so it runs in dotnet and in Unity EditMode.
    /// </summary>
    public class TerrainShapeTests
    {
        static readonly ulong[] Seeds = { 1337, 1338, 7, 42, 2024, 31337, 900001, 5 };

        // --- Thin fins -------------------------------------------------------------------------

        /// <summary>
        /// Profiles in random directions over mountainous ground: every crest (local maximum on a
        /// profile) is tested across 8 directions. A fin is a crest that falls away steeper than
        /// the cap on BOTH sides by at least FinMinDrop: two steep faces closer than ~2x their height.
        /// </summary>
        [Test]
        public void NoThinFins()
        {
            var report = new List<string>();
            int crests = 0, fins = 0;
            foreach (ulong seed in Seeds)
            {
                var g = new TerrainGenerator(seed);
                foreach (Crest c in MountainCrests(g, seed, 100))
                {
                    crests++;
                    double score = ShapeProbe.FinScore(g, c.X, c.Z, out double s, out double dir, out double drop);
                    if (score < ShapeProbe.FinSlope) continue;
                    fins++;
                    if (report.Count < 12)
                        report.Add("seed " + seed + " (" + c.X.ToString("0.0") + ", " + c.Z.ToString("0.0") + ") h " + c.H.ToString("0") +
                                   ": both sides drop " + drop.ToString("0") + " m within " + s.ToString("0") + " m (dir " + dir.ToString("0") + " deg, " +
                                   ShapeProbe.Degrees(score).ToString("0") + " deg) " + g.Regions.Sample(c.X, c.Z).Family);
                }
            }
            TestContext.WriteLine("crests " + crests + ", fins " + fins);
            foreach (string r in report) TestContext.WriteLine(r);
            Assert.That(crests, Is.GreaterThan(1500), "not enough mountain crests sampled");
            Assert.AreEqual(0, fins, "thin fins:\n" + string.Join("\n", report));
        }

        [Test]
        public void GregsSliceSpotIsNotAFin()
        {
            // Playtest report: seed 1338, World (1351.4, 1238.9), region (1,1) Highlands: a paper-thin wall.
            var g = new TerrainGenerator(1338);
            double worst = ShapeProbe.WorstFinNear(g, 1351.4, 1238.9, 120.0, out double wx, out double wz);
            Assert.That(worst, Is.LessThan(ShapeProbe.FinSlope), "fin of " + ShapeProbe.Degrees(worst).ToString("0") + " deg at (" + wx.ToString("0") + ", " + wz.ToString("0") + ")");
        }

        /// <summary>The detector itself: a synthetic knife-edge is a fin, a broad massif and a one-sided cliff are not.</summary>
        [Test]
        public void FinDetectorSeparatesFinsFromMassifsAndCliffs()
        {
            Func<double, double, double> fin = (x, z) => Math.Max(0.0, 60.0 - Math.Abs(x) * 3.0);      // 60 m wall, 40 m wide
            Func<double, double, double> massif = (x, z) => Math.Max(0.0, 200.0 - Math.Abs(x) * 0.9); // 42 deg flanks
            Func<double, double, double> cliff = (x, z) => x < 0 ? 40.0 : x < 10 ? 40.0 - x * 4.0 : 0.0; // one-sided
            Assert.That(ShapeProbe.FinScore(fin, 0, 0, out _, out _, out _), Is.GreaterThan(ShapeProbe.FinSlope));
            Assert.That(ShapeProbe.FinScore(massif, 0, 0, out _, out _, out _), Is.LessThan(ShapeProbe.FinSlope));
            Assert.That(ShapeProbe.FinScore(cliff, -1, 0, out _, out _, out _), Is.LessThan(ShapeProbe.FinSlope));
        }

        /// <summary>
        /// Slope budget over dry land (cliff bands and mesa risers included): steep ground stays
        /// rare. Before RLC-069: 4.5% over 55 degrees, 99th percentile 70 degrees.
        /// </summary>
        [Test]
        public void SteepGroundIsRare()
        {
            var slopes = new List<double>();
            foreach (ulong seed in new ulong[] { 1337, 1338, 7, 42 })
            {
                var g = new TerrainGenerator(seed);
                var rng = new Pcg32(seed ^ 0x51097EUL);
                for (int i = 0; i < 15000; i++)
                {
                    double x = (rng.NextDouble() * 2.0 - 1.0) * 6000.0, z = (rng.NextDouble() * 2.0 - 1.0) * 6000.0;
                    g.HeightAndWater(x, z, out float h, out float w);
                    if (h < w + 2f) continue;
                    double gx = (g.HeightAt(x + 2.0, z) - g.HeightAt(x - 2.0, z)) / 4.0;
                    double gz = (g.HeightAt(x, z + 2.0) - g.HeightAt(x, z - 2.0)) / 4.0;
                    slopes.Add(ShapeProbe.Degrees(Math.Sqrt(gx * gx + gz * gz)));
                }
            }
            slopes.Sort();
            int n = slopes.Count, over = 0;
            foreach (double s in slopes) if (s > 55.0) over++;
            double p99 = slopes[n * 99 / 100], share = over / (double)n;
            TestContext.WriteLine("dry samples " + n + ": p50 " + slopes[n / 2].ToString("0") + ", p99 " + p99.ToString("0") + ", max " + slopes[n - 1].ToString("0") + " deg, over 55 deg " + (100 * share).ToString("0.00") + "%");
            Assert.That(share, Is.LessThan(0.025));
            Assert.That(p99, Is.LessThan(63.0));
        }

        // --- Terraces --------------------------------------------------------------------------

        /// <summary>
        /// Mesa terraces only in the mesa band: no staircase profile more than TerraceBandTop above
        /// the local water table. And mesas still exist below it (so the detector is live).
        /// </summary>
        [Test]
        public void TerracesStayInTheMesaBand()
        {
            var high = new List<string>();
            int highProbes = 0, highTerraced = 0, lowProbes = 0, lowTerraced = 0, ctlProbes = 0, ctlTerraced = 0;
            foreach (ulong seed in Seeds)
            {
                var g = new TerrainGenerator(seed);
                var rng = new Pcg32(seed ^ 0x7E77ACEUL);
                for (int i = 0; i < 20000; i++)
                {
                    double x = (rng.NextDouble() * 2.0 - 1.0) * 8000.0, z = (rng.NextDouble() * 2.0 - 1.0) * 8000.0;
                    RegionSample r = g.Regions.Sample(x, z);
                    bool mesa = r.Blended.Terrace >= 0.3;
                    if (!mesa && r.Blended.Terrace > 0.01) continue;
                    TerrainSample s = g.Sample(x, z);
                    double above = s.Height - s.WaterLevel;
                    if (!mesa)
                    {
                        // Control: the detector's false-positive rate on untouched ground of similar height.
                        if (above <= 12.0 || above >= 45.0 || (i & 7) != 0) continue;
                        ctlProbes++;
                        if (ShapeProbe.IsTerraced(g, x, z, out _)) ctlTerraced++;
                        continue;
                    }
                    // Above the band, all the way down the 64 m probe profile: well over the water
                    // table, or within 12 m of the snow line (the generator fades terraces out below it).
                    if (above - ShapeProbe.ProfileHalf > ShapeProbe.TerraceBandTop || s.Height - ShapeProbe.ProfileHalf > s.SnowLine - 12.0)
                    {
                        highProbes++;
                        if (!ShapeProbe.IsTerraced(g, x, z, out int steps)) continue;
                        highTerraced++;
                        if (high.Count < 12)
                            high.Add("seed " + seed + " (" + x.ToString("0") + ", " + z.ToString("0") + ") h " + s.Height.ToString("0") + ", " + above.ToString("0") +
                                     " m above water, snow line " + s.SnowLine.ToString("0") + ": " + steps + " steps");
                    }
                    else if (above > 12.0 && above < 45.0)
                    {
                        lowProbes++;
                        if (ShapeProbe.IsTerraced(g, x, z, out _)) lowTerraced++;
                    }
                }
            }
            double lowRate = lowTerraced / (double)Math.Max(1, lowProbes), ctlRate = ctlTerraced / (double)Math.Max(1, ctlProbes);
            TestContext.WriteLine("above the band: " + highProbes + " probes, " + highTerraced + " terraced; mesa band: " + lowProbes + " probes, " +
                                  (100 * lowRate).ToString("0.0") + "% terraced; untouched control: " + ctlProbes + " probes, " + (100 * ctlRate).ToString("0.0") + "%");
            foreach (string l in high) TestContext.WriteLine(l);
            Assert.That(highProbes, Is.GreaterThan(50), "not enough tall badlands sampled");
            Assert.AreEqual(0, highTerraced, "terraces above the mesa band:\n" + string.Join("\n", high));
            Assert.That(lowRate, Is.GreaterThan(2.0 * ctlRate + 0.005), "mesas should still be terraced in the mesa band");
        }

        [Test]
        public void GregsStairStepSpotIsSmooth()
        {
            // Playtest report: seed 1338, World (1917.7, 440.7), region (3,0) Badlands: a
            // stair-stepped peak with snow on every step. The snowy slopes must be smooth.
            var g = new TerrainGenerator(1338);
            int terraced = 0, probes = 0;
            for (double dz = -240; dz <= 240; dz += 12)
            for (double dx = -240; dx <= 240; dx += 12)
            {
                double x = 1917.7 + dx, z = 440.7 + dz;
                TerrainSample s = g.Sample(x, z);
                if (s.Height - ShapeProbe.ProfileHalf <= s.SnowLine - 12.0 && s.Height - s.WaterLevel - ShapeProbe.ProfileHalf <= ShapeProbe.TerraceBandTop) continue;
                probes++;
                if (ShapeProbe.IsTerraced(g, x, z, out _)) terraced++;
            }
            TestContext.WriteLine("probes above the band " + probes + ", terraced " + terraced);
            Assert.That(probes, Is.GreaterThan(5), "the peak should still rise above the mesa band");
            Assert.AreEqual(0, terraced);
        }

        // --- Diagnostics -----------------------------------------------------------------------

        [Test, Explicit("diagnostic: slope and fin statistics per family")]
        public void DiagnoseShapes()
        {
            foreach (ulong seed in Seeds)
            {
                var g = new TerrainGenerator(seed);
                var byFamily = new Dictionary<RegionFamily, int[]>();
                foreach (Crest c in MountainCrests(g, seed, 100))
                {
                    RegionFamily f = g.Regions.Sample(c.X, c.Z).Family;
                    if (!byFamily.TryGetValue(f, out int[] n)) byFamily[f] = n = new int[3];
                    n[0]++;
                    double score = ShapeProbe.FinScore(g, c.X, c.Z, out _, out _, out _);
                    if (score >= ShapeProbe.FinSlope) n[1]++;
                    if (score >= 1.0) n[2]++;
                }
                var parts = new List<string>();
                foreach (var kv in byFamily) parts.Add(kv.Key + " " + kv.Value[0] + "/" + kv.Value[1] + "/" + kv.Value[2]);
                TestContext.WriteLine("seed " + seed + " crests/fins/over45: " + string.Join(", ", parts));
            }
            var g2 = new TerrainGenerator(1338);
            TestContext.WriteLine("slice spot worst " + ShapeProbe.Degrees(ShapeProbe.WorstFinNear(g2, 1351.4, 1238.9, 120.0, out double wx, out double wz)).ToString("0") + " deg at " + wx + "," + wz);
        }

        // --- Sampling --------------------------------------------------------------------------

        internal struct Crest { public double X, Z, H; }

        /// <summary>
        /// Crests on random 1.2 km profiles (2 m steps) through ground with relief (>= 40 m above water):
        /// local maxima that rise at least 6 m over the profile within 30 m on both sides.
        /// </summary>
        internal static IEnumerable<Crest> MountainCrests(TerrainGenerator g, ulong seed, int lines)
        {
            var rng = new Pcg32(seed ^ 0xC2E57UL);
            const double step = 2.0;
            const int n = 600;
            var h = new float[n];
            int found = 0, attempts = 0;
            while (found < lines && attempts++ < lines * 60)
            {
                double ox = (rng.NextDouble() * 2.0 - 1.0) * 5000.0, oz = (rng.NextDouble() * 2.0 - 1.0) * 5000.0;
                g.HeightAndWater(ox, oz, out float h0, out float w0);
                if (h0 - w0 < 40f) continue;
                found++;
                double a = rng.NextDouble() * Math.PI;
                double dx = Math.Cos(a), dz = Math.Sin(a);
                for (int i = 0; i < n; i++) h[i] = g.HeightAt(ox + dx * (i - n / 2) * step, oz + dz * (i - n / 2) * step);
                for (int i = 15; i < n - 15; i++)
                {
                    if (h[i] < h[i - 1] || h[i] < h[i + 1]) continue;
                    if (h[i] - h[i - 15] < 6f || h[i] - h[i + 15] < 6f) continue;
                    double x = ox + dx * (i - n / 2) * step, z = oz + dz * (i - n / 2) * step;
                    yield return new Crest { X = x, Z = z, H = h[i] };
                }
            }
        }
    }

    /// <summary>Geometric probes on a height field (shared by the shape tests and diagnostics).</summary>
    public static class ShapeProbe
    {
        /// <summary>Fin threshold: both-sided average slope above 55 degrees.</summary>
        public static readonly double FinSlope = Math.Tan(55.0 * Math.PI / 180.0);
        /// <summary>Walls are at least this tall on both sides; smaller spines (outcrops, cliff-band ledges) are fine.</summary>
        public const double FinMinDrop = 20.0;
        /// <summary>No terracing this far above the local water table.</summary>
        public const double TerraceBandTop = 100.0;
        /// <summary>Half the length of the IsTerraced profile (m).</summary>
        public const double ProfileHalf = 32.0;

        static readonly double[] Spans = { 4, 6, 8, 11, 15, 20, 26, 34, 44 };

        public static double Degrees(double slope) => Math.Atan(slope) * 180.0 / Math.PI;

        public static double FinScore(TerrainGenerator g, double x, double z, out double span, out double dirDeg, out double drop) =>
            FinScore((px, pz) => g.HeightAt(px, pz), x, z, out span, out dirDeg, out drop);

        /// <summary>
        /// Steepest both-sided fall (drop / distance, the smaller of the two sides) over 8 directions
        /// and spans of 4..44 m, counting only falls of at least FinMinDrop on both sides.
        /// </summary>
        public static double FinScore(Func<double, double, double> height, double x, double z, out double span, out double dirDeg, out double drop)
        {
            double h0 = height(x, z);
            double best = 0.0;
            span = 0; dirDeg = 0; drop = 0;
            for (int d = 0; d < 8; d++)
            {
                double a = d * Math.PI / 8.0;
                double dx = Math.Cos(a), dz = Math.Sin(a);
                foreach (double s in Spans)
                {
                    double dropA = h0 - height(x + dx * s, z + dz * s);
                    double dropB = h0 - height(x - dx * s, z - dz * s);
                    double m = Math.Min(dropA, dropB);
                    if (m < FinMinDrop) continue;
                    double score = m / s;
                    if (score > best) { best = score; span = s; dirDeg = d * 22.5; drop = m; }
                }
            }
            return best;
        }

        /// <summary>Worst fin score over a grid of points (4 m) around a spot.</summary>
        public static double WorstFinNear(TerrainGenerator g, double cx, double cz, double radius, out double wx, out double wz)
        {
            double worst = 0;
            wx = cx; wz = cz;
            for (double dz = -radius; dz <= radius; dz += 4.0)
            for (double dx = -radius; dx <= radius; dx += 4.0)
            {
                double x = cx + dx, z = cz + dz;
                float h = g.HeightAt(x, z);
                // Only crest-like points: higher than all 4 neighbours at 4 m.
                if (h < g.HeightAt(x + 4, z) || h < g.HeightAt(x - 4, z) || h < g.HeightAt(x, z + 4) || h < g.HeightAt(x, z - 4))
                {
                    // also a crest across one axis only (a ridge line)
                    bool ridgeX = h >= g.HeightAt(x + 4, z) && h >= g.HeightAt(x - 4, z);
                    bool ridgeZ = h >= g.HeightAt(x, z + 4) && h >= g.HeightAt(x, z - 4);
                    if (!ridgeX && !ridgeZ) continue;
                }
                double s = FinScore(g, x, z, out _, out _, out _);
                if (s > worst) { worst = s; wx = x; wz = z; }
            }
            return worst;
        }

        /// <summary>
        /// Staircase test along the uphill direction: a 64 m profile at 1 m whose mean slope is at
        /// least 0.2 and which alternates at least twice between flat treads (slope under 0.35x the
        /// mean for 3+ m) and steep risers (over 1.8x the mean).
        /// </summary>
        public static bool IsTerraced(TerrainGenerator g, double x, double z, out int steps)
        {
            steps = 0;
            double gx = g.HeightAt(x + 4.0, z) - g.HeightAt(x - 4.0, z);
            double gz = g.HeightAt(x, z + 4.0) - g.HeightAt(x, z - 4.0);
            double len = Math.Sqrt(gx * gx + gz * gz);
            if (len < 1e-6) return false;
            double ux = gx / len, uz = gz / len;
            const int n = 64;
            var p = new double[n + 1];
            for (int i = 0; i <= n; i++) p[i] = g.HeightAt(x + ux * (i - n / 2), z + uz * (i - n / 2));
            double mean = (p[n] - p[0]) / n;
            if (mean < 0.25) return false;
            int flatRun = 0;
            bool sawTread = false;
            for (int i = 0; i < n; i++)
            {
                double s = p[i + 1] - p[i];
                if (s < 0.25 * mean) { flatRun++; if (flatRun >= 4) sawTread = true; continue; }
                flatRun = 0;
                if (sawTread && s > 2.5 * mean) { steps++; sawTread = false; }
            }
            return steps >= 2;
        }
    }
}
