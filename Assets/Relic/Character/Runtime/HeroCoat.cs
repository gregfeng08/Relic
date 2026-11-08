using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The coat (appearance-driven color, trim and length, tinted by the armor material): a
    /// tapered, rounded torso open at the front, a stand collar with a toggle, soft sleeves with
    /// turned-back cuffs, a flared two-panel skirt split at the back on spring chains, patch
    /// pockets, the folded hood, and thin gold-style trims on the edges, cuffs and hem.
    /// </summary>
    public static class HeroCoat
    {
        const float Thick = 0.009f;

        public static void Build(HeroBuild b)
        {
            CoatSpec spec = b.App.Outfit.Coat;
            if (!spec.On) return;
            Color coat = Tinted(HeroBuild.C(spec.Color), b.Look.Armor);
            Color trim = HeroBuild.C(spec.TrimColor);
            Color dark = coat * 0.82f; dark.a = 1f;
            Torso(b, coat, dark, trim);
            for (int s = -1; s <= 1; s += 2) Sleeve(b, coat, dark, trim, s);
            Skirt(b, spec, coat, dark, trim);
        }

        /// <summary>Armor tints the chosen coat color the way it tints the default brown.</summary>
        static Color Tinted(Color coat, ArmorLook armor)
        {
            Rgb d = MaterialPalette.Coat, a = armor.Coat;
            return new Color(coat.r * Ratio(a.R, d.R), coat.g * Ratio(a.G, d.G), coat.b * Ratio(a.B, d.B), 1f);
        }

        static float Ratio(float a, float d) => Mathf.Clamp(a / Mathf.Max(0.05f, d), 0.6f, 1.4f);

        static BoneWeight ShoulderAware(Vector3 p)
        {
            BoneWeight torso = HeroBuild.TorsoWeight(p);
            float ax = Mathf.Abs(p.x);
            float t = Mathf.Clamp01((ax - 0.11f) / 0.06f) * Mathf.Clamp01((p.y - 1.27f) / 0.08f) * 0.55f;
            if (t <= 0f) return torso;
            return MeshKit.Blend(torso, HeroBuild.ArmWeight(p.x < 0f ? -1 : 1, p), t);
        }

        static void Torso(HeroBuild b, Color coat, Color dark, Color trim)
        {
            b.Toon(coat);
            b.Kit.Weigh = ShoulderAware;
            // Tapered and rounded: a soft waist, a gentle chest, sloping shoulders into the collar.
            var r = new List<Ring>
            {
                new Ring(new Vector3(0f, 0.985f, 0.0f), 0.136f, 0.102f),
                new Ring(new Vector3(0f, 1.05f, 0.003f), 0.130f, 0.100f),
                new Ring(new Vector3(0f, 1.13f, 0.006f), 0.136f, 0.104f),
                new Ring(new Vector3(0f, 1.22f, 0.008f), 0.148f, 0.108f),
                new Ring(new Vector3(0f, 1.30f, 0.006f), 0.156f, 0.106f),
                new Ring(new Vector3(0f, 1.35f, 0.002f), 0.160f, 0.100f),
                new Ring(new Vector3(0f, 1.385f, -0.002f), 0.146f, 0.092f),
                new Ring(new Vector3(0f, 1.41f, -0.005f), 0.112f, 0.080f),
                new Ring(new Vector3(0f, 1.432f, -0.006f), 0.080f, 0.068f),
            };
            const float gap = 0.24f;
            Shell(b, r, 18, gap, Mathf.PI * 2f - gap, ShoulderAware);
            // Collar: a short stand-up band, open at the throat, trimmed.
            b.Toon(dark);
            var collar = new List<Ring>
            {
                new Ring(new Vector3(0f, 1.42f, -0.006f), 0.084f, 0.076f),
                new Ring(new Vector3(0f, 1.46f, -0.012f), 0.082f, 0.073f),
                new Ring(new Vector3(0f, 1.485f, -0.016f), 0.086f, 0.076f),
            };
            Shell(b, collar, 14, 0.55f, Mathf.PI * 2f - 0.55f, HeroBuild.TorsoWeight);
            // Trims down each edge of the opening.
            b.Toon(trim, 1.1f);
            b.Kit.Weigh = ShoulderAware;
            for (int s = -1; s <= 1; s += 2)
            {
                var pts = new List<Vector3>();
                var rad = new List<float>();
                foreach (Ring ring in r)
                {
                    if (ring.C.y > 1.40f) continue;
                    pts.Add(ring.At((gap + 0.012f) * s) + Vector3.forward * 0.004f);
                    rad.Add(0.006f);
                }
                b.Kit.Path(MeshKit.Spline(pts, 2), GearFill(pts.Count * 2 - 1, 0.006f), 5, 0.4f, Vector3.forward);
            }
            // Toggle across the opening at the collar, on a cord loop.
            Vector3 tg = new Vector3(-0.045f, 1.385f, 0.094f);
            b.Kit.Tube(tg + new Vector3(-0.015f, -0.003f, 0f), tg + new Vector3(0.015f, 0.003f, 0f), 0.007f, 0.007f, 8);
            b.Toon(MaterialPalette.Cord, 1.1f);
            b.Kit.Torus(new Vector3(0.012f, 1.388f, 0.10f), Vector3.forward, 0.022f, 0.01f, 0.003f, 10, 4);
            // The hood, folded down behind the neck.
            b.Toon(coat);
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            b.Kit.Ellipsoid(new Vector3(0f, 1.40f, -0.102f), new Vector3(0.115f, 0.05f, 0.048f), 14, 6);
        }

        static List<float> GearFill(int n, float v) => HeroGear.Fill(n, v);

        /// <summary>An open cloth tube with thickness (outer, lining and edges).</summary>
        static void Shell(HeroBuild b, IList<Ring> rings, int sides, float a0, float a1, System.Func<Vector3, BoneWeight> weigh)
        {
            int rows = rings.Count - 1;
            b.Kit.Sheet(rows, sides,
                (i, j) => rings[i].At(Mathf.Lerp(a0, a1, (float)j / sides)),
                (i, j) => { Vector3 p = rings[i].At(Mathf.Lerp(a0, a1, (float)j / sides)); Vector3 d = p - rings[i].C; d.y = 0f; return d; },
                Thick,
                (i, j) => weigh(rings[i].At(Mathf.Lerp(a0, a1, (float)j / sides))));
        }

        static void Sleeve(HeroBuild b, Color coat, Color dark, Color trim, int s)
        {
            float x = HeroRig.ShoulderX * s;
            b.Arm(s);
            b.Toon(coat);
            var r = new List<Ring>
            {
                new Ring(new Vector3(x - 0.006f * s, 1.41f, -0.01f), 0.056f, 0.060f),
                new Ring(new Vector3(x + 0.002f * s, 1.35f, -0.01f), 0.058f, 0.060f),
                new Ring(new Vector3(x + 0.002f * s, 1.24f, -0.01f), 0.054f, 0.056f),
                new Ring(new Vector3(x, 1.12f, -0.008f), 0.051f, 0.053f),
                new Ring(new Vector3(x, 1.03f, -0.01f), 0.055f, 0.057f),
                new Ring(new Vector3(x, 0.955f, -0.01f), 0.059f, 0.061f),
            };
            b.Kit.Loft(r, 12, true, false);
            // Turned-back cuff with a thin trim line.
            b.Toon(dark);
            var cuff = new List<Ring>
            {
                new Ring(new Vector3(x, 0.985f, -0.01f), 0.062f, 0.064f),
                new Ring(new Vector3(x, 0.972f, -0.01f), 0.068f, 0.070f),
                new Ring(new Vector3(x, 0.915f, -0.01f), 0.069f, 0.071f),
                new Ring(new Vector3(x, 0.905f, -0.01f), 0.062f, 0.064f),
            };
            b.Kit.Loft(cuff, 12, false, false);
            b.Kit.Loft(cuff, 12, false, false, 0f, Mathf.PI * 2f, true, true, 0.006f);
            b.Toon(trim, 1f);
            b.Kit.Torus(new Vector3(x, 0.968f, -0.01f), Vector3.up, 0.0695f, 0.0715f, 0.0035f, 12, 3);
        }

        static void Skirt(HeroBuild b, CoatSpec spec, Color coat, Color dark, Color trim)
        {
            float len = spec.Length;
            float heavy = b.Look.Armor.Weight == ArmorWeight.Heavy ? 0.05f : b.Look.Armor.Weight == ArmorWeight.Medium ? 0.025f : 0f;
            float hem = Mathf.Lerp(0.86f, 0.41f, len);
            float flare = Mathf.Lerp(0.17f, 0.255f, len) * (1f + heavy);
            var d = new SkirtDef { Name = "Coat", GapTop = 0.24f, GapHem = Mathf.Lerp(0.34f, 0.6f, len), VentFromY = len > 0.4f ? 0.74f : -10f, VentHem = 0.16f };
            const int n = 6;
            d.Y = new float[n]; d.Rx = new float[n]; d.Rz = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                d.Y[i] = Mathf.Lerp(0.99f, hem, t);
                d.Rx[i] = Mathf.Lerp(0.138f, flare, Mathf.Pow(t, 0.9f));
                d.Rz[i] = Mathf.Lerp(0.104f, flare * 0.81f, Mathf.Pow(t, 0.9f));
            }
            if (len < 0.35f) { d.ChainsPerPanel = 3; d.Sub = 1; d.Cols = 8; }
            var w = HeroSkirt.Build(b, d, coat);
            int rows = n - 1;
            for (int s = -1; s <= 1; s += 2)
            {
                var ws = w[s < 0 ? 0 : 1];
                if (len >= 0.35f)
                {
                    // Patch pocket with a flap, near the front edge at hip height.
                    float r0 = Mathf.Lerp(0.5f, 1.4f, len), r1 = r0 + Mathf.Lerp(0.6f, 0.9f, len);
                    HeroSkirt.Patch(b, d, s, ws, r0, r1, 0.08f, 0.40f, 0.005f, coat * 0.96f + Color.black);
                    HeroSkirt.Patch(b, d, s, ws, r0 - 0.15f, r0 + 0.08f, 0.06f, 0.42f, 0.009f, dark);
                }
                // Thin trim along the hem.
                HeroSkirt.Patch(b, d, s, ws, rows - 0.1f, rows, 0f, 1f, 0.005f, trim, 1f);
            }
        }
    }
}
