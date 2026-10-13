using System;

namespace Relic.Core.Random
{
    /// <summary>
    /// PCG32 random generator. Deterministic across platforms; use it for all
    /// generation (never UnityEngine.Random or System.Random) so a seed always
    /// produces the same world, item or boss.
    /// </summary>
    public struct Pcg32
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong _state;
        readonly ulong _inc;

        public Pcg32(ulong seed, ulong stream = 0xDA3E39CB94B95BDBUL)
        {
            _state = 0;
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        /// <summary>A generator for one purpose within one seed, e.g. ForPurpose(worldSeed, "relic.stats").</summary>
        public static Pcg32 ForPurpose(ulong seed, string purpose) => new Pcg32(Hash.Combine(seed, Hash.Of(purpose)));

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _inc);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (((ulong)NextUInt() << 21) ^ (NextUInt() >> 11)) * (1.0 / 9007199254740992.0);

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            uint span = (uint)(maxExclusive - min);
            // Lemire's debiased bounded integer.
            ulong m = (ulong)NextUInt() * span;
            uint low = (uint)m;
            if (low < span)
            {
                uint threshold = (uint)(-span) % span;
                while (low < threshold)
                {
                    m = (ulong)NextUInt() * span;
                    low = (uint)m;
                }
            }
            return min + (int)(m >> 32);
        }

        public bool Chance(float p) => NextFloat() < p;

        /// <summary>Exponential sample with rate lambda (mean 1/lambda). Used for overroll steps.</summary>
        public double Exponential(double lambda)
        {
            double u = 1.0 - NextDouble(); // (0, 1]
            return -Math.Log(u) / lambda;
        }
    }
}
