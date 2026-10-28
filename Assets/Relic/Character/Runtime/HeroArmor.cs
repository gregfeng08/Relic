using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Armor weight on top of the coat. Light: the coat alone. Medium: leather pauldrons,
    /// bracers and a chest strap. Heavy: plus metal plates on shoulders and chest, and greaves.
    /// </summary>
    public static class HeroArmor
    {
        public static void Build(HeroBuild b)
        {
            ArmorLook a = b.Look.Armor;
            if (a.Weight == ArmorWeight.Light) return;
            for (int s = -1; s <= 1; s += 2)
            {
                Pauldron(b, s, a.Leather, 0f, 1.6f);
                Bracer(b, s, a.Leather);
            }
            ChestStrap(b, a);
            if (a.Weight != ArmorWeight.Heavy) return;
            for (int s = -1; s <= 1; s += 2)
            {
                Pauldron(b, s, a.Plate, 0.014f, 1.5f);
                Greave(b, s, a.Plate.Scale(0.85f));
            }
            ChestPlate(b, a.Plate);
        }

        /// <summary>A dome over the shoulder, a little outward and down the arm (on the arm/chest blend).</summary>
        static void Pauldron(HeroBuild b, int s, Rgb color, float grow, float outline, float scale = 1f)
        {
            b.Toon(color, outline);
            b.Kit.Weigh = p => MeshKit.Blend(HeroBuild.ArmWeight(s, new Vector3(p.x, HeroRig.ShoulderY - 0.05f, p.z)), MeshKit.W1(HeroRig.Chest), 0.35f);
            Vector3 c = new Vector3((HeroRig.ShoulderX + 0.012f) * s, 1.395f - grow * 1.6f, -0.008f);
            float rx = (0.074f + grow) * scale, ry = (0.05f + grow * 0.5f) * scale, rz = (0.084f + grow) * scale;
            var rings = new List<Ring>();
            for (int i = 0; i <= 3; i++)
            {
                float t = Mathf.PI * 0.5f * i / 4f; // equator to near the pole
                rings.Add(new Ring(c + new Vector3(0.006f * s * i, Mathf.Sin(t) * ry - 0.02f, 0f), rx * Mathf.Cos(t), rz * Mathf.Cos(t)));
            }
            int rows = rings.Count - 1;
            const int cols = 10;
            b.Kit.Sheet(rows, cols,
                (i, j) => rings[i].At(Mathf.PI * 2f * j / cols),
                (i, j) => rings[i].At(Mathf.PI * 2f * j / cols) - (c + Vector3.down * 0.03f),
                0.006f,
                (i, j) => b.Kit.Weigh(rings[i].At(Mathf.PI * 2f * j / cols)));
        }

        static void Bracer(HeroBuild b, int s, Rgb color)
        {
            float x = HeroRig.ShoulderX * s;
            b.Arm(s);
            b.Toon(color, 1.4f);
            var r = new List<Ring>
            {
                new Ring(new Vector3(x, 1.075f, -0.01f), 0.060f, 0.062f),
                new Ring(new Vector3(x, 1.065f, -0.01f), 0.066f, 0.068f),
                new Ring(new Vector3(x, 1.0f, -0.01f), 0.068f, 0.070f),
                new Ring(new Vector3(x, 0.99f, -0.01f), 0.062f, 0.064f),
            };
            b.Kit.Loft(r, 12, false, false);
            // Two buckled straps.
            b.Toon(MaterialPalette.Cord, 1.2f);
            b.Kit.Torus(new Vector3(x, 1.033f, -0.01f), Vector3.up, 0.0705f, 0.0725f, 0.004f, 12, 4);
        }

        static void ChestStrap(HeroBuild b, ArmorLook a)
        {
            // Right shoulder to left hip, over the coat front.
            b.Toon(a.Leather, 1.4f);
            b.Torso();
            var pts = new List<Vector3>();
            var rad = new List<float>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                float y = Mathf.Lerp(1.40f, 1.0f, t);
                float x = Mathf.Lerp(0.12f, -0.13f, t);
                float rz = y > 1.15f ? 0.118f : 0.112f;
                float rx = y > 1.15f ? 0.17f : 0.15f;
                float u = Mathf.Clamp(x / rx, -0.95f, 0.95f);
                float z = rz * Mathf.Sqrt(1f - u * u) + 0.012f;
                pts.Add(new Vector3(x, y, z));
                rad.Add(0.015f);
            }
            b.Kit.Path(pts, rad, 6, 0.3f, Vector3.forward);
            b.Toon(MaterialPalette.Gold, 1.2f);
            b.Kit.Box(pts[4] + Vector3.forward * 0.006f, new Vector3(0.028f, 0.03f, 0.01f));
        }

        static void ChestPlate(HeroBuild b, Rgb color)
        {
            b.Toon(color, 1.5f);
            b.Torso();
            var r = new List<Ring>
            {
                new Ring(new Vector3(0f, 1.08f, 0.012f), 0.128f, 0.104f),
                new Ring(new Vector3(0f, 1.20f, 0.016f), 0.146f, 0.112f),
                new Ring(new Vector3(0f, 1.31f, 0.012f), 0.152f, 0.112f),
                new Ring(new Vector3(0f, 1.37f, 0.004f), 0.120f, 0.098f),
            };
            const int cols = 10;
            const float a0 = -1.0f, a1 = 1.0f;
            b.Kit.Sheet(r.Count - 1, cols,
                (i, j) => r[i].At(Mathf.Lerp(a0, a1, (float)j / cols)),
                (i, j) => { Vector3 d = r[i].At(Mathf.Lerp(a0, a1, (float)j / cols)) - r[i].C; d.y = 0f; return d; },
                0.008f,
                (i, j) => HeroBuild.TorsoWeight(r[i].At(Mathf.Lerp(a0, a1, (float)j / cols))));
        }

        static void Greave(HeroBuild b, int s, Rgb color)
        {
            float x = HeroRig.LegX * s;
            b.Leg(s);
            b.Toon(color, 1.4f);
            var r = new List<Ring>
            {
                new Ring(new Vector3(x, 0.46f, 0.01f), 0.074f, 0.078f),
                new Ring(new Vector3(x, 0.36f, 0.006f), 0.078f, 0.082f),
                new Ring(new Vector3(x, 0.25f, 0.004f), 0.084f, 0.088f),
            };
            const int cols = 8;
            const float a0 = -0.95f, a1 = 0.95f;
            b.Kit.Sheet(r.Count - 1, cols,
                (i, j) => r[i].At(Mathf.Lerp(a0, a1, (float)j / cols)),
                (i, j) => { Vector3 d = r[i].At(Mathf.Lerp(a0, a1, (float)j / cols)) - r[i].C; d.y = 0f; return d; },
                0.006f,
                (i, j) => HeroBuild.LegWeight(s, r[i].At(Mathf.Lerp(a0, a1, (float)j / cols))));
        }
    }
}
