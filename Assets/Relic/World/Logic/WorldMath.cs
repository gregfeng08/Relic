using System;

namespace Relic.World
{
    /// <summary>Small allocation-free math helpers shared by the world generator (pure C#, no UnityEngine).</summary>
    public static class WorldMath
    {
        public static double Clamp01(double v) => v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;

        public static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;

        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>Hermite smoothstep of t, clamped to [0, 1].</summary>
        public static double Smooth01(double t)
        {
            t = Clamp01(t);
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>GLSL-style smoothstep. edge0 may be greater than edge1 (falling ramp).</summary>
        public static double SmoothStep(double edge0, double edge1, double x) => Smooth01((x - edge0) / (edge1 - edge0));

        /// <summary>Floor that is correct for negative and very large values (up to ~9e15).</summary>
        public static long FloorToLong(double v)
        {
            long i = (long)v;
            return v < i ? i - 1 : i;
        }

        /// <summary>Maps the top bits of a hash to [0, 1).</summary>
        public static double UnitFromBits(ulong bits, int bitCount) => (bits & ((1UL << bitCount) - 1UL)) / (double)(1UL << bitCount);

        public static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        public static double Max(double a, double b) => a > b ? a : b;

        public static double Abs(double a) => Math.Abs(a);
    }

    /// <summary>A linear RGB color in [0, 1], kept engine-free so Logic stays pure.</summary>
    public struct Rgb : IEquatable<Rgb>
    {
        public float R, G, B;

        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public static Rgb operator *(Rgb a, Rgb b) => new Rgb(a.R * b.R, a.G * b.G, a.B * b.B);
        public static Rgb operator *(Rgb a, float s) => new Rgb(a.R * s, a.G * s, a.B * s);
        public static Rgb operator +(Rgb a, Rgb b) => new Rgb(a.R + b.R, a.G + b.G, a.B + b.B);

        public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

        public Rgb Clamped() => new Rgb(C(R), C(G), C(B));

        static float C(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public bool Equals(Rgb o) => R == o.R && G == o.G && B == o.B;
        public override bool Equals(object obj) => obj is Rgb o && Equals(o);
        public override int GetHashCode() => R.GetHashCode() ^ (G.GetHashCode() * 397) ^ (B.GetHashCode() * 7919);
        public override string ToString() => "(" + R.ToString("0.00") + ", " + G.ToString("0.00") + ", " + B.ToString("0.00") + ")";
    }
}
