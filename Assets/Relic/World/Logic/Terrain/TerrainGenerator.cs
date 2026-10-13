using System;
using Relic.World.Noise;
using Relic.World.Regions;

namespace Relic.World.Terrain
{
    /// <summary>Everything the mesh builder needs about one terrain point.</summary>
    public struct TerrainSample
    {
        public float Height;
        /// <summary>Local water surface (sea level or a lake's water table). Water shows where Height is below it.</summary>
        public float WaterLevel;
        public Biome Biome;
        public float Temperature;
        public float Humidity;
        /// <summary>Splat color for flat ground (biome color times region palette).</summary>
        public Rgb Ground;
        /// <summary>Splat color for exposed rock on steep slopes.</summary>
        public Rgb Rock;
        public RegionFamily Family;
        public RegionId Region;
        /// <summary>0 deep inside a region, toward 1 on a border.</summary>
        public float BorderFactor;

        public bool IsUnderwater => Height < WaterLevel;
    }

    /// <summary>
    /// The world's height and climate function: a pure function of seed and coordinates.
    /// Thread-safe and allocation-free after construction (build one per world and share it).
    ///
    /// Layers, each scaled by the blended region parameters:
    /// continentalness (very low frequency, through a spline) → erosion (flat vs. rugged) →
    /// peaks and valleys (ridged, domain-warped) → hills and detail fBm (scaled by ruggedness)
    /// → optional mesa terracing. Climate (temperature, humidity, altitude cooling) then picks
    /// the biome per point.
    /// </summary>
    public sealed class TerrainGenerator
    {
        /// <summary>Bump when the height function changes, so saves can tell old terrain from new.</summary>
        public const int Version = 1;
        public const float SeaLevel = 0f;

        // Wavelengths in meters.
        const double ContinentWavelength = 7000.0;
        const double ErosionWavelength = 2400.0;
        const double MountainMaskWavelength = 3000.0;
        const double PeaksWavelength = 1150.0;
        const double HillsWavelength = 320.0;
        const double DetailWavelength = 56.0;
        const double WarpWavelength = 650.0;
        const double TemperatureWavelength = 3600.0;
        const double HumidityWavelength = 3000.0;
        const double TintWavelength = 37.0;

        const double TerraceStep = 13.0;

        readonly ulong _seed;
        readonly IRegionField _regions;
        readonly SimplexNoise2 _continent, _erosion, _mountainMask, _peaks, _hills, _detail;
        readonly SimplexNoise2 _warpX, _warpZ, _temperature, _humidity, _tint;
        readonly MonotoneSpline _continentSpline;

        public TerrainGenerator(ulong seed, IRegionField regions = null)
        {
            _seed = seed;
            _regions = regions ?? new WarpedVoronoiRegionField(seed);
            _continent = SimplexNoise2.ForPurpose(seed, "terrain.continent");
            _erosion = SimplexNoise2.ForPurpose(seed, "terrain.erosion");
            _mountainMask = SimplexNoise2.ForPurpose(seed, "terrain.mountainmask");
            _peaks = SimplexNoise2.ForPurpose(seed, "terrain.peaks");
            _hills = SimplexNoise2.ForPurpose(seed, "terrain.hills");
            _detail = SimplexNoise2.ForPurpose(seed, "terrain.detail");
            _warpX = SimplexNoise2.ForPurpose(seed, "terrain.warp.x");
            _warpZ = SimplexNoise2.ForPurpose(seed, "terrain.warp.z");
            _temperature = SimplexNoise2.ForPurpose(seed, "climate.temperature");
            _humidity = SimplexNoise2.ForPurpose(seed, "climate.humidity");
            _tint = SimplexNoise2.ForPurpose(seed, "terrain.tint");

            // Continentalness → base height in meters: deep sea, shelf, coast, lowland, upland.
            _continentSpline = new MonotoneSpline(
                new[] { -1.2, -0.5, -0.2, -0.05, 0.05, 0.2, 0.45, 0.75, 1.2 },
                new[] { -70.0, -38.0, -9.0, -1.5, 2.0, 6.0, 18.0, 38.0, 65.0 });
        }

        public ulong Seed => _seed;

        public IRegionField Regions => _regions;

        public float HeightAt(double x, double z)
        {
            RegionSample r = _regions.Sample(x, z);
            Height(x, z, r.Blended, out double h, out _);
            return (float)h;
        }

        public float WaterLevelAt(double x, double z)
        {
            RegionSample r = _regions.Sample(x, z);
            Height(x, z, r.Blended, out _, out double w);
            return (float)w;
        }

        public void HeightAndWater(double x, double z, out float height, out float water)
        {
            RegionSample r = _regions.Sample(x, z);
            Height(x, z, r.Blended, out double h, out double w);
            height = (float)h;
            water = (float)w;
        }

        public TerrainSample Sample(double x, double z)
        {
            RegionSample r = _regions.Sample(x, z);
            RegionParams p = r.Blended;
            Height(x, z, p, out double h, out double water);

            double temperature = 0.5 * _temperature.Fbm(x / TemperatureWavelength, z / TemperatureWavelength, 2)
                                 + p.TemperatureBias
                                 - WorldMath.Max(0.0, h) / 420.0; // altitude cooling
            double humidity = 0.5 * _humidity.Fbm(x / HumidityWavelength, z / HumidityWavelength, 2) + p.HumidityBias;
            Biome biome = BiomeTable.Classify(temperature, humidity, h - water);

            float tint = (float)(0.93 + 0.07 * _tint.Fbm(x / TintWavelength, z / TintWavelength, 2));
            Rgb ground = (BiomeTable.GroundColor(biome) * p.GroundTint * tint).Clamped();

            return new TerrainSample
            {
                Height = (float)h,
                WaterLevel = (float)water,
                Biome = biome,
                Temperature = (float)temperature,
                Humidity = (float)humidity,
                Ground = ground,
                Rock = p.RockTint.Clamped(),
                Family = r.Family,
                Region = r.Id0,
                BorderFactor = r.BorderFactor,
            };
        }

        void Height(double x, double z, in RegionParams p, out double height, out double water)
        {
            // 1. Continentalness: ocean vs. coast vs. inland. Spawn is always on land.
            double distOrigin = Math.Sqrt(x * x + z * z);
            double c = 0.1 + 1.3 * _continent.Fbm(x / ContinentWavelength, z / ContinentWavelength, 3)
                       + p.ContinentBias
                       + 0.35 * WorldMath.SmoothStep(2400.0, 700.0, distOrigin);
            double baseH = _continentSpline.Evaluate(c);
            double land = WorldMath.SmoothStep(-0.08, 0.2, c);

            // 2. Erosion: flat (1) vs. rugged (0).
            double e = WorldMath.Clamp01(p.Erosion + 0.35 * _erosion.Fbm(x / ErosionWavelength, z / ErosionWavelength, 2));
            double rugged = p.Ruggedness;

            // 4. Domain warp for the shaped layers: blobs become ridges and winding valleys.
            double warpAmp = 110.0 * (0.6 + 0.4 * rugged);
            double wx = x + warpAmp * _warpX.Fbm(x / WarpWavelength, z / WarpWavelength, 2);
            double wz = z + warpAmp * _warpZ.Fbm(x / WarpWavelength, z / WarpWavelength, 2);

            // 3. Peaks and valleys: ridged noise, gathered into ranges by a low-frequency mask.
            double pv = _peaks.Ridged(wx / PeaksWavelength, wz / PeaksWavelength, 3);
            double mask = WorldMath.SmoothStep(-0.35, 0.45, _mountainMask.Fbm(x / MountainMaskWavelength, z / MountainMaskWavelength, 2));
            double flat = 1.0 - e;
            double mountains = p.HeightScale * pv * pv * flat * flat * land * (0.25 + 0.75 * mask);

            // 5. Hills and detail octaves, scaled by ruggedness.
            double hills = _hills.Fbm(wx / HillsWavelength, wz / HillsWavelength, 4) * 30.0 * rugged * (1.0 - 0.55 * e) * (0.35 + 0.65 * land);
            double detail = _detail.Fbm(wx / DetailWavelength, wz / DetailWavelength, 3) * 2.4 * rugged * (0.5 + 0.5 * land);

            double h = baseH + mountains + hills + detail;

            // Local water: the sea, or a lake water table a few meters under the continental base.
            water = WorldMath.Max(SeaLevel, baseH + p.WaterLevelBias);

            // 6. Mesa terracing (badlands): flat steps and steep risers above the water line.
            double terrace = p.Terrace * WorldMath.SmoothStep(water + 1.0, water + 6.0, h);
            if (terrace > 1e-4)
            {
                double k = h / TerraceStep;
                double fl = Math.Floor(k);
                double stepped = (fl + WorldMath.SmoothStep(0.38, 0.62, k - fl)) * TerraceStep;
                h = WorldMath.Lerp(h, stepped, terrace);
            }

            height = h;
        }

        /// <summary>
        /// A dry, fairly flat spawn point near the origin (spiral search). Deterministic.
        /// </summary>
        public void FindSpawn(out double x, out double z)
        {
            for (int ring = 0; ring < 60; ring++)
            {
                int n = ring == 0 ? 1 : ring * 8;
                for (int i = 0; i < n; i++)
                {
                    double ang = ring == 0 ? 0.0 : i * (2.0 * Math.PI / n);
                    double px = ring * 8.0 * Math.Cos(ang), pz = ring * 8.0 * Math.Sin(ang);
                    HeightAndWater(px, pz, out float h, out float w);
                    if (h < w + 1.5f) continue;
                    float hx = HeightAt(px + 2.0, pz), hz = HeightAt(px, pz + 2.0);
                    if (Math.Abs(hx - h) < 1.0f && Math.Abs(hz - h) < 1.0f) { x = px; z = pz; return; }
                }
            }
            x = 0.0; z = 0.0;
        }
    }
}
