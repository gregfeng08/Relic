using System;
using NUnit.Framework;
using Relic.World.Regions;
using Relic.World.Terrain;

namespace Relic.World.Tests
{
    /// <summary>Snow caps: an altitude snow line shifted by climate, reachable by tall peaks in every region.</summary>
    public class SnowTests
    {
        [Test]
        public void SnowLineIsLowInTheColdAndHighInTheHeat()
        {
            double frost = TerrainGenerator.SnowLineFor(RegionCatalog.Base(RegionFamily.Frost).TemperatureBias, RegionCatalog.Base(RegionFamily.Frost).HeightScale);
            double meadow = TerrainGenerator.SnowLineFor(RegionCatalog.Base(RegionFamily.Forest).TemperatureBias, 400.0);
            double badlands = TerrainGenerator.SnowLineFor(RegionCatalog.Base(RegionFamily.Badlands).TemperatureBias, 400.0);
            Assert.Less(frost, meadow);
            Assert.Less(meadow, badlands);
        }

        [Test]
        public void SnowLineIsAlwaysReachableByTheTallestPeaks()
        {
            // Tallest mountains reach roughly 0.7 x HeightScale above an upland base of ~40 m; the line must sit below that.
            foreach (RegionFamily f in Enum.GetValues(typeof(RegionFamily)))
            {
                RegionParams p = RegionCatalog.Base(f);
                if (p.HeightScale < 100.0) continue; // gentle families have no high peaks
                for (double t = -1.0; t <= 1.5; t += 0.25)
                    Assert.Less(TerrainGenerator.SnowLineFor(t, p.HeightScale), 0.7 * p.HeightScale + 40.0, f + " at temperature " + t);
            }
        }

        [Test]
        public void TallestPeaksOutsideFrostAreSnowyAndLowlandsAreNot()
        {
            var g = new TerrainGenerator(1337);
            double bestH = double.MinValue, bx = 0, bz = 0;
            for (double z = -5000; z <= 5000; z += 40)
            for (double x = -5000; x <= 5000; x += 40)
            {
                RegionSample r = g.Regions.Sample(x, z);
                if (r.Family == RegionFamily.Frost || r.W0 < 0.7f) continue;
                float h = g.HeightAt(x, z);
                if (h > bestH) { bestH = h; bx = x; bz = z; }
            }
            TerrainSample peak = g.Sample(bx, bz);
            Assert.Greater(peak.Snow, 0.7f, "tallest non-frost peak (" + bestH.ToString("0") + " m, " + peak.Family + ") should be snowy");
            Assert.AreEqual(Biome.Snow, peak.Biome);

            g.FindSpawn(out double sx, out double sz);
            Assert.Less(g.Sample(sx, sz).Snow, 0.05f, "spawn lowland has no snow");
        }

        [Test, Explicit("diagnostic: per-family peak heights vs snow line")]
        public void DiagnosePeaks()
        {
            var g = new TerrainGenerator(1337);
            var tops = new System.Collections.Generic.Dictionary<RegionFamily, System.Collections.Generic.List<float>>();
            for (double z = -8000; z <= 8000; z += 48)
            for (double x = -8000; x <= 8000; x += 48)
            {
                RegionSample r = g.Regions.Sample(x, z);
                if (r.W0 < 0.7f) continue;
                if (!tops.TryGetValue(r.Family, out var l)) tops[r.Family] = l = new System.Collections.Generic.List<float>();
                l.Add(g.HeightAt(x, z));
            }
            foreach (var kv in tops)
            {
                kv.Value.Sort();
                int n = kv.Value.Count;
                TestContext.WriteLine(kv.Key + ": n " + n + " p50 " + kv.Value[n / 2].ToString("0") + " p90 " + kv.Value[n * 9 / 10].ToString("0") + " p99 " + kv.Value[n * 99 / 100].ToString("0") + " max " + kv.Value[n - 1].ToString("0") +
                                      " line " + TerrainGenerator.SnowLineFor(RegionCatalog.Base(kv.Key).TemperatureBias, RegionCatalog.Base(kv.Key).HeightScale).ToString("0"));
            }
        }

        [Test]
        public void SnowBandIsSoftNotAContourLine()
        {
            // Walking up through the snow line, cover rises over tens of meters of altitude.
            var g = new TerrainGenerator(1337);
            int partial = 0, total = 0;
            for (double z = -3000; z <= 3000; z += 25)
            for (double x = -3000; x <= 3000; x += 25)
            {
                TerrainSample s = g.Sample(x, z);
                if (Math.Abs(s.Height - s.SnowLine) > 20f) continue;
                total++;
                if (s.Snow > 0.04f && s.Snow < 0.72f) partial++;
            }
            if (total > 20) Assert.Greater(partial, total / 3, "most points near the snow line are partly snowy");
        }
    }
}
