using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The hero's skeleton. Every bone's rest rotation is identity, so a bind pose is just a
    /// translation and parts can be authored straight in avatar-root space (meters, feet at 0,
    /// facing +Z, character's left = -X). About 6.7 heads tall: chin 1.475 m, crown 1.735 m.
    /// </summary>
    public sealed class HeroRig
    {
        public const int Root = 0, Hips = 1, Spine = 2, Chest = 3, Neck = 4, Head = 5;
        public const int UpperArmL = 6, ForeArmL = 7, HandL = 8, UpperArmR = 9, ForeArmR = 10, HandR = 11;
        public const int ThighL = 12, ShinL = 13, FootL = 14, ThighR = 15, ShinR = 16, FootR = 17;

        // Key heights and offsets (root space).
        public const float HipY = 0.94f, SpineY = 1.06f, ChestY = 1.24f, NeckY = 1.44f, HeadY = 1.50f;
        public const float ShoulderX = 0.165f, ShoulderY = 1.385f, ElbowY = 1.115f, WristY = 0.875f;
        public const float LegX = 0.085f, HipJointY = 0.90f, KneeY = 0.49f, AnkleY = 0.085f;
        public const float HeadCenterY = 1.605f, HeadHalfH = 0.13f, HeadHalfW = 0.098f, HeadHalfD = 0.108f;

        public readonly List<Transform> Bones = new List<Transform>();
        public readonly List<Vector3> Rest = new List<Vector3>();
        public readonly List<int> Parent = new List<int>();
        public Transform RootTransform => Bones[0];

        public HeroRig(Transform parent)
        {
            var root = new GameObject("HeroRoot").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = parent.gameObject.layer;
            Bones.Add(root); Rest.Add(Vector3.zero); Parent.Add(-1);

            Add("Hips", Root, new Vector3(0f, HipY, 0f));
            Add("Spine", Hips, new Vector3(0f, SpineY, 0f));
            Add("Chest", Spine, new Vector3(0f, ChestY, 0f));
            Add("Neck", Chest, new Vector3(0f, NeckY, 0f));
            Add("Head", Neck, new Vector3(0f, HeadY, 0f));
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? -ShoulderX : ShoulderX;
                string n = s == 0 ? "L" : "R";
                int ua = Add("UpperArm" + n, Chest, new Vector3(x, ShoulderY, -0.01f));
                int fa = Add("ForeArm" + n, ua, new Vector3(x, ElbowY, -0.01f));
                Add("Hand" + n, fa, new Vector3(x, WristY, -0.01f));
            }
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? -LegX : LegX;
                string n = s == 0 ? "L" : "R";
                int th = Add("Thigh" + n, Hips, new Vector3(x, HipJointY, 0f));
                int sh = Add("Shin" + n, th, new Vector3(x, KneeY, 0.005f));
                Add("Foot" + n, sh, new Vector3(x, AnkleY, -0.01f));
            }
        }

        /// <summary>Adds a bone at a root-space rest position (chains for hair, scarf, coat).</summary>
        public int Add(string name, int parent, Vector3 restRootPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(Bones[parent], false);
            t.gameObject.layer = Bones[0].gameObject.layer;
            t.localPosition = restRootPos - Rest[parent];
            t.localRotation = Quaternion.identity;
            Bones.Add(t); Rest.Add(restRootPos); Parent.Add(parent);
            return Bones.Count - 1;
        }

        /// <summary>Puts every bone back to its rest pose (before rebinding a mesh).</summary>
        public void ResetPose()
        {
            for (int i = 1; i < Bones.Count; i++)
            {
                Bones[i].localPosition = Rest[i] - Rest[Parent[i]];
                Bones[i].localRotation = Quaternion.identity;
                Bones[i].localScale = Vector3.one;
            }
        }

        public Matrix4x4[] BindPoses()
        {
            var b = new Matrix4x4[Bones.Count];
            for (int i = 0; i < b.Length; i++) b[i] = Matrix4x4.Translate(-Rest[i]);
            return b;
        }

        /// <summary>Side helpers: s = -1 (left), +1 (right).</summary>
        public static int UpperArm(int s) => s < 0 ? UpperArmL : UpperArmR;
        public static int ForeArm(int s) => s < 0 ? ForeArmL : ForeArmR;
        public static int Hand(int s) => s < 0 ? HandL : HandR;
        public static int Thigh(int s) => s < 0 ? ThighL : ThighR;
        public static int Shin(int s) => s < 0 ? ShinL : ShinR;
        public static int Foot(int s) => s < 0 ? FootL : FootR;
    }
}
