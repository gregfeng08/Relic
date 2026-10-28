using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Always-carried kit (pillar 7: you carry real things): backpack with flap and two pockets,
    /// waterskin, health and mana flasks on the belt; plus the inherited amulet (the relic) and
    /// two trinket charms hanging from the belt.
    /// </summary>
    public static class HeroGear
    {
        public static void Build(HeroBuild b)
        {
            Backpack(b);
            Waterskin(b);
            Flasks(b);
            Amulet(b, b.Look.Amulet);
            Charm(b, b.Look.Charms[0], new Vector3(0.062f, 0.975f, 0.104f));
            Charm(b, b.Look.Charms[1], new Vector3(0.098f, 0.972f, 0.094f));
        }

        static void Backpack(HeroBuild b)
        {
            b.Rigid(HeroRig.Chest);
            Rgb canvas = MaterialPalette.Canvas, leather = MaterialPalette.Leather;
            b.Toon(canvas, 1.6f);
            b.Kit.Box(new Vector3(0f, 1.15f, -0.185f), new Vector3(0.25f, 0.30f, 0.14f), 5f);
            // Flap over the top, down the outer face.
            b.Toon(leather, 1.4f);
            b.Kit.Sheet(2, 6,
                (i, j) =>
                {
                    float u = Mathf.Lerp(-0.125f, 0.125f, j / 6f);
                    if (i == 0) return new Vector3(u, 1.305f, -0.135f);
                    if (i == 1) return new Vector3(u, 1.305f, -0.258f);
                    return new Vector3(u * 0.96f, 1.20f, -0.262f);
                },
                (i, j) => i == 0 ? Vector3.up : i == 1 ? new Vector3(0f, 1f, -1f) : Vector3.back,
                0.006f, (i, j) => MeshKit.W1(HeroRig.Chest));
            // Buckle tab on the flap.
            b.Kit.Box(new Vector3(0f, 1.18f, -0.266f), new Vector3(0.03f, 0.07f, 0.008f));
            b.Toon(MaterialPalette.Gold, 1.2f);
            b.Kit.Box(new Vector3(0f, 1.17f, -0.272f), new Vector3(0.018f, 0.018f, 0.006f));
            // Two pockets.
            for (int s = -1; s <= 1; s += 2)
            {
                b.Toon(canvas.Scale(0.9f), 1.4f);
                b.Kit.Box(new Vector3(0.062f * s, 1.065f, -0.268f), new Vector3(0.085f, 0.085f, 0.04f), 5f);
                b.Toon(leather, 1.2f);
                b.Kit.Box(new Vector3(0.062f * s, 1.098f, -0.29f), new Vector3(0.088f, 0.026f, 0.012f), 5f);
            }
            // Shoulder straps over the coat.
            b.Toon(leather, 1.3f);
            for (int s = -1; s <= 1; s += 2)
            {
                var pts = new List<Vector3>
                {
                    new Vector3(0.075f * s, 1.29f, -0.13f), new Vector3(0.10f * s, 1.405f, -0.07f),
                    new Vector3(0.115f * s, 1.415f, 0.02f), new Vector3(0.122f * s, 1.36f, 0.098f),
                    new Vector3(0.128f * s, 1.25f, 0.112f), new Vector3(0.13f * s, 1.16f, 0.10f),
                };
                b.Kit.Weigh = HeroBuild.TorsoWeight;
                b.Kit.Path(MeshKit.Spline(pts, 2), Fill(11, 0.014f), 4, 0.3f, new Vector3(0f, 0.3f, 1f));
            }
        }

        static void Waterskin(HeroBuild b)
        {
            // Hangs on the right side of the backpack.
            b.Rigid(HeroRig.Chest);
            b.Toon(new Color(0.62f, 0.44f, 0.27f), 1.4f);
            var basePos = new Vector3(0.155f, 1.0f, -0.19f);
            b.Kit.Lathe(basePos, new List<Vector2>
            {
                new Vector2(0.02f, 0.0f), new Vector2(0.05f, 0.03f), new Vector2(0.056f, 0.075f),
                new Vector2(0.04f, 0.13f), new Vector2(0.014f, 0.16f), new Vector2(0.012f, 0.175f),
            }, 10, 2f, 0.7f);
            b.Toon(MaterialPalette.Cord, 1.2f);
            b.Kit.Box(basePos + new Vector3(0f, 0.185f, 0f), new Vector3(0.02f, 0.02f, 0.02f), 4f);
            b.Kit.Torus(basePos + new Vector3(-0.01f, 0.16f, 0f), new Vector3(0f, 0f, 1f), 0.035f, 0.05f, 0.003f, 10, 4);
        }

        static void Flasks(HeroBuild b)
        {
            b.Rigid(HeroRig.Hips);
            // Health: a round flask; mana: a slim vial. On the belt, front left.
            var hp = new Vector3(-0.072f, 0.895f, 0.112f);
            b.Toon(MaterialPalette.HealthPotion, 1.3f);
            b.Kit.Lathe(hp, new List<Vector2>
            {
                new Vector2(0.012f, 0f), new Vector2(0.03f, 0.015f), new Vector2(0.034f, 0.04f),
                new Vector2(0.022f, 0.065f), new Vector2(0.009f, 0.075f), new Vector2(0.009f, 0.09f),
            }, 10, 2f, 0.8f);
            b.Toon(new Color(0.72f, 0.56f, 0.38f), 1.1f);
            b.Kit.Tube(hp + new Vector3(0f, 0.088f, 0f), hp + new Vector3(0f, 0.105f, 0f), 0.011f, 0.012f, 6);
            var mp = new Vector3(-0.112f, 0.905f, 0.096f);
            b.Toon(MaterialPalette.ManaPotion, 1.3f);
            b.Kit.Lathe(mp, new List<Vector2>
            {
                new Vector2(0.006f, 0f), new Vector2(0.014f, 0.01f), new Vector2(0.014f, 0.06f),
                new Vector2(0.006f, 0.075f), new Vector2(0.006f, 0.085f),
            }, 8);
            b.Toon(new Color(0.72f, 0.56f, 0.38f), 1.1f);
            b.Kit.Tube(mp + new Vector3(0f, 0.083f, 0f), mp + new Vector3(0f, 0.097f, 0f), 0.0075f, 0.008f, 6);
        }

        static void Amulet(HeroBuild b, AmuletLook a)
        {
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            var p = new Vector3(0f, 1.285f, 0.118f);
            // Cord from under the scarf.
            b.Toon(MaterialPalette.Cord, 1.1f);
            for (int s = -1; s <= 1; s += 2)
                b.Kit.Path(new List<Vector3> { new Vector3(0.05f * s, 1.42f, 0.07f), new Vector3(0.03f * s, 1.36f, 0.105f), p + new Vector3(0.006f * s, 0.03f, 0f) },
                    new List<float> { 0.003f, 0.003f, 0.003f }, 4);
            b.Rigid(HeroRig.Chest);
            b.Toon(a.Metal, 1.3f);
            Color gemColor = a.Glows ? HeroMaterials.ToColor(a.GlowColor) : new Color(0.35f, 0.42f, 0.40f);
            switch (a.Shape)
            {
                case AmuletShape.Ring:
                    b.Kit.Torus(p, Vector3.forward, 0.026f, 0.026f, 0.006f, 14, 5);
                    Gem(b, a, gemColor, p, 0.012f);
                    break;
                case AmuletShape.Bell:
                    b.Kit.Lathe(p + Vector3.down * 0.025f, new List<Vector2>
                    {
                        new Vector2(0.026f, 0f), new Vector2(0.022f, 0.012f), new Vector2(0.016f, 0.035f), new Vector2(0.008f, 0.048f),
                    }, 10);
                    Gem(b, a, gemColor, p + Vector3.down * 0.03f, 0.009f);
                    break;
                case AmuletShape.Key:
                    b.Kit.Torus(p + Vector3.up * 0.012f, Vector3.forward, 0.014f, 0.014f, 0.004f, 10, 4);
                    b.Kit.Tube(p, p + Vector3.down * 0.05f, 0.004f, 0.004f, 6);
                    b.Kit.Box(p + new Vector3(0.008f, -0.044f, 0f), new Vector3(0.014f, 0.012f, 0.005f));
                    Gem(b, a, gemColor, p + Vector3.up * 0.012f, 0.007f);
                    break;
                case AmuletShape.Cage:
                    b.Kit.Tube(p + Vector3.up * 0.026f, p + Vector3.up * 0.034f, 0.016f, 0.008f, 8);
                    b.Kit.Tube(p + Vector3.down * 0.026f, p + Vector3.down * 0.032f, 0.016f, 0.01f, 8);
                    for (int k = 0; k < 4; k++)
                    {
                        float ang = k * Mathf.PI * 0.5f + 0.4f;
                        var o = new Vector3(Mathf.Sin(ang) * 0.016f, 0f, Mathf.Cos(ang) * 0.016f);
                        b.Kit.Tube(p + o + Vector3.up * 0.026f, p + o + Vector3.down * 0.026f, 0.0028f, 0.0028f, 4);
                    }
                    Gem(b, a, gemColor, p, 0.012f);
                    break;
                case AmuletShape.Disc:
                    b.Kit.Xf = Matrix4x4.TRS(p, Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                    b.Kit.Lathe(new Vector3(0f, -0.004f, 0f), new List<Vector2> { new Vector2(0.026f, 0f), new Vector2(0.028f, 0.004f), new Vector2(0.024f, 0.008f) }, 14);
                    b.Kit.Xf = Matrix4x4.identity;
                    Gem(b, a, gemColor, p + Vector3.forward * 0.006f, 0.011f);
                    break;
                default:
                    // The diamond from the sheet: a rhombus frame around an eye-like gem.
                    var frame = new List<Vector3>
                    {
                        p + new Vector3(0f, 0.032f, 0f), p + new Vector3(0.026f, 0f, 0f), p + new Vector3(0f, -0.032f, 0f),
                        p + new Vector3(-0.026f, 0f, 0f), p + new Vector3(0f, 0.032f, 0f),
                    };
                    b.Kit.Path(frame, Fill(5, 0.005f), 5, 1f, Vector3.forward, false);
                    b.Toon(a.Metal.Scale(0.85f), 1.2f);
                    b.Kit.Xf = Matrix4x4.TRS(p, Quaternion.Euler(0f, 0f, 45f), new Vector3(1f, 1.25f, 1f));
                    b.Kit.Box(Vector3.zero, new Vector3(0.032f, 0.032f, 0.008f), 3f);
                    b.Kit.Xf = Matrix4x4.identity;
                    Gem(b, a, gemColor, p + Vector3.forward * 0.005f, 0.01f);
                    break;
            }
        }

        static void Gem(HeroBuild b, AmuletLook a, Color c, Vector3 at, float r)
        {
            if (a.Glows) b.Kit.Use(b.Mats.Glow(a.GlowColor, a.Glow));
            else b.Toon(c, 1f);
            b.Kit.Ellipsoid(at, new Vector3(r, r * 0.8f, r * 0.55f), 10, 6);
        }

        static void Charm(HeroBuild b, CharmLook c, Vector3 at)
        {
            if (c == null) return;
            b.Rigid(HeroRig.Hips);
            b.Toon(MaterialPalette.Cord, 1f);
            Vector3 p = at + Vector3.down * 0.045f;
            b.Kit.Tube(at, p + Vector3.up * 0.012f, 0.0025f, 0.0025f, 4, false);
            b.Toon(c.Metal, 1.2f);
            switch (c.Shape)
            {
                case CharmShape.Ring:
                    b.Kit.Torus(p, Vector3.forward, 0.012f, 0.012f, 0.0035f, 10, 4);
                    break;
                case CharmShape.Brooch:
                    b.Kit.Ellipsoid(p, new Vector3(0.015f, 0.018f, 0.005f), 10, 5);
                    break;
                case CharmShape.Signet:
                    b.Kit.Torus(p, Vector3.right, 0.011f, 0.011f, 0.0035f, 10, 4);
                    b.Kit.Box(p + Vector3.up * 0.011f, new Vector3(0.014f, 0.006f, 0.012f));
                    break;
                default:
                    b.Kit.Xf = Matrix4x4.TRS(p, Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                    b.Kit.Lathe(new Vector3(0f, -0.002f, 0f), new List<Vector2> { new Vector2(0.013f, 0f), new Vector2(0.014f, 0.002f), new Vector2(0.013f, 0.004f) }, 10);
                    b.Kit.Xf = Matrix4x4.identity;
                    break;
            }
            if (c.Glows)
            {
                b.Kit.Use(b.Mats.Glow(c.GlowColor, c.Glow));
                b.Kit.Ellipsoid(p + Vector3.forward * 0.004f, new Vector3(0.006f, 0.006f, 0.004f), 8, 4);
            }
        }

        public static List<float> Fill(int n, float v)
        {
            var l = new List<float>(n);
            for (int i = 0; i < n; i++) l.Add(v);
            return l;
        }
    }
}
