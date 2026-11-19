using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The blank body (HeroAppearance.Blank, the unwritten wayfarer): a smooth, faceless humanoid
    /// in one plain colour (Body.SkinTone) on the toon material. No face, hair or clothes: an egg
    /// head, neck, torso with rounded shoulders, arms, simple hands, legs and feet, skinned to the
    /// same rig as every other look, so the Humanoid avatar and Animator drive it unchanged.
    /// Part seams sit inside neighbouring parts so the outline reads one clean silhouette.
    /// </summary>
    public static class HeroBlank
    {
        public static void Build(HeroBuild b)
        {
            Color c = b.SkinColor;
            Head(b, c);
            Torso(b, c);
            for (int s = -1; s <= 1; s += 2)
            {
                Arm(b, c, s);
                HeroBody.Hand(b, s, true);
                Leg(b, c, s);
                Foot(b, c, s);
            }
        }

        static void Head(HeroBuild b, Color c)
        {
            b.Toon(c, 0.9f);
            b.Kit.NormalBend = 0.5f;
            b.Kit.PaintData.w = 1f; // no cast-shadow streaks on the blank face
            b.Rigid(HeroRig.Head);
            // A smooth egg, a little narrower toward the chin.
            var rings = new List<Ring>();
            const int stacks = 14;
            for (int i = 1; i < stacks; i++)
            {
                float t = (float)i / stacks;
                float k = Mathf.Sin(Mathf.PI * t);
                float taper = Mathf.Lerp(0.84f, 1f, Mathf.SmoothStep(0f, 1f, t * 1.6f));
                float y = HeroRig.HeadCenterY + (-Mathf.Cos(Mathf.PI * t)) * 0.122f;
                rings.Add(new Ring(new Vector3(0f, y, 0.002f), 0.1f * k * taper, 0.108f * k * taper));
            }
            b.Kit.Loft(rings, 20, true, true);
            b.Toon(c, 1f);
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            b.Kit.Tube(new Vector3(0f, 1.39f, -0.005f), new Vector3(0f, 1.53f, -0.002f), 0.047f, 0.043f, 12);
        }

        static void Torso(HeroBuild b, Color c)
        {
            b.Toon(c);
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Hips);
            b.Kit.Ellipsoid(new Vector3(0f, 0.895f, -0.004f), new Vector3(0.132f, 0.098f, 0.1f), 18, 10);
            b.Torso();
            var r = new List<Ring>
            {
                new Ring(new Vector3(0f, 0.91f, -0.004f), 0.118f, 0.088f),
                new Ring(new Vector3(0f, 0.97f, 0f), 0.124f, 0.092f),
                new Ring(new Vector3(0f, 1.05f, 0.003f), 0.113f, 0.085f),
                new Ring(new Vector3(0f, 1.15f, 0.006f), 0.121f, 0.09f),
                new Ring(new Vector3(0f, 1.25f, 0.008f), 0.137f, 0.096f),
                new Ring(new Vector3(0f, 1.33f, 0.004f), 0.152f, 0.094f),
                new Ring(new Vector3(0f, 1.385f, -0.002f), 0.140f, 0.084f),
                new Ring(new Vector3(0f, 1.42f, -0.004f), 0.09f, 0.066f),
                new Ring(new Vector3(0f, 1.445f, -0.004f), 0.052f, 0.05f),
            };
            b.Kit.Loft(r, 22, false, false);
        }

        static void Arm(HeroBuild b, Color c, int s)
        {
            float x = HeroRig.ShoulderX * s;
            b.Toon(c);
            // A rounded shoulder cap that bends between the chest and the upper arm.
            b.Arm(s);
            b.Kit.Ellipsoid(new Vector3(x - 0.012f * s, 1.37f, -0.008f), new Vector3(0.058f, 0.05f, 0.058f), 14, 8);
            var r = new List<Ring>
            {
                new Ring(new Vector3(x, 1.38f, -0.01f), 0.055f, 0.057f),
                new Ring(new Vector3(x, 1.29f, -0.01f), 0.052f, 0.054f),
                new Ring(new Vector3(x, 1.20f, -0.01f), 0.047f, 0.049f),
                new Ring(new Vector3(x, 1.115f, -0.01f), 0.041f, 0.043f),
                new Ring(new Vector3(x, 1.03f, -0.01f), 0.042f, 0.043f),
                new Ring(new Vector3(x, 0.95f, -0.01f), 0.036f, 0.037f),
                new Ring(new Vector3(x, 0.88f, -0.01f), 0.029f, 0.031f),
            };
            b.Kit.Loft(r, 14, false, false);
        }

        static void Leg(HeroBuild b, Color c, int s)
        {
            float x = HeroRig.LegX * s;
            b.Toon(c);
            b.Leg(s);
            var r = new List<Ring>
            {
                new Ring(new Vector3(x * 0.9f, 0.91f, -0.004f), 0.074f, 0.08f),
                new Ring(new Vector3(x, 0.80f, 0f), 0.074f, 0.078f),
                new Ring(new Vector3(x, 0.64f, 0.004f), 0.061f, 0.065f),
                new Ring(new Vector3(x, 0.50f, 0.008f), 0.052f, 0.055f),
                new Ring(new Vector3(x, 0.40f, 0.002f), 0.055f, 0.06f),
                new Ring(new Vector3(x, 0.26f, -0.004f), 0.043f, 0.046f),
                new Ring(new Vector3(x, 0.14f, -0.008f), 0.036f, 0.039f),
                new Ring(new Vector3(x, 0.085f, -0.01f), 0.035f, 0.037f),
            };
            b.Kit.Loft(r, 14, false, false);
        }

        static void Foot(HeroBuild b, Color c, int s)
        {
            float x = HeroRig.LegX * s;
            b.Toon(c);
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Foot(s));
            float[] zs = { -0.056f, -0.03f, 0.03f, 0.09f, 0.13f, 0.152f };
            float[] ws = { 0.036f, 0.045f, 0.047f, 0.045f, 0.037f, 0.024f };
            float[] hs = { 0.044f, 0.05f, 0.042f, 0.029f, 0.023f, 0.016f };
            float[] cy = { 0.058f, 0.062f, 0.05f, 0.036f, 0.03f, 0.027f };
            var foot = new List<Ring>();
            for (int i = 0; i < zs.Length; i++)
                foot.Add(new Ring(new Vector3(x, cy[i], zs[i]), Vector3.right, Vector3.up, ws[i], hs[i], 3f));
            b.Kit.Loft(foot, 12, true, true);
        }
    }
}
