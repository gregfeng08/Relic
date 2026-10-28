using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Very long straight white hair: a tilted cap, swept bangs, side strands, a back curtain to
    /// below the waist, the forward side lock over the left shoulder that curls at the end, a
    /// cowlick, and the small gold crown pinned on the upper left. Long strands ride spring chains.
    /// </summary>
    public static class HeroHair
    {
        const float Flat = 0.32f;

        public static void Build(HeroBuild b)
        {
            Cap(b);
            Bangs(b);
            BackCurtain(b);
            SideStrands(b);
            Cowlick(b);
            Crown(b);
        }

        static void Cap(HeroBuild b)
        {
            b.Toon(MaterialPalette.Hair, 1.5f);
            b.Rigid(HeroRig.Head);
            // An ellipsoid shell around the skull, tilted so its rim runs from the hairline
            // (front, high) down to the nape (back, low).
            var c = new Vector3(0f, HeroRig.HeadCenterY + 0.012f, -0.006f);
            b.Kit.Xf = Matrix4x4.TRS(c, Quaternion.Euler(-40f, 0f, 0f), Vector3.one);
            var rings = new List<Ring>();
            const int stacks = 10;
            for (int i = 4; i < stacks; i++)
            {
                float t = Mathf.PI * i / stacks;
                float y = -Mathf.Cos(t), k = Mathf.Sin(t);
                rings.Add(new Ring(new Vector3(0f, y * (HeroRig.HeadHalfH + 0.012f), 0f), (HeroRig.HeadHalfW + 0.017f) * k, (HeroRig.HeadHalfD + 0.016f) * k));
            }
            b.Kit.Loft(rings, 18, false, true);
            b.Kit.Xf = Matrix4x4.identity;
            // Nape and sides: an open sheet around the back half of the lower head.
            var nape = new List<Ring>();
            for (int i = 0; i < 4; i++)
            {
                float y = Mathf.Lerp(1.50f, 1.62f, i / 3f);
                float k = Mathf.Lerp(0.78f, 1f, Mathf.Sin(Mathf.PI * 0.5f * i / 3f));
                nape.Add(new Ring(new Vector3(0f, y, -0.012f), (HeroRig.HeadHalfW + 0.016f) * k, (HeroRig.HeadHalfD + 0.012f) * k));
            }
            b.Kit.Loft(nape, 14, false, false, Mathf.PI * 0.42f, Mathf.PI * 1.58f, false);
        }

        /// <summary>A tapered ribbon along a smoothed path; optional chain (simulated) or rigid to the head.</summary>
        static void Strand(HeroBuild b, List<Vector3> joints, float width, float tipWidth, Vector3 outward, bool simulate, float stiffness = 0.35f, int perSeg = 3, float flat = Flat)
        {
            ChainDef chain = null;
            if (simulate)
            {
                chain = b.Chain("Hair", HeroRig.Head, joints, "hair");
                chain.Stiffness = stiffness;
                chain.Drag = 0.35f;
                chain.Radius = width * 0.6f;
                b.Kit.Weigh = p => b.ChainWeight(chain, HeroRig.Head, p);
            }
            else b.Rigid(HeroRig.Head);
            List<Vector3> pts = MeshKit.Spline(joints, perSeg);
            var rad = new List<float>();
            for (int i = 0; i < pts.Count; i++)
            {
                float t = (float)i / (pts.Count - 1);
                // Full width near the root, a long taper to a point.
                float w = Mathf.Lerp(width, tipWidth, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 0.85f)));
                if (i == 0) w *= 0.8f;
                rad.Add(w);
            }
            b.Kit.Path(pts, rad, 6, flat, outward);
        }

        static void Bangs(HeroBuild b)
        {
            b.Toon(MaterialPalette.Hair, 1.5f);
            // Swept bangs: roots along the front hairline, tips over the brow, parting on the right
            // so they sweep toward the character's left (the side lock side).
            float[] rootX = { 0.075f, 0.05f, 0.022f, -0.006f, -0.034f, -0.06f, -0.082f };
            float[] tipX = { 0.092f, 0.056f, 0.020f, -0.020f, -0.052f, -0.080f, -0.100f };
            float[] tipY = { 1.585f, 1.605f, 1.602f, 1.590f, 1.598f, 1.578f, 1.555f };
            for (int i = 0; i < rootX.Length; i++)
            {
                float rx = rootX[i], tx = tipX[i], ty = tipY[i];
                var j = new List<Vector3>
                {
                    new Vector3(rx * 0.8f, 1.728f, 0.035f),
                    new Vector3(Mathf.Lerp(rx, tx, 0.4f), 1.69f, HeroBody.FrontZ(1.69f, Mathf.Lerp(rx, tx, 0.4f)) + 0.016f),
                    new Vector3(Mathf.Lerp(rx, tx, 0.8f), Mathf.Lerp(1.69f, ty, 0.7f), HeroBody.FrontZ(Mathf.Lerp(1.69f, ty, 0.7f), tx) + 0.012f),
                    new Vector3(tx, ty, HeroBody.FrontZ(ty, tx) + 0.009f),
                };
                Strand(b, j, 0.019f, 0.002f, Vector3.forward, false, 0f, 3, 0.36f);
            }
        }

        static void BackCurtain(HeroBuild b)
        {
            b.Toon(MaterialPalette.Hair, 1.5f);
            const int n = 11;
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n;                     // 0..1 across the back
                float a = Mathf.Lerp(0.56f, 1.44f, u) * Mathf.PI; // angle from the front
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float side = Mathf.Abs(Mathf.Cos(a));          // 1 at the very back... 0 at the sides
                float spread = Mathf.Sin(a);                   // -1..1, left/right
                float tipY = Mathf.Lerp(0.80f, 0.92f, Mathf.Abs(spread)) + (i % 2) * 0.03f;
                // Over the shoulders/backpack, then straight down, widening into a curtain.
                var j = new List<Vector3>
                {
                    new Vector3(dir.x * 0.085f, 1.69f, dir.z * 0.10f - 0.01f),
                    new Vector3(dir.x * 0.125f, 1.53f, dir.z * 0.13f - 0.02f),
                    new Vector3(spread * 0.17f, 1.36f, -0.10f - 0.12f * side),
                    new Vector3(spread * 0.19f, 1.16f, -0.13f - 0.15f * side),
                    new Vector3(spread * 0.20f, 0.98f, -0.13f - 0.15f * side),
                    new Vector3(spread * 0.21f, tipY, -0.12f - 0.14f * side),
                };
                Strand(b, j, 0.034f, 0.006f, dir, true, 0.3f, 3);
            }
        }

        static void SideStrands(HeroBuild b)
        {
            b.Toon(MaterialPalette.Hair, 1.5f);
            // Right temple: straight to the collarbone.
            var r = new List<Vector3>
            {
                new Vector3(0.088f, 1.67f, 0.02f), new Vector3(0.112f, 1.58f, 0.03f),
                new Vector3(0.118f, 1.48f, 0.03f), new Vector3(0.12f, 1.38f, 0.035f),
            };
            Strand(b, r, 0.022f, 0.004f, Vector3.right, true, 0.45f);
            // Left: the forward side lock, over the shoulder onto the chest, curling at the end.
            var l = new List<Vector3>
            {
                new Vector3(-0.088f, 1.68f, 0.025f), new Vector3(-0.118f, 1.57f, 0.045f),
                new Vector3(-0.13f, 1.44f, 0.08f), new Vector3(-0.125f, 1.32f, 0.11f),
                new Vector3(-0.11f, 1.22f, 0.125f), new Vector3(-0.085f, 1.15f, 0.13f),
            };
            // The curl: a loop in the frontal plane at the end of the lock.
            Vector3 end = l[l.Count - 1];
            var lock_ = new List<Vector3>(l);
            for (int k = 1; k <= 5; k++)
            {
                float a = k / 5f * Mathf.PI * 1.5f;
                lock_.Add(end + new Vector3(Mathf.Sin(a) * -0.035f + 0.0f, -Mathf.Cos(a) * 0.03f + 0.03f - 0.02f * (k / 5f), 0.004f));
            }
            var joints = new List<Vector3> { lock_[0], lock_[1], lock_[2], lock_[3], lock_[4], lock_[6], lock_[8], lock_[10] };
            ChainDef chain = b.Chain("SideLock", HeroRig.Head, joints, "hair");
            chain.Stiffness = 0.5f;
            chain.Radius = 0.02f;
            b.Kit.Weigh = p => b.ChainWeight(chain, HeroRig.Head, p);
            List<Vector3> pts = MeshKit.Spline(lock_, 3);
            var rad = new List<float>();
            for (int i = 0; i < pts.Count; i++)
            {
                float t = (float)i / (pts.Count - 1);
                rad.Add(Mathf.Lerp(0.03f, 0.004f, t * t));
            }
            b.Kit.Path(pts, rad, 6, 0.4f, Vector3.forward);
            // A couple of loose strands framing the face.
            var loose = new List<Vector3> { new Vector3(-0.07f, 1.70f, 0.07f), new Vector3(-0.098f, 1.61f, 0.075f), new Vector3(-0.104f, 1.52f, 0.06f) };
            Strand(b, loose, 0.012f, 0.002f, Vector3.forward, false);
            var loose2 = new List<Vector3> { new Vector3(0.07f, 1.70f, 0.07f), new Vector3(0.096f, 1.62f, 0.07f), new Vector3(0.1f, 1.54f, 0.055f) };
            Strand(b, loose2, 0.012f, 0.002f, Vector3.forward, false);
        }

        static void Cowlick(HeroBuild b)
        {
            b.Toon(MaterialPalette.Hair, 1.3f);
            var j = new List<Vector3>
            {
                new Vector3(0.025f, 1.735f, 0.01f), new Vector3(0.04f, 1.775f, 0.02f),
                new Vector3(0.062f, 1.79f, 0.04f), new Vector3(0.078f, 1.772f, 0.05f),
            };
            Strand(b, j, 0.008f, 0.002f, Vector3.right, false, 0f, 3, 0.6f);
        }

        static void Crown(HeroBuild b)
        {
            b.Toon(MaterialPalette.Gold, 1.4f);
            b.Rigid(HeroRig.Head);
            // Pinned on the upper left, tilted outward.
            b.Kit.Xf = Matrix4x4.TRS(new Vector3(-0.072f, 1.735f, 0.0f), Quaternion.Euler(0f, 20f, 32f), Vector3.one);
            const float r = 0.028f;
            var band = new List<Ring>
            {
                new Ring(new Vector3(0f, -0.006f, 0f), r, r * 0.85f),
                new Ring(new Vector3(0f, 0.008f, 0f), r * 1.02f, r * 0.87f),
            };
            b.Kit.Loft(band, 14, true, true);
            // Five points; the middle front one tallest.
            for (int i = 0; i < 5; i++)
            {
                float a = (i - 2) * 0.55f;
                float h = i == 2 ? 0.034f : i == 1 || i == 3 ? 0.026f : 0.018f;
                var basePos = new Vector3(Mathf.Sin(a) * r, 0.006f, Mathf.Cos(a) * r * 0.85f);
                var tip = basePos + new Vector3(Mathf.Sin(a) * 0.004f, h, Mathf.Cos(a) * 0.003f);
                b.Kit.Path(new List<Vector3> { basePos, Vector3.Lerp(basePos, tip, 0.5f), tip }, new List<float> { 0.008f, 0.005f, 0.0008f }, 4, 0.45f, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)), false);
            }
            b.Kit.Xf = Matrix4x4.identity;
        }
    }
}
