using Relic.Core.Random;

namespace Relic.World.Regions
{
    /// <summary>Base parameters for each region family, plus seeded per-region variation.</summary>
    public static class RegionCatalog
    {
        public const int FamilyCount = 7;

        /// <summary>Relative odds of each family away from spawn (index = RegionFamily).</summary>
        static readonly float[] Odds = { 18f, 20f, 16f, 12f, 12f, 10f, 12f };

        public static RegionFamily PickFamily(ref Pcg32 rng)
        {
            float total = 0f;
            for (int i = 0; i < Odds.Length; i++) total += Odds[i];
            float r = rng.NextFloat() * total;
            for (int i = 0; i < Odds.Length; i++)
            {
                r -= Odds[i];
                if (r < 0f) return (RegionFamily)i;
            }
            return RegionFamily.Meadow;
        }

        /// <summary>The family's base parameters, varied by the region's own generator.</summary>
        public static RegionParams Build(RegionFamily f, ref Pcg32 rng, int tier)
        {
            RegionParams p = Base(f);
            p.Family = f;
            p.HeightScale *= rng.Range(0.8f, 1.2f) * (1f + 0.03f * tier);
            p.Ruggedness *= rng.Range(0.85f, 1.15f) * (1f + 0.02f * tier);
            p.Erosion = Clamp01(p.Erosion + rng.Range(-0.1f, 0.1f));
            p.WaterLevelBias += rng.Range(-1.5f, 1.5f);
            p.ContinentBias += rng.Range(-0.05f, 0.05f);
            p.TemperatureBias += rng.Range(-0.1f, 0.1f);
            p.HumidityBias += rng.Range(-0.1f, 0.1f);
            float hue = rng.Range(-0.05f, 0.05f);
            p.GroundTint = new Rgb(p.GroundTint.R * (1f + hue), p.GroundTint.G, p.GroundTint.B * (1f - hue));
            float rockShade = rng.Range(0.92f, 1.08f);
            p.RockTint = p.RockTint * rockShade;
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

        static RegionParams Make(float heightScale, float rugged, float erosion, float waterBias, float continentBias,
                                 float tempBias, float humidBias, float terrace, Rgb ground, Rgb rock)
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

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
