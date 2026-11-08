using System;
using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>Shape of a flared cloth skirt in two panels (coat skirt, or a skirt as legwear).</summary>
    public sealed class SkirtDef
    {
        public string Name = "Skirt";
        public float[] Y, Rx, Rz;               // rings, waist to hem
        public float GapTop, GapHem;            // front opening half-angle (radians) at waist and hem
        public float VentFromY = -10f;          // back vent opens below this height...
        public float VentHem;                   // ...to this half-angle at the hem
        public int Cols = 10, ChainsPerPanel = 4, Sub = 2;
        public float Stiffness = 0.75f, Gravity = 0.45f, Drag = 0.5f;
        public float Thick = 0.009f;
    }

    /// <summary>
    /// Builds a two-panel skirt with thickness on spring chains (a joint per ring, ChainsPerPanel
    /// columns per panel), weighted bilinearly between chains; the waistband stays on the hips.
    /// Returns, per side, the weight function so patches (pockets, trims) can ride the same cloth.
    /// </summary>
    public static class HeroSkirt
    {
        public static Func<float, float, BoneWeight>[] Build(HeroBuild b, SkirtDef d, Color color, float outline = 1.6f)
        {
            int rows = d.Y.Length - 1;
            var weights = new Func<float, float, BoneWeight>[2];
            for (int s = -1; s <= 1; s += 2)
            {
                int side = s;
                var chains = new ChainDef[d.ChainsPerPanel];
                for (int c = 0; c < d.ChainsPerPanel; c++)
                {
                    float u = (float)c / (d.ChainsPerPanel - 1);
                    var joints = new List<Vector3>();
                    for (int i = 0; i <= rows; i++) joints.Add(Point(d, side, i, u) - Outward(d, side, i, u) * 0.03f);
                    ChainDef ch = b.Chain(d.Name + (s < 0 ? "L" : "R") + c, HeroRig.Hips, joints, "coat");
                    ch.Stiffness = d.Stiffness;
                    ch.Drag = d.Drag;
                    ch.Gravity = d.Gravity;
                    ch.Radius = 0.015f;
                    ch.CollideMask = SpringChains.Legs;
                    ch.CollideFrom = 1;
                    chains[c] = ch;
                }
                BoneWeight W(float row, float u)
                {
                    float cu = Mathf.Clamp01(u) * (d.ChainsPerPanel - 1);
                    int c0 = Mathf.Clamp(Mathf.FloorToInt(cu), 0, d.ChainsPerPanel - 2);
                    float fu = cu - c0;
                    int i0 = Mathf.Clamp(Mathf.FloorToInt(row), 0, rows - 1);
                    float fr = Mathf.Clamp01(row - i0);
                    BoneWeight top = MeshKit.W2(chains[c0].Bones[i0], chains[c0 + 1].Bones[i0], fu);
                    BoneWeight bottom = MeshKit.W2(chains[c0].Bones[i0 + 1], chains[c0 + 1].Bones[i0 + 1], fu);
                    BoneWeight w = MeshKit.Blend(top, bottom, fr);
                    if (row < 0.6f) w = MeshKit.Blend(MeshKit.W1(HeroRig.Hips), w, row / 0.6f);
                    return w;
                }
                weights[s < 0 ? 0 : 1] = W;
                b.Toon(color, outline);
                b.Kit.Sheet(rows * d.Sub, d.Cols,
                    (i, j) => Point(d, side, (float)i / d.Sub, (float)j / d.Cols),
                    (i, j) => Outward(d, side, (float)i / d.Sub, (float)j / d.Cols),
                    d.Thick,
                    (i, j) => W((float)i / d.Sub, (float)j / d.Cols));
            }
            return weights;
        }

        /// <summary>Root-space point at row (fractional ring index) and u (0 = front edge, 1 = back edge).</summary>
        public static Vector3 Point(SkirtDef d, int s, float row, float u)
        {
            int i0 = Mathf.Clamp(Mathf.FloorToInt(row), 0, d.Y.Length - 2);
            float f = Mathf.Clamp01(row - i0);
            float y = Mathf.Lerp(d.Y[i0], d.Y[i0 + 1], f);
            float rx = Mathf.Lerp(d.Rx[i0], d.Rx[i0 + 1], f);
            float rz = Mathf.Lerp(d.Rz[i0], d.Rz[i0 + 1], f);
            float y01 = row / (d.Y.Length - 1);
            float vent = y > d.VentFromY ? 0.004f : Mathf.Lerp(0.004f, d.VentHem, Mathf.Clamp01((d.VentFromY - y) / Mathf.Max(0.01f, d.VentFromY - d.Y[d.Y.Length - 1])));
            float a0 = Mathf.Lerp(d.GapTop, d.GapHem, y01 * y01), a1 = Mathf.PI - vent;
            float a = Mathf.Lerp(a0, a1, u);
            return new Vector3(Mathf.Sin(a) * rx * s, y, Mathf.Cos(a) * rz + 0.005f);
        }

        public static Vector3 Outward(SkirtDef d, int s, float row, float u)
        {
            Vector3 p = Point(d, s, row, u);
            var o = new Vector3(p.x, 0f, p.z - 0.005f);
            return (o.normalized + Vector3.down * 0.25f).normalized;
        }

        /// <summary>A thin patch riding the skirt (pockets, flaps, trims) over rows r0..r1 and u0..u1.</summary>
        public static void Patch(HeroBuild b, SkirtDef d, int s, Func<float, float, BoneWeight> w, float r0, float r1, float u0, float u1, float lift, Color color, float outline = 1.2f)
        {
            const int pr = 2, pc = 4;
            b.Toon(color, outline);
            b.Kit.Sheet(pr, pc,
                (i, j) =>
                {
                    float row = Mathf.Lerp(r0, r1, (float)i / pr), u = Mathf.Lerp(u0, u1, (float)j / pc);
                    return Point(d, s, row, u) + Outward(d, s, row, u) * lift;
                },
                (i, j) => Outward(d, s, Mathf.Lerp(r0, r1, (float)i / pr), Mathf.Lerp(u0, u1, (float)j / pc)),
                0.004f,
                (i, j) => w(Mathf.Lerp(r0, r1, (float)i / pr), Mathf.Lerp(u0, u1, (float)j / pc)));
        }
    }
}
