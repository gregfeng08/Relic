using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The body under the gear: head and painted face, neck, shirt, trousers, laced boots and
    /// the claw-tipped gauntlets (Greg's reference sheet). Slender anime proportions.
    /// </summary>
    public static class HeroBody
    {
        public static void Build(HeroBuild b)
        {
            Head(b);
            Face(b);
            Torso(b);
            Legs(b);
            for (int s = -1; s <= 1; s += 2) { Boot(b, s); Gauntlet(b, s); }
        }

        /// <summary>Head shape: rounded skull, narrowing to a soft V chin that sits forward.</summary>
        public static void HeadRings(List<Ring> rings, float grow)
        {
            const int stacks = 12;
            float chin = HeroRig.HeadCenterY - HeroRig.HeadHalfH;
            for (int i = 1; i < stacks; i++)
            {
                float t = (float)i / stacks;
                float k = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.75f);
                float jaw = t < 0.5f ? Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.5f)) : 1f;
                float y = chin + t * HeroRig.HeadHalfH * 2f;
                float z = Mathf.Lerp(0.012f, -0.008f, t);
                rings.Add(new Ring(new Vector3(0f, y, z), (HeroRig.HeadHalfW + grow) * k * jaw, (HeroRig.HeadHalfD + grow) * k * Mathf.Lerp(0.85f, 1f, jaw)));
            }
        }

        static void Head(HeroBuild b)
        {
            b.Toon(MaterialPalette.Skin, 1.4f);
            b.Rigid(HeroRig.Head);
            var rings = new List<Ring>();
            HeadRings(rings, 0f);
            b.Kit.Loft(rings, 16, true, true);
            // Neck.
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            b.Kit.Tube(new Vector3(0f, 1.40f, -0.005f), new Vector3(0f, 1.53f, 0f), 0.040f, 0.036f, 10);
        }

        /// <summary>Front surface z of the head at height y and side offset x (for placing face paint).</summary>
        public static float FrontZ(float y, float x)
        {
            float chin = HeroRig.HeadCenterY - HeroRig.HeadHalfH;
            float t = Mathf.Clamp01((y - chin) / (HeroRig.HeadHalfH * 2f));
            float k = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.75f);
            float jaw = t < 0.5f ? Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.5f)) : 1f;
            float rx = HeroRig.HeadHalfW * k * jaw, rz = HeroRig.HeadHalfD * k * Mathf.Lerp(0.85f, 1f, jaw);
            float u = Mathf.Clamp01(Mathf.Abs(x) / Mathf.Max(1e-4f, rx));
            return Mathf.Lerp(0.012f, -0.008f, t) + rz * Mathf.Sqrt(1f - u * u);
        }

        static void Face(HeroBuild b)
        {
            b.Rigid(HeroRig.Head);
            var lash = new Color(0.16f, 0.10f, 0.10f);
            Color iris = HeroMaterials.ToColor(MaterialPalette.Eye);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = 0.038f * s, y = 1.592f;
                float z = FrontZ(y, x) - 0.004f;
                Matrix4x4 m = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, 16f * s, 0f), Vector3.one);
                b.Kit.Xf = m;
                b.Flat(new Color(1f, 0.98f, 0.95f));
                b.Kit.Ellipsoid(new Vector3(0f, 0f, 0f), new Vector3(0.017f, 0.020f, 0.0045f), 12, 4);
                b.Flat(iris);
                b.Kit.Ellipsoid(new Vector3(0.001f * -s, -0.002f, 0.0022f), new Vector3(0.0125f, 0.0165f, 0.0042f), 12, 4);
                b.Flat(new Color(0.08f, 0.20f, 0.12f));
                b.Kit.Ellipsoid(new Vector3(0.001f * -s, -0.001f, 0.0042f), new Vector3(0.0062f, 0.0095f, 0.0035f), 8, 4);
                b.Flat(Color.white, 1.1f);
                b.Kit.Ellipsoid(new Vector3(0.004f * s, 0.007f, 0.0062f), new Vector3(0.0035f, 0.0042f, 0.002f), 6, 4);
                // Upper lash line: a thick dark arc, flicked out at the corner.
                b.Flat(lash);
                var pts = new List<Vector3>();
                var rad = new List<float>();
                for (int i = 0; i <= 6; i++)
                {
                    float a = Mathf.Lerp(-1.1f, 1.25f, i / 6f);
                    pts.Add(new Vector3(Mathf.Sin(a) * 0.019f * s, Mathf.Cos(a) * 0.019f + 0.002f + (i == 6 ? 0.003f : 0f), 0.004f));
                    rad.Add(Mathf.Lerp(0.0018f, 0.0032f, Mathf.Sin(Mathf.PI * i / 6f)));
                }
                b.Kit.Path(pts, rad, 4, 0.6f, Vector3.forward);
                // Brow (pale, mostly under the bangs).
                b.Flat(new Color(0.78f, 0.72f, 0.66f));
                b.Kit.Tube(new Vector3(-0.010f * s, 0.034f, 0.001f), new Vector3(0.012f * s, 0.036f, -0.002f), 0.0011f, 0.0007f, 4);
            }
            b.Kit.Xf = Matrix4x4.identity;
            // Small closed smile and a hint of a nose.
            float my = 1.522f;
            b.Flat(new Color(0.55f, 0.28f, 0.26f));
            b.Kit.Tube(new Vector3(-0.011f, my + 0.002f, FrontZ(my, 0.011f) - 0.001f), new Vector3(0.011f, my + 0.002f, FrontZ(my, 0.011f) - 0.001f), 0.0016f, 0.0016f, 4);
            b.Flat(new Color(0.86f, 0.62f, 0.52f));
            float ny = 1.556f;
            b.Kit.Ellipsoid(new Vector3(0.002f, ny, FrontZ(ny, 0f) + 0.001f), new Vector3(0.0035f, 0.003f, 0.0025f), 6, 4);
        }

        static void Torso(HeroBuild b)
        {
            b.Toon(MaterialPalette.Shirt);
            b.Torso();
            var r = new List<Ring>
            {
                new Ring(new Vector3(0f, 0.86f, 0f), 0.118f, 0.088f),
                new Ring(new Vector3(0f, 0.96f, 0f), 0.122f, 0.088f),
                new Ring(new Vector3(0f, 1.03f, 0.003f), 0.112f, 0.083f),
                new Ring(new Vector3(0f, 1.13f, 0.006f), 0.122f, 0.090f),
                new Ring(new Vector3(0f, 1.24f, 0.008f), 0.138f, 0.096f),
                new Ring(new Vector3(0f, 1.33f, 0.004f), 0.146f, 0.094f),
                new Ring(new Vector3(0f, 1.385f, 0f), 0.135f, 0.085f),
                new Ring(new Vector3(0f, 1.43f, 0f), 0.065f, 0.052f),
            };
            b.Kit.Loft(r, 16, true, true);
            // Yellow belt with a gold buckle.
            b.Toon(MaterialPalette.Belt);
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Hips);
            b.Kit.Torus(new Vector3(0f, 0.985f, 0.002f), Vector3.up, 0.128f, 0.094f, 0.008f, 16, 5, 2.6f);
            b.Toon(MaterialPalette.Gold);
            b.Kit.Box(new Vector3(0f, 0.985f, 0.099f), new Vector3(0.038f, 0.03f, 0.012f));
        }

        static void Legs(HeroBuild b)
        {
            b.Toon(MaterialPalette.Trousers);
            // Seat: one piece over both hip joints, on the hips.
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Hips);
            b.Kit.Ellipsoid(new Vector3(0f, 0.90f, -0.005f), new Vector3(0.135f, 0.085f, 0.098f), 14, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Leg(s);
                float x = HeroRig.LegX * s;
                // Baggy legs that flare and bunch over the boots.
                var r = new List<Ring>
                {
                    new Ring(new Vector3(x * 0.92f, 0.93f, -0.004f), 0.080f, 0.088f),
                    new Ring(new Vector3(x, 0.80f, 0f), 0.077f, 0.082f),
                    new Ring(new Vector3(x, 0.64f, 0.004f), 0.069f, 0.072f),
                    new Ring(new Vector3(x, 0.50f, 0.008f), 0.064f, 0.066f),
                    new Ring(new Vector3(x, 0.36f, 0.002f), 0.069f, 0.070f),
                    new Ring(new Vector3(x, 0.24f, 0f), 0.077f, 0.079f),
                    new Ring(new Vector3(x, 0.16f, 0.004f), 0.086f, 0.090f),
                    new Ring(new Vector3(x, 0.125f, 0.008f), 0.080f, 0.086f),
                };
                b.Kit.Loft(r, 12, false, true);
                // Folds where the cloth bunches.
                b.Kit.Torus(new Vector3(x, 0.205f, 0.002f), Vector3.up, 0.079f, 0.081f, 0.008f, 14, 4);
            }
        }

        static void Boot(HeroBuild b, int s)
        {
            float x = HeroRig.LegX * s;
            b.Leg(s);
            b.Toon(MaterialPalette.Boots);
            // Shaft (top hidden under the trousers).
            var shaft = new List<Ring>
            {
                new Ring(new Vector3(x, 0.06f, -0.01f), 0.058f, 0.064f),
                new Ring(new Vector3(x, 0.13f, -0.008f), 0.060f, 0.062f),
                new Ring(new Vector3(x, 0.20f, -0.006f), 0.056f, 0.058f),
            };
            b.Kit.Loft(shaft, 10, false, false);
            // Foot: lofted heel to toe, chunky and rounded.
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Foot(s));
            var foot = new List<Ring>();
            float[] zs = { -0.068f, -0.03f, 0.04f, 0.11f, 0.16f, 0.182f };
            float[] ws = { 0.046f, 0.058f, 0.060f, 0.058f, 0.047f, 0.030f };
            float[] hs = { 0.050f, 0.062f, 0.058f, 0.042f, 0.034f, 0.024f };
            float[] cy = { 0.075f, 0.080f, 0.072f, 0.060f, 0.055f, 0.050f };
            for (int i = 0; i < zs.Length; i++)
                foot.Add(new Ring(new Vector3(x, cy[i], zs[i]), Vector3.right, Vector3.up, ws[i], hs[i], 3f));
            b.Kit.Loft(foot, 12, true, true);
            // Dark sole.
            b.Toon(MaterialPalette.Sole);
            var sole = new List<Ring>();
            for (int i = 0; i < zs.Length; i++)
                sole.Add(new Ring(new Vector3(x, 0.014f, zs[i] + (i == 0 ? -0.004f : i == zs.Length - 1 ? 0.006f : 0f)), Vector3.right, Vector3.up, ws[i] + 0.005f, 0.0145f, 6f));
            b.Kit.Loft(sole, 12, true, true);
            // Laces: three crosses up the front of the shaft.
            b.Leg(s);
            for (int i = 0; i < 2; i++)
            {
                float y = 0.095f + i * 0.05f;
                float zf = i == 0 ? 0.064f : 0.058f;
                b.Kit.Tube(new Vector3(x - 0.02f, y - 0.012f, zf), new Vector3(x + 0.02f, y + 0.012f, zf), 0.0035f, 0.0035f, 4);
                b.Kit.Tube(new Vector3(x + 0.02f, y - 0.012f, zf), new Vector3(x - 0.02f, y + 0.012f, zf), 0.0035f, 0.0035f, 4);
            }
        }

        static void Gauntlet(HeroBuild b, int s)
        {
            float x = HeroRig.ShoulderX * s;
            int hand = HeroRig.Hand(s);
            Rgb metal = MaterialPalette.Gauntlet;
            b.Toon(metal, 1.4f);
            // Wrist guard (over the cuff line), on the forearm/hand blend.
            b.Arm(s);
            b.Kit.Tube(new Vector3(x, 0.93f, -0.01f), new Vector3(x, 0.865f, -0.01f), 0.042f, 0.047f, 10);
            b.Rigid(hand);
            // Palm plate: thin in X (palm faces the body), wide in Z.
            var palm = new List<Ring>
            {
                new Ring(new Vector3(x, 0.868f, -0.01f), 0.020f, 0.036f, 4f),
                new Ring(new Vector3(x + 0.002f * s, 0.835f, -0.008f), 0.022f, 0.042f, 4f),
                new Ring(new Vector3(x + 0.004f * s, 0.795f, -0.006f), 0.019f, 0.040f, 4f),
            };
            b.Kit.Loft(palm, 10, true, true);
            // Four segmented fingers curling toward the palm, each ending in a claw.
            Color clawCol = HeroMaterials.ToColor(metal.Scale(0.62f));
            for (int f = 0; f < 4; f++)
            {
                float fz = -0.034f + f * 0.0215f;
                float len = f == 0 || f == 3 ? 0.85f : 1f;
                var pts = new List<Vector3>();
                var rad = new List<float>();
                Vector3 p = new Vector3(x + 0.004f * s, 0.792f, fz);
                Vector3 dir = Vector3.down;
                float[] seg = { 0.030f * len, 0.024f * len, 0.020f * len };
                pts.Add(p); rad.Add(0.0085f);
                for (int k = 0; k < seg.Length; k++)
                {
                    dir = Quaternion.AngleAxis(-14f * s, Vector3.forward) * dir;
                    p += dir * seg[k];
                    pts.Add(p); rad.Add(0.0078f - k * 0.0008f);
                }
                b.Toon(metal, 1.2f);
                b.Kit.Path(pts, rad, 5, 0.85f, Vector3.right);
                // Claw: a curved cone.
                b.Toon(clawCol, 1.2f);
                Vector3 c0 = p, c1 = p + Quaternion.AngleAxis(-22f * s, Vector3.forward) * dir * 0.022f;
                b.Kit.Path(new List<Vector3> { c0, Vector3.Lerp(c0, c1, 0.5f), c1 }, new List<float> { 0.0062f, 0.0038f, 0.0006f }, 5, 0.8f, Vector3.right, false);
            }
            // Thumb from the front edge, pointing forward-down.
            b.Toon(metal, 1.2f);
            Vector3 t0 = new Vector3(x - 0.006f * s, 0.848f, 0.026f);
            Vector3 t1 = t0 + new Vector3(-0.012f * s, -0.024f, 0.016f);
            Vector3 t2 = t1 + new Vector3(-0.010f * s, -0.020f, 0.006f);
            b.Kit.Path(new List<Vector3> { t0, t1, t2 }, new List<float> { 0.010f, 0.0085f, 0.0075f }, 6);
            b.Toon(clawCol, 1.2f);
            b.Kit.Path(new List<Vector3> { t2, t2 + new Vector3(-0.004f * s, -0.012f, 0.002f), t2 + new Vector3(-0.009f * s, -0.020f, 0f) },
                new List<float> { 0.006f, 0.0035f, 0.0006f }, 5, 1f, null, false);
        }
    }
}
