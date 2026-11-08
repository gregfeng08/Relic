using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Anime hair as chunky, layered clumps with pointed tips: a seamless cap whose hairline dips
    /// under the skin (no rim), bangs (swept, straight or parted), the optional forward side lock
    /// with a curl, a cowlick, and a soft highlight band ("angel ring") on the crown. The back is
    /// built per style by HeroHairStyles. Hair shades with its own (warm) tint.
    /// </summary>
    public static class HeroHair
    {
        public static void Build(HeroBuild b)
        {
            HairSpec h = b.App.Hair;
            Cap(b);
            AngelRing(b);
            Bangs(b, h.Bangs);
            HeroHairStyles.Back(b, h);
            if (h.SideLock) SideLock(b);
            if (h.Cowlick) Cowlick(b);
        }

        // Cap ellipsoid (authoring space, before HeroShape).
        static readonly Vector3 CapC = new Vector3(0f, HeroRig.HeadCenterY + 0.012f, -0.008f);
        static readonly Vector3 CapR = new Vector3(HeroRig.HeadHalfW + 0.018f, HeroRig.HeadHalfH + 0.02f, HeroRig.HeadHalfD + 0.018f);

        /// <summary>A point on the cap surface at latitude/longitude (longitude 0 = front).</summary>
        public static Vector3 OnCap(float lat, float lon, float lift = 0f)
        {
            var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
            return CapC + Vector3.Scale(d, CapR + Vector3.one * lift);
        }

        static void Cap(HeroBuild b)
        {
            b.Hair();
            b.Rigid(HeroRig.Head);
            const int stacks = 14, sides = 20;
            int first = b.Kit.VertexCount;
            for (int i = 1; i < stacks; i++)
            {
                float lat = -Mathf.PI * 0.5f + Mathf.PI * i / stacks;
                for (int j = 0; j < sides; j++)
                {
                    float lon = Mathf.PI * 2f * j / sides;
                    var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
                    // In front and below the hairline the cap sinks under the skin: a soft hairline, no rim.
                    float face = Smooth(0.05f, 0.45f, d.z) * (1f - Smooth(0.18f, 0.5f, d.y));
                    float chin = Smooth(-0.3f, -0.65f, d.y) * Smooth(-0.5f, 0.1f, d.z);
                    float s = Mathf.Lerp(1f, 0.82f, Mathf.Max(face, chin));
                    b.Kit.Vert(CapC + Vector3.Scale(d, CapR) * s);
                }
            }
            int top = b.Kit.Vert(CapC + Vector3.up * CapR.y), bottom = b.Kit.Vert(CapC + Vector3.down * CapR.y * 0.82f);
            for (int i = 0; i < stacks - 2; i++)
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    int a = first + i * sides + j, bb = first + i * sides + j1, cc = first + (i + 1) * sides + j1, dd = first + (i + 1) * sides + j;
                    float lat = -Mathf.PI * 0.5f + Mathf.PI * (i + 1.5f) / stacks, lon = Mathf.PI * 2f * (j + 0.5f) / sides;
                    b.Kit.Quad(a, bb, cc, dd, new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon)));
                }
            int last = first + (stacks - 2) * sides;
            for (int j = 0; j < sides; j++)
            {
                int j1 = (j + 1) % sides;
                b.Kit.Tri(last + j, last + j1, top, Vector3.up);
                b.Kit.Tri(first + j, first + j1, bottom, Vector3.down);
            }
        }

        /// <summary>The glossy highlight band across the crown (unlit, no outline).</summary>
        static void AngelRing(HeroBuild b)
        {
            b.Rigid(HeroRig.Head);
            Color c = Color.Lerp(b.HairColor, Color.white, 0.6f);
            b.Flat(c, 1.06f);
            const int n = 14;
            for (int k = 0; k < n; k++)
            {
                // Short dashes around the back and sides, following the crown's curve, sitting on the locks.
                float lon0 = Mathf.Lerp(0.42f, 1.58f, (float)k / n) * Mathf.PI, lon1 = lon0 + 0.06f * Mathf.PI;
                float lat = 0.42f + 0.06f * Mathf.Sin(k * 1.7f);
                var pts = new List<Vector3> { OnCap(lat, lon0, 0.016f), OnCap(lat + 0.02f, Mathf.Lerp(lon0, lon1, 0.5f), 0.018f), OnCap(lat, lon1, 0.016f) };
                b.Kit.Path(pts, new List<float> { 0.002f, 0.0045f, 0.002f }, 4, 0.3f, OnCap(lat, lon0) - CapC, false);
            }
        }

        internal static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>A chunky lock: swells after the root, tapers to a point (anime clump).</summary>
        public static void Lock(HeroBuild b, IList<Vector3> joints, float width, float tip, Vector3 outward, float flat, int sides = 6, int perSeg = 3, float rootScale = 0.75f)
        {
            List<Vector3> pts = MeshKit.Spline(joints, perSeg);
            var rad = new List<float>();
            for (int i = 0; i < pts.Count; i++)
            {
                float t = (float)i / (pts.Count - 1);
                float w = Mathf.Lerp(width, tip, Mathf.Pow(Mathf.Clamp01((t - 0.25f) / 0.75f), 1.3f));
                if (t < 0.2f) w *= Mathf.Lerp(rootScale, 1f, t / 0.2f);
                rad.Add(w);
            }
            b.Kit.Path(pts, rad, sides, flat, outward);
        }

        public static ChainDef Chain(HeroBuild b, string name, int parent, IList<Vector3> joints, float stiffness, float radius)
        {
            ChainDef c = b.Chain(name, parent, joints, "hair");
            c.Stiffness = stiffness;
            c.Drag = 0.4f;
            c.Radius = radius;
            c.CollideFrom = 1;
            b.Kit.Weigh = p => b.ChainWeight(c, parent, p);
            return c;
        }

        static void Bangs(HeroBuild b, BangsStyle style)
        {
            b.Hair(1.3f);
            b.Rigid(HeroRig.Head);
            float[] rootX, tipX, tipY;
            switch (style)
            {
                case BangsStyle.Straight:
                    rootX = new[] { 0.066f, 0.034f, 0.002f, -0.03f, -0.062f };
                    tipX = new[] { 0.08f, 0.04f, 0.002f, -0.036f, -0.076f };
                    tipY = new[] { 1.592f, 1.6f, 1.602f, 1.6f, 1.592f };
                    break;
                case BangsStyle.Parted:
                    rootX = new[] { 0.05f, 0.022f, -0.022f, -0.05f };
                    tipX = new[] { 0.095f, 0.06f, -0.06f, -0.095f };
                    tipY = new[] { 1.57f, 1.605f, 1.605f, 1.57f };
                    break;
                default:
                    rootX = new[] { 0.06f, 0.03f, 0.0f, -0.03f, -0.06f };
                    tipX = new[] { 0.082f, 0.04f, -0.004f, -0.05f, -0.09f };
                    tipY = new[] { 1.585f, 1.604f, 1.598f, 1.60f, 1.575f };
                    break;
            }
            for (int i = 0; i < rootX.Length; i++)
            {
                float rx = rootX[i], tx = tipX[i], ty = tipY[i];
                float mx = Mathf.Lerp(rx, tx, 0.5f), my = Mathf.Lerp(1.70f, ty, 0.55f);
                var j = new List<Vector3>
                {
                    new Vector3(rx * 0.6f, 1.745f, 0.0f),
                    new Vector3(Mathf.Lerp(rx, tx, 0.2f), 1.705f, HeroFace.FrontZ(1.705f, rx) + 0.02f),
                    new Vector3(mx, my, HeroFace.FrontZ(my, mx) + 0.016f),
                    new Vector3(tx, ty, HeroFace.FrontZ(ty, tx) + 0.009f),
                };
                Lock(b, j, 0.034f, 0.004f, Vector3.forward, 0.38f);
            }
            // Cheek locks framing the face.
            for (int s = -1; s <= 1; s += 2)
            {
                var cheek = new List<Vector3>
                {
                    new Vector3(0.075f * s, 1.69f, 0.045f), new Vector3(0.106f * s, 1.61f, 0.055f),
                    new Vector3(0.108f * s, 1.53f, 0.045f), new Vector3(0.094f * s, 1.47f, 0.04f),
                };
                Lock(b, cheek, 0.024f, 0.004f, new Vector3(s, 0f, 0.4f), 0.45f);
            }
        }

        static void SideLock(HeroBuild b)
        {
            b.Hair();
            // The forward lock over the left shoulder, ending in a soft curl.
            var l = new List<Vector3>
            {
                new Vector3(-0.09f, 1.67f, 0.02f), new Vector3(-0.124f, 1.56f, 0.04f),
                new Vector3(-0.135f, 1.44f, 0.075f), new Vector3(-0.128f, 1.32f, 0.105f),
                new Vector3(-0.112f, 1.22f, 0.118f), new Vector3(-0.09f, 1.16f, 0.122f),
            };
            Vector3 end = l[l.Count - 1];
            var path = new List<Vector3>(l);
            for (int k = 1; k <= 5; k++)
            {
                float a = k / 5f * Mathf.PI * 1.4f;
                path.Add(end + new Vector3(-Mathf.Sin(a) * 0.034f, Mathf.Cos(a) * 0.03f - 0.03f + 0.015f * (k / 5f), 0.004f));
            }
            var joints = new List<Vector3> { path[0], path[1], path[2], path[3], path[4], path[6], path[8], path[10] };
            ChainDef chain = Chain(b, "SideLock", HeroRig.Head, joints, 0.55f, 0.022f);
            List<Vector3> pts = MeshKit.Spline(path, 3);
            var rad = new List<float>();
            for (int i = 0; i < pts.Count; i++)
            {
                float t = (float)i / (pts.Count - 1);
                rad.Add(Mathf.Lerp(0.034f, 0.006f, t * t) * (t < 0.06f ? 0.8f : 1f));
            }
            b.Kit.Path(pts, rad, 6, 0.5f, Vector3.forward);
        }

        static void Cowlick(HeroBuild b)
        {
            b.Hair(1.2f);
            b.Rigid(HeroRig.Head);
            var j = new List<Vector3>
            {
                new Vector3(0.01f, 1.745f, 0.0f), new Vector3(0.022f, 1.79f, 0.012f),
                new Vector3(0.045f, 1.805f, 0.03f), new Vector3(0.062f, 1.785f, 0.042f),
            };
            Lock(b, j, 0.01f, 0.002f, Vector3.right, 0.6f, 5);
        }
    }
}
