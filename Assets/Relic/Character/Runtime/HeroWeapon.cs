using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The equipped weapon, kitbashed from its ItemVisual parts (blade, crossguard, grip, pommel,
    /// limb, string, haft, head, orb, cover, binding, clasp, band...) with the item's material,
    /// wear and glow. Stowed on the back / hip / belt strap, or in the right hand when drawn.
    /// Built in weapon space: grip at the origin, the business end along +Y.
    /// </summary>
    public static class HeroWeapon
    {
        public static void Build(HeroBuild b)
        {
            WeaponLook w = b.Look.Weapon;
            if (w == null) return;
            Place(b, w, out int bone, out Matrix4x4 xf);
            b.Rigid(bone);
            b.Kit.Xf = xf;
            switch (w.Kind)
            {
                case WeaponKind.Sword: Sword(b, w, 1f, !b.WeaponDrawn); break;
                case WeaponKind.Greatsword: Sword(b, w, 1.45f, false); break;
                case WeaponKind.Bow: Bow(b, w); break;
                case WeaponKind.Staff: Staff(b, w); break;
                case WeaponKind.Tome: Tome(b, w); break;
            }
            b.Kit.Xf = Matrix4x4.identity;
        }

        static void Place(HeroBuild b, WeaponLook w, out int bone, out Matrix4x4 xf)
        {
            if (b.WeaponDrawn)
            {
                bone = HeroRig.HandR;
                // In the fist, pointing forward (the drawn pose raises the forearm).
                Quaternion q = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                if (w.Kind == WeaponKind.Tome) q = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(90f, 0f, 0f);
                xf = Matrix4x4.TRS(new Vector3(HeroRig.ShoulderX + 0.004f, 0.80f, -0.004f), q, Vector3.one);
                return;
            }
            switch (w.Stow)
            {
                case StowSlot.Hip:
                    bone = HeroRig.Hips;
                    xf = Matrix4x4.TRS(new Vector3(-0.185f, 0.975f, 0.10f), Quaternion.FromToRotation(Vector3.up, new Vector3(-0.12f, -0.78f, -0.62f).normalized), Vector3.one);
                    return;
                case StowSlot.BeltStrap:
                    bone = HeroRig.Hips;
                    xf = Matrix4x4.TRS(new Vector3(0.205f, 0.86f, -0.03f), Quaternion.Euler(0f, 90f, -8f), Vector3.one);
                    return;
                default:
                    bone = HeroRig.Chest;
                    Vector3 c = new Vector3(0f, 1.16f, -0.285f);
                    if (w.Kind == WeaponKind.Greatsword)
                        xf = Matrix4x4.TRS(c + new Vector3(0.13f, 0.24f, 0f), Quaternion.Euler(0f, 0f, 150f), Vector3.one);
                    else if (w.Kind == WeaponKind.Bow)
                        xf = Matrix4x4.TRS(c, Quaternion.Euler(0f, 0f, -38f), Vector3.one);
                    else // staff: center it on the pack, head over the right shoulder
                        xf = Matrix4x4.TRS(c + Quaternion.Euler(0f, 0f, -32f) * new Vector3(0f, -0.06f, 0f), Quaternion.Euler(0f, 0f, -32f), Vector3.one);
                    return;
            }
        }

        static void Sword(HeroBuild b, WeaponLook w, float scale, bool sheathed)
        {
            float gripLen = 0.10f * scale, bladeLen = 0.70f * scale, width = 0.022f * scale;
            b.Toon(w.Secondary, 1.2f);
            b.Kit.Tube(new Vector3(0f, -gripLen * 0.5f, 0f), new Vector3(0f, gripLen * 0.5f, 0f), 0.012f * scale, 0.013f * scale, 6);
            b.Toon(w.Accent, 1.3f);
            if (w.Has("pommel")) b.Kit.Ellipsoid(new Vector3(0f, -gripLen * 0.5f - 0.012f, 0f), new Vector3(0.017f, 0.017f, 0.017f) * scale, 8, 5);
            if (w.Has("crossguard"))
                b.Kit.Path(new List<Vector3> { new Vector3(-0.06f * scale, gripLen * 0.5f + 0.012f, 0f), new Vector3(0f, gripLen * 0.5f + 0.006f, 0f), new Vector3(0.06f * scale, gripLen * 0.5f + 0.012f, 0f) },
                    new List<float> { 0.008f * scale, 0.011f * scale, 0.008f * scale }, 6, 0.8f, Vector3.forward);
            if (w.Has("band")) b.Kit.Torus(new Vector3(0f, gripLen * 0.5f + 0.03f * scale, 0f), Vector3.up, width * 1.15f, 0.012f * scale, 0.005f * scale, 8, 4);
            float y0 = gripLen * 0.5f + 0.012f;
            if (sheathed)
            {
                b.Toon(w.Secondary.Scale(0.85f), 1.3f);
                b.Kit.Path(new List<Vector3> { new Vector3(0f, y0, 0f), new Vector3(0f, y0 + bladeLen * 0.6f, 0f), new Vector3(0f, y0 + bladeLen + 0.02f, 0f) },
                    new List<float> { width * 1.35f, width * 1.3f, width * 0.9f }, 8, 0.45f, Vector3.forward);
                b.Toon(w.Accent, 1.2f);
                b.Kit.Path(new List<Vector3> { new Vector3(0f, y0 + bladeLen - 0.02f, 0f), new Vector3(0f, y0 + bladeLen + 0.03f, 0f) }, new List<float> { width * 1.38f, width * 0.6f }, 8, 0.5f, Vector3.forward);
                return;
            }
            // Blade: flat diamond section, tapering to the point.
            b.Toon(w.Primary, 1.3f);
            var pts = new List<Vector3>();
            var rad = new List<float>();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f;
                pts.Add(new Vector3(0f, y0 + bladeLen * t, 0f));
                rad.Add(i == 6 ? 0.0008f : width * Mathf.Lerp(1f, 0.82f, t) * (t > 0.85f ? Mathf.Lerp(1f, 0.15f, (t - 0.85f) / 0.15f) : 1f));
            }
            b.Kit.Path(pts, rad, 4, 0.22f, Vector3.forward, true, 2f);
            if (w.Glows)
            {
                b.Kit.Use(b.Mats.Glow(w.GlowColor, w.Glow));
                for (int s = -1; s <= 1; s += 2)
                    b.Kit.Tube(new Vector3(0f, y0 + 0.03f, 0.0035f * s), new Vector3(0f, y0 + bladeLen * 0.75f, 0.0035f * s), 0.0035f * scale, 0.002f, 4);
            }
        }

        static void Bow(HeroBuild b, WeaponLook w)
        {
            const float half = 0.62f;
            b.Toon(w.Primary, 1.3f);
            var top = new List<Vector3>();
            var rad = new List<float>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                float curve = Mathf.Sin(t * Mathf.PI * 0.5f) * 0.10f - (t > 0.8f ? (t - 0.8f) * 0.25f : 0f); // recurve at the tips
                top.Add(new Vector3(0f, t * half, -curve));
                rad.Add(Mathf.Lerp(0.016f, 0.006f, t));
            }
            var bottom = new List<Vector3>();
            foreach (Vector3 p in top) bottom.Add(new Vector3(p.x, -p.y, p.z));
            b.Kit.Path(top, rad, 6, 0.6f, Vector3.right);
            b.Kit.Path(bottom, rad, 6, 0.6f, Vector3.right);
            b.Toon(w.Secondary, 1.1f);
            b.Kit.Tube(new Vector3(0f, -0.06f, 0f), new Vector3(0f, 0.06f, 0f), 0.019f, 0.019f, 6);
            b.Toon(new Color(0.9f, 0.88f, 0.8f), 0.8f);
            b.Kit.Tube(top[8], bottom[8], 0.0022f, 0.0022f, 4, false);
            if (w.Glows)
            {
                b.Kit.Use(b.Mats.Glow(w.GlowColor, w.Glow));
                b.Kit.Ellipsoid(new Vector3(0f, 0.075f, 0.012f), new Vector3(0.01f, 0.012f, 0.008f), 8, 4);
            }
        }

        static void Staff(HeroBuild b, WeaponLook w)
        {
            // Haft from y = -0.95 to 0.62, the grip around the origin.
            b.Toon(w.Primary, 1.3f);
            b.Kit.Tube(new Vector3(0f, -0.95f, 0f), new Vector3(0f, 0.62f, 0f), 0.016f, 0.019f, 8);
            b.Toon(w.Accent, 1.2f);
            b.Kit.Tube(new Vector3(0f, -0.97f, 0f), new Vector3(0f, -0.90f, 0f), 0.019f, 0.02f, 8);
            b.Kit.Torus(new Vector3(0f, 0.6f, 0f), Vector3.up, 0.022f, 0.022f, 0.006f, 10, 4);
            // The spiral head from the sheet: the wood curls up and around the orb.
            b.Toon(w.Primary, 1.3f);
            var spiral = new List<Vector3>();
            var rad = new List<float>();
            Vector3 center = new Vector3(0f, 0.76f, 0f);
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 1.6f, t);
                float r = Mathf.Lerp(0.11f, 0.055f, t);
                spiral.Add(center + new Vector3(Mathf.Cos(a) * r * 0.9f, Mathf.Sin(a) * r, 0f));
                rad.Add(Mathf.Lerp(0.02f, 0.008f, t));
            }
            spiral.Insert(0, new Vector3(0f, 0.62f, 0f));
            rad.Insert(0, 0.019f);
            b.Kit.Path(MeshKit.Spline(spiral, 2), Resample(rad, 2), 6, 1f, Vector3.forward);
            // The orb (every staff has one; the sheet draws it held by the spiral).
            {
                if (w.Glows) b.Kit.Use(b.Mats.Glow(w.GlowColor, w.Glow));
                else b.Toon(new Color(0.72f, 0.86f, 0.92f), 1.2f);
                b.Kit.Ellipsoid(center + new Vector3(0.008f, 0.0f, 0f), new Vector3(0.036f, 0.036f, 0.036f), 12, 8);
            }
        }

        static List<float> Resample(List<float> r, int perSeg)
        {
            var o = new List<float>();
            for (int i = 0; i < r.Count - 1; i++)
                for (int k = 0; k < perSeg; k++) o.Add(Mathf.Lerp(r[i], r[i + 1], (float)k / perSeg));
            o.Add(r[r.Count - 1]);
            return o;
        }

        static void Tome(HeroBuild b, WeaponLook w)
        {
            // Book standing on its spine edge along Y: 0.21 tall, 0.16 wide (Z), 0.05 thick (X).
            b.Toon(w.Primary, 1.4f);
            b.Kit.Box(new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.21f, 0.16f), 8f);
            b.Toon(w.Secondary, 1.0f);
            b.Kit.Box(new Vector3(0f, 0f, 0.004f), new Vector3(0.038f, 0.198f, 0.16f), 8f);
            b.Toon(w.Accent, 1.2f);
            if (w.Has("binding")) b.Kit.Box(new Vector3(0f, 0f, -0.078f), new Vector3(0.056f, 0.2f, 0.014f), 6f);
            if (w.Has("clasp")) b.Kit.Box(new Vector3(0f, 0f, 0.082f), new Vector3(0.058f, 0.03f, 0.012f), 4f);
            if (w.Has("chain")) b.Kit.Torus(new Vector3(0f, 0.11f, -0.07f), Vector3.forward, 0.012f, 0.012f, 0.0025f, 8, 4);
            if (w.Glows)
            {
                b.Kit.Use(b.Mats.Glow(w.GlowColor, w.Glow));
                b.Kit.Torus(new Vector3(0.026f, 0f, 0f), Vector3.right, 0.03f, 0.03f, 0.003f, 14, 4);
            }
            // The strap that holds it to the belt.
            b.Toon(MaterialPalette.Cord, 1.1f);
            b.Kit.Torus(Vector3.zero, Vector3.up, 0.03f, 0.085f, 0.005f, 12, 4, 2f);
        }
    }
}
