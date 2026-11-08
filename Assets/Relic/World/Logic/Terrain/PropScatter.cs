using Relic.Core.Random;
using Relic.World.Chunks;
using Relic.World.Regions;

namespace Relic.World.Terrain
{
    /// <summary>
    /// Deterministic per-chunk prop placement, a pure function of (seed, chunk); thread-safe.
    /// Two passes with their own random streams:
    /// - <see cref="Place"/>: a jittered 8 m grid of trees, bushes and boulders, by biome density
    ///   times a region-family factor (dense forests, lone meadow trees, sparse highlands, dead
    ///   badlands), never under water, on cliffs or above the snow line.
    /// - <see cref="PlaceSmall"/>: a jittered 4 m grid of grass, flowers, rocks, dead shrubs and
    ///   succulents, only needed near the player.
    /// </summary>
    public static partial class PropScatter
    {
        public const int CellSize = 8;
        public const int CellsPerSide = ChunkMath.ChunkSize / CellSize; // 8
        public const int MaxPerChunk = CellsPerSide * CellsPerSide + 16;

        public const int SmallCellSize = 4;
        public const int SmallCellsPerSide = ChunkMath.ChunkSize / SmallCellSize; // 16
        public const int MaxSmallPerChunk = SmallCellsPerSide * SmallCellsPerSide;

        static readonly ulong Salt = Hash.Of("props");
        static readonly ulong SmallSalt = Hash.Of("props.small");

        /// <summary>Tree density (probability per 8 m cell) by biome.</summary>
        public static float TreeDensity(Biome b)
        {
            switch (b)
            {
                case Biome.Forest: return 0.42f;
                case Biome.Jungle: return 0.5f;
                case Biome.Swamp: return 0.16f;
                case Biome.Grassland: return 0.035f;
                case Biome.Savanna: return 0.03f;
                case Biome.Steppe: return 0.012f;
                case Biome.Tundra: return 0.07f;
                case Biome.Snow: return 0.025f;
                case Biome.Beach: return 0.02f; // palms on warm coasts
                default: return 0f;
            }
        }

        /// <summary>Region character on top of the biome: forests are dense, highlands sparse.</summary>
        public static float FamilyTreeFactor(RegionFamily f)
        {
            switch (f)
            {
                case RegionFamily.Forest: return 1.6f;
                case RegionFamily.Meadow: return 0.7f;
                case RegionFamily.Highlands: return 0.4f;
                case RegionFamily.Badlands: return 0.25f;
                case RegionFamily.Marsh: return 0.9f;
                default: return 1f;
            }
        }

        static float BushDensity(Biome b, RegionFamily f)
        {
            float d;
            switch (b)
            {
                case Biome.Forest: d = 0.12f; break;
                case Biome.Jungle: d = 0.2f; break;
                case Biome.Swamp: d = 0.12f; break;
                case Biome.Grassland: d = 0.04f; break;
                case Biome.Savanna: d = 0.03f; break;
                case Biome.Tundra: d = 0.03f; break;
                default: d = 0f; break;
            }
            if (f == RegionFamily.Badlands || f == RegionFamily.Highlands) d *= 0.3f;
            return d;
        }

        public static float BoulderDensity(Biome b, RegionFamilyHint family)
        {
            float d;
            switch (b)
            {
                case Biome.Desert: d = 0.03f; break;
                case Biome.Steppe: d = 0.025f; break;
                case Biome.Tundra: d = 0.03f; break;
                case Biome.Beach: d = 0.01f; break;
                case Biome.Seabed: d = 0f; break;
                default: d = 0.012f; break;
            }
            return family == RegionFamilyHint.Rocky ? d * 2.5f : d;
        }

        /// <summary>Coarse hint so rocky regions get more boulders.</summary>
        public enum RegionFamilyHint : byte { Normal, Rocky }

        static bool Rocky(RegionFamily f) => f == RegionFamily.Highlands || f == RegionFamily.Badlands || f == RegionFamily.Frost;

        /// <summary>Fills output with the chunk's trees, bushes and boulders and returns how many were placed.</summary>
        public static int Place(TerrainGenerator gen, ChunkCoord chunk, PropInstance[] output)
        {
            var rng = new Pcg32(Hash.Combine(gen.Seed, Hash.Coord(chunk.X, chunk.Z), Salt));
            double ox = ChunkMath.OriginX(chunk), oz = ChunkMath.OriginZ(chunk);
            int count = 0;
            for (int cz = 0; cz < CellsPerSide; cz++)
            {
                for (int cx = 0; cx < CellsPerSide; cx++)
                {
                    // Draw all random numbers up front so placement in one cell never shifts another.
                    float jx = rng.NextFloat(), jz = rng.NextFloat();
                    float roll = rng.NextFloat();
                    float scaleRoll = rng.NextFloat();
                    float yaw = rng.NextFloat() * 360f;
                    float kindRoll = rng.NextFloat();
                    if (count >= output.Length) continue;

                    double lx = cx * CellSize + 1.0 + jx * (CellSize - 2.0);
                    double lz = cz * CellSize + 1.0 + jz * (CellSize - 2.0);
                    double wx = ox + lx, wz = oz + lz;
                    TerrainSample s = gen.Sample(wx, wz);
                    if ((double)s.Height < (double)s.WaterLevel + 0.6) continue;

                    double tree = s.Height > s.SnowLine ? 0.0 : TreeDensity(s.Biome) * FamilyTreeFactor(s.Family); // nothing grows above the snow line
                    if (s.Biome == Biome.Beach && s.Temperature < 0.25f) tree = 0.0; // palms only on warm coasts
                    double bush = s.Height > s.SnowLine ? 0.0 : BushDensity(s.Biome, s.Family);
                    double boulder = BoulderDensity(s.Biome, Rocky(s.Family) ? RegionFamilyHint.Rocky : RegionFamilyHint.Normal);
                    if (roll >= tree + bush + boulder) continue;

                    // Reject steep ground (cheap two-sample slope).
                    float hx = gen.HeightAt(wx + 1.5, wz), hz = gen.HeightAt(wx, wz + 1.5);
                    double slope = System.Math.Max(System.Math.Abs((double)hx - s.Height), System.Math.Abs((double)hz - s.Height)) / 1.5;
                    bool isTree = roll < tree;
                    bool isBush = !isTree && roll < tree + bush;
                    if ((isTree || isBush) && slope > 0.6f) continue;
                    if (!isTree && !isBush && slope > 1.2f) continue;

                    PropKind kind;
                    double scale;
                    if (isTree)
                    {
                        kind = TreeKind(s, kindRoll);
                        scale = 0.75 + scaleRoll * 0.6;
                        if (s.Biome == Biome.Jungle) scale *= 1.25;
                        if (s.Family == RegionFamily.Forest) scale *= 1.1;
                    }
                    else if (isBush)
                    {
                        kind = PropKind.Bush;
                        scale = 0.7 + scaleRoll * 0.7;
                    }
                    else
                    {
                        kind = PropKind.Boulder;
                        scale = 0.5 + (double)scaleRoll * scaleRoll * 2.2;
                    }

                    output[count++] = new PropInstance
                    {
                        Kind = kind,
                        X = (float)lx,
                        Y = (float)(s.Height - (kind == PropKind.Boulder ? 0.25 * scale : 0.2)),
                        Z = (float)lz,
                        Scale = (float)scale,
                        Yaw = yaw,
                    };
                }
            }
            return count;
        }

        static PropKind TreeKind(in TerrainSample s, float kindRoll)
        {
            bool cold = s.Temperature < -0.2f || s.Biome == Biome.Tundra || s.Biome == Biome.Snow || s.Family == RegionFamily.Frost;
            if (cold) return s.Snow > 0.02f || s.Biome == Biome.Snow || s.Temperature < -0.45f ? PropKind.SnowPine : PropKind.Pine;
            if (s.Family == RegionFamily.Badlands || s.Biome == Biome.Desert || s.Biome == Biome.Steppe) return PropKind.DeadTree;
            bool warm = s.Temperature > 0.3f;
            if (warm && (s.Biome == Biome.Beach || ((s.Family == RegionFamily.Coast || s.Biome == Biome.Savanna) && s.Height - s.WaterLevel < 10f))) return PropKind.Palm;
            if (s.Family == RegionFamily.Marsh || s.Biome == Biome.Swamp) return kindRoll < 0.3f ? PropKind.DeadTree : PropKind.Broadleaf;
            if (s.Biome == Biome.Forest)
            {
                if (s.Temperature < 0.15f && kindRoll < 0.35f) return PropKind.Pine;
                if (kindRoll < 0.55f) return PropKind.Birch;
                return PropKind.Broadleaf;
            }
            return kindRoll < 0.3f ? PropKind.Birch : PropKind.Broadleaf;
        }
    }
}
