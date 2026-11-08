using Relic.Core.Random;
using Relic.World.Chunks;
using Relic.World.Regions;

namespace Relic.World.Terrain
{
    /// <summary>Ground cover: grass, flowers, rocks, dead shrubs and succulents on a 4 m grid.</summary>
    public static partial class PropScatter
    {
        /// <summary>Green grass clumps per 4 m cell.</summary>
        public static float GrassDensity(Biome b)
        {
            switch (b)
            {
                case Biome.Grassland: return 0.7f;
                case Biome.Forest: return 0.35f;
                case Biome.Jungle: return 0.55f;
                case Biome.Swamp: return 0.5f;
                case Biome.Tundra: return 0.22f;
                default: return 0f;
            }
        }

        /// <summary>Dry (straw) grass clumps per 4 m cell.</summary>
        public static float DryGrassDensity(Biome b)
        {
            switch (b)
            {
                case Biome.Savanna: return 0.5f;
                case Biome.Steppe: return 0.4f;
                case Biome.Beach: return 0.05f;
                case Biome.Desert: return 0.03f;
                default: return 0f;
            }
        }

        static float GroundFamilyFactor(RegionFamily f)
        {
            switch (f)
            {
                case RegionFamily.Meadow: return 1.3f;
                case RegionFamily.Highlands: return 0.5f;
                case RegionFamily.Badlands: return 0.35f;
                case RegionFamily.Frost: return 0.4f;
                default: return 1f;
            }
        }

        /// <summary>Fills output with the chunk's ground cover and returns how many were placed.</summary>
        public static int PlaceSmall(TerrainGenerator gen, ChunkCoord chunk, PropInstance[] output)
        {
            var rng = new Pcg32(Hash.Combine(gen.Seed, Hash.Coord(chunk.X, chunk.Z), SmallSalt));
            double ox = ChunkMath.OriginX(chunk), oz = ChunkMath.OriginZ(chunk);
            int count = 0;
            for (int cz = 0; cz < SmallCellsPerSide; cz++)
            {
                for (int cx = 0; cx < SmallCellsPerSide; cx++)
                {
                    float jx = rng.NextFloat(), jz = rng.NextFloat();
                    float roll = rng.NextFloat();
                    float scaleRoll = rng.NextFloat();
                    float yaw = rng.NextFloat() * 360f;
                    if (count >= output.Length) continue;

                    double lx = cx * SmallCellSize + 0.4 + jx * (SmallCellSize - 0.8);
                    double lz = cz * SmallCellSize + 0.4 + jz * (SmallCellSize - 0.8);
                    double wx = ox + lx, wz = oz + lz;
                    TerrainSample s = gen.Sample(wx, wz);
                    if ((double)s.Height < (double)s.WaterLevel + 0.3 || s.Snow > 0.3f) continue;

                    bool meadow = s.Family == RegionFamily.Meadow;
                    bool badlands = s.Family == RegionFamily.Badlands;
                    float ff = GroundFamilyFactor(s.Family);
                    float grass = GrassDensity(s.Biome) * ff, dry = DryGrassDensity(s.Biome) * ff;
                    if (badlands) { dry += grass; grass = 0f; }
                    float flowers = s.Biome == Biome.Grassland ? (meadow ? 0.14f : 0.05f) : s.Biome == Biome.Forest ? 0.04f : s.Biome == Biome.Tundra ? 0.02f : 0f;
                    bool rockyFamily = badlands || s.Family == RegionFamily.Highlands || s.Family == RegionFamily.Frost;
                    float rock = rockyFamily || s.Biome == Biome.Desert || s.Biome == Biome.Steppe ? 0.05f : 0.012f;
                    bool arid = badlands || s.Biome == Biome.Desert || s.Biome == Biome.Savanna || s.Biome == Biome.Steppe;
                    float shrub = arid ? (badlands || s.Biome == Biome.Desert ? 0.06f : 0.025f) : 0f;
                    float succulent = (s.Biome == Biome.Desert || (badlands && s.Temperature > 0.2f)) ? 0.035f : s.Biome == Biome.Savanna ? 0.008f : 0f;

                    PropKind kind;
                    float r = roll;
                    if ((r -= flowers) < 0f) kind = PropKind.Flowers;
                    else if ((r -= succulent) < 0f) kind = PropKind.Succulent;
                    else if ((r -= shrub) < 0f) kind = PropKind.DeadShrub;
                    else if ((r -= rock) < 0f) kind = PropKind.Rock;
                    else if ((r -= grass) < 0f) kind = PropKind.Grass;
                    else if ((r -= dry) < 0f) kind = PropKind.DryGrass;
                    else continue;

                    // Nothing roots on rock faces.
                    float hx = gen.HeightAt(wx + 1.0, wz), hz = gen.HeightAt(wx, wz + 1.0);
                    double slope = System.Math.Max(System.Math.Abs((double)hx - s.Height), System.Math.Abs((double)hz - s.Height));
                    if (slope > (kind == PropKind.Rock ? 1.2 : 0.75)) continue;

                    double scale;
                    switch (kind)
                    {
                        case PropKind.Rock: scale = 0.35 + scaleRoll * scaleRoll * 0.9; break;
                        case PropKind.Succulent: scale = 0.6 + scaleRoll * 0.7; break;
                        case PropKind.DeadShrub: scale = 0.6 + scaleRoll * 0.6; break;
                        default: scale = 0.7 + scaleRoll * 0.6; break;
                    }
                    output[count++] = new PropInstance
                    {
                        Kind = kind,
                        X = (float)lx,
                        Y = (float)(s.Height - (kind == PropKind.Rock ? 0.15 * scale : 0.05)),
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
