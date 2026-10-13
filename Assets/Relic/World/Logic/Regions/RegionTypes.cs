using System;

namespace Relic.World.Regions
{
    /// <summary>The broad character of a region. The biome (splat, foliage) is still chosen per point by climate.</summary>
    public enum RegionFamily : byte
    {
        Meadow = 0,    // gentle rolling grassland, the starting sanctuary's home
        Forest = 1,    // humid hills, dark greens
        Highlands = 2, // tall ridged mountains and cliffs
        Coast = 3,     // low land that falls into the sea
        Badlands = 4,  // hot, dry, terraced mesas in red clay
        Frost = 5,     // cold mountains, tundra and snow
        Marsh = 6,     // flat, wet, many ponds
    }

    /// <summary>Stable identity of a region: the jittered-grid cell that owns its center.</summary>
    public readonly struct RegionId : IEquatable<RegionId>
    {
        public readonly long X;
        public readonly long Z;

        public RegionId(long x, long z) { X = x; Z = z; }

        public bool Equals(RegionId o) => X == o.X && Z == o.Z;
        public override bool Equals(object obj) => obj is RegionId o && Equals(o);
        public override int GetHashCode() => unchecked((int)(X * 73856093L ^ Z * 19349663L));
        public static bool operator ==(RegionId a, RegionId b) => a.Equals(b);
        public static bool operator !=(RegionId a, RegionId b) => !a.Equals(b);
        public override string ToString() => "(" + X + ", " + Z + ")";
    }

    /// <summary>
    /// Parameters that shape terrain and climate inside a region. All fields except Family are
    /// blended across borders; the terrain is then generated once from the blend.
    /// </summary>
    public struct RegionParams
    {
        public RegionFamily Family;
        /// <summary>Mountain amplitude in meters (peaks-and-valleys layer).</summary>
        public float HeightScale;
        /// <summary>Multiplier for hills, detail octaves and domain-warp strength.</summary>
        public float Ruggedness;
        /// <summary>0 = sharp and rugged, 1 = worn flat.</summary>
        public float Erosion;
        /// <summary>Offset of the local water table relative to the continental base height (meters). Higher means more lakes.</summary>
        public float WaterLevelBias;
        /// <summary>Shifts continentalness: negative pulls the sea in (coasts), positive raises land.</summary>
        public float ContinentBias;
        public float TemperatureBias;
        public float HumidityBias;
        /// <summary>0..1 strength of mesa-style terracing.</summary>
        public float Terrace;
        /// <summary>Palette: multiplies the biome ground color.</summary>
        public Rgb GroundTint;
        /// <summary>Palette: color of exposed rock on slopes and cliffs.</summary>
        public Rgb RockTint;

        /// <summary>Accumulates w * p into this (Family is left alone).</summary>
        public void AddWeighted(in RegionParams p, float w)
        {
            HeightScale += p.HeightScale * w;
            Ruggedness += p.Ruggedness * w;
            Erosion += p.Erosion * w;
            WaterLevelBias += p.WaterLevelBias * w;
            ContinentBias += p.ContinentBias * w;
            TemperatureBias += p.TemperatureBias * w;
            HumidityBias += p.HumidityBias * w;
            Terrace += p.Terrace * w;
            GroundTint = GroundTint + p.GroundTint * w;
            RockTint = RockTint + p.RockTint * w;
        }
    }

    /// <summary>
    /// The result of looking up a point in a region field: up to three contributing regions
    /// (sorted by weight, strongest first), their normalized weights, and the blended parameters.
    /// </summary>
    public struct RegionSample
    {
        public RegionId Id0, Id1, Id2;
        public float W0, W1, W2;
        /// <summary>Parameters of the strongest region (its Family is the dominant family).</summary>
        public RegionFamily Family;
        /// <summary>Parameters blended by weight. Family = the dominant region's family.</summary>
        public RegionParams Blended;
        /// <summary>
        /// 0 deep inside a region, rising to 1 on a border (1 - W0 scaled). Handy for debug views.
        /// </summary>
        public float BorderFactor => 1f - W0;
    }

    /// <summary>
    /// Maps world positions to regions. Swappable: the terrain and chunk code only talk to this
    /// interface, so a later terrain-aware layout (borders snapping to ridges and rivers) can
    /// replace the warped Voronoi field without touching them.
    /// Implementations must be deterministic, thread-safe and allocation-free in Sample.
    /// </summary>
    public interface IRegionField
    {
        ulong Seed { get; }

        RegionSample Sample(double x, double z);

        RegionParams ParamsOf(RegionId id);

        /// <summary>Nominal center of a region in world meters (before domain warp; for debug and tests).</summary>
        void CenterOf(RegionId id, out double x, out double z);
    }
}
