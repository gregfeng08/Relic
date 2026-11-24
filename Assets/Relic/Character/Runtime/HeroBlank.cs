using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The blank body (HeroAppearance.Blank, the unwritten wayfarer): a small, soft, faceless
    /// figure in one plain colour (Body.SkinTone) on the toon material, on HeroShape's small
    /// proportions (about 3.5 heads, 1.27 m). A big round head sitting straight on the shoulders,
    /// a short pear-shaped body, short rounded legs and arms that taper softly into mittens, small
    /// rounded feet. No face, hair or clothes. Arms run into the mittens and legs into the body in
    /// single lofts, so there are no wrist or hip seams. Skinned to the same rig as every other
    /// look, so the Humanoid avatar and the Animator drive it unchanged.
    /// Shapes are written in final (remapped) heights and converted with HeroShape.AuthorY.
    /// </summary>
    public static class HeroBlank
    {
        /// <summary>The head's radius in authoring space (times HeroShape.HeadScale in the final body).</summary>
        public const float HeadRadius = 0.132f;

        public static void Build(HeroBuild b)
        {
            Color c = b.SkinColor;
            Head(b, c);
            Torso(b, c);
            for (int s = -1; s <= 1; s += 2)
            {
                Arm(b, c, s);
                Leg(b, c, s);
            }
        }

        static float A(HeroBuild b, float finalY) => b.Shape.AuthorY(finalY);

        static Ring R(HeroBuild b, float x, float finalY, float z, float rx, float rz) => new Ring(new Vector3(x, A(b, finalY), z), rx, rz);

        /// <summary>
        /// An ellipsoid (center and radii in final proportions when final, else authoring). The end
        /// rings are pulled in so the loft's pole caps land on the surface: round, no points.
        /// </summary>
        static void Blob(HeroBuild b, Vector3 c, Vector3 r, int sides = 14, int stacks = 10, bool final = true)
        {
            var rings = new List<Ring>();
            for (int i = 1; i < stacks; i++)
            {
                float t = Mathf.PI * i / stacks;
                float k = Mathf.Sin(t);
                float y = -Mathf.Cos(t);
                if (i == 1 || i == stacks - 1) y = Mathf.Sign(y) * (1f - 0.35f * k);
                float fy = c.y + y * r.y;
                rings.Add(new Ring(new Vector3(c.x, final ? A(b, fy) : fy, c.z), r.x * k, r.z * k));
            }
            b.Kit.Loft(rings, sides, true, true);
        }

        static void Head(HeroBuild b, Color c)
        {
            // Round, slightly wider than tall; authored at head scale (HeroShape.SmallHead maps it to ~0.36 m).
            b.Toon(c, 0.9f);
            b.Kit.PaintData.w = 1f; // no cast-shadow streaks on the blank face
            b.Rigid(HeroRig.Head);
            // A true UV sphere with exact pole vertices at the sides (a loft's caps would bump or flatten).
            const int stacks = 18, sides = 24;
            var center = new Vector3(0f, 1.602f, 0.006f);
            Vector3 P(float t, float a) => center + new Vector3(-Mathf.Cos(t) * HeadRadius, Mathf.Sin(t) * Mathf.Cos(a) * 0.128f, Mathf.Sin(t) * Mathf.Sin(a) * 0.128f);
            int first = b.Kit.VertexCount;
            for (int i = 1; i < stacks; i++)
                for (int j = 0; j < sides; j++)
                    b.Kit.Vert(P(Mathf.PI * i / stacks, Mathf.PI * 2f * j / sides));
            int left = b.Kit.Vert(P(0f, 0f)), right = b.Kit.Vert(P(Mathf.PI, 0f));
            for (int i = 0; i < stacks - 2; i++)
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    Vector3 o = P(Mathf.PI * (i + 1.5f) / stacks, Mathf.PI * 2f * (j + 0.5f) / sides) - center;
                    b.Kit.Quad(first + i * sides + j, first + i * sides + j1, first + (i + 1) * sides + j1, first + (i + 1) * sides + j, o);
                }
            int last = first + (stacks - 2) * sides;
            for (int j = 0; j < sides; j++)
            {
                int j1 = (j + 1) % sides;
                b.Kit.Tri(first + j, first + j1, left, Vector3.left);
                b.Kit.Tri(last + j, last + j1, right, Vector3.right);
            }
        }

        static void Torso(HeroBuild b, Color c)
        {
            // A short, soft pear: widest at the hips, sloping into small shoulders under the head.
            b.Toon(c);
            b.Torso();
            var r = new List<Ring>
            {
                R(b, 0f, 0.42f, 0f, 0.07f, 0.06f),
                R(b, 0f, 0.45f, 0f, 0.13f, 0.106f),
                R(b, 0f, 0.52f, 0.002f, 0.162f, 0.124f),
                R(b, 0f, 0.61f, 0.004f, 0.15f, 0.117f),
                R(b, 0f, 0.70f, 0.004f, 0.134f, 0.106f),
                R(b, 0f, 0.78f, 0.002f, 0.124f, 0.097f),
                R(b, 0f, 0.845f, 0f, 0.114f, 0.09f),
                R(b, 0f, 0.885f, 0f, 0.096f, 0.08f),
                R(b, 0f, 0.915f, 0f, 0.07f, 0.064f),
                R(b, 0f, 0.94f, 0f, 0.05f, 0.05f),
            };
            b.Kit.Loft(r, 22, true, false);
        }

        static void Arm(HeroBuild b, Color c, int s)
        {
            float x = HeroRig.ShoulderX * s;
            b.Toon(c);
            b.Arm(s);
            // A round shoulder riding the upper arm (one bone: it never shears into a point).
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.UpperArm(s));
            Blob(b, new Vector3(x * 0.94f, 0.812f, -0.006f), new Vector3(0.056f, 0.056f, 0.056f));
            b.Arm(s);
            // Arm and mitten in one loft: tapering softly, then the mitten (thin across the palm).
            var r = new List<Ring>
            {
                R(b, x, 0.80f, -0.006f, 0.049f, 0.051f),
                R(b, x, 0.78f, -0.006f, 0.05f, 0.052f),
                R(b, x, 0.70f, -0.006f, 0.046f, 0.048f),
                R(b, x, 0.62f, -0.006f, 0.042f, 0.044f),
                R(b, x, 0.55f, -0.006f, 0.04f, 0.041f),
                R(b, x, 0.485f, -0.006f, 0.036f, 0.038f),
                R(b, x, 0.445f, -0.004f, 0.034f, 0.039f),
                R(b, x, 0.415f, 0f, 0.033f, 0.046f),
                R(b, x, 0.385f, 0.002f, 0.033f, 0.048f),
                R(b, x, 0.355f, 0.002f, 0.029f, 0.043f),
                R(b, x, 0.333f, 0.001f, 0.02f, 0.03f),
                R(b, x, 0.322f, 0f, 0.009f, 0.014f),
            };
            b.Kit.Loft(r, 14, true, true);
            // The mitten's thumb, on the front.
            b.Rigid(HeroRig.Hand(s));
            Blob(b, new Vector3(x - 0.006f * s, 0.408f, 0.038f), new Vector3(0.017f, 0.024f, 0.018f), 10, 8);
        }

        static void Leg(HeroBuild b, Color c, int s)
        {
            float x = HeroRig.LegX * s;
            b.Toon(c);
            b.Leg(s);
            // Short, rounded legs starting inside the body.
            var r = new List<Ring>
            {
                R(b, x * 0.75f, 0.53f, 0f, 0.066f, 0.07f),
                R(b, x * 0.9f, 0.45f, 0f, 0.074f, 0.078f),
                R(b, x, 0.38f, 0.002f, 0.068f, 0.071f),
                R(b, x, 0.30f, 0.004f, 0.06f, 0.063f),
                R(b, x, 0.235f, 0.004f, 0.056f, 0.059f),
                R(b, x, 0.17f, 0.002f, 0.054f, 0.057f),
                R(b, x, 0.11f, 0f, 0.05f, 0.053f),
                R(b, x, 0.07f, 0f, 0.047f, 0.05f),
                R(b, x, 0.05f, 0f, 0.043f, 0.046f),
            };
            b.Kit.Loft(r, 14, false, false);
            // A small rounded foot.
            b.Kit.Weigh = p => MeshKit.W1(HeroRig.Foot(s));
            Blob(b, new Vector3(x, 0.042f, 0.026f), new Vector3(0.05f, 0.042f, 0.082f), 14, 10);
        }
    }
}
