using System;
using System.Collections.Generic;
using NUnit.Framework;
using Relic.Core.Random;
using Relic.World.Chunks;
using Relic.World.Regions;
using Relic.World.Terrain;

namespace Relic.World.Tests
{
    public class TerrainTests
    {
        /// <summary>
        /// FNV-style hash of the exact float bits of N sample heights. If this changes, the world
        /// changed: bump TerrainGenerator.Version and update the expected value on purpose.
        /// The same constant must hold under .NET (dotnet test) and Unity's Mono (EditMode),
        /// which checks cross-runtime determinism too.
        /// </summary>
        static ulong HeightHash(TerrainGenerator g, int n)
        {
            var rng = new Pcg32(2026);
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < n; i++)
            {
                double x = (rng.NextDouble() * 2.0 - 1.0) * 30000.0;
                double z = (rng.NextDouble() * 2.0 - 1.0) * 30000.0;
                float v = g.HeightAt(x, z);
                h ^= (ulong)BitConverter.ToInt32(BitConverter.GetBytes(v), 0) & 0xFFFFFFFFUL;
                h *= 1099511628211UL;
            }
            return h;
        }

        public const ulong GoldenHeightHash = 0x8568628B3ABA87ADUL;

        [Test]
        public void SameSeedGivesSameWorld()
        {
            ulong a = HeightHash(new TerrainGenerator(1337), 4000);
            ulong b = HeightHash(new TerrainGenerator(1337), 4000);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, HeightHash(new TerrainGenerator(1338), 4000));
        }

        [Test]
        public void HeightHashMatchesGolden()
        {
            ulong h = HeightHash(new TerrainGenerator(1337), 4000);
            Assert.AreEqual(GoldenHeightHash, h, "golden height hash changed: 0x" + h.ToString("X16") + "UL (TerrainGenerator.Version " + TerrainGenerator.Version + ")");
        }

        [Test]
        public void HeightsAreFiniteAndInRangeIncludingFarAway()
        {
            var g = new TerrainGenerator(9);
            var coords = new List<double> { 0, 1, -1, 1e3, -1e3, 1e5, -1e5, 1e6, -1e6, 4.2e6, -7.7e6 };
            foreach (double x in coords)
            foreach (double z in coords)
            {
                TerrainSample s = g.Sample(x, z);
                Assert.IsTrue(WorldMath.IsFinite(s.Height), "NaN height at " + x + "," + z);
                Assert.IsTrue(WorldMath.IsFinite(s.WaterLevel));
                Assert.That(s.Height, Is.InRange(-150f, 900f), "height out of range at " + x + "," + z);
                Assert.That(s.Ground.R, Is.InRange(0f, 1f));
                Assert.IsTrue(Enum.IsDefined(typeof(Biome), s.Biome));
            }
        }

        [Test]
        public void HeightIsContinuous()
        {
            // Walk lines at 0.1 m steps through many region borders. Any big step is zoomed into
            // (100 sub-steps): a steep cliff shrinks with the step size, a discontinuity would not.
            var g = new TerrainGenerator(4711);
            const double step = 0.1;
            double worst = 0;
            for (int line = 0; line < 3; line++)
            {
                double ang = 0.21 + line * 1.1;
                double dx = Math.Cos(ang), dz = Math.Sin(ang);
                float prev = g.HeightAt(0, 0);
                for (int i = 1; i < 60000; i++)
                {
                    double t = i * step * 1.0;
                    float h = g.HeightAt(dx * t, dz * t);
                    double d = Math.Abs(h - prev);
                    if (d > 0.3) worst = Math.Max(worst, RefinedStep(g, dx, dz, t - step, step));
                    prev = h;
                }
            }
            Assert.That(worst, Is.LessThan(0.08), "largest refined step (0.001 m): " + worst);
        }

        static double RefinedStep(TerrainGenerator g, double dx, double dz, double t0, double length)
        {
            double worst = 0;
            float prev = g.HeightAt(dx * t0, dz * t0);
            for (int k = 1; k <= 100; k++)
            {
                double t = t0 + length * k / 100.0;
                float h = g.HeightAt(dx * t, dz * t);
                worst = Math.Max(worst, Math.Abs(h - prev));
                prev = h;
            }
            return worst;
        }

        [Test]
        public void ChunkEdgesMatchExactly()
        {
            var g = new TerrainGenerator(55);
            for (int lod = 0; lod <= ChunkMath.MaxLod; lod++)
            {
                int n = ChunkMath.VerticesPerSide(lod);
                for (long cx = -3; cx <= 3; cx++)
                {
                    // Last column of chunk cx and first column of chunk cx+1 are the same world points.
                    for (int j = 0; j < n; j++)
                    {
                        double xa = ChunkMath.VertexWorld(cx, n - 1, lod);
                        double xb = ChunkMath.VertexWorld(cx + 1, 0, lod);
                        double z = ChunkMath.VertexWorld(2, j, lod);
                        Assert.AreEqual(xa, xb);
                        Assert.AreEqual(g.HeightAt(xa, z), g.HeightAt(xb, z));
                    }
                }
            }
        }

        [Test]
        public void SpawnIsDryLandAndWaterExistsSomewhere()
        {
            for (ulong seed = 0; seed < 6; seed++)
            {
                var g = new TerrainGenerator(seed);
                g.FindSpawn(out double sx, out double sz);
                g.HeightAndWater(sx, sz, out float h, out float w);
                Assert.That(h, Is.GreaterThan(w + 1f), "seed " + seed);
                Assert.That(Math.Sqrt(sx * sx + sz * sz), Is.LessThan(300.0), "spawn near origin, seed " + seed);
            }

            // Over a 16 km square, some (but not most) of the land is under water.
            var gen = new TerrainGenerator(1337);
            int wet = 0, n = 0;
            for (int i = 0; i < 80; i++)
            for (int j = 0; j < 80; j++)
            {
                gen.HeightAndWater(-8000 + i * 200.0, -8000 + j * 200.0, out float h, out float w);
                if (h < w) wet++;
                n++;
            }
            double frac = wet / (double)n;
            Assert.That(frac, Is.InRange(0.02, 0.5), "water fraction " + frac);
        }

        [Test]
        public void SeveralBiomesAndFamiliesNearSpawn()
        {
            var g = new TerrainGenerator(1337);
            var biomes = new HashSet<Biome>();
            var families = new HashSet<RegionFamily>();
            for (int i = -20; i <= 20; i++)
            for (int j = -20; j <= 20; j++)
            {
                double x = i * 100.0, z = j * 100.0;
                if (x * x + z * z > 2000.0 * 2000.0) continue;
                TerrainSample s = g.Sample(x, z);
                biomes.Add(s.Biome);
                families.Add(s.Family);
            }
            Assert.That(families.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(biomes.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void BiomeTableCoversAllBiomes()
        {
            var seen = new HashSet<Biome>();
            for (double t = -1.5; t <= 1.5; t += 0.05)
            for (double m = -1.5; m <= 1.5; m += 0.05)
            for (double a = -3; a <= 20; a += 1.0)
                seen.Add(BiomeTable.Classify(t, m, a));
            Assert.AreEqual(BiomeTable.Count, seen.Count);
            foreach (Biome b in Enum.GetValues(typeof(Biome)))
            {
                Rgb c = BiomeTable.GroundColor(b);
                Assert.That(c.R, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SwappableRegionFieldIsUsed()
        {
            var flat = new FlatField();
            var g = new TerrainGenerator(3, flat);
            Assert.AreSame(flat, g.Regions);
            Assert.AreEqual(RegionFamily.Badlands, g.Sample(100, 100).Family);
        }

        sealed class FlatField : IRegionField
        {
            public ulong Seed => 3;

            public RegionSample Sample(double x, double z) =>
                new RegionSample { Id0 = new RegionId(0, 0), W0 = 1f, Family = RegionFamily.Badlands, Blended = ParamsOf(default) };

            public RegionParams ParamsOf(RegionId id)
            {
                var p = RegionCatalog.Base(RegionFamily.Badlands);
                p.Family = RegionFamily.Badlands;
                return p;
            }

            public void CenterOf(RegionId id, out double x, out double z) { x = 0; z = 0; }
        }
    }

    public class ChunkTests
    {
        [Test]
        public void ChunkIndexFloorsNegatives()
        {
            Assert.AreEqual(0, ChunkMath.ChunkIndex(0.0));
            Assert.AreEqual(0, ChunkMath.ChunkIndex(63.99));
            Assert.AreEqual(1, ChunkMath.ChunkIndex(64.0));
            Assert.AreEqual(-1, ChunkMath.ChunkIndex(-0.01));
            Assert.AreEqual(-2, ChunkMath.ChunkIndex(-64.01));
            Assert.AreEqual(15625, ChunkMath.ChunkIndex(1e6));
        }

        [Test]
        public void LodResolutions()
        {
            Assert.AreEqual(65, ChunkMath.VerticesPerSide(0));
            Assert.AreEqual(33, ChunkMath.VerticesPerSide(1));
            Assert.AreEqual(17, ChunkMath.VerticesPerSide(2));
            Assert.AreEqual(9, ChunkMath.VerticesPerSide(3));
            Assert.AreEqual(8, ChunkMath.StepMeters(3));
            Assert.That(ChunkMath.SkirtDepth(3), Is.GreaterThan(ChunkMath.SkirtDepth(0)));
        }

        [Test]
        public void LodPlanIsSortedAndLodGrowsWithDistance()
        {
            var s = LodSettings.Default;
            ChunkOffset[] plan = LodPlan.Build(s);
            Assert.AreEqual(0, plan[0].Dx);
            Assert.AreEqual(0, plan[0].Dz);
            Assert.AreEqual(0, plan[0].Lod);
            for (int i = 1; i < plan.Length; i++)
            {
                Assert.That(plan[i].Distance, Is.GreaterThanOrEqualTo(plan[i - 1].Distance));
                Assert.That(plan[i].Lod, Is.GreaterThanOrEqualTo(plan[i - 1].Lod));
                Assert.That(plan[i].Distance, Is.LessThanOrEqualTo(s.ViewDistance));
            }
            Assert.AreEqual(3, plan[plan.Length - 1].Lod);
            // About pi * (1536/64)^2 chunks.
            Assert.That(plan.Length, Is.InRange(1700, 2000));
        }

        [Test]
        public void DistanceToChunk()
        {
            var c = new ChunkCoord(1, 0); // [64,128) x [0,64)
            Assert.AreEqual(0.0, ChunkMath.DistanceSqToChunk(c, 70, 10));
            Assert.AreEqual(4.0 * 4.0, ChunkMath.DistanceSqToChunk(c, 60, 10));
            Assert.AreEqual(3.0 * 3.0 + 4.0 * 4.0, ChunkMath.DistanceSqToChunk(c, 131, -4));
        }

        [Test]
        public void FloatingOriginRecentersPastThresholdOnChunkGrid()
        {
            Assert.IsFalse(FloatingOriginMath.ShouldRecenter(3999, 0));
            Assert.IsTrue(FloatingOriginMath.ShouldRecenter(3000, 3000));
            FloatingOriginMath.NewOrigin(123456.7, -98765.4, out double ox, out double oz);
            Assert.AreEqual(0.0, ox % ChunkMath.ChunkSize);
            Assert.AreEqual(0.0, oz % ChunkMath.ChunkSize);
            Assert.That(Math.Abs(123456.7 - ox), Is.LessThan(ChunkMath.ChunkSize));
            Assert.That(Math.Abs(-98765.4 - oz), Is.LessThan(ChunkMath.ChunkSize));
        }
    }
}
