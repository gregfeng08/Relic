using Relic.Core.Random;
using Relic.Core.Stats;
using Relic.World.Noise;

namespace Relic.World.Regions
{
    /// <summary>
    /// Regions as a domain-warped, size-weighted Voronoi diagram over a jittered 1024 m grid.
    ///
    /// 1. The sample point is first offset by low-frequency noise (2 octaves, ~2200 m wavelength,
    ///    ~300 m amplitude), so borders wander like coastlines instead of running straight.
    /// 2. Each cell's center is jittered by up to ±40% of a cell, and each center has a seeded
    ///    size s; distance is measured as d / s (multiplicatively weighted Voronoi), so some
    ///    regions are larger than others and borders curve.
    /// 3. The nearest three regions (by weighted distance) are blended with a smoothstep over a
    ///    ~200 m border band. A second factor fades each weight out before the 4th-nearest region
    ///    can take its slot, which keeps the blend continuous everywhere (not just at the
    ///    two-region borders).
    ///
    /// Deterministic, infinite, thread-safe and allocation-free in Sample.
    /// </summary>
    public sealed class WarpedVoronoiRegionField : IRegionField
    {
        public const double CellSize = 1024.0;
        /// <summary>Jitter as a fraction of the cell (centers move up to ±Jitter/2 cells).</summary>
        public const double Jitter = 0.8;
        public const double MinSize = 0.82;
        public const double MaxSize = 1.22;
        /// <summary>Width of the blend band across a border, in (weighted) meters.</summary>
        public const double BorderBand = 200.0;
        public const double WarpWavelength = 2200.0;
        public const double WarpAmplitude = 300.0;
        /// <summary>
        /// Candidate cells searched per axis around the sample's cell (from -SearchLow to +SearchHigh).
        /// With Jitter 0.8 and size ratio 1.22/0.82 the 4th-nearest weighted center is always inside
        /// this 6x6 block (verified by a brute-force test).
        /// </summary>
        public const int SearchLow = 2, SearchHigh = 3;

        /// <summary>Cells within this Chebyshev radius of the origin get a guaranteed mix of families.</summary>
        public const int SpawnBlockRadius = 2;

        readonly ulong _seed;
        readonly ulong _saltRegion;
        readonly ulong _saltShape;
        readonly SimplexNoise2 _warpX;
        readonly SimplexNoise2 _warpZ;
        readonly RegionId _spawnRegion;
        readonly RegionFamily[] _spawnBlock;

        public WarpedVoronoiRegionField(ulong seed)
        {
            _seed = seed;
            _saltRegion = Hash.Of("region");
            _saltShape = Hash.Of("region.shape");
            _warpX = SimplexNoise2.ForPurpose(seed, "region.warp.x");
            _warpZ = SimplexNoise2.ForPurpose(seed, "region.warp.z");

            // Geometry does not depend on families, so the spawn region can be found first.
            Top4 top = FindNearest(0.0, 0.0);
            _spawnRegion = top.Id0;

            // Deal the six non-meadow families around spawn from reshuffled decks so every family
            // appears several times within ~2 km. The spawn region itself is always a meadow.
            int side = SpawnBlockRadius * 2 + 1;
            _spawnBlock = new RegionFamily[side * side];
            var rng = Pcg32.ForPurpose(seed, "region.spawnblock");
            var deck = new RegionFamily[RegionCatalog.FamilyCount - 1];
            int dealt = deck.Length;
            for (int i = 0; i < _spawnBlock.Length; i++)
            {
                long cx = i / side - SpawnBlockRadius, cz = i % side - SpawnBlockRadius;
                if (cx == _spawnRegion.X && cz == _spawnRegion.Z) { _spawnBlock[i] = RegionFamily.Meadow; continue; }
                if (dealt == deck.Length)
                {
                    for (int k = 0; k < deck.Length; k++) deck[k] = (RegionFamily)(k + 1);
                    for (int k = deck.Length - 1; k > 0; k--)
                    {
                        int j = rng.Range(0, k + 1);
                        var t = deck[k]; deck[k] = deck[j]; deck[j] = t;
                    }
                    dealt = 0;
                }
                _spawnBlock[i] = deck[dealt++];
            }
        }

        public ulong Seed => _seed;

        public RegionId SpawnRegion => _spawnRegion;

        public RegionSample Sample(double x, double z)
        {
            Warp(x, z, out double qx, out double qz);
            Top4 top = FindNearest(qx, qz);

            double e1 = top.D0;
            double e4 = top.D3;
            double w0 = Weight(top.D0, e1, e4);
            double w1 = Weight(top.D1, e1, e4);
            double w2 = Weight(top.D2, e1, e4);
            double sum = w0 + w1 + w2;
            if (sum < 1e-9) { w0 = 1.0; w1 = 0.0; w2 = 0.0; sum = 1.0; }

            var s = new RegionSample
            {
                Id0 = top.Id0, Id1 = top.Id1, Id2 = top.Id2,
                W0 = (float)(w0 / sum), W1 = (float)(w1 / sum), W2 = (float)(w2 / sum),
            };
            // Re-normalize in float so the three weights sum to 1 as closely as float allows.
            s.W2 = 1f - s.W0 - s.W1;
            if (s.W2 < 0f) { s.W2 = 0f; }

            RegionParams p0 = ParamsOf(top.Id0);
            var blended = new RegionParams();
            blended.AddWeighted(p0, s.W0);
            if (s.W1 > 0f) { RegionParams p1 = ParamsOf(top.Id1); blended.AddWeighted(p1, s.W1); }
            if (s.W2 > 0f) { RegionParams p2 = ParamsOf(top.Id2); blended.AddWeighted(p2, s.W2); }
            blended.Family = p0.Family;
            s.Blended = blended;
            s.Family = p0.Family;
            return s;
        }

        public RegionParams ParamsOf(RegionId id)
        {
            var rng = new Pcg32(Hash.Combine(_seed, Hash.Coord(id.X, id.Z), _saltRegion));
            RegionFamily family = RegionCatalog.PickFamily(ref rng);
            if (InSpawnBlock(id)) family = _spawnBlock[SpawnIndex(id)];
            CenterOf(id, out double cx, out double cz);
            int tier = Difficulty.TierAt(cx, cz);
            return RegionCatalog.Build(family, ref rng, tier);
        }

        public void CenterOf(RegionId id, out double x, out double z)
        {
            Shape(id.X, id.Z, out x, out z, out _);
        }

        /// <summary>The seeded size weight of a region (MinSize..MaxSize).</summary>
        public double SizeOf(RegionId id)
        {
            Shape(id.X, id.Z, out _, out _, out double s);
            return s;
        }

        /// <summary>The domain-warped lookup position for a world point.</summary>
        public void Warp(double x, double z, out double qx, out double qz)
        {
            double f = 1.0 / WarpWavelength;
            qx = x + WarpAmplitude * _warpX.Fbm(x * f, z * f, 2);
            qz = z + WarpAmplitude * _warpZ.Fbm(x * f, z * f, 2);
        }

        /// <summary>Unnormalized weight of a region at weighted distance d, given the nearest (d1) and 4th-nearest (d4).</summary>
        static double Weight(double d, double d1, double d4)
        {
            double border = WorldMath.Smooth01(1.0 - (d - d1) / BorderBand);
            double fourth = WorldMath.Smooth01((d4 - d) / BorderBand);
            return border * fourth;
        }

        void Shape(long cx, long cz, out double centerX, out double centerZ, out double size)
        {
            ulong h = Hash.Combine(_seed, Hash.Coord(cx, cz), _saltShape);
            double jx = WorldMath.UnitFromBits(h, 21) - 0.5;
            double jz = WorldMath.UnitFromBits(h >> 21, 21) - 0.5;
            double u = WorldMath.UnitFromBits(h >> 42, 22);
            centerX = (cx + Jitter * jx) * CellSize;
            centerZ = (cz + Jitter * jz) * CellSize;
            size = MinSize + (MaxSize - MinSize) * u;
        }

        internal Top4 FindNearest(double qx, double qz)
        {
            long fx = WorldMath.FloorToLong(qx / CellSize);
            long fz = WorldMath.FloorToLong(qz / CellSize);
            var top = Top4.Empty;
            for (long cx = fx - SearchLow; cx <= fx + SearchHigh; cx++)
            {
                for (long cz = fz - SearchLow; cz <= fz + SearchHigh; cz++)
                {
                    Shape(cx, cz, out double px, out double pz, out double size);
                    double dx = qx - px, dz = qz - pz;
                    double d = System.Math.Sqrt(dx * dx + dz * dz) / size;
                    top.Insert(d, new RegionId(cx, cz));
                }
            }
            return top;
        }

        /// <summary>Brute-force nearest four over a wide block. For tests only.</summary>
        internal Top4 FindNearestBruteForce(double qx, double qz, int radius)
        {
            long fx = WorldMath.FloorToLong(qx / CellSize);
            long fz = WorldMath.FloorToLong(qz / CellSize);
            var top = Top4.Empty;
            for (long cx = fx - radius; cx <= fx + radius; cx++)
            for (long cz = fz - radius; cz <= fz + radius; cz++)
            {
                Shape(cx, cz, out double px, out double pz, out double size);
                double dx = qx - px, dz = qz - pz;
                top.Insert(System.Math.Sqrt(dx * dx + dz * dz) / size, new RegionId(cx, cz));
            }
            return top;
        }

        static bool InSpawnBlock(RegionId id) =>
            id.X >= -SpawnBlockRadius && id.X <= SpawnBlockRadius && id.Z >= -SpawnBlockRadius && id.Z <= SpawnBlockRadius;

        static int SpawnIndex(RegionId id) =>
            (int)(id.X + SpawnBlockRadius) * (SpawnBlockRadius * 2 + 1) + (int)(id.Z + SpawnBlockRadius);

        /// <summary>The four nearest candidates, sorted by weighted distance (allocation-free).</summary>
        internal struct Top4
        {
            public double D0, D1, D2, D3;
            public RegionId Id0, Id1, Id2, Id3;

            public static Top4 Empty => new Top4
            {
                D0 = double.MaxValue, D1 = double.MaxValue, D2 = double.MaxValue, D3 = double.MaxValue,
            };

            public void Insert(double d, RegionId id)
            {
                if (d >= D3) return;
                if (d < D2)
                {
                    D3 = D2; Id3 = Id2;
                    if (d < D1)
                    {
                        D2 = D1; Id2 = Id1;
                        if (d < D0)
                        {
                            D1 = D0; Id1 = Id0;
                            D0 = d; Id0 = id;
                        }
                        else { D1 = d; Id1 = id; }
                    }
                    else { D2 = d; Id2 = id; }
                }
                else { D3 = d; Id3 = id; }
            }
        }
    }
}
