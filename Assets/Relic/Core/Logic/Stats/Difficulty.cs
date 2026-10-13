using System;

namespace Relic.Core.Stats
{
    /// <summary>
    /// Region difficulty tier, shared by every stream: terrain, enemies, loot and bosses
    /// all read the same number. Danger rises with distance from the origin (the starting
    /// sanctuary), like RotMG's realm toward its center, but nothing is locked.
    /// </summary>
    public static class Difficulty
    {
        public const int MaxTier = 10;
        public const double MetersPerTier = 1200.0;

        public static int TierAt(double x, double z)
        {
            double d = Math.Sqrt(x * x + z * z);
            int tier = (int)Math.Floor(d / MetersPerTier);
            return tier < 0 ? 0 : tier > MaxTier ? MaxTier : tier;
        }
    }
}
