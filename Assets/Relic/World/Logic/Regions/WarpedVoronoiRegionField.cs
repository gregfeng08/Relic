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
    /// 3. Every region within ~200 weighted meters of the nearest is blended with a smoothstep
    ///    falloff (the nearest weighs 1). Any number of regions can take part, so no weight has
    ///    to be faded early to free a slot: the blend is continuous and changes no faster than
    ///    the band allows, even at four-way junctions.
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
        public const double BorderBand = 250.0;
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
            double d0 = top.D0;

            // Blend EVERY region within one band of the nearest (usually 1-3, at a four-way
            // junction 4+). An earlier version kept 3 slots and faded each weight out before the
            // 4th-nearest region could take its slot; near four-way junctions that left only tiny
            // weights to normalize, which swung within meters and stood 50-100 m walls up
            // (RLC-069). Here the nearest always weighs 1, so the blend changes no faster than the
            // band allows. Blend in double: float expressions can round differently under Unity's
            // Mono than .NET.
            var blended = new RegionParams();
            RegionFamily family = RegionFamily.Meadow;
            double sum = 0.0;
            long fx = WorldMath.FloorToLong(qx / CellSize);
            long fz = WorldMath.FloorToLong(qz / CellSize);
            for (long cx = fx - SearchLow; cx <= fx + SearchHigh; cx++)
            {
                for (long cz = fz - SearchLow; cz <= fz + SearchHigh; cz++)
                {
                    Shape(cx, cz, out double px, out double pz, out double size);
                    double dx = qx - px, dz = qz - pz;
                    double w = BorderWeight(System.Math.Sqrt(dx * dx + dz * dz) / size, d0);
                    if (w <= 0.0) continue;
                    RegionParams p = ParamsOf(new RegionId(cx, cz));
                    if (cx == top.Id0.X && cz == top.Id0.Z) family = p.Family;
                    blended.AddWeighted(p, w);
                    sum += w;
                }
            }
            blended.Scale(1.0 / sum);

            // The three strongest regions for display (weights renormalized among them).
            double w0 = BorderWeight(top.D0, d0), w1 = BorderWeight(top.D1, d0), w2 = BorderWeight(top.D2, d0);
            double top3 = w0 + w1 + w2;
            blended.Family = family;
            return new RegionSample
            {
                Id0 = top.Id0, Id1 = top.Id1, Id2 = top.Id2,
                W0 = (float)(w0 / top3), W1 = (float)(w1 / top3), W2 = (float)(w2 / top3),
                Blended = blended,
                Family = family,
            };
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

        /// <summary>Unnormalized weight of a region at weighted distance d, given the nearest's d0: 1 for the nearest, 0 one band out.</summary>
        static double BorderWeight(double d, double d0) => WorldMath.Smooth01(1.0 - (d - d0) / BorderBand);

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
