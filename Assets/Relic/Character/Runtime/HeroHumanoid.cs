using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The hero rig as a Unity Humanoid (Mecanim) avatar, built in code from HeroRig with
    /// AvatarBuilder (docs/design/14-character.md, Decision 2026-11-18). The 17 core bones map
    /// to the required human bones plus Chest and Neck; hair, scarf, coat and bookmark chains stay
    /// generic (driven by SpringChains on top of the Animator).
    /// The avatar's reference pose is a T-pose (the rig's rest pose with the arms raised to the
    /// sides), so muscle values mean what they mean on any other humanoid and clips retarget.
    /// Two limits are widened from Unity's defaults because the rig has no shoulder bones and the
    /// swim/dive poses look far up: Arm Down-Up reaches -100 (arms at the sides) and Neck/Head Nod
    /// reach +-75.
    /// </summary>
    public static class HeroHumanoid
    {
        public struct Pair
        {
            public int Bone;
            public HumanBodyBones Human;
            public Pair(int bone, HumanBodyBones human) { Bone = bone; Human = human; }
        }

        public static readonly Pair[] Map =
        {
            new Pair(HeroRig.Hips, HumanBodyBones.Hips),
            new Pair(HeroRig.Spine, HumanBodyBones.Spine),
            new Pair(HeroRig.Chest, HumanBodyBones.Chest),
            new Pair(HeroRig.Neck, HumanBodyBones.Neck),
            new Pair(HeroRig.Head, HumanBodyBones.Head),
            new Pair(HeroRig.UpperArmL, HumanBodyBones.LeftUpperArm),
            new Pair(HeroRig.ForeArmL, HumanBodyBones.LeftLowerArm),
            new Pair(HeroRig.HandL, HumanBodyBones.LeftHand),
            new Pair(HeroRig.UpperArmR, HumanBodyBones.RightUpperArm),
            new Pair(HeroRig.ForeArmR, HumanBodyBones.RightLowerArm),
            new Pair(HeroRig.HandR, HumanBodyBones.RightHand),
            new Pair(HeroRig.ThighL, HumanBodyBones.LeftUpperLeg),
            new Pair(HeroRig.ShinL, HumanBodyBones.LeftLowerLeg),
            new Pair(HeroRig.FootL, HumanBodyBones.LeftFoot),
            new Pair(HeroRig.ThighR, HumanBodyBones.RightUpperLeg),
            new Pair(HeroRig.ShinR, HumanBodyBones.RightLowerLeg),
            new Pair(HeroRig.FootR, HumanBodyBones.RightFoot),
        };

        /// <summary>Number of core (human) bones at the start of HeroRig.Bones, the root included.</summary>
        public const int CoreBones = 18;

        /// <summary>True when muscle m belongs to a bone this rig maps (no fingers, toes, eyes, jaw, shoulders, upper chest).</summary>
        public static bool UsesMuscle(int m)
        {
            int bone = HumanTrait.BoneFromMuscle(m);
            foreach (Pair p in Map) if ((int)p.Human == bone) return true;
            return false;
        }

        /// <summary>
        /// Builds the avatar for a rig in its rest pose (call before animating it). The rig root is
        /// briefly unparented (AvatarBuilder wants the top of the hierarchy) and the arms briefly
        /// raised to the T-pose; both are restored before returning.
        /// </summary>
        public static Avatar Build(HeroRig rig)
        {
            Transform root = rig.RootTransform;
            Transform parent = root.parent;
            Vector3 rootPos = root.localPosition;
            Quaternion rootRot = root.localRotation;
            var saved = new Quaternion[CoreBones];
            for (int i = 0; i < CoreBones; i++) saved[i] = rig.Bones[i].localRotation;

            var skeleton = new List<SkeletonBone>();
            var human = new List<HumanBone>();
            try
            {
                root.SetParent(null, false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                for (int i = 1; i < CoreBones; i++)
                {
                    rig.Bones[i].localPosition = rig.Bind[i] - rig.Bind[rig.Parent[i]];
                    rig.Bones[i].localRotation = TPose(i);
                }
                skeleton.Add(new SkeletonBone { name = root.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one });
                for (int i = 1; i < CoreBones; i++)
                    skeleton.Add(new SkeletonBone { name = rig.Bones[i].name, position = rig.Bones[i].localPosition, rotation = rig.Bones[i].localRotation, scale = Vector3.one });
                foreach (Pair p in Map)
                    human.Add(new HumanBone { boneName = rig.Bones[p.Bone].name, humanName = HumanTrait.BoneName[(int)p.Human], limit = Limit(p.Human, BoneLength(rig, p.Bone)) });

                var desc = new HumanDescription
                {
                    human = human.ToArray(),
                    skeleton = skeleton.ToArray(),
                    upperArmTwist = 0.5f,
                    lowerArmTwist = 0.5f,
                    upperLegTwist = 0.5f,
                    lowerLegTwist = 0.5f,
                    armStretch = 0.05f,
                    legStretch = 0.05f,
                    feetSpacing = 0f,
                    hasTranslationDoF = false,
                };
                Avatar avatar = AvatarBuilder.BuildHumanAvatar(root.gameObject, desc);
                avatar.name = "HeroHumanoid";
                return avatar;
            }
            finally
            {
                for (int i = 1; i < CoreBones; i++) rig.Bones[i].localRotation = saved[i];
                root.SetParent(parent, false);
                root.localPosition = rootPos;
                root.localRotation = rootRot;
            }
        }

        /// <summary>The reference pose: rest, with the upper arms raised 90 degrees to the sides.</summary>
        public static Quaternion TPose(int bone)
        {
            if (bone == HeroRig.UpperArmL) return Quaternion.Euler(0f, 0f, -90f);
            if (bone == HeroRig.UpperArmR) return Quaternion.Euler(0f, 0f, 90f);
            return Quaternion.identity;
        }

        static float BoneLength(HeroRig rig, int bone)
        {
            for (int i = 1; i < CoreBones; i++)
                if (rig.Parent[i] == bone && i != bone) return Mathf.Max(0.05f, (rig.Bind[i] - rig.Bind[bone]).magnitude);
            return bone == HeroRig.Head ? 0.2f : 0.1f;
        }

        static HumanLimit Limit(HumanBodyBones bone, float length)
        {
            Vector3 min = Vector3.zero, max = Vector3.zero;
            for (int dof = 0; dof < 3; dof++)
            {
                int m = HumanTrait.MuscleFromBone((int)bone, dof);
                if (m < 0) continue;
                float lo = HumanTrait.GetMuscleDefaultMin(m), hi = HumanTrait.GetMuscleDefaultMax(m);
                string name = HumanTrait.MuscleName[m];
                if (name.EndsWith("Arm Down-Up")) lo = Mathf.Min(lo, -100f);
                if (name.EndsWith("Nod Down-Up")) { lo = Mathf.Min(lo, -75f); hi = Mathf.Max(hi, 75f); }
                min[dof] = lo;
                max[dof] = hi;
            }
            return new HumanLimit { useDefaultValues = false, min = min, max = max, center = Vector3.zero, axisLength = length };
        }
    }
}
