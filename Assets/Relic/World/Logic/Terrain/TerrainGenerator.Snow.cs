using Relic.World.Noise;

namespace Relic.World.Terrain
{
    /// <summary>
    /// Snow caps (grounded: high peaks are snowy in every climate). The snow line is an absolute
    /// altitude shifted by the region's climate (low in Frost, mid in Meadow and Forest, high in
    /// Badlands and warm coasts) but always low enough for the region's tallest peaks to reach.
    /// Cover rises through a noisy band of about 40 m: rock and alpine turf, patchy snow,
    /// full snow. Slope is left to the shader: snow only holds below ~47 degrees, so cliffs stay
    /// rock with snow on their ledges. Biome follows: alpine tundra (sparse snowy pines) just
    /// below the line, Snow (no trees) above it.
    /// </summary>
    public sealed partial class TerrainGenerator
    {
        const double SnowNoiseWavelength = 70.0;
        const double SnowNoiseAmplitude = 14.0;
        static readonly Rgb AlpineGround = new Rgb(0.55f, 0.58f, 0.47f);

        readonly SimplexNoise2 _snow;

        /// <summary>
        /// Snow line altitude (m) for a climate temperature (before altitude cooling: region bias
        /// plus climate noise) and the region's mountain height scale.
        /// </summary>
        public static double SnowLineFor(double climateTemperature, double heightScale)
        {
            double climateLine = WorldMath.Max(70.0, 175.0 + 150.0 * climateTemperature);
            // Calibrated on seed 1337 (8 km square): a region's top 1% of heights sits near 30 + 0.5 x HeightScale,
            // so this puts a visible cap on the tallest peaks of every family.
            double reachable = 30.0 + 0.5 * heightScale;
            double line = climateLine < reachable ? climateLine : reachable;
            // Gentle lowland families (low HeightScale) don't get snowy hills: never below 115 m
            // unless the climate itself is that cold.
            double floor = climateLine < 115.0 ? climateLine : 115.0;
            return WorldMath.Max(line, floor);
        }

        void Snowfall(double x, double z, double h, double climate, double temperature, double heightScale,
                      ref Biome biome, ref Rgb ground, out double snow, out double snowLine)
        {
            snowLine = SnowLineFor(climate, heightScale);
            double wobble = SnowNoiseAmplitude * _snow.Fbm(x / SnowNoiseWavelength, z / SnowNoiseWavelength, 2);
            // 0..0.75: the cover band; 0.75..1: "deep" snow well above the line, which the shader lets
            // cling to steeper faces so tall needles read as snowy rather than bare.
            double band = 0.75 * WorldMath.SmoothStep(snowLine - 28.0, snowLine + 12.0, h + wobble)
                          + 0.25 * WorldMath.SmoothStep(snowLine + 12.0, snowLine + 140.0, h);
            // Snowfields of cold climates (Frost lowlands) fade in with temperature, no contour.
            double cold = 0.75 * WorldMath.SmoothStep(-0.62, -0.82, temperature);
            snow = band > cold ? band : cold;

            if (biome == Biome.Seabed) { snow = 0.0; return; }

            // Alpine ground under the snow: greens fade to a grey-green turf toward the line.
            double alpine = WorldMath.SmoothStep(snowLine - 70.0, snowLine - 12.0, h + wobble);
            if (alpine > 0.0) ground = Rgb.Lerp(ground, AlpineGround, (float)(alpine * 0.75));

            if (h + wobble > snowLine) biome = Biome.Snow;
            else if (h + wobble > snowLine - 22.0 && biome != Biome.Snow && biome != Biome.Beach) biome = Biome.Tundra;
        }
    }
}
