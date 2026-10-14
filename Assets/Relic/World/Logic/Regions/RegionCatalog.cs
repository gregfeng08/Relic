using Relic.Core.Random;

namespace Relic.World.Regions
{
    /// <summary>
    /// Base parameters for each region family, plus seeded per-region variation.
    /// All arithmetic that feeds the height function is done in double (or integers): Unity's Mono
    /// may evaluate float expressions at higher precision than .NET, which would make the same seed
    /// give slightly different worlds on different runtimes.
    /// </summary>
    public static class RegionCatalog
    {
        public const int FamilyCount = 7;

        /// <summary>Relative odds of each family away from spawn (index = RegionFamily).</summary>
        static readonly int[] Odds = { 18, 20, 16, 12, 12, 10, 12 };
        static readonly int OddsTotal = 100;

        public static RegionFamily PickFamily(ref Pcg32 rng)
        {
            int r = rng.Range(0, OddsTotal);
            for (int i = 0; i < Odds.Length; i++)
            {
                r -= Odds[i];
                if (r < 0) return (RegionFamily)i;
            }
            return RegionFamily.Meadow;
        }

        static double Range(ref Pcg32 rng, double min, double max) => min + (max - min) * rng.NextDouble();

        /// <summary>The family's base parameters, varied by the region's own generator.</summary>
        public static RegionParams Build(RegionFamily f, ref Pcg32 rng, int tier)
        {
            RegionParams p = Base(f);
            p.Family = f;
            p.HeightScale *= Range(ref rng, 0.8, 1.2) * (1.0 + 0.03 * tier);
            p.Ruggedness *= Range(ref rng, 0.85, 1.15) * (1.0 + 0.02 * tier);
            p.Erosion = WorldMath.Clamp01(p.Erosion + Range(ref rng, -0.1, 0.1));
            p.WaterLevelBias += Range(ref rng, -1.5, 1.5);
            p.ContinentBias += Range(ref rng, -0.05, 0.05);
            p.TemperatureBias += Range(ref rng, -0.1, 0.1);
            p.HumidityBias += Range(ref rng, -0.1, 0.1);
            double hue = Range(ref rng, -0.05, 0.05);
            p.GroundTint = new Rgb((float)(p.GroundTint.R * (1.0 + hue)), p.GroundTint.G, (float)(p.GroundTint.B * (1.0 - hue)));
            double rockShade = Range(ref rng, 0.92, 1.08);
            p.RockTint = new Rgb((float)(p.RockTint.R * rockShade), (float)(p.RockTint.G * rockShade), (float)(p.RockTint.B * rockShade));
            return p;
        }

        public static RegionParams Base(RegionFamily f)
        {
            switch (f)
            {
                case RegionFamily.Forest:
                    return Make(110f, 0.95f, 0.40f, -13f, 0.08f, 0.05f, 0.45f, 0f, new Rgb(0.92f, 1.00f, 0.92f), new Rgb(0.46f, 0.47f, 0.45f));
                case RegionFamily.Highlands:
                    return Make(300f, 1.20f, 0.10f, -20f, 0.20f, -0.15f, 0.00f, 0f, new Rgb(1.00f, 1.00f, 0.95f), new Rgb(0.58f, 0.55f, 0.52f));
                case RegionFamily.Coast:
                    return Make(45f, 0.65f, 0.60f, -9f, -0.15f, 0.25f, 0.15f, 0f, new Rgb(1.02f, 1.02f, 0.95f), new Rgb(0.76f, 0.70f, 0.60f));
                case RegionFamily.Badlands:
                    return Make(150f, 0.80f, 0.30f, -30f, 0.15f, 0.50f, -0.65f, 0.85f, new Rgb(1.05f, 0.95f, 0.88f), new Rgb(0.74f, 0.42f, 0.27f));
                case RegionFamily.Frost:
                    return Make(210f, 1.00f, 0.30f, -14f, 0.10f, -0.80f, 0.00f, 0f, new Rgb(0.95f, 1.00f, 1.05f), new Rgb(0.55f, 0.58f, 0.66f));
                case RegionFamily.Marsh:
                    return Make(18f, 0.45f, 0.85f, -1.5f, 0.03f, 0.15f, 0.75f, 0f, new Rgb(0.92f, 0.96f, 0.85f), new Rgb(0.43f, 0.43f, 0.37f));
                default: // Meadow
                    return Make(55f, 0.65f, 0.65f, -12f, 0.12f, 0.10f, 0.05f, 0f, new Rgb(1.00f, 1.00f, 1.00f), new Rgb(0.55f, 0.53f, 0.50f));
            }
        }

        static RegionParams Make(double heightScale, double rugged, double erosion, double waterBias, double continentBias,
                                 double tempBias, double humidBias, double terrace, Rgb ground, Rgb rock)
        {
            return new RegionParams
            {
                HeightScale = heightScale,
                Ruggedness = rugged,
                Erosion = erosion,
                WaterLevelBias = waterBias,
                ContinentBias = continentBias,
                TemperatureBias = tempBias,
                HumidityBias = humidBias,
                Terrace = terrace,
                GroundTint = ground,
                RockTint = rock,
            };
        }

    }
}
