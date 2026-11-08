using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The body under the coat, from the appearance: shirt (with sleeves when there's no coat),
    /// belt, trousers bunched over the boots or a skirt over tights, laced boots, and hands as
    /// claw-tipped gauntlets, gloves or bare.
    /// </summary>
    public static class HeroBody
    {
        public static void Build(HeroBuild b)
        {
            HeroFace.Build(b);
            Torso(b);
            if (b.App.Outfit.Legwear == LegwearKind.Skirt) SkirtLegs(b); else Trousers(b);
            for (int s = -1; s <= 1; s += 2) { Boot(b, s); Hand(b, s); }
        }

        static void Torso(HeroBuild b)
        {
            OutfitSpec o = b.App.Outfit;
            Color shirt = HeroBuild.C(o.ShirtColor);
            b.Toon(shirt);
            b.Torso();
            var r = new List<Ring>
            {
                new Ring(new Vector3(0f, 0.86f, 0f), 0.116f, 0.088f),
                new Ring(new Vector3(0f, 0.96f, 0f), 0.118f, 0.087f),
                new Ring(new Vector3(0f, 1.03f, 0.003f), 0.108f, 0.082f),
                new Ring(new Vector3(0f, 1.13f, 0.006f), 0.118f, 0.089f),
                new Ring(new Vector3(0f, 1.23f, 0.009f), 0.132f, 0.096f),
                new Ring(new Vector3(0f, 1.32f, 0.005f), 0.140f, 0.093f),
                new Ring(new Vector3(0f, 1.375f, 0f), 0.130f, 0.084f),
                new Ring(new Vector3(0f, 1.415f, 0f), 0.085f, 0.064f),
                new Ring(new Vector3(0f, 1.44f, 0f), 0.05f, 0.045f),
            };
            b.Kit.Loft(r, 18, true, false);
            if (!o.Coat.On)
            {
                // No coat: the shirt gets soft long sleeves with a cuff.
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = HeroRig.ShoulderX * s;
                    b.Arm(s);
                    b.Toon(shirt);
                    var sl = new List<Ring>
                    {
                        new Ring(new Vector3(x - 0.008f * s, 1.40f, -0.01f), 0.05f, 0.054f),
                        new Ring(new Vector3(x, 1.30f, -0.01f), 0.048f, 0.05f),
                        new Ring(new Vector3(x, 1.12f, -0.008f), 0.042f, 0.044f),
                        new Ring(new Vector3(x, 0.96f, -0.01f), 0.046f, 0.048f),
                        new Ring(new Vector3(x, 0.90f, -0.01f), 0.048f, 0.05f),
                    };
                    b.Kit.Loft(sl, 12, true, false);
                    b.Toon(shirt * 0.85f + Color.black, 1f);
                    b.Kit.Torus(new Vector3(x, 0.915f, -0.01f), Vector3.up, 0.049f, 0.051f, 0.007f, 12, 5, 1.6f);
                }
            }
            // Belt with a gold buckle.
            b.Toon(HeroBuild.C(o.BeltColor), 1.2f);
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Hips);
            b.Kit.Torus(new Vector3(0f, 0.985f, 0.002f), Vector3.up, 0.124f, 0.092f, 0.008f, 18, 5, 2.6f);
            b.Toon(MaterialPalette.Gold, 1.2f);
            b.Kit.Box(new Vector3(0f, 0.985f, 0.097f), new Vector3(0.036f, 0.03f, 0.012f));
        }

        static void Trousers(HeroBuild b)
        {
            b.Toon(HeroBuild.C(b.App.Outfit.LegwearColor));
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Hips);
            b.Kit.Ellipsoid(new Vector3(0f, 0.90f, -0.005f), new Vector3(0.13f, 0.085f, 0.096f), 14, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Leg(s);
                float x = HeroRig.LegX * s;
                // Soft, slightly baggy legs that bunch over the boots.
                var r = new List<Ring>
                {
                    new Ring(new Vector3(x * 0.92f, 0.93f, -0.004f), 0.076f, 0.084f),
                    new Ring(new Vector3(x, 0.80f, 0f), 0.072f, 0.077f),
                    new Ring(new Vector3(x, 0.64f, 0.004f), 0.064f, 0.067f),
                    new Ring(new Vector3(x, 0.50f, 0.008f), 0.058f, 0.061f),
                    new Ring(new Vector3(x, 0.36f, 0.002f), 0.062f, 0.064f),
                    new Ring(new Vector3(x, 0.24f, 0f), 0.070f, 0.072f),
                    new Ring(new Vector3(x, 0.165f, 0.004f), 0.078f, 0.082f),
                    new Ring(new Vector3(x, 0.13f, 0.008f), 0.073f, 0.078f),
                };
                b.Kit.Loft(r, 14, false, true);
                b.Kit.Torus(new Vector3(x, 0.205f, 0.002f), Vector3.up, 0.072f, 0.074f, 0.007f, 14, 5);
            }
        }

        static void SkirtLegs(HeroBuild b)
        {
            OutfitSpec o = b.App.Outfit;
            // Slim legs in tights.
            b.Toon(HeroBuild.C(o.TightsColor), 1.2f);
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Hips);
            b.Kit.Ellipsoid(new Vector3(0f, 0.90f, -0.005f), new Vector3(0.118f, 0.08f, 0.09f), 14, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Leg(s);
                float x = HeroRig.LegX * s;
                var r = new List<Ring>
                {
                    new Ring(new Vector3(x * 0.92f, 0.93f, -0.004f), 0.066f, 0.072f),
                    new Ring(new Vector3(x, 0.78f, 0f), 0.058f, 0.062f),
                    new Ring(new Vector3(x, 0.62f, 0.004f), 0.048f, 0.052f),
                    new Ring(new Vector3(x, 0.50f, 0.008f), 0.043f, 0.046f),
                    new Ring(new Vector3(x, 0.38f, 0.004f), 0.046f, 0.05f),
                    new Ring(new Vector3(x, 0.24f, 0f), 0.04f, 0.043f),
                    new Ring(new Vector3(x, 0.17f, 0.002f), 0.042f, 0.045f),
                };
                b.Kit.Loft(r, 12, false, true);
            }
            // A flared skirt above the knee, two panels on spring chains, with a trim at the hem.
            var d = new SkirtDef
            {
                Name = "Skirt", GapTop = 0.002f, GapHem = 0.002f, ChainsPerPanel = 3, Sub = 1, Cols = 9,
                Y = new[] { 1.0f, 0.92f, 0.83f, 0.74f },
                Rx = new[] { 0.128f, 0.158f, 0.19f, 0.215f },
                Rz = new[] { 0.098f, 0.126f, 0.152f, 0.172f },
                Stiffness = 0.8f, Gravity = 0.4f, Thick = 0.007f,
            };
            Color c = HeroBuild.C(o.LegwearColor);
            var w = HeroSkirt.Build(b, d, c);
            Color trim = HeroBuild.C(o.Coat.TrimColor);
            for (int s = -1; s <= 1; s += 2)
                HeroSkirt.Patch(b, d, s, w[s < 0 ? 0 : 1], 2.82f, 3f, 0f, 1f, 0.004f, trim, 1f);
        }

        static void Boot(HeroBuild b, int s)
        {
            float x = HeroRig.LegX * s;
            Color boot = HeroBuild.C(b.App.Outfit.BootsColor);
            bool slim = b.App.Outfit.Legwear == LegwearKind.Skirt;
            float shaftTop = slim ? 0.27f : 0.20f;
            b.Leg(s);
            b.Toon(boot);
            var shaft = new List<Ring>
            {
                new Ring(new Vector3(x, 0.06f, -0.01f), 0.056f, 0.062f),
                new Ring(new Vector3(x, 0.13f, -0.008f), 0.054f, 0.057f),
                new Ring(new Vector3(x, shaftTop, -0.006f), slim ? 0.05f : 0.053f, slim ? 0.053f : 0.056f),
            };
            b.Kit.Loft(shaft, 12, false, !slim);
            if (slim)
            {
                // Folded-over top of the boot.
                b.Toon(boot * 0.85f + Color.black, 1.2f);
                b.Kit.Torus(new Vector3(x, shaftTop - 0.005f, -0.006f), Vector3.up, 0.053f, 0.056f, 0.009f, 14, 5, 1.4f);
                b.Toon(boot);
            }
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Foot(s));
            var foot = new List<Ring>();
            float[] zs = { -0.066f, -0.03f, 0.04f, 0.105f, 0.15f, 0.17f };
            float[] ws = { 0.044f, 0.055f, 0.057f, 0.055f, 0.045f, 0.029f };
            float[] hs = { 0.048f, 0.058f, 0.054f, 0.040f, 0.033f, 0.023f };
            float[] cy = { 0.073f, 0.078f, 0.070f, 0.058f, 0.053f, 0.048f };
            for (int i = 0; i < zs.Length; i++)
                foot.Add(new Ring(new Vector3(x, cy[i], zs[i]), Vector3.right, Vector3.up, ws[i], hs[i], 3f));
            b.Kit.Loft(foot, 14, true, true);
            b.Toon(MaterialPalette.Sole, 1.2f);
            var sole = new List<Ring>();
            for (int i = 0; i < zs.Length; i++)
                sole.Add(new Ring(new Vector3(x, 0.014f, zs[i] + (i == 0 ? -0.004f : i == zs.Length - 1 ? 0.006f : 0f)), Vector3.right, Vector3.up, ws[i] + 0.005f, 0.0145f, 6f));
            b.Kit.Loft(sole, 12, true, true);
            // Laces.
            b.Leg(s);
            b.Toon(boot * 0.6f + Color.black, 0.8f);
            for (int i = 0; i < 2; i++)
            {
                float y = 0.095f + i * 0.05f;
                float zf = i == 0 ? 0.062f : 0.056f;
                b.Kit.Tube(new Vector3(x - 0.018f, y - 0.011f, zf), new Vector3(x + 0.018f, y + 0.011f, zf), 0.003f, 0.003f, 4);
                b.Kit.Tube(new Vector3(x + 0.018f, y - 0.011f, zf), new Vector3(x - 0.018f, y + 0.011f, zf), 0.003f, 0.003f, 4);
            }
        }

        /// <summary>Gauntlets (segmented metal, claw tips), gloves (soft, rounded) or bare hands.</summary>
        static void Hand(HeroBuild b, int s)
        {
            OutfitSpec o = b.App.Outfit;
            bool claws = o.Hands == HandsKind.Gauntlets;
            Color main = o.Hands == HandsKind.Bare ? b.SkinColor : HeroBuild.C(o.HandsColor);
            Color tip = claws ? main * 0.62f + Color.black : main;
            float x = HeroRig.ShoulderX * s;
            int hand = HeroRig.Hand(s);
            void Paint(Color c, float w) { if (o.Hands == HandsKind.Bare) b.Toon(c, w, b.SkinShade, 0.3f); else b.Toon(c, w); }

            Paint(main, 1.2f);
            if (o.Hands != HandsKind.Bare)
            {
                b.Arm(s);
                float r0 = claws ? 0.042f : 0.04f, r1 = claws ? 0.047f : 0.043f;
                b.Kit.Tube(new Vector3(x, 0.93f, -0.01f), new Vector3(x, 0.865f, -0.01f), r0, r1, 12);
            }
            b.Rigid(hand);
            var palm = new List<Ring>
            {
                new Ring(new Vector3(x, 0.868f, -0.01f), 0.019f, 0.034f, 3f),
                new Ring(new Vector3(x + 0.002f * s, 0.835f, -0.008f), 0.021f, 0.039f, 3f),
                new Ring(new Vector3(x + 0.004f * s, 0.797f, -0.006f), 0.018f, 0.037f, 3f),
            };
            b.Kit.Loft(palm, 12, true, true);
            float thick = claws ? 1f : 0.88f;
            for (int f = 0; f < 4; f++)
            {
                float fz = -0.031f + f * 0.02f;
                float len = (f == 0 || f == 3 ? 0.85f : 1f) * (claws ? 1f : 0.92f);
                var pts = new List<Vector3>();
                var rad = new List<float>();
                Vector3 p = new Vector3(x + 0.004f * s, 0.794f, fz);
                Vector3 dir = Vector3.down;
                float[] seg = { 0.028f * len, 0.022f * len, 0.018f * len };
                pts.Add(p); rad.Add(0.0082f * thick);
                for (int k = 0; k < seg.Length; k++)
                {
                    dir = Quaternion.AngleAxis(-14f * s, Vector3.forward) * dir;
                    p += dir * seg[k];
                    pts.Add(p); rad.Add((0.0075f - k * 0.0007f) * thick);
                }
                Paint(main, 1f);
                b.Kit.Path(pts, rad, 5, 0.85f, Vector3.right);
                if (!claws) continue;
                Paint(tip, 1f);
                Vector3 c0 = p, c1 = p + Quaternion.AngleAxis(-22f * s, Vector3.forward) * dir * 0.02f;
                b.Kit.Path(new List<Vector3> { c0, Vector3.Lerp(c0, c1, 0.5f), c1 }, new List<float> { 0.006f, 0.0036f, 0.0006f }, 5, 0.8f, Vector3.right, false);
            }
            Paint(main, 1f);
            Vector3 t0 = new Vector3(x - 0.006f * s, 0.848f, 0.025f);
            Vector3 t1 = t0 + new Vector3(-0.011f * s, -0.022f, 0.015f);
            Vector3 t2 = t1 + new Vector3(-0.009f * s, -0.018f, 0.005f);
            b.Kit.Path(new List<Vector3> { t0, t1, t2 }, new List<float> { 0.0095f * thick, 0.008f * thick, 0.007f * thick }, 6);
            if (!claws) return;
            Paint(tip, 1f);
            b.Kit.Path(new List<Vector3> { t2, t2 + new Vector3(-0.004f * s, -0.011f, 0.002f), t2 + new Vector3(-0.008f * s, -0.018f, 0f) },
                new List<float> { 0.0058f, 0.0034f, 0.0006f }, 5, 1f, null, false);
        }
    }
}
