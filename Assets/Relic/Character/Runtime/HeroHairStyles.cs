using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The back of the hair per HairStyle: wide overlapping locks from the crown that read as one
    /// soft mass, gathered into tails (low tail, ponytail, twin tails) with a ribbon tie, or
    /// hanging free (long straight, bob). Tails and long locks ride spring chains.
    /// </summary>
    public static class HeroHairStyles
    {
        public static void Back(HeroBuild b, HairSpec h)
        {
            float len = h.Length;
            switch (h.Style)
            {
                case HairStyle.Ponytail:
                    {
                        var tie = new Vector3(0f, 1.69f, -0.125f);
                        Gathered(b, tie, 0.6f, 1.48f);
                        Tie(b, tie, new Vector3(0f, 0.5f, -1f), HeroRig.Head);
                        Tail(b, "Pony", tie, new[] { new Vector3(0f, 1.66f, -0.20f), new Vector3(0f, 1.52f, -0.235f), new Vector3(0f, 1.38f, -0.23f) },
                            Mathf.Lerp(1.30f, 0.95f, len), 0.06f, HeroRig.Head);
                        break;
                    }
                case HairStyle.TwinTails:
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var tie = new Vector3(0.105f * s, 1.665f, -0.045f);
                        GatheredHalf(b, tie, s);
                        Tie(b, tie, new Vector3(s, 0.2f, -0.4f), HeroRig.Head);
                        Tail(b, s < 0 ? "TwinL" : "TwinR", tie, new[] { new Vector3(0.15f * s, 1.62f, -0.06f), new Vector3(0.185f * s, 1.50f, -0.07f), new Vector3(0.20f * s, 1.36f, -0.07f) },
                            Mathf.Lerp(1.22f, 0.92f, len), 0.05f, HeroRig.Head);
                    }
                    break;
                case HairStyle.Bob:
                    Bob(b, len);
                    break;
                case HairStyle.LongStraight:
                    Long(b, len);
                    break;
                default:
                    {
                        var tie = new Vector3(0f, 1.36f, -0.165f);
                        Gathered(b, tie, 1f, 1.42f);
                        Tie(b, tie, new Vector3(0f, 0.35f, -1f), HeroRig.Chest);
                        Tail(b, "LowTail", tie, new[] { new Vector3(0f, 1.24f, -0.27f), new Vector3(0f, 1.10f, -0.29f) },
                            Mathf.Lerp(1.02f, 0.82f, len), 0.08f, HeroRig.Chest);
                        break;
                    }
            }
        }

        /// <summary>Gathered-mass weights: head on top, easing into neck/chest toward a low tie.</summary>
        static BoneWeight MassWeight(Vector3 p)
        {
            if (p.y >= 1.54f) return MeshKit.W1(HeroRig.Head);
            float t = Mathf.Clamp01((1.54f - p.y) / 0.16f);
            BoneWeight w = MeshKit.W2(HeroRig.Head, HeroRig.Neck, t);
            if (p.y < 1.42f) w = MeshKit.Blend(w, MeshKit.W1(HeroRig.Chest), Mathf.Clamp01((1.42f - p.y) / 0.06f));
            return w;
        }

        /// <summary>Wide locks from the crown and sides converging on a tie (pull 0..1 = how far down the back they reach).</summary>
        static void Gathered(HeroBuild b, Vector3 tie, float pull, float midY)
        {
            b.Hair();
            b.Kit.Weigh = MassWeight;
            const int n = 7;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / (n - 1);
                float a = Mathf.Lerp(0.52f, 1.48f, u) * Mathf.PI;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float side = Mathf.Abs(dir.x);
                var j = new List<Vector3>
                {
                    new Vector3(dir.x * 0.07f, 1.735f, dir.z * 0.07f - 0.01f),
                    new Vector3(dir.x * 0.124f, 1.63f, dir.z * 0.126f - 0.012f),
                    new Vector3(dir.x * 0.12f * pull + dir.x * 0.06f * (1f - pull), midY, dir.z * 0.12f - 0.03f - 0.02f * side),
                    new Vector3(tie.x + dir.x * 0.04f, tie.y + 0.02f * pull, tie.z + 0.01f),
                };
                if (pull < 0.8f) j.RemoveAt(2);
                HeroHair.Lock(b, j, 0.062f, 0.03f, dir, 0.42f, 6, 2, 0.9f);
            }
        }

        /// <summary>One side's half of the back, swept up to a twin-tail tie.</summary>
        static void GatheredHalf(HeroBuild b, Vector3 tie, int s)
        {
            b.Hair();
            b.Rigid(HeroRig.Head);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.Lerp(0.55f, 1.0f, i / 3f) * Mathf.PI;
                var dir = new Vector3(Mathf.Sin(a) * s, 0f, Mathf.Cos(a));
                var j = new List<Vector3>
                {
                    new Vector3(dir.x * 0.05f, 1.74f, dir.z * 0.06f - 0.01f),
                    new Vector3(dir.x * 0.122f, 1.62f, dir.z * 0.124f - 0.012f),
                    new Vector3(tie.x * 0.85f, tie.y + 0.01f, tie.z - 0.01f),
                };
                HeroHair.Lock(b, j, 0.058f, 0.03f, dir, 0.42f, 6, 2, 0.9f);
            }
        }

        /// <summary>A thick tail from a tie, plus two companion locks, on chains.</summary>
        static void Tail(HeroBuild b, string name, Vector3 tie, Vector3[] path, float endY, float width, int parent)
        {
            b.Hair();
            var j = new List<Vector3> { tie + new Vector3(0f, 0.005f, -0.01f) };
            foreach (Vector3 p in path) if (p.y > endY + 0.05f) j.Add(p);
            Vector3 last = j[j.Count - 1];
            j.Add(new Vector3(last.x * 1.05f, endY, last.z + 0.01f));
            HeroHair.Chain(b, name, parent, j, 0.35f, width * 0.5f);
            bool thick = width >= 0.06f;
            HeroHair.Lock(b, j, width, width * 0.25f, new Vector3(tie.x, 0f, -1f), 0.6f, thick ? 8 : 6, 2, 0.7f);
            // Thick tails get two companion locks; slim twin tails one (outward).
            for (int s = thick ? -1 : (tie.x < 0f ? -1 : 1); s <= 1; s += thick ? 2 : 3)
            {
                var k = new List<Vector3>();
                for (int i = 0; i < j.Count; i++)
                {
                    Vector3 off = i == 0 ? new Vector3(0.015f * s, 0f, 0.005f) : new Vector3(0.03f * s, 0.01f, 0.012f);
                    k.Add(j[i] + off + (i == j.Count - 1 ? Vector3.up * 0.06f : Vector3.zero));
                }
                HeroHair.Chain(b, name + (s < 0 ? "L" : "R"), parent, k, 0.3f, width * 0.35f);
                HeroHair.Lock(b, k, width * 0.62f, width * 0.12f, new Vector3(s, 0f, -1f), 0.55f, 5, 2, 0.7f);
            }
        }

        /// <summary>The ribbon tie: a band and a small bow with two short ends.</summary>
        static void Tie(HeroBuild b, Vector3 tie, Vector3 facing, int parent)
        {
            HeroAppearance app = b.App;
            Color c = app.Accessories.Count > 0 && app.Accessories[0].Kind == AccessoryKind.Ribbon ? HeroBuild.C(app.Accessories[0].Color) : HeroBuild.C(app.Outfit.Scarf.Color);
            b.Toon(c, 1.2f);
            if (parent == HeroRig.Head) b.Rigid(HeroRig.Head); else b.Kit.Weigh = MassWeight;
            Vector3 f = facing.normalized;
            b.Kit.Torus(tie + new Vector3(0f, 0.004f, 0f), f, 0.032f, 0.026f, 0.01f, 12, 5);
            Vector3 side = Vector3.Cross(Vector3.up, f).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            for (int s = -1; s <= 1; s += 2)
            {
                b.Kit.Xf = Matrix4x4.TRS(tie + side * (0.034f * s) + f * 0.018f + Vector3.up * 0.012f, Quaternion.LookRotation(f) * Quaternion.Euler(0f, 0f, 30f * s), Vector3.one);
                b.Kit.Ellipsoid(Vector3.zero, new Vector3(0.03f, 0.018f, 0.011f), 10, 6);
                b.Kit.Xf = Matrix4x4.identity;
            }
            var ends = new List<Vector3> { tie + f * 0.022f, tie + f * 0.03f + Vector3.down * 0.06f + side * 0.015f, tie + f * 0.035f + Vector3.down * 0.12f + side * 0.03f };
            ChainDef ribbon = b.Chain("Ribbon", parent, ends, "scarf");
            ribbon.Stiffness = 0.3f;
            ribbon.Radius = 0.01f;
            ribbon.CollideFrom = 1;
            b.Kit.Weigh = p => b.ChainWeight(ribbon, parent, p);
            b.Kit.Path(MeshKit.Spline(ends, 2), new List<float> { 0.015f, 0.016f, 0.017f, 0.018f, 0.015f }, 5, 0.25f, f);
        }

        static void Bob(HeroBuild b, float len)
        {
            b.Hair();
            b.Rigid(HeroRig.Head);
            float endY = Mathf.Lerp(1.52f, 1.43f, len);
            const int n = 9;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(0.38f, 1.62f, (float)i / (n - 1)) * Mathf.PI;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var j = new List<Vector3>
                {
                    new Vector3(dir.x * 0.06f, 1.74f, dir.z * 0.06f - 0.01f),
                    new Vector3(dir.x * 0.128f, 1.64f, dir.z * 0.13f - 0.01f),
                    new Vector3(dir.x * 0.135f, 1.54f, dir.z * 0.135f - 0.015f),
                    new Vector3(dir.x * 0.112f, endY, dir.z * 0.112f - 0.012f), // ends curl in
                };
                HeroHair.Lock(b, j, 0.06f, 0.012f, dir, 0.42f, 6, 2, 0.9f);
            }
        }

        static void Long(HeroBuild b, float len)
        {
            b.Hair();
            float endY = Mathf.Lerp(1.2f, 0.85f, len);
            const int n = 7;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / (n - 1);
                float a = Mathf.Lerp(0.56f, 1.44f, u) * Mathf.PI;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float side = Mathf.Abs(Mathf.Cos(a)), spread = Mathf.Sin(a);
                var j = new List<Vector3>
                {
                    new Vector3(dir.x * 0.07f, 1.73f, dir.z * 0.07f - 0.01f),
                    new Vector3(dir.x * 0.126f, 1.58f, dir.z * 0.13f - 0.02f),
                    new Vector3(spread * 0.17f, 1.38f, -0.10f - 0.13f * side),
                    new Vector3(spread * 0.2f, Mathf.Lerp(1.38f, endY, 0.5f), -0.12f - 0.15f * side),
                    new Vector3(spread * 0.21f, endY, -0.12f - 0.14f * side),
                };
                HeroHair.Chain(b, "Long" + i, HeroRig.Head, j, 0.32f, 0.03f);
                HeroHair.Lock(b, j, 0.07f, 0.014f, dir, 0.4f, 6, 2, 0.9f);
            }
        }
    }
}
