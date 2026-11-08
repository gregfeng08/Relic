using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>The red scarf: wrapped high and bulky around the neck, two tails trailing behind on chains.</summary>
    public static class HeroScarf
    {
        public static void Build(HeroBuild b)
        {
            if (!b.App.Outfit.Scarf.On) return;
            Color red = HeroBuild.C(b.App.Outfit.Scarf.Color);
            b.Toon(red, 1.6f);
            b.Kit.Weigh = HeroBuild.TorsoWeight;
            // A puffy cowl wrapped high around the neck, with one wrap line across it.
            var cowl = new List<Ring>
            {
                new Ring(new Vector3(0f, 1.392f, 0.004f), 0.098f, 0.094f),
                new Ring(new Vector3(0f, 1.414f, 0.008f), 0.114f, 0.110f),
                new Ring(new Vector3(0f, 1.442f, 0.010f), 0.118f, 0.114f),
                new Ring(new Vector3(0f, 1.468f, 0.010f), 0.109f, 0.106f),
                new Ring(new Vector3(0f, 1.488f, 0.007f), 0.094f, 0.091f),
                new Ring(new Vector3(0f, 1.50f, 0.005f), 0.079f, 0.077f),
            };
            b.Kit.Loft(cowl, 20, false, false);
            b.Kit.Loft(cowl, 20, false, false, 0f, Mathf.PI * 2f, true, true, 0.014f);
            b.Toon(red * 0.88f + Color.black, 1.4f);
            b.Kit.Torus(new Vector3(0f, 1.44f, 0.012f), new Vector3(0.12f, 1f, 0.18f), 0.117f, 0.113f, 0.012f, 18, 6, 1.1f);
            b.Toon(red, 1.6f);
            // A soft knot at the front right; the tails hang down the chest and stream back when running.
            b.Kit.Ellipsoid(new Vector3(0.066f, 1.425f, 0.088f), new Vector3(0.034f, 0.03f, 0.028f), 12, 6);
            Tail(b, red, new[]
            {
                new Vector3(0.07f, 1.425f, 0.10f), new Vector3(0.085f, 1.35f, 0.122f), new Vector3(0.092f, 1.26f, 0.13f),
                new Vector3(0.095f, 1.17f, 0.128f), new Vector3(0.098f, 1.09f, 0.122f),
            }, 0.036f);
            Tail(b, red, new[]
            {
                new Vector3(0.052f, 1.42f, 0.10f), new Vector3(0.056f, 1.35f, 0.125f), new Vector3(0.058f, 1.27f, 0.132f),
                new Vector3(0.06f, 1.20f, 0.13f),
            }, 0.032f);
        }

        static void Tail(HeroBuild b, Color red, Vector3[] joints, float width)
        {
            b.Toon(red, 1.6f);
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
            b.Kit.Path(pts, rad, 6, 0.32f, Vector3.forward);
        }
    }
}
