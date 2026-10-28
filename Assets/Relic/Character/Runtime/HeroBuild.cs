using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>A dangling bone chain (hair strand, scarf tail, coat panel) for SpringChains.</summary>
    public sealed class ChainDef
    {
        public readonly List<int> Bones = new List<int>();
        /// <summary>0 = limp, 1 = holds its rest shape firmly.</summary>
        public float Stiffness = 0.5f;
        public float Drag = 0.4f;
        public float Gravity = 1f;
        /// <summary>Radius used against the body colliders.</summary>
        public float Radius = 0.02f;
        /// <summary>How much the chain reacts to the body's motion (0..1).</summary>
        public float Inertia = 1f;
        public string Kind;
    }

    /// <summary>
    /// Shared state while building the hero: the mesh kit, rig, materials, the equipment look,
    /// and weight helpers that blend across joints so the one skinned mesh bends smoothly.
    /// </summary>
    public sealed class HeroBuild
    {
        public readonly MeshKit Kit = new MeshKit();
        public readonly HeroRig Rig;
        public readonly HeroMaterials Mats;
        public readonly HeroLook Look;
        public readonly bool WeaponDrawn;
        public readonly List<ChainDef> Chains = new List<ChainDef>();

        public HeroBuild(HeroRig rig, HeroMaterials mats, HeroLook look, bool weaponDrawn)
        {
            Rig = rig; Mats = mats; Look = look; WeaponDrawn = weaponDrawn;
        }

        public void Toon(Rgb c, float outline = 1.6f) => Kit.Use(Mats.Toon(c, outline));
        public void Toon(Color c, float outline = 1.6f) => Kit.Use(Mats.Toon(c, outline));
        public void Flat(Color c, float intensity = 1f) => Kit.Use(Mats.Flat(c, intensity));

        public void Rigid(int bone) => Kit.Rigid(bone);

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Torso weights by height: hips → spine → chest → neck → head.</summary>
        public static BoneWeight TorsoWeight(Vector3 p)
        {
            float y = p.y;
            if (y <= HeroRig.HipY) return MeshKit.W1(HeroRig.Hips);
            if (y <= HeroRig.SpineY) return MeshKit.W2(HeroRig.Hips, HeroRig.Spine, Smooth(HeroRig.HipY, HeroRig.SpineY, y));
            if (y <= HeroRig.ChestY) return MeshKit.W2(HeroRig.Spine, HeroRig.Chest, Smooth(HeroRig.SpineY, HeroRig.ChestY, y));
            if (y <= HeroRig.NeckY) return MeshKit.W2(HeroRig.Chest, HeroRig.Neck, Smooth(HeroRig.NeckY - 0.05f, HeroRig.NeckY + 0.01f, y));
            return MeshKit.W2(HeroRig.Neck, HeroRig.Head, Smooth(HeroRig.HeadY - 0.02f, HeroRig.HeadY + 0.03f, y));
        }

        /// <summary>Arm weights (bind pose arms hang straight down): chest → upper arm → forearm → hand.</summary>
        public static BoneWeight ArmWeight(int s, Vector3 p)
        {
            float y = p.y;
            int ua = HeroRig.UpperArm(s), fa = HeroRig.ForeArm(s), h = HeroRig.Hand(s);
            if (y >= HeroRig.ShoulderY - 0.02f)
                return MeshKit.W2(ua, HeroRig.Chest, Smooth(HeroRig.ShoulderY - 0.02f, HeroRig.ShoulderY + 0.06f, y) * 0.6f);
            if (y >= HeroRig.ElbowY) return MeshKit.W2(ua, fa, 1f - Smooth(HeroRig.ElbowY, HeroRig.ElbowY + 0.05f, y));
            if (y >= HeroRig.WristY) return MeshKit.W2(fa, h, 1f - Smooth(HeroRig.WristY - 0.005f, HeroRig.WristY + 0.03f, y));
            return MeshKit.W1(h);
        }

        /// <summary>Leg weights: hips → thigh → shin → foot.</summary>
        public static BoneWeight LegWeight(int s, Vector3 p)
        {
            float y = p.y;
            int th = HeroRig.Thigh(s), sh = HeroRig.Shin(s), f = HeroRig.Foot(s);
            if (y >= HeroRig.HipJointY - 0.04f) return MeshKit.W2(th, HeroRig.Hips, Smooth(HeroRig.HipJointY - 0.04f, HeroRig.HipJointY + 0.06f, y));
            if (y >= HeroRig.KneeY) return MeshKit.W2(th, sh, 1f - Smooth(HeroRig.KneeY - 0.03f, HeroRig.KneeY + 0.05f, y));
            if (y >= HeroRig.AnkleY + 0.04f) return MeshKit.W2(sh, f, 1f - Smooth(HeroRig.AnkleY + 0.03f, HeroRig.AnkleY + 0.08f, y));
            return MeshKit.W1(f);
        }

        public void Torso() => Kit.Weigh = TorsoWeight;
        public void Arm(int s) => Kit.Weigh = p => ArmWeight(s, p);
        public void Leg(int s) => Kit.Weigh = p => LegWeight(s, p);

        /// <summary>
        /// Builds a chain of bones along a rest polyline (root-space points) under parent and
        /// returns the def; geometry along the same polyline can then use ChainWeight.
        /// </summary>
        public ChainDef Chain(string name, int parent, IList<Vector3> joints, string kind)
        {
            var def = new ChainDef { Kind = kind };
            int p = parent;
            for (int i = 0; i < joints.Count; i++)
            {
                p = Rig.Add(name + i, p, joints[i]);
                def.Bones.Add(p);
            }
            Chains.Add(def);
            return def;
        }

        /// <summary>Weights a point by its projection onto a chain's rest polyline (parent before the first joint).</summary>
        public BoneWeight ChainWeight(ChainDef c, int parent, Vector3 p)
        {
            int n = c.Bones.Count;
            float best = float.MaxValue;
            int seg = 0; float segT = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = Rig.Rest[c.Bones[i]], b = Rig.Rest[c.Bones[i + 1]];
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) { best = d; seg = i; segT = t; }
            }
            // Before the first joint: the parent holds it.
            Vector3 first = Rig.Rest[c.Bones[0]];
            if (seg == 0 && segT <= 0f)
            {
                Vector3 dir = n > 1 ? (Rig.Rest[c.Bones[1]] - first).normalized : Vector3.down;
                float before = Vector3.Dot(p - first, dir);
                if (before < 0f) return MeshKit.W2(c.Bones[0], parent, Mathf.Clamp01(-before / 0.04f));
            }
            return MeshKit.W2(c.Bones[seg], c.Bones[seg + 1], segT);
        }
    }
}
