namespace Relic.Core.Random
{
    /// <summary>
    /// Deterministic hashing for seeds. Same inputs give the same output on every
    /// machine and runtime, which System.HashCode and System.Random do not promise.
    /// </summary>
    public static class Hash
    {
        /// <summary>SplitMix64 finalizer: a fast, well-distributed 64-bit mix.</summary>
        public static ulong Mix(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        public static ulong Combine(ulong a, ulong b) => Mix(a ^ Mix(b));

        public static ulong Combine(ulong a, ulong b, ulong c) => Combine(Combine(a, b), c);

        public static ulong Combine(ulong a, ulong b, ulong c, ulong d) => Combine(Combine(a, b, c), d);

        /// <summary>Signed coordinates (chunk x/z, cell ids) folded into the hash without sign issues.</summary>
        public static ulong Coord(long x, long z) => Combine(unchecked((ulong)x), unchecked((ulong)z));

        /// <summary>
        /// Stable string hash (FNV-1a 64) for salts such as "terrain.height" or "relic.name".
        /// Never use string.GetHashCode for seeds: it is randomized per process.
        /// </summary>
        public static ulong Of(string s)
        {
            ulong h = 14695981039346656037UL;
            if (s == null) return h;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 1099511628211UL;
            }
            return Mix(h);
        }
    }
}
