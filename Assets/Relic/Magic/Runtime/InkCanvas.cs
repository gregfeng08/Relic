using System;
using System.Collections.Generic;
using Relic.Core.Random;
using UnityEngine;

namespace Relic.Magic.Desk
{
    /// <summary>
    /// A square parchment texture that strokes are inked onto. Coordinates are
    /// texture pixels with Y up, the same space the spell logic uses.
    /// </summary>
    public sealed class InkCanvas : IDisposable
    {
        public readonly int Size;
        public Texture2D Texture { get; private set; }

        readonly Color32[] _pixels;
        readonly Color32[] _paper;

        public InkCanvas(int size)
        {
            Size = size;
            Texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Writing Desk Canvas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _pixels = new Color32[size * size];
            _paper = MakeParchment(size);
            Clear();
            Apply();
        }

        /// <summary>Warm paper with a little grain and darker edges (deterministic).</summary>
        static Color32[] MakeParchment(int size)
        {
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half, dy = (y - half) / half;
                float edge = Mathf.Clamp01((Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) - 0.75f) / 0.25f);
                float grain = (Hash.Coord(x / 2, y / 2) & 0xFF) / 255f - 0.5f;
                float blotch = Mathf.PerlinNoise(x * 0.012f, y * 0.012f) - 0.5f;
                float shade = 1f - 0.12f * edge * edge + 0.025f * grain + 0.05f * blotch;
                px[y * size + x] = new Color32(
                    (byte)Mathf.Clamp(238 * shade, 0, 255),
                    (byte)Mathf.Clamp(224 * shade, 0, 255),
                    (byte)Mathf.Clamp(192 * shade, 0, 255),
                    255);
            }
            return px;
        }

        public void Clear() => Array.Copy(_paper, _pixels, _pixels.Length);

        public void Apply()
        {
            Texture.SetPixels32(_pixels);
            Texture.Apply(false);
        }

        public void Polyline(IList<Vec2> pts, Color32 color, float radius)
        {
            if (pts == null || pts.Count == 0) return;
            if (pts.Count == 1) Stamp(pts[0], color, radius);
            for (int i = 1; i < pts.Count; i++) Line(pts[i - 1], pts[i], color, radius);
        }

        public void Line(Vec2 a, Vec2 b, Color32 color, float radius)
        {
            float len = Vec2.Distance(a, b);
            float step = Mathf.Max(0.5f, radius * 0.5f);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
            for (int i = 0; i <= n; i++) Stamp(Vec2.Lerp(a, b, (float)i / n), color, radius);
        }

        /// <summary>A circle outline, optionally dashed (for faint guides).</summary>
        public void Circle(Vec2 center, float r, Color32 color, float radius, bool dashed)
        {
            int segments = Mathf.Max(24, Mathf.CeilToInt(r * 0.6f));
            for (int i = 0; i < segments; i++)
            {
                if (dashed && (i % 2) == 1) continue;
                float a0 = 2f * Mathf.PI * i / segments, a1 = 2f * Mathf.PI * (i + 1) / segments;
                Line(center + new Vec2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, center + new Vec2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, color, radius);
            }
        }

        /// <summary>A soft round dab of ink (anti-aliased edge).</summary>
        public void Stamp(Vec2 p, Color32 color, float radius)
        {
            int x0 = Mathf.FloorToInt(p.X - radius - 1), x1 = Mathf.CeilToInt(p.X + radius + 1);
            int y0 = Mathf.FloorToInt(p.Y - radius - 1), y1 = Mathf.CeilToInt(p.Y + radius + 1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Size - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Size - 1, x1); x++)
            {
                float dx = x - p.X, dy = y - p.Y;
                float coverage = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                if (coverage <= 0f) continue;
                int idx = y * Size + x;
                var old = _pixels[idx];
                _pixels[idx] = new Color32(
                    (byte)(old.r + (color.r - old.r) * coverage),
                    (byte)(old.g + (color.g - old.g) * coverage),
                    (byte)(old.b + (color.b - old.b) * coverage),
                    255);
            }
        }

        public void Dispose()
        {
            if (Texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Texture);
            else UnityEngine.Object.DestroyImmediate(Texture);
            Texture = null;
        }
    }
}
