using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The long brown trench coat: open front, collar with a gold toggle, sleeves with turned-back
    /// cuffs, a flared skirt below the knees in two panels split at the back (on spring chains),
    /// big patch pockets, and the hood folded down behind the neck. Tinted by the armor material.
    /// </summary>
    public static class HeroCoat
    {
        const float Thick = 0.009f;

        public static void Build(HeroBuild b)
        {
            ArmorLook a = b.Look.Armor;
            Torso(b, a);
            for (int s = -1; s <= 1; s += 2) Sleeve(b, a, s);
            Skirt(b, a);
        }

        static BoneWeight ShoulderAware(Vector3 p)
        {
            BoneWeight torso = HeroBuild.TorsoWeight(p);
            float ax = Mathf.Abs(p.x);
            float t = Mathf.Clamp01((ax - 0.12f) / 0.06f) * Mathf.Clamp01((p.y - 1.28f) / 0.08f) * 0.55f;
            if (t <= 0f) return torso;
            return MeshKit.Blend(torso, HeroBuild.ArmWeight(p.x < 0f ? -1 : 1, p), t);
        }

        static void Torso(HeroBuild b, ArmorLook a)
        {
            b.Toon(a.Coat);
            b.Kit.Weigh = ShoulderAware;
            // Body of the coat, open at the front (arc leaves a gap around angle 0).
            var r = new List<Ring>
            {
                new Ring(new Vector3(0f, 0.985f, 0.0f), 0.140f, 0.104f),
                new Ring(new Vector3(0f, 1.08f, 0.004f), 0.138f, 0.103f),
                new Ring(new Vector3(0f, 1.20f, 0.008f), 0.154f, 0.110f),
                new Ring(new Vector3(0f, 1.30f, 0.006f), 0.166f, 0.110f),
                new Ring(new Vector3(0f, 1.365f, 0.0f), 0.180f, 0.102f),
                new Ring(new Vector3(0f, 1.41f, -0.004f), 0.128f, 0.088f),
                new Ring(new Vector3(0f, 1.44f, -0.006f), 0.074f, 0.066f),
            };
            const float gap = 0.26f;
            int sides = 22;
            Shell(b, r, sides, gap, Mathf.PI * 2f - gap);
            // Collar: a short stand-up band, open at the throat.
            b.Toon(a.CoatTrim);
            var collar = new List<Ring>
            {
                new Ring(new Vector3(0f, 1.425f, -0.006f), 0.086f, 0.078f),
                new Ring(new Vector3(0f, 1.47f, -0.012f), 0.084f, 0.074f),
                new Ring(new Vector3(0f, 1.495f, -0.016f), 0.088f, 0.077f),
            };
            Shell(b, collar, 16, 0.55f, Mathf.PI * 2f - 0.55f);
            // Lapel edges: a slightly darker strip down each side of the opening.
            for (int s = -1; s <= 1; s += 2)
            {
                var pts = new List<Vector3>();
                var rad = new List<float>();
                foreach (Ring ring in r)
                {
                    if (ring.C.y > 1.40f) continue;
                    pts.Add(ring.At(gap * s + 0.03f * s) + Vector3.forward * 0.004f);
                    rad.Add(0.009f);
                }
                b.Kit.Path(pts, rad, 5, 0.45f, Vector3.forward);
            }
            // Gold toggle at the collar (character's right, as in the sheet) on a cord loop.
            b.Toon(MaterialPalette.Gold, 1.3f);
            Vector3 tg = new Vector3(0.05f, 1.385f, 0.095f);
            b.Kit.Tube(tg + new Vector3(-0.016f, -0.004f, 0f), tg + new Vector3(0.016f, 0.004f, 0f), 0.0075f, 0.0075f, 6);
            b.Toon(MaterialPalette.Cord, 1.2f);
            b.Kit.Torus(new Vector3(-0.012f, 1.39f, 0.10f), Vector3.forward, 0.022f, 0.01f, 0.003f, 10, 4);
            // The hood, folded down behind the neck.
            b.Toon(a.Coat);
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            b.Kit.Ellipsoid(new Vector3(0f, 1.405f, -0.105f), new Vector3(0.12f, 0.055f, 0.05f), 12, 6);
        }

        /// <summary>An open cloth tube with thickness (outer, lining and edges).</summary>
        static void Shell(HeroBuild b, IList<Ring> rings, int sides, float a0, float a1)
        {
            int rows = rings.Count - 1;
            b.Kit.Sheet(rows, sides,
                (i, j) => rings[i].At(Mathf.Lerp(a0, a1, (float)j / sides)),
                (i, j) => { Vector3 p = rings[i].At(Mathf.Lerp(a0, a1, (float)j / sides)); Vector3 d = p - rings[i].C; d.y = 0f; return d; },
                Thick,
                (i, j) => b.Kit.Weigh(rings[i].At(Mathf.Lerp(a0, a1, (float)j / sides))));
        }

        static void Sleeve(HeroBuild b, ArmorLook a, int s)
        {
            float x = HeroRig.ShoulderX * s;
            b.Arm(s);
            b.Toon(a.Coat);
            var r = new List<Ring>
            {
                new Ring(new Vector3(x - 0.004f * s, 1.415f, -0.01f), 0.060f, 0.064f),
                new Ring(new Vector3(x + 0.004f * s, 1.33f, -0.01f), 0.061f, 0.062f),
                new Ring(new Vector3(x + 0.002f * s, 1.20f, -0.01f), 0.056f, 0.058f),
                new Ring(new Vector3(x, 1.12f, -0.008f), 0.055f, 0.057f),
                new Ring(new Vector3(x, 1.03f, -0.01f), 0.058f, 0.060f),
                new Ring(new Vector3(x, 0.955f, -0.01f), 0.061f, 0.063f),
            };
            b.Kit.Loft(r, 12, true, false);
            // Turned-back cuff.
            b.Toon(a.CoatTrim);
            var cuff = new List<Ring>
            {
                new Ring(new Vector3(x, 0.985f, -0.01f), 0.064f, 0.066f),
                new Ring(new Vector3(x, 0.97f, -0.01f), 0.071f, 0.073f),
                new Ring(new Vector3(x, 0.915f, -0.01f), 0.072f, 0.074f),
                new Ring(new Vector3(x, 0.905f, -0.01f), 0.064f, 0.066f),
            };
            b.Kit.Loft(cuff, 12, false, false);
            b.Kit.Loft(cuff, 12, false, false, 0f, Mathf.PI * 2f, true, true, 0.006f);
        }

        // ---- skirt ------------------------------------------------------------------------

        static readonly float[] SkirtY = { 0.99f, 0.90f, 0.79f, 0.66f, 0.53f, 0.41f };
        static readonly float[] SkirtRx = { 0.142f, 0.158f, 0.178f, 0.203f, 0.230f, 0.258f };
        static readonly float[] SkirtRz = { 0.106f, 0.122f, 0.140f, 0.163f, 0.186f, 0.208f };
        const int ChainsPerPanel = 4;
        const int Cols = 10;

        static float FrontGap(float y01) => Mathf.Lerp(0.24f, 0.62f, y01 * y01);
        static float BackVent(float y) => y > 0.74f ? 0.004f : Mathf.Lerp(0.004f, 0.16f, Mathf.Clamp01((0.74f - y) / 0.33f));

        /// <summary>Root-space point on the skirt at row (fractional) and panel parameter u (0 = front edge, 1 = back edge).</summary>
        static Vector3 SkirtPoint(int s, float row, float u, float heavy)
        {
            int i0 = Mathf.Clamp(Mathf.FloorToInt(row), 0, SkirtY.Length - 2);
            float f = Mathf.Clamp01(row - i0);
            float y = Mathf.Lerp(SkirtY[i0], SkirtY[i0 + 1], f);
            float rx = Mathf.Lerp(SkirtRx[i0], SkirtRx[i0 + 1], f) * (1f + heavy);
            float rz = Mathf.Lerp(SkirtRz[i0], SkirtRz[i0 + 1], f) * (1f + heavy);
            float y01 = row / (SkirtY.Length - 1);
            float a0 = FrontGap(y01), a1 = Mathf.PI - BackVent(y);
            float a = Mathf.Lerp(a0, a1, u);
            return new Vector3(Mathf.Sin(a) * rx * s, y, Mathf.Cos(a) * rz + 0.005f);
        }

        static void Skirt(HeroBuild b, ArmorLook a)
        {
            float heavy = a.Weight == ArmorWeight.Heavy ? 0.06f : a.Weight == ArmorWeight.Medium ? 0.03f : 0f;
            int rows = SkirtY.Length - 1;
            for (int s = -1; s <= 1; s += 2)
            {
                // Chains: one per panel column group, a joint at every row.
                var chains = new ChainDef[ChainsPerPanel];
                for (int c = 0; c < ChainsPerPanel; c++)
                {
                    float u = (float)c / (ChainsPerPanel - 1);
                    var joints = new List<Vector3>();
                    for (int i = 0; i <= rows; i++) joints.Add(SkirtPoint(s, i, u, heavy) - Outward(s, i, u, heavy) * 0.03f);
                    chains[c] = b.Chain("Coat" + (s < 0 ? "L" : "R") + c, HeroRig.Hips, joints, "coat");
                    chains[c].Stiffness = 0.75f;
                    chains[c].CollideMask = SpringChains.Legs;
                    chains[c].CollideFrom = 1;
                    chains[c].Drag = 0.5f;
                    chains[c].Gravity = 0.45f;
                    chains[c].Radius = 0.015f;
                }
                int side = s;
                BoneWeight W(float row, float u)
                {
                    float cu = u * (ChainsPerPanel - 1);
                    int c0 = Mathf.Clamp(Mathf.FloorToInt(cu), 0, ChainsPerPanel - 2);
                    float fu = cu - c0;
                    int i0 = Mathf.Clamp(Mathf.FloorToInt(row), 0, rows - 1);
                    float fr = row - i0;
                    BoneWeight top = MeshKit.W2(chains[c0].Bones[i0], chains[c0 + 1].Bones[i0], fu);
                    BoneWeight bottom = MeshKit.W2(chains[c0].Bones[i0 + 1], chains[c0 + 1].Bones[i0 + 1], fu);
                    BoneWeight w = MeshKit.Blend(top, bottom, fr);
                    // The waistband stays on the hips.
                    if (row < 0.6f) w = MeshKit.Blend(MeshKit.W1(HeroRig.Hips), w, row / 0.6f);
                    return w;
                }
                const int sub = 2; // grid rows per skirt ring
                b.Toon(a.Coat);
                b.Kit.Sheet(rows * sub, Cols,
                    (i, j) => SkirtPoint(side, (float)i / sub, (float)j / Cols, heavy),
                    (i, j) => Outward(side, (float)i / sub, (float)j / Cols, heavy),
                    Thick,
                    (i, j) => W((float)i / sub, (float)j / Cols));
                // Patch pocket with a flap, near the front edge at hip height.
                Patch(b, side, heavy, 1.75f, 2.65f, 0.08f, 0.40f, 0.005f, a.Coat.Scale(0.95f), W);
                Patch(b, side, heavy, 1.62f, 1.85f, 0.06f, 0.42f, 0.009f, a.CoatTrim, W);
                // Hem band.
                Patch(b, side, heavy, rows - 0.22f, rows, 0f, 1f, 0.004f, a.CoatTrim, W);
            }
        }

        static Vector3 Outward(int s, float row, float u, float heavy)
        {
            Vector3 p = SkirtPoint(s, row, u, heavy);
            Vector3 d = new Vector3(p.x, 0f, p.z - 0.005f);
            // Flares: the surface tilts outward as it descends.
            return (d.normalized + Vector3.down * 0.25f).normalized;
        }

        /// <summary>A thin patch on the skirt surface over rows r0..r1 and panel u0..u1, skinned like the skirt.</summary>
        static void Patch(HeroBuild b, int s, float heavy, float r0, float r1, float u0, float u1, float lift, Rgb color, System.Func<float, float, BoneWeight> w)
        {
            const int pr = 2, pc = 4;
            b.Toon(color, 1.2f);
            b.Kit.Sheet(pr, pc,
                (i, j) =>
                {
                    float row = Mathf.Lerp(r0, r1, (float)i / pr), u = Mathf.Lerp(u0, u1, (float)j / pc);
                    return SkirtPoint(s, row, u, heavy) + Outward(s, row, u, heavy) * lift;
                },
                (i, j) => Outward(s, Mathf.Lerp(r0, r1, (float)i / pr), Mathf.Lerp(u0, u1, (float)j / pc), heavy),
                0.004f,
                (i, j) => w(Mathf.Lerp(r0, r1, (float)i / pr), Mathf.Lerp(u0, u1, (float)j / pc)));
        }
    }
}
