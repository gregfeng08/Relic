using System;
using Relic.Core.Random;

namespace Relic.Items
{
    /// <summary>
    /// Rolls past the ceiling (02-core-loop): k = floor(Exponential(λ = 1)), so
    /// P(k ≥ n) = e^−n. Each step adds +5% over the ceiling. A hidden cap of 10
    /// steps stops a once-in-a-million roll from breaking balance.
    /// </summary>
    public static class Overroll
    {
        public const double Lambda = 1.0;
        public const int HiddenCap = 10;
        public const float StepBonus = 0.05f;

        public static int Steps(ref Pcg32 rng) => FromSample(rng.Exponential(Lambda));

        /// <summary>Maps one exponential sample to capped steps.</summary>
        public static int FromSample(double exponentialSample)
        {
            if (double.IsNaN(exponentialSample) || exponentialSample < 0) return 0;
            return (int)Math.Min(HiddenCap, Math.Floor(exponentialSample));
        }

        /// <summary>Value of a roll that went k steps past the ceiling.</summary>
        public static float Apply(float ceiling, int steps) => ceiling * (1f + StepBonus * steps);

        /// <summary>Theoretical P(k ≥ n) before the cap.</summary>
        public static double ChanceAtLeast(int n) => Math.Exp(-Lambda * n);
    }
}
