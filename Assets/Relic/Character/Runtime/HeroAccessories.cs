using UnityEngine;

namespace Relic.Character
{
    /// <summary>Hair accessories from the appearance: a crossed hair pin, a flower, or a ribbon bow.</summary>
    public static class HeroAccessories
    {
        public static void Build(HeroBuild b)
        {
            int slot = 0;
            foreach (AccessorySpec a in b.App.Accessories)
            {
                // A ribbon that already ties the hair (tail styles) is shown as the tie itself.
                bool tied = a.Kind == AccessoryKind.Ribbon && slot == 0 && HasTie(b.App.Hair.Style);
                if (!tied) Place(b, a, slot);
                slot++;
            }
        }

        static bool HasTie(HairStyle s) => s == HairStyle.LowTail || s == HairStyle.Ponytail || s == HairStyle.TwinTails;

        static void Place(HeroBuild b, AccessorySpec a, int slot)
        {
            int s = a.Side == Side.Left ? -1 : 1;
            float lon = (0.30f + 0.08f * slot) * Mathf.PI * s;
            float lat = 0.62f - 0.12f * slot;
            Vector3 at = HeroHair.OnCap(lat, lon, 0.022f);
            Vector3 n = (at - HeroHair.OnCap(lat, lon, -0.05f)).normalized;
            b.Rigid(HeroRig.Head);
            b.Kit.Xf = Matrix4x4.TRS(at, Quaternion.LookRotation(n, Vector3.up), Vector3.one);
            Color c = HeroBuild.C(a.Color);
            switch (a.Kind)
            {
                case AccessoryKind.Flower:
                    b.Toon(c, 1f);
                    for (int i = 0; i < 5; i++)
                    {
                        float ang = i * Mathf.PI * 2f / 5f;
                        var p = new Vector3(Mathf.Cos(ang) * 0.014f, Mathf.Sin(ang) * 0.014f, 0f);
                        b.Kit.Xf = Matrix4x4.TRS(at, Quaternion.LookRotation(n, Vector3.up), Vector3.one) * Matrix4x4.TRS(p, Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg), Vector3.one);
                        b.Kit.Ellipsoid(Vector3.zero, new Vector3(0.012f, 0.008f, 0.004f), 8, 4);
                    }
                    b.Kit.Xf = Matrix4x4.TRS(at, Quaternion.LookRotation(n, Vector3.up), Vector3.one);
                    b.Toon(new Color(1f, 0.85f, 0.35f), 0.8f);
                    b.Kit.Ellipsoid(new Vector3(0f, 0f, 0.003f), new Vector3(0.006f, 0.006f, 0.004f), 8, 4);
                    break;
                case AccessoryKind.Ribbon:
                    b.Toon(c, 1f);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        b.Kit.Xf = Matrix4x4.TRS(at, Quaternion.LookRotation(n, Vector3.up), Vector3.one) * Matrix4x4.TRS(new Vector3(0.022f * i, 0f, 0f), Quaternion.Euler(0f, 0f, 25f * i), Vector3.one);
                        b.Kit.Ellipsoid(Vector3.zero, new Vector3(0.022f, 0.014f, 0.008f), 10, 5);
                    }
                    b.Kit.Xf = Matrix4x4.TRS(at, Quaternion.LookRotation(n, Vector3.up), Vector3.one);
                    b.Kit.Ellipsoid(Vector3.zero, new Vector3(0.008f, 0.009f, 0.008f), 8, 4);
                    break;
                default:
                    // Two crossed pins with a small gem.
                    b.Toon(c, 1f);
                    b.Kit.Tube(new Vector3(-0.022f, -0.012f, 0f), new Vector3(0.022f, 0.012f, 0f), 0.0035f, 0.0035f, 5);
                    b.Kit.Tube(new Vector3(-0.022f, 0.012f, 0f), new Vector3(0.022f, -0.012f, 0f), 0.0035f, 0.0035f, 5);
                    b.Toon(new Color(0.55f, 0.85f, 0.95f), 0.8f);
                    b.Kit.Ellipsoid(new Vector3(0f, 0f, 0.003f), new Vector3(0.006f, 0.006f, 0.004f), 8, 4);
                    break;
            }
            b.Kit.Xf = Matrix4x4.identity;
        }
    }
}
