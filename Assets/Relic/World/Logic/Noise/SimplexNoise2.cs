using System;
using Relic.Core.Random;

namespace Relic.World.Noise
{
    /// <summary>
    /// Seeded 2D simplex noise in double precision. Lattice gradients come from an integer
    /// hash of (seed, i, j) instead of a permutation table, so the noise never repeats and
    /// stays accurate far from the origin (tested at 1e6 m). Output is roughly in [-1, 1].
    /// Immutable, thread-safe and allocation-free.
    /// </summary>
    public readonly struct SimplexNoise2
    {
        const double F2 = 0.36602540378443864676; // (sqrt(3) - 1) / 2
        const double G2 = 0.21132486540518711775; // (3 - sqrt(3)) / 6
        const double Scale = 99.2;                // normalizes unit gradients with the 0.5 kernel to about [-1, 1]
        const int GradCount = 32;

        // Gradients: 8 integer directions per quadrant (about 11.25 degrees apart), rotated four times.
        // Built from integers and Math.Sqrt (correctly rounded by IEEE 754) instead of sin/cos,
        // whose last bits can differ between .NET and Unity's Mono and would break determinism.
        static readonly int[] QuadrantX = { 10, 10, 15, 11, 9, 8, 3, 1 };
        static readonly int[] QuadrantY = { 1, 3, 8, 9, 11, 15, 10, 10 };
        static readonly double[] GradX = BuildGrad(true);
        static readonly double[] GradY = BuildGrad(false);

        readonly ulong _seed;

        public SimplexNoise2(ulong seed) { _seed = Hash.Mix(seed ^ 0x5851F42D4C957F2DUL); }

        /// <summary>A noise layer for one purpose, e.g. ForPurpose(worldSeed, "terrain.hills").</summary>
        public static SimplexNoise2 ForPurpose(ulong seed, string purpose) => new SimplexNoise2(Hash.Combine(seed, Hash.Of(purpose)));

        public double Sample(double x, double y)
        {
            double s = (x + y) * F2;
            long i = WorldMath.FloorToLong(x + s);
            long j = WorldMath.FloorToLong(y + s);
            double t = (i + j) * G2;
            double x0 = x - (i - t);
            double y0 = y - (j - t);

            long i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; } else { i1 = 0; j1 = 1; }

            double x1 = x0 - i1 + G2;
            double y1 = y0 - j1 + G2;
            double x2 = x0 - 1.0 + 2.0 * G2;
            double y2 = y0 - 1.0 + 2.0 * G2;

            double n = 0.0;
            double t0 = 0.5 - x0 * x0 - y0 * y0;
            if (t0 > 0.0) { t0 *= t0; n += t0 * t0 * Grad(i, j, x0, y0); }
            double t1 = 0.5 - x1 * x1 - y1 * y1;
            if (t1 > 0.0) { t1 *= t1; n += t1 * t1 * Grad(i + i1, j + j1, x1, y1); }
            double t2 = 0.5 - x2 * x2 - y2 * y2;
            if (t2 > 0.0) { t2 *= t2; n += t2 * t2 * Grad(i + 1, j + 1, x2, y2); }
            return n * Scale;
        }

        /// <summary>Fractal Brownian motion: octaves of noise at doubling frequency and halving amplitude. Normalized to about [-1, 1].</summary>
        public double Fbm(double x, double y, int octaves, double lacunarity = 2.0, double gain = 0.5)
        {
            double sum = 0.0, amp = 1.0, norm = 0.0;
            for (int o = 0; o < octaves; o++)
            {
                // Per-octave offsets decorrelate octaves that share one lattice hash.
                sum += amp * Sample(x + o * 131.71, y - o * 71.37);
                norm += amp;
                amp *= gain;
                x *= lacunarity;
                y *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>
        /// Ridged multifractal noise in [0, 1]: sharp crests where the base noise crosses zero.
        /// Each octave is weighted by the previous one so detail gathers on the ridges.
        /// </summary>
        public double Ridged(double x, double y, int octaves, double lacunarity = 2.0, double gain = 0.5)
        {
            double sum = 0.0, amp = 1.0, norm = 0.0, weight = 1.0;
            for (int o = 0; o < octaves; o++)
            {
                double r = 1.0 - Math.Abs(Sample(x + o * 57.13, y + o * 113.9));
                if (r < 0.0) r = 0.0;
                r *= r;
                r *= weight;
                weight = WorldMath.Clamp01(r * 1.5);
                sum += r * amp;
                norm += amp;
                amp *= gain;
                x *= lacunarity;
                y *= lacunarity;
            }
            return WorldMath.Clamp01(sum / norm);
        }

        double Grad(long i, long j, double x, double y)
        {
            ulong h = _seed ^ unchecked((ulong)i * 0x9E3779B97F4A7C15UL);
            h ^= unchecked((ulong)j * 0xC2B2AE3D27D4EB4FUL);
            h ^= h >> 29;
            h = unchecked(h * 0xBF58476D1CE4E5B9UL);
            h ^= h >> 32;
            int g = (int)(h & (GradCount - 1));
            return GradX[g] * x + GradY[g] * y;
        }

        static double[] BuildGrad(bool xs)
        {
            var a = new double[GradCount];
            for (int k = 0; k < GradCount; k++)
            {
                int q = k / 8, m = k % 8;
                double gx = QuadrantX[m], gy = QuadrantY[m];
                for (int r = 0; r < q; r++) { double tmp = gx; gx = -gy; gy = tmp; }
                double len = Math.Sqrt(gx * gx + gy * gy);
                a[k] = (xs ? gx : gy) / len;
            }
            return a;
        }
    }
}
