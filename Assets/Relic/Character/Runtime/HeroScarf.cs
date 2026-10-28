using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>The red scarf: wrapped high and bulky around the neck, two tails trailing behind on chains.</summary>
    public static class HeroScarf
    {
        public static void Build(HeroBuild b)
        {
            Rgb red = MaterialPalette.Scarf;
            b.Toon(red, 1.6f);
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            // A puffy cowl wrapped high around the neck, with one wrap line across it.
            var cowl = new List<Ring>
            {
                new Ring(new Vector3(0f, 1.405f, 0.004f), 0.104f, 0.100f),
                new Ring(new Vector3(0f, 1.432f, 0.010f), 0.118f, 0.114f),
                new Ring(new Vector3(0f, 1.468f, 0.010f), 0.110f, 0.107f),
                new Ring(new Vector3(0f, 1.50f, 0.006f), 0.094f, 0.092f),
                new Ring(new Vector3(0f, 1.518f, 0.004f), 0.080f, 0.078f),
            };
            b.Kit.Loft(cowl, 14, false, false);
            b.Kit.Loft(cowl, 14, false, false, 0f, Mathf.PI * 2f, true, true, 0.014f);
            b.Toon(red.Scale(0.88f), 1.4f);
            b.Kit.Torus(new Vector3(0f, 1.452f, 0.012f), new Vector3(0.12f, 1f, 0.18f), 0.116f, 0.112f, 0.014f, 16, 5, 1.1f);
            b.Toon(red, 1.6f);
            // A soft knot where the tails come out (back right).
            b.Kit.Ellipsoid(new Vector3(0.066f, 1.45f, -0.105f), new Vector3(0.036f, 0.032f, 0.03f), 10, 6);
            Tail(b, new[]
            {
                new Vector3(0.068f, 1.445f, -0.115f), new Vector3(0.085f, 1.37f, -0.19f), new Vector3(0.10f, 1.27f, -0.25f),
                new Vector3(0.11f, 1.16f, -0.29f), new Vector3(0.115f, 1.05f, -0.31f),
            }, 0.036f);
            Tail(b, new[]
            {
                new Vector3(0.05f, 1.44f, -0.115f), new Vector3(0.05f, 1.36f, -0.20f), new Vector3(0.05f, 1.27f, -0.25f),
                new Vector3(0.05f, 1.18f, -0.29f),
            }, 0.032f);
        }

        static void Tail(HeroBuild b, Vector3[] joints, float width)
        {
            ChainDef c = b.Chain("ScarfTail", HeroRig.Neck, joints, "scarf");
            c.Stiffness = 0.25f;
            c.Drag = 0.3f;
            c.Radius = 0.025f;
            b.Kit.Weigh = p => b.ChainWeight(c, HeroRig.Neck, p);
            List<Vector3> pts = MeshKit.Spline(joints, 3);
            var rad = new List<float>();
            for (int i = 0; i < pts.Count; i++)
            {
                float t = (float)i / (pts.Count - 1);
                rad.Add(width * Mathf.Lerp(0.8f, 1.05f, t) * (i == pts.Count - 1 ? 0.7f : 1f));
            }
            b.Kit.Path(pts, rad, 6, 0.28f, Vector3.back);
        }
    }
}
