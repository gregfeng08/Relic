namespace Relic.World.Terrain
{
    /// <summary>Per-point biome chosen by climate. Drives splat color now and foliage later.</summary>
    public enum Biome : byte
    {
        Seabed = 0,
        Beach = 1,
        Grassland = 2,
        Forest = 3,
        Steppe = 4,
        Savanna = 5,
        Desert = 6,
        Jungle = 7,
        Swamp = 8,
        Tundra = 9,
        Snow = 10,
    }

    /// <summary>
    /// Whittaker-style table: temperature x humidity, with special cases for water edges and cold.
    /// Temperature and humidity are unitless, centered on 0, typically within [-1.5, 1.5].
    /// </summary>
    public static class BiomeTable
    {
        public const int Count = 11;

        /// <param name="temperature">Already includes region bias and altitude cooling.</param>
        /// <param name="humidity">Includes region bias.</param>
        /// <param name="heightAboveWater">Terrain height minus local water level, in meters.</param>
        public static Biome Classify(double temperature, double humidity, double heightAboveWater)
        {
            if (heightAboveWater < -0.6) return Biome.Seabed;
            if (temperature < -0.75) return Biome.Snow;
            if (heightAboveWater < 1.6 && temperature > -0.45 && humidity < 0.65) return Biome.Beach;
            if (temperature < -0.4) return Biome.Tundra;
            if (temperature > 0.45)
            {
                if (humidity < -0.2) return Biome.Desert;
                if (humidity < 0.3) return Biome.Savanna;
                return Biome.Jungle;
            }
            if (humidity < -0.3) return Biome.Steppe;
            if (humidity < 0.22) return Biome.Grassland;
            if (humidity < 0.62) return Biome.Forest;
            return Biome.Swamp;
        }

        /// <summary>Base ground color (linear-ish, stylized and saturated) for a biome.</summary>
        public static Rgb GroundColor(Biome b)
        {
            switch (b)
            {
                case Biome.Seabed: return new Rgb(0.62f, 0.58f, 0.44f);
                case Biome.Beach: return new Rgb(0.93f, 0.85f, 0.62f);
                case Biome.Grassland: return new Rgb(0.45f, 0.74f, 0.28f);
                case Biome.Forest: return new Rgb(0.24f, 0.54f, 0.22f);
                case Biome.Steppe: return new Rgb(0.74f, 0.71f, 0.38f);
                case Biome.Savanna: return new Rgb(0.80f, 0.68f, 0.34f);
                case Biome.Desert: return new Rgb(0.87f, 0.55f, 0.33f);
                case Biome.Jungle: return new Rgb(0.16f, 0.60f, 0.30f);
                case Biome.Swamp: return new Rgb(0.33f, 0.43f, 0.22f);
                case Biome.Tundra: return new Rgb(0.60f, 0.65f, 0.52f);
                case Biome.Snow: return new Rgb(0.94f, 0.96f, 1.00f);
                default: return new Rgb(1f, 0f, 1f);
            }
        }
    }
}
