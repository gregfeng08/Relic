using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The plain cloak (CoatStyle.Cloak, the unwritten wayfarer's): a soft closed body, a rounded
    /// shoulder mantle split at the front under a small clasp, wide bell sleeves, and a closed
    /// two-panel skirt on spring chains with a thin page-edge trim. Also the raised hood (any coat
    /// style): an oval-opening shell around the head with a darker lining, a rolled edge, and a soft
    /// point at the back that the bookmark ribbon hangs from.
    /// </summary>
    public static class HeroCloak
    {
        public static void Build(HeroBuild b, CoatSpec spec, Color coat, Color dark, Color trim)
        {
            Body(b, coat);
            for (int s = -1; s <= 1; s += 2) Sleeve(b, coat, dark, s);
            Skirt(b, spec, coat, trim);
            Mantle(b, coat, dark, trim);
        }

        static void Body(HeroBuild b, Color coat)
        {
            b.Toon(coat);
            b.Kit.Weigh = HeroCoat.ShoulderAware;
            var r = new List<Ring>
            {
                new Ring(new Vector3(0f, 0.985f, 0.0f), 0.138f, 0.104f),
                new Ring(new Vector3(0f, 1.05f, 0.003f), 0.133f, 0.102f),
                new Ring(new Vector3(0f, 1.13f, 0.006f), 0.139f, 0.106f),
                new Ring(new Vector3(0f, 1.22f, 0.008f), 0.150f, 0.110f),
                new Ring(new Vector3(0f, 1.30f, 0.006f), 0.157f, 0.107f),
                new Ring(new Vector3(0f, 1.36f, 0.0f), 0.150f, 0.098f),
                new Ring(new Vector3(0f, 1.41f, -0.005f), 0.105f, 0.078f),
                new Ring(new Vector3(0f, 1.44f, -0.007f), 0.074f, 0.066f),
            };
            b.Kit.Loft(r, 18, false, false);
        }

        static void Sleeve(HeroBuild b, Color coat, Color dark, int s)
        {
            float x = HeroRig.ShoulderX * s;
            b.Arm(s);
            b.Toon(coat);
            // Soft bell sleeves that swallow the wrists.
            var r = new List<Ring>
            {
                new Ring(new Vector3(x - 0.006f * s, 1.41f, -0.01f), 0.057f, 0.061f),
                new Ring(new Vector3(x + 0.002f * s, 1.33f, -0.01f), 0.060f, 0.062f),
                new Ring(new Vector3(x + 0.002f * s, 1.21f, -0.01f), 0.056f, 0.058f),
                new Ring(new Vector3(x, 1.09f, -0.008f), 0.058f, 0.060f),
                new Ring(new Vector3(x, 0.98f, -0.01f), 0.066f, 0.068f),
                new Ring(new Vector3(x, 0.895f, -0.01f), 0.075f, 0.077f),
            };
            b.Kit.Loft(r, 14, true, false);
            b.Toon(dark, 0.6f);
            var lining = new List<Ring> { r[3], r[4], r[5] };
            b.Kit.Loft(lining, 14, false, false, 0f, Mathf.PI * 2f, true, true, 0.006f);
        }

        static void Skirt(HeroBuild b, CoatSpec spec, Color coat, Color trim)
        {
            float len = spec.Length;
            float hem = Mathf.Lerp(0.86f, 0.41f, len);
            float flare = Mathf.Lerp(0.19f, 0.27f, len);
            var d = new SkirtDef { Name = "Cloak", GapTop = 0.03f, GapHem = 0.09f, ChainsPerPanel = 4, Cols = 10, Stiffness = 0.72f };
            const int n = 6;
            d.Y = new float[n]; d.Rx = new float[n]; d.Rz = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                d.Y[i] = Mathf.Lerp(0.995f, hem, t);
                d.Rx[i] = Mathf.Lerp(0.14f, flare, Mathf.Pow(t, 0.85f));
                d.Rz[i] = Mathf.Lerp(0.106f, flare * 0.86f, Mathf.Pow(t, 0.85f));
            }
            var w = HeroSkirt.Build(b, d, coat);
            for (int s = -1; s <= 1; s += 2)
                HeroSkirt.Patch(b, d, s, w[s < 0 ? 0 : 1], n - 1.12f, n - 1, 0f, 1f, 0.004f, trim, 1f);
        }

        /// <summary>Mantle weights: the chest, with the outer edges partly riding the upper arms.</summary>
        static BoneWeight MantleWeight(Vector3 p)
        {
            BoneWeight torso = HeroBuild.TorsoWeight(p);
            float t = Mathf.Clamp01((Mathf.Abs(p.x) - 0.13f) / 0.09f);
            t = t * t * (3f - 2f * t) * 0.45f;
            if (t <= 0f) return torso;
            var arm = MeshKit.W1(HeroRig.UpperArm(p.x < 0f ? -1 : 1));
            return MeshKit.Blend(torso, arm, t);
        }

        static void Mantle(HeroBuild b, Color coat, Color dark, Color trim)
        {
            b.Toon(coat);
            b.Kit.Weigh = MantleWeight;
            var m = new List<Ring>
            {
                new Ring(new Vector3(0f, 1.452f, -0.012f), 0.084f, 0.078f),
                new Ring(new Vector3(0f, 1.43f, -0.010f), 0.136f, 0.112f),
                new Ring(new Vector3(0f, 1.398f, -0.008f), 0.192f, 0.134f),
                new Ring(new Vector3(0f, 1.352f, -0.006f), 0.229f, 0.146f),
                new Ring(new Vector3(0f, 1.305f, -0.004f), 0.243f, 0.153f),
                new Ring(new Vector3(0f, 1.272f, -0.003f), 0.247f, 0.156f),
            };
            const float gap = 0.035f;
            HeroCoat.Shell(b, m, 22, gap, Mathf.PI * 2f - gap, MantleWeight);
            // A thin page-edge trim along the mantle's hem.
            b.Toon(trim, 1f);
            Ring hemRing = m[m.Count - 1];
            var pts = new List<Vector3>();
            const int k = 28;
            for (int i = 0; i <= k; i++) pts.Add(hemRing.At(Mathf.Lerp(gap + 0.02f, Mathf.PI * 2f - gap - 0.02f, (float)i / k), -0.004f) + Vector3.up * 0.004f);
            b.Kit.Path(pts, HeroGear.Fill(pts.Count, 0.0045f), 4, 0.5f, Vector3.up);
            // A small round clasp where the mantle closes.
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            b.Toon(trim * 0.9f + Color.black * 0.1f, 1f);
            b.Kit.Xf = Matrix4x4.TRS(new Vector3(0f, 1.405f, 0.128f), Quaternion.Euler(-28f, 0f, 0f), Vector3.one);
            b.Kit.Ellipsoid(Vector3.zero, new Vector3(0.017f, 0.017f, 0.007f), 12, 4);
            b.Toon(dark, 0.8f);
            b.Kit.Ellipsoid(new Vector3(0f, 0f, 0.004f), new Vector3(0.008f, 0.008f, 0.005f), 10, 3);
            b.Kit.Xf = Matrix4x4.identity;
        }

        // The raised hood, in authoring space (HeroShape maps it with the head above the neck).
        static readonly Vector3 HoodC = new Vector3(0f, HeroRig.HeadCenterY + 0.008f, -0.014f);
        static readonly Vector3 HoodR = new Vector3(0.152f, 0.174f, 0.16f);
        const float Tilt = 10f * Mathf.Deg2Rad;          // the face opening looks slightly down
        const float OpenTop = 63f, OpenSide = 50f;       // opening half-angles (degrees)
        static readonly Vector3 TipPull = new Vector3(0f, 0.04f, -0.085f);

        /// <summary>
        /// A point on the hood: u = 0 at the face opening .. 1 at the back point; phi around the face
        /// axis (0 = up, pi/2 = the character's right). shrink insets the lining.
        /// </summary>
        public static Vector3 HoodPoint(float u, float phi, float shrink = 0f)
        {
            float sp = Mathf.Sin(phi);
            float th0 = Mathf.Lerp(OpenTop, OpenSide, sp * sp) * Mathf.Deg2Rad;
            float th = Mathf.Lerp(th0, Mathf.PI, u);
            var f = new Vector3(0f, -Mathf.Sin(Tilt), Mathf.Cos(Tilt));
            var up = new Vector3(0f, Mathf.Cos(Tilt), Mathf.Sin(Tilt));
            Vector3 d = f * Mathf.Cos(th) + (up * Mathf.Cos(phi) + Vector3.right * sp) * Mathf.Sin(th);
            Vector3 p = HoodC + Vector3.Scale(d, HoodR - Vector3.one * shrink);
            float k = HeroHair.Smooth(0.5f, 1f, u);
            return p + TipPull * (k * k) * (1f - shrink * 6f);
        }

        /// <summary>The soft point at the back of the raised hood (authoring space).</summary>
        public static Vector3 HoodTip => HoodPoint(1f, 0f);

        public static void Hood(HeroBuild b, Color coat, Color lining, Color trim)
        {
            const int rows = 12, cols = 28;
            const float shrink = 0.009f;
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            int[] first = new int[2];
            for (int layer = 0; layer < 2; layer++)
            {
                bool inner = layer == 1;
                float inset = inner ? shrink : 0f;
                // The lining has no outline: its hull would push in toward the face.
                if (inner) b.Toon(lining, 0f); else b.Toon(coat);
                first[layer] = b.Kit.VertexCount;
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        b.Kit.Vert(HoodPoint(U(i, rows), Phi(j, cols), inset));
                int pole = b.Kit.Vert(HoodPoint(1f, 0f, inset));
                float sign = inner ? -1f : 1f;
                for (int i = 0; i < rows - 1; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        int j1 = (j + 1) % cols;
                        int a = first[layer] + i * cols + j, bb = first[layer] + i * cols + j1;
                        int c = first[layer] + (i + 1) * cols + j1, dd = first[layer] + (i + 1) * cols + j;
                        Vector3 mid = HoodPoint((U(i, rows) + U(i + 1, rows)) * 0.5f, Phi(j + 0.5f, cols), inset);
                        b.Kit.Quad(a, bb, c, dd, (mid - HoodC) * sign);
                    }
                int last = first[layer] + (rows - 1) * cols;
                for (int j = 0; j < cols; j++)
                {
                    Vector3 o = HoodPoint(0.98f, Phi(j + 0.5f, cols), inset) - HoodC;
                    b.Kit.Tri(last + j, last + (j + 1) % cols, pole, o * sign);
                }
            }
            // Close the edge between the shell and its lining, facing out of the opening.
            var face = new Vector3(0f, -Mathf.Sin(Tilt), Mathf.Cos(Tilt));
            for (int j = 0; j < cols; j++)
            {
                int j1 = (j + 1) % cols;
                b.Kit.Quad(first[0] + j, first[0] + j1, first[1] + j1, first[1] + j, face);
            }
            // A rolled edge around the face.
            b.Toon(trim, 0.9f);
            var rim = new List<Vector3>();
            for (int j = 0; j <= cols; j++)
            {
                float phi = Phi(j, cols);
                rim.Add(Vector3.Lerp(HoodPoint(0f, phi, 0f), HoodPoint(0f, phi, shrink), 0.5f));
            }
            b.Kit.Path(MeshKit.Spline(rim, 2), HeroGear.Fill(rim.Count * 2 - 1, 0.0062f), 5, 0.75f, face);
        }

        // Rows bunch slightly toward the opening (where the shape changes most).
        static float U(int i, int rows) => Mathf.Pow((float)i / rows, 1.1f);
        // phi = pi (straight down, under the chin) is the seam-free start of the loop.
        static float Phi(float j, int cols) => Mathf.PI + Mathf.PI * 2f * j / cols;
    }
}
