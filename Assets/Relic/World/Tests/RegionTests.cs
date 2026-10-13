using System;
using System.Collections.Generic;
using NUnit.Framework;
using Relic.Core.Random;
using Relic.World.Regions;

namespace Relic.World.Tests
{
    public class RegionTests
    {
        static IEnumerable<(double x, double z)> Points(ulong seed, int count, double range)
        {
            var rng = new Pcg32(seed);
            for (int i = 0; i < count; i++)
                yield return ((rng.NextDouble() * 2.0 - 1.0) * range, (rng.NextDouble() * 2.0 - 1.0) * range);
        }

        [Test]
        public void BlendWeightsAreNonNegativeAndSumToOne()
        {
            var f = new WarpedVoronoiRegionField(12345);
            foreach (var (x, z) in Points(1, 20000, 20000))
            {
                RegionSample s = f.Sample(x, z);
                Assert.That(s.W0, Is.GreaterThanOrEqualTo(0f));
                Assert.That(s.W1, Is.GreaterThanOrEqualTo(0f));
                Assert.That(s.W2, Is.GreaterThanOrEqualTo(0f));
                Assert.That(s.W0 + s.W1 + s.W2, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(s.W0, Is.GreaterThanOrEqualTo(s.W1 - 1e-6f), "Id0 must be the strongest region");
            }
        }

        [Test]
        public void MostPointsAreDeepInsideOneRegion()
        {
            var f = new WarpedVoronoiRegionField(777);
            int pure = 0, n = 0;
            foreach (var (x, z) in Points(2, 5000, 30000))
            {
                if (f.Sample(x, z).W0 > 0.999f) pure++;
                n++;
            }
            double frac = pure / (double)n;
            Assert.That(frac, Is.InRange(0.45, 0.95), "border bands should be a minority but present: " + frac);
        }

        [Test]
        public void RegionSampleIsDeterministic()
        {
            var a = new WarpedVoronoiRegionField(99);
            var b = new WarpedVoronoiRegionField(99);
            foreach (var (x, z) in Points(3, 2000, 50000))
            {
                RegionSample sa = a.Sample(x, z), sb = b.Sample(x, z);
                Assert.AreEqual(sa.Id0, sb.Id0);
                Assert.AreEqual(sa.W0, sb.W0);
                Assert.AreEqual(sa.Blended.HeightScale, sb.Blended.HeightScale);
                Assert.AreEqual(sa.Family, sb.Family);
            }
        }

        [Test]
        public void SearchBlockFindsTheTrueNearestFour()
        {
            var f = new WarpedVoronoiRegionField(4242);
            foreach (var (x, z) in Points(4, 4000, 40000))
            {
                var fast = f.FindNearest(x, z);
                var slow = f.FindNearestBruteForce(x, z, 6);
                Assert.AreEqual(slow.Id0, fast.Id0);
                Assert.AreEqual(slow.Id1, fast.Id1);
                Assert.AreEqual(slow.Id2, fast.Id2);
                Assert.AreEqual(slow.Id3, fast.Id3);
            }
        }

        [Test]
        public void BlendedParametersAreContinuousAcrossBorders()
        {
            // Walk long lines through many regions in small steps; blended parameters must never jump.
            var f = new WarpedVoronoiRegionField(2024);
            const double step = 0.1;
            for (int line = 0; line < 4; line++)
            {
                double ang = 0.37 + line * 0.83;
                double dx = Math.Cos(ang), dz = Math.Sin(ang);
                RegionParams prev = f.Sample(0, 0).Blended;
                int borders = 0;
                RegionId prevId = f.Sample(0, 0).Id0;
                for (int i = 1; i < 200000; i++)
                {
                    double t = i * step;
                    RegionSample s = f.Sample(dx * t, dz * t);
                    RegionParams p = s.Blended;
                    Assert.That(Math.Abs(p.HeightScale - prev.HeightScale), Is.LessThan(3.0), "height scale jumped at t=" + t);
                    Assert.That(Math.Abs(p.ContinentBias - prev.ContinentBias), Is.LessThan(0.01), "continent bias jumped at t=" + t);
                    Assert.That(Math.Abs(p.WaterLevelBias - prev.WaterLevelBias), Is.LessThan(0.15), "water bias jumped at t=" + t);
                    Assert.That(Math.Abs(p.RockTint.R - prev.RockTint.R), Is.LessThan(0.01), "palette jumped at t=" + t);
                    if (s.Id0 != prevId) borders++;
                    prevId = s.Id0;
                    prev = p;
                }
                Assert.Greater(borders, 5, "a 20 km line should cross several regions");
            }
        }

        [Test]
        public void BordersAreNotStraight()
        {
            // With domain warp, the dominant region along a straight line through a cell should not
            // be the plain (unwarped) Voronoi answer everywhere.
            var f = new WarpedVoronoiRegionField(5);
            int disagree = 0, n = 0;
            foreach (var (x, z) in Points(5, 3000, 20000))
            {
                RegionId warped = f.Sample(x, z).Id0;
                RegionId plain = f.FindNearest(x, z).Id0;
                if (warped != plain) disagree++;
                n++;
            }
            Assert.That(disagree / (double)n, Is.GreaterThan(0.05));
        }

        [Test]
        public void RegionSizesVary()
        {
            var f = new WarpedVoronoiRegionField(8);
            double min = double.MaxValue, max = 0;
            for (int x = -10; x <= 10; x++)
            for (int z = -10; z <= 10; z++)
            {
                double s = f.SizeOf(new RegionId(x, z));
                min = Math.Min(min, s);
                max = Math.Max(max, s);
            }
            Assert.That(min, Is.LessThan(0.9));
            Assert.That(max, Is.GreaterThan(1.15));
        }

        [Test]
        public void SpawnIsMeadowAndAtLeastThreeFamiliesWithinTwoKilometers()
        {
            for (ulong seed = 0; seed < 40; seed++)
            {
                var f = new WarpedVoronoiRegionField(seed);
                Assert.AreEqual(RegionFamily.Meadow, f.Sample(0, 0).Family, "seed " + seed);
                var seen = new HashSet<RegionFamily>();
                for (int ring = 1; ring <= 8; ring++)
                {
                    double r = ring * 250.0;
                    for (int k = 0; k < 32; k++)
                    {
                        double a = k * Math.PI / 16.0;
                        seen.Add(f.Sample(r * Math.Cos(a), r * Math.Sin(a)).Family);
                    }
                }
                Assert.That(seen.Count, Is.GreaterThanOrEqualTo(3), "seed " + seed);
            }
        }

        [Test]
        public void TierComesFromCoreDifficulty()
        {
            // Regions far out are taller and rougher on average (mild tier scaling).
            var f = new WarpedVoronoiRegionField(31);
            var p = f.ParamsOf(new RegionId(50, 0));
            var basis = RegionCatalog.Base(p.Family);
            Assert.That(p.HeightScale, Is.GreaterThan(basis.HeightScale * 0.8f * 1.2f), "tier 10 should scale heights up");
        }
    }
}
