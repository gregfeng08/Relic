using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Head and face, Genshin-cute: a soft face narrowing to a gentle pointed chin, big eyes with
    /// a gradient iris, a dark upper lash line and two highlights, a hint of a nose, a tiny mouth
    /// and blush. Face lighting normals are bent forward so the cel shadow doesn't streak across it.
    /// </summary>
    public static class HeroFace
    {
        public static void Build(HeroBuild b)
        {
            Head(b);
            Eyes(b);
            Details(b);
        }

        /// <summary>Head profile at height fraction t (0 chin, 1 crown): width/depth factors and center z.</summary>
        static void Profile(float t, out float kx, out float kz, out float cz)
        {
            t = Mathf.Clamp01(t);
            float k = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.68f);
            // Full cheeks, narrowing to a small soft point at the chin.
            float jaw = t < 0.55f ? Mathf.Lerp(0.5f, 1f, Mathf.Pow(Mathf.SmoothStep(0f, 1f, t / 0.55f), 0.8f)) : 1f;
            kx = k * jaw;
            kz = k * Mathf.Lerp(0.88f, 1f, jaw);
            cz = Mathf.Lerp(0.012f, -0.006f, t);
        }

        public static void HeadRings(List<Ring> rings, float grow)
        {
            const int stacks = 14;
            float chin = HeroRig.HeadCenterY - HeroRig.HeadHalfH;
            for (int i = 1; i < stacks; i++)
            {
                float t = (float)i / stacks;
                Profile(t, out float kx, out float kz, out float cz);
                float y = chin + t * HeroRig.HeadHalfH * 2f;
                rings.Add(new Ring(new Vector3(0f, y, cz), (HeroRig.HeadHalfW + grow) * kx, (HeroRig.HeadHalfD + grow) * kz));
            }
        }

        /// <summary>Front surface z of the head at height y and side offset x (for placing face paint).</summary>
        public static float FrontZ(float y, float x)
        {
            float chin = HeroRig.HeadCenterY - HeroRig.HeadHalfH;
            Profile((y - chin) / (HeroRig.HeadHalfH * 2f), out float kx, out float kz, out float cz);
            float rx = HeroRig.HeadHalfW * kx, rz = HeroRig.HeadHalfD * kz;
            float u = Mathf.Clamp01(Mathf.Abs(x) / Mathf.Max(1e-4f, rx));
            return cz + rz * Mathf.Sqrt(1f - u * u);
        }

        static void Head(HeroBuild b)
        {
            b.Skin(0.9f);
            b.Kit.NormalBend = 0.7f;
            b.Kit.PaintData.w = 1f; // the face ignores cast shadows (no hair-shadow streaks)
            b.Rigid(HeroRig.Head);
            var rings = new List<Ring>();
            HeadRings(rings, 0f);
            b.Kit.Loft(rings, 18, true, true);
            b.Skin(1f);
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            b.Kit.Tube(new Vector3(0f, 1.40f, -0.005f), new Vector3(0f, 1.52f, -0.002f), 0.034f, 0.031f, 10);
        }

        static void Eyes(HeroBuild b)
        {
            FaceSpec f = b.App.Face;
            b.Rigid(HeroRig.Head);
            Color iris = HeroBuild.C(f.EyeColor);
            Color irisDark = iris * 0.45f, irisLight = Color.Lerp(iris, Color.white, 0.35f);
            irisDark.a = irisLight.a = 1f;
            var lash = new Color(0.22f, 0.13f, 0.13f);
            float size = f.EyeSize;
            float hScale = f.EyeShape == EyeShape.Round ? 1.04f : f.EyeShape == EyeShape.Sharp ? 0.84f : 1f;
            float wScale = f.EyeShape == EyeShape.Round ? 0.96f : f.EyeShape == EyeShape.Sharp ? 1.06f : 1f;
            float tilt = f.EyeShape == EyeShape.Sharp ? 7f : f.EyeShape == EyeShape.Round ? 0f : 3f;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = 0.04f * s, y = 1.574f;
                float z = FrontZ(y, x) - 0.0045f;
                b.Kit.Xf = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, 20f * s, tilt * s), new Vector3(size * wScale, size * hScale, 1f));
                b.Flat(new Color(1f, 0.98f, 0.96f));
                b.Kit.Ellipsoid(Vector3.zero, new Vector3(0.019f, 0.023f, 0.0068f), 18, 3);
                // Gradient iris: base, a dark upper cap, a light lower crescent, the pupil.
                b.Flat(iris);
                b.Kit.Ellipsoid(new Vector3(0.001f * -s, -0.002f, 0.0034f), new Vector3(0.0155f, 0.0195f, 0.0045f), 18, 3);
                b.Flat(irisDark);
                b.Kit.Ellipsoid(new Vector3(0.001f * -s, 0.0065f, 0.0047f), new Vector3(0.0138f, 0.0105f, 0.0042f), 12, 3);
                b.Flat(irisLight);
                b.Kit.Ellipsoid(new Vector3(0.001f * -s, -0.0115f, 0.0047f), new Vector3(0.0095f, 0.0055f, 0.0042f), 10, 3);
                b.Flat(irisDark * 0.6f + new Color(0f, 0f, 0f, 1f));
                b.Kit.Ellipsoid(new Vector3(0.001f * -s, 0.001f, 0.0059f), new Vector3(0.0065f, 0.009f, 0.0036f), 10, 3);
                b.Flat(Color.white, 1.15f);
                b.Kit.Ellipsoid(new Vector3(0.0055f * s, 0.008f, 0.0078f), new Vector3(0.0046f, 0.0054f, 0.002f), 8, 3);
                b.Kit.Ellipsoid(new Vector3(-0.0045f * s, -0.009f, 0.0076f), new Vector3(0.0021f, 0.0021f, 0.0018f), 6, 3);
                // Upper lash line: dark, thick in the middle, a small flick at the outer corner.
                b.Flat(lash);
                var pts = new List<Vector3>();
                var rad = new List<float>();
                for (int i = 0; i <= 7; i++)
                {
                    float a = Mathf.Lerp(-1.15f, 1.3f, i / 7f);
                    pts.Add(new Vector3(Mathf.Sin(a) * 0.0195f * s, Mathf.Cos(a) * 0.0185f + 0.0005f + (i == 7 ? 0.003f : 0f), 0.0078f));
                    rad.Add(Mathf.Lerp(0.0015f, 0.0034f, Mathf.Sin(Mathf.PI * i / 7f)));
                }
                b.Kit.Path(pts, rad, 4, 0.6f, Vector3.forward);
            }
            b.Kit.Xf = Matrix4x4.identity;
        }

        static void Details(HeroBuild b)
        {
            b.Rigid(HeroRig.Head);
            Color skin = b.SkinColor;
            // Blush.
            b.Flat(Color.Lerp(skin, new Color(1f, 0.52f, 0.55f), 0.6f));
            for (int s = -1; s <= 1; s += 2)
            {
                float bx = 0.058f * s, by = 1.547f;
                b.Kit.Xf = Matrix4x4.TRS(new Vector3(bx, by, FrontZ(by, bx) - 0.001f), Quaternion.Euler(0f, 32f * s, 0f), Vector3.one);
                b.Kit.Ellipsoid(Vector3.zero, new Vector3(0.016f, 0.0075f, 0.0035f), 12, 3);
            }
            b.Kit.Xf = Matrix4x4.identity;
            // A hint of a nose: a tiny soft shadow.
            b.Flat(new Color(skin.r * 0.88f, skin.g * 0.74f, skin.b * 0.70f));
            float ny = 1.547f;
            b.Kit.Ellipsoid(new Vector3(0.0015f, ny, FrontZ(ny, 0f) - 0.0012f), new Vector3(0.0028f, 0.0018f, 0.0018f), 6, 4);
            // A tiny smile.
            float my = 1.522f;
            b.Flat(new Color(0.62f, 0.30f, 0.30f));
            var mouth = new List<Vector3>();
            for (int i = 0; i <= 4; i++)
            {
                float mx = Mathf.Lerp(-0.0075f, 0.0075f, i / 4f);
                float yy = my - 0.0025f * Mathf.Sin(Mathf.PI * i / 4f);
                mouth.Add(new Vector3(mx, yy, FrontZ(yy, mx) - 0.0006f));
            }
            b.Kit.Path(mouth, new List<float> { 0.001f, 0.0014f, 0.0015f, 0.0014f, 0.001f }, 4, 0.7f, Vector3.forward);
        }
    }
}
