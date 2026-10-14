using Relic.Core.Random;
using Relic.World.Chunks;

namespace Relic.World.Terrain
{
    /// <summary>Placeholder props scattered on the terrain (art comes later; placement is final-ish).</summary>
    public enum PropKind : byte
    {
        Broadleaf = 0,
        Pine = 1,
        Boulder = 2,
    }

    /// <summary>One prop, positioned relative to its chunk's origin (meters).</summary>
    public struct PropInstance
    {
        public PropKind Kind;
        public float X, Y, Z;
        public float Scale;
        public float Yaw; // degrees
    }

    /// <summary>
    /// Deterministic per-chunk prop placement: a jittered 8 m grid, kept or dropped by the biome's
    /// density, never under water or on cliffs. Pure function of (seed, chunk); thread-safe.
    /// </summary>
    public static class PropScatter
    {
        public const int CellSize = 8;
        public const int CellsPerSide = ChunkMath.ChunkSize / CellSize; // 8
        public const int MaxPerChunk = CellsPerSide * CellsPerSide + 16;

        static readonly ulong Salt = Hash.Of("props");

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
                default: return 0f;
            }
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

        /// <summary>Fills output with the chunk's props and returns how many were placed.</summary>
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

                    double tree = TreeDensity(s.Biome);
                    var hint = s.Family == Regions.RegionFamily.Highlands || s.Family == Regions.RegionFamily.Badlands || s.Family == Regions.RegionFamily.Frost
                        ? RegionFamilyHint.Rocky : RegionFamilyHint.Normal;
                    double boulder = BoulderDensity(s.Biome, hint);
                    if (roll >= tree + boulder) continue;

                    // Reject steep ground (cheap two-sample slope).
                    float hx = gen.HeightAt(wx + 1.5, wz), hz = gen.HeightAt(wx, wz + 1.5);
                    double slope = System.Math.Max(System.Math.Abs((double)hx - s.Height), System.Math.Abs((double)hz - s.Height)) / 1.5;
                    bool isTree = roll < tree;
                    if (isTree && slope > 0.6f) continue;
                    if (!isTree && slope > 1.2f) continue;

                    PropKind kind;
                    double scale;
                    if (isTree)
                    {
                        bool cold = s.Temperature < -0.2f || s.Biome == Biome.Tundra || s.Biome == Biome.Snow;
                        bool mixed = s.Biome == Biome.Forest && s.Temperature < 0.15f && kindRoll < 0.35f;
                        kind = cold || mixed ? PropKind.Pine : PropKind.Broadleaf;
                        scale = 0.75 + scaleRoll * 0.6;
                        if (s.Biome == Biome.Jungle) scale *= 1.25;
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
    }
}
