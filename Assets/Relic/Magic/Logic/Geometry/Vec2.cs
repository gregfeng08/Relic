using System;

namespace Relic.Magic
{
    /// <summary>
    /// A 2D point in drawing space. Y points up (like Unity's screen space), so
    /// "the top of the circle" is +Y and "clockwise from the top" means turning
    /// from +Y toward +X. Pure C# so Logic stays free of UnityEngine.
    /// </summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public float LengthSquared => X * X + Y * Y;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static float DistanceSquared(Vec2 a, Vec2 b) => (a - b).LengthSquared;
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        /// <summary>Rotates counter-clockwise by <paramref name="radians"/> around the origin.</summary>
        public Vec2 Rotated(float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return new Vec2(X * c - Y * s, X * s + Y * c);
        }

        /// <summary>Angle measured clockwise from +Y (the top), in [0, 2π).</summary>
        public float ClockwiseAngleFromTop()
        {
            double a = Math.Atan2(X, Y);
            if (a < 0) a += 2 * Math.PI;
            return (float)a;
        }

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }

    /// <summary>Axis-aligned bounds of some points.</summary>
    public struct Bounds2
    {
        public float MinX, MinY, MaxX, MaxY;

        public static Bounds2 Empty => new Bounds2 { MinX = float.MaxValue, MinY = float.MaxValue, MaxX = float.MinValue, MaxY = float.MinValue };

        public bool IsEmpty => MinX > MaxX;
        public float Width => IsEmpty ? 0f : MaxX - MinX;
        public float Height => IsEmpty ? 0f : MaxY - MinY;
        public float MaxExtent => Math.Max(Width, Height);
        public Vec2 Center => new Vec2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

        public void Encapsulate(Vec2 p)
        {
            if (p.X < MinX) MinX = p.X;
            if (p.Y < MinY) MinY = p.Y;
            if (p.X > MaxX) MaxX = p.X;
            if (p.Y > MaxY) MaxY = p.Y;
        }

        public void Encapsulate(Bounds2 b)
        {
            if (b.IsEmpty) return;
            Encapsulate(new Vec2(b.MinX, b.MinY));
            Encapsulate(new Vec2(b.MaxX, b.MaxY));
        }

        /// <summary>Gap between two boxes (0 when they overlap).</summary>
        public static float Gap(Bounds2 a, Bounds2 b)
        {
            float dx = Math.Max(0f, Math.Max(a.MinX - b.MaxX, b.MinX - a.MaxX));
            float dy = Math.Max(0f, Math.Max(a.MinY - b.MaxY, b.MinY - a.MaxY));
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
